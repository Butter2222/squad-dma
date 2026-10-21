using System.Diagnostics;
using System.Text;
using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>One engine console variable: where its object is and where it keeps its value.</summary>
    public readonly record struct CvarHandle(string Name, ulong Object, uint ValueOffset, bool IsFloat, bool Pair = true);

    /// <summary>
    /// Finds Unreal's console variable registry in the running game and reads and writes the variables in it
    /// (r.ViewDistanceScale, r.Fog, foliage.DensityScale ...).
    ///
    /// Nothing about the registry is known from the SDK, so it is found by structure, in three steps:
    ///  1. The engine keeps one global pointer to its console manager in the module's data section. Every pointer
    ///     there that leads to an object with a vtable in the module is a candidate.
    ///  2. The console manager holds a TMap of every variable. A candidate is the manager if it contains a
    ///     TArray-shaped field (data pointer, count, capacity) whose first elements are an FString (or FName)
    ///     followed by an object pointer, and whose strings read like variable names ("r.Something").
    ///  3. The variable objects are not described anywhere either, so the place each class keeps its value is
    ///     inferred: a TConsoleVariableData holds the value twice in a row (game thread and render thread copy), so
    ///     the offset where most variables of one class have two equal neighbouring values is the value.
    ///
    /// Everything that is found is logged, and the full list is written to Configuration\ConsoleVariables.txt.
    /// The search is repeated for every game start (addresses change), it takes a few seconds.
    /// </summary>
    public sealed class ConsoleVariables
    {
        public static ConsoleVariables Instance { get; } = new();

        private const int ChunkBytes = 0x40000;
        private const int ScatterChunk = 1024;
        private const int MaxCandidates = 600_000;
        private const int MaxNameBytes = 400;

        private readonly object _lock = new();
        private bool _ready;
        private ulong _moduleBase;
        private ulong _sampleObject, _sampleVtable;
        private readonly Dictionary<string, ulong> _objects = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<ulong, ulong> _vtables = new();
        private readonly Dictionary<ulong, (uint Offset, bool IsFloat, bool Indirect)> _layouts = new();

        public int Count => _objects.Count;
        public IReadOnlyCollection<string> Names => _objects.Keys;

        #region Using a variable
        public bool TryResolve(string name, out CvarHandle handle)
        {
            handle = default;
            if (!_ready || !_objects.TryGetValue(name, out var obj) || !_vtables.TryGetValue(obj, out var vt) || !_layouts.TryGetValue(vt, out var layout))
                return false;

            if (layout.Indirect)
            {
                // a variable bound to a global (FAutoConsoleVariableRef): the object holds a pointer to the real value
                ulong target;
                try { target = Memory.ReadValue<ulong>(obj + layout.Offset); }
                catch (DMAShutdown) { throw; }
                catch { return false; }
                if (target == 0) return false;
                handle = new CvarHandle(name, target, 0, layout.IsFloat, false);
                return true;
            }

            handle = new CvarHandle(name, obj, layout.Offset, layout.IsFloat);
            return true;
        }

        /// <summary>Whether the registry knows the variable at all (even if the place of its value is not known).</summary>
        public bool Exists(string name) => _ready && _objects.ContainsKey(name);

        public float ReadFloat(CvarHandle h) => Memory.ReadValue<float>(h.Object + h.ValueOffset);
        public int ReadInt(CvarHandle h) => Memory.ReadValue<int>(h.Object + h.ValueOffset);

        /// <summary>The game thread copy and the render thread copy sit next to each other; both are written.</summary>
        public void WriteFloat(CvarHandle h, float value)
        {
            Memory.WriteValue(h.Object + h.ValueOffset, value);
            if (h.Pair) Memory.WriteValue(h.Object + h.ValueOffset + 4, value);
        }

        public void WriteInt(CvarHandle h, int value)
        {
            Memory.WriteValue(h.Object + h.ValueOffset, value);
            if (h.Pair) Memory.WriteValue(h.Object + h.ValueOffset + 4, value);
        }
        #endregion

        /// <summary>
        /// Makes sure the registry is located for this game process. Returns false (with the reason logged) when it
        /// could not be found. Cheap when it already is.
        /// </summary>
        public bool EnsureReady(ulong moduleBase, Action<string> log, CancellationToken token)
        {
            lock (_lock)
            {
                if (_ready && _moduleBase == moduleBase && StillValid())
                    return true;

                _ready = false;
                _objects.Clear();
                _vtables.Clear();
                _layouts.Clear();
                _moduleBase = moduleBase;

                var clock = Stopwatch.StartNew();
                try
                {
                    _ready = Locate(moduleBase, log, token);
                }
                catch (DMAShutdown) { throw; }
                catch (OperationCanceledException)
                {
                    log("search cancelled");
                    return false;
                }
                catch (Exception ex)
                {
                    Logger.Error($"[ConsoleVariables] The search failed: {ex}");
                    return false;
                }

                log($"search finished in {clock.Elapsed.TotalSeconds:F1} s: {(_ready ? $"{_objects.Count} variable(s), {_layouts.Count} value layout(s)" : "not found")}");
                return _ready;
            }
        }

        /// <summary>The registry is only good for the process it was found in; one known object is enough to notice a restart.</summary>
        private bool StillValid()
        {
            try { return _sampleObject != 0 && Memory.ReadValue<ulong>(_sampleObject) == _sampleVtable; }
            catch (DMAShutdown) { throw; }
            catch { return false; }
        }

        #region Locating
        private readonly record struct Section(string Name, ulong Address, uint Size, uint Characteristics);

        private sealed class MapInfo
        {
            public ulong Object, SlotAddress, DataPointer;
            public uint MapOffset, Stride, ValueOffset;
            public int Count;
            public bool KeyIsString;
        }

        private static bool Plausible(ulong p) => p >= 0x1_0000_0000UL && p < 0x7FF0_0000_0000UL;

        private bool Locate(ulong moduleBase, Action<string> log, CancellationToken token)
        {
            // --- the module's own layout
            var header = Memory.ReadBuffer(moduleBase, 0x1000).ToArray();
            if (header[0] != 'M' || header[1] != 'Z') { log("module header is not a PE image"); return false; }
            int pe = BitConverter.ToInt32(header, 0x3C);
            if (pe <= 0 || pe > 0xE00 || BitConverter.ToUInt32(header, pe) != 0x4550) { log("PE signature not found"); return false; }

            int sectionCount = BitConverter.ToUInt16(header, pe + 6);
            int optionalSize = BitConverter.ToUInt16(header, pe + 20);
            uint imageSize = BitConverter.ToUInt32(header, pe + 24 + 0x38);
            int sectionTable = pe + 24 + optionalSize;

            var sections = new List<Section>();
            for (int i = 0; i < sectionCount && sectionTable + (i + 1) * 40 <= header.Length; i++)
            {
                int o = sectionTable + i * 40;
                string name = Encoding.ASCII.GetString(header, o, 8).TrimEnd('\0');
                sections.Add(new Section(name, moduleBase + BitConverter.ToUInt32(header, o + 12), BitConverter.ToUInt32(header, o + 8), BitConverter.ToUInt32(header, o + 36)));
            }
            log($"module 0x{moduleBase:X}, image {imageSize / 1048576.0:F0} MB, sections: {string.Join(", ", sections.Select(s => $"{s.Name} {s.Size / 1048576.0:F1} MB"))}");

            bool InImage(ulong p) => p >= moduleBase && p < moduleBase + imageSize;

            // --- step 1: every pointer in the writable data sections that leads out of the module
            var candidates = new Dictionary<ulong, ulong>();
            foreach (var s in sections.Where(s => (s.Characteristics & 0x80000000) != 0 && (s.Characteristics & 0x20000000) == 0 && s.Size > 0))
            {
                ReadRegion(s.Address, s.Size, token, (address, buf) =>
                {
                    for (int i = 0; i + 8 <= buf.Length && candidates.Count < MaxCandidates; i += 8)
                    {
                        ulong p = BitConverter.ToUInt64(buf, i);
                        if (Plausible(p) && (p & 7) == 0 && !InImage(p))
                            candidates.TryAdd(p, address + (ulong)i);
                    }
                });
            }
            log($"{candidates.Count} pointer(s) in the data sections lead into the heap");

            // keep the ones that are objects with a vtable in the module
            var list = candidates.Keys.ToArray();
            var objects = new List<ulong>();
            for (int start = 0; start < list.Length; start += ScatterChunk)
            {
                token.ThrowIfCancellationRequested();
                int n = Math.Min(ScatterChunk, list.Length - start);
                var map = new ScatterReadMap(n);
                var round = map.AddRound();
                for (int i = 0; i < n; i++)
                    round.AddEntry<ulong>(i, 0, list[start + i]);
                map.Execute();
                for (int i = 0; i < n; i++)
                    if (map.Results[i][0].TryGetResult<ulong>(out var vt) && InImage(vt))
                        objects.Add(list[start + i]);
            }
            log($"{objects.Count} of them are objects with a vtable in the module");

            // --- step 2: which of them holds the variable map
            MapInfo best = null;
            for (int start = 0; start < objects.Count; start += 512)
            {
                token.ThrowIfCancellationRequested();
                int n = Math.Min(512, objects.Count - start);
                var map = new ScatterReadMap(n);
                var round = map.AddRound();
                for (int i = 0; i < n; i++)
                    round.AddEntry<byte[]>(i, 0, objects[start + i], (int)ConsoleRegistry.ObjectScanSize);
                map.Execute();

                for (int i = 0; i < n; i++)
                {
                    if (!map.Results[i][0].TryGetResult<byte[]>(out var buf)) continue;
                    var found = LooksLikeRegistry(buf, objects[start + i]);
                    if (found != null && (best == null || found.Count > best.Count))
                    {
                        found.SlotAddress = candidates[found.Object];
                        best = found;
                    }
                }
            }

            if (best == null)
            {
                log("no object holding the console variable map was found");
                return false;
            }

            log($"console manager at 0x{best.Object:X} (global slot module+0x{best.SlotAddress - moduleBase:X}); map at +0x{best.MapOffset:X}: " +
                $"{best.Count} slot(s), element stride 0x{best.Stride:X}, keys are {(best.KeyIsString ? "FString" : "FName")}");

            // --- the registry itself
            EnumerateRegistry(best, log, token);
            if (_objects.Count == 0)
                return false;

            // --- step 3: where each variable class keeps its value
            InferValueLayouts(moduleBase, log, token);

            var sample = _objects.Values.FirstOrDefault(o => _vtables.ContainsKey(o));
            _sampleObject = sample;
            _sampleVtable = sample == 0 ? 0 : _vtables[sample];

            WriteDump(moduleBase, log);
            return true;
        }

        /// <summary>Reads a region in chunks; a chunk that cannot be read as a whole is retried page by page.</summary>
        private static void ReadRegion(ulong start, ulong size, CancellationToken token, Action<ulong, byte[]> consume)
        {
            for (ulong offset = 0; offset < size; offset += ChunkBytes)
            {
                token.ThrowIfCancellationRequested();
                int length = (int)Math.Min(ChunkBytes, size - offset);
                try
                {
                    consume(start + offset, Memory.ReadBuffer(start + offset, length).ToArray());
                }
                catch (DMAShutdown) { throw; }
                catch
                {
                    for (int page = 0; page < length; page += 0x1000)
                    {
                        try { consume(start + offset + (ulong)page, Memory.ReadBuffer(start + offset + (ulong)page, Math.Min(0x1000, length - page)).ToArray()); }
                        catch (DMAShutdown) { throw; }
                        catch { /* not resident */ }
                    }
                }
            }
        }

        private static byte[] TryRead(ulong address, int size)
        {
            try { return Memory.ReadBuffer(address, size).ToArray(); }
            catch (DMAShutdown) { throw; }
            catch { return null; }
        }

        private static bool LooksLikeName(string s) =>
            s.Length >= 2 && s.Length <= 128 && s.All(c => c >= 0x20 && c < 0x7F) && s.Any(char.IsLetter);

        private static string DecodeUtf16(byte[] buffer, int chars)
        {
            int length = Math.Min(chars, buffer.Length / 2);
            while (length > 0 && BitConverter.ToUInt16(buffer, (length - 1) * 2) == 0) length--;
            return Encoding.Unicode.GetString(buffer, 0, length * 2);
        }

        /// <summary>
        /// Searches an object for a TArray-shaped field (data, count, capacity) that is the element array of a map
        /// from name to object pointer, and returns where it is, or null.
        /// </summary>
        private MapInfo LooksLikeRegistry(byte[] objectBytes, ulong objectAddress)
        {
            MapInfo result = null;
            for (int i = 0; i + 16 <= objectBytes.Length; i += 8)
            {
                ulong data = BitConverter.ToUInt64(objectBytes, i);
                int num = BitConverter.ToInt32(objectBytes, i + 8);
                int max = BitConverter.ToInt32(objectBytes, i + 12);
                if (!Plausible(data) || (data & 7) != 0 || num < 1000 || num > 400_000 || max < num || max > num * 4 + 64)
                    continue;

                foreach (bool stringKey in new[] { true, false })
                {
                    uint stride = stringKey ? ConsoleRegistry.ElementStrideStringKey : ConsoleRegistry.ElementStrideNameKey;
                    uint valueOffset = stringKey ? ConsoleRegistry.ValueOffsetStringKey : ConsoleRegistry.ValueOffsetNameKey;
                    var elements = TryRead(data, (int)stride * 24);
                    if (elements == null) continue;

                    int valid = 0, named = 0;
                    var ids = new List<uint>();
                    for (int e = 0; e < 24; e++)
                    {
                        int o = e * (int)stride;
                        ulong value = BitConverter.ToUInt64(elements, o + (int)valueOffset);
                        if (!Plausible(value)) continue;

                        if (stringKey)
                        {
                            ulong key = BitConverter.ToUInt64(elements, o);
                            int keyNum = BitConverter.ToInt32(elements, o + 8), keyMax = BitConverter.ToInt32(elements, o + 12);
                            if (!Plausible(key) || keyNum < 2 || keyNum > 130 || keyMax < keyNum || keyMax > 4096) continue;
                            valid++;
                            var text = TryRead(key, keyNum * 2);
                            if (text != null && LooksLikeName(DecodeUtf16(text, keyNum))) named++;
                        }
                        else
                        {
                            int id = BitConverter.ToInt32(elements, o), number = BitConverter.ToInt32(elements, o + 4);
                            if (id <= 0 || id > 60_000_000 || number < 0 || number > 8) continue;
                            valid++;
                            ids.Add((uint)id);
                        }
                    }

                    if (!stringKey && ids.Count >= 6)
                    {
                        try
                        {
                            foreach (var kv in Memory.GetNamesById(ids))
                                if (LooksLikeName(kv.Value)) named++;
                        }
                        catch (DMAShutdown) { throw; }
                        catch { }
                    }

                    if (valid >= 8 && named >= 5 && (result == null || num > result.Count))
                    {
                        result = new MapInfo
                        {
                            Object = objectAddress, MapOffset = (uint)i, DataPointer = data, Count = num,
                            Stride = stride, ValueOffset = valueOffset, KeyIsString = stringKey
                        };
                    }
                }
            }
            return result;
        }

        private void EnumerateRegistry(MapInfo map, Action<string> log, CancellationToken token)
        {
            var slots = new List<(ulong KeyPointer, int KeyNum, uint NameId, ulong Value)>();
            ReadRegion(map.DataPointer, (ulong)map.Count * map.Stride, token, (address, buf) =>
            {
                // chunks are element aligned only when the stride divides the chunk size; realign
                ulong first = (address - map.DataPointer) % map.Stride;
                for (int o = first == 0 ? 0 : (int)(map.Stride - first); o + (int)map.Stride <= buf.Length; o += (int)map.Stride)
                {
                    ulong value = BitConverter.ToUInt64(buf, o + (int)map.ValueOffset);
                    if (!Plausible(value)) continue;

                    if (map.KeyIsString)
                    {
                        ulong key = BitConverter.ToUInt64(buf, o);
                        int keyNum = BitConverter.ToInt32(buf, o + 8), keyMax = BitConverter.ToInt32(buf, o + 12);
                        if (Plausible(key) && keyNum >= 2 && keyNum <= 200 && keyMax >= keyNum && keyMax < 4096)
                            slots.Add((key, keyNum, 0, value));
                    }
                    else
                    {
                        int id = BitConverter.ToInt32(buf, o), number = BitConverter.ToInt32(buf, o + 4);
                        if (id > 0 && id < 60_000_000 && number >= 0 && number < 8)
                            slots.Add((0, 0, (uint)id, value));
                    }
                }
            });
            log($"{slots.Count} usable slot(s) of {map.Count}");

            if (map.KeyIsString)
            {
                for (int start = 0; start < slots.Count; start += ScatterChunk)
                {
                    token.ThrowIfCancellationRequested();
                    int n = Math.Min(ScatterChunk, slots.Count - start);
                    var scatter = new ScatterReadMap(n);
                    var round = scatter.AddRound();
                    for (int i = 0; i < n; i++)
                        round.AddEntry<byte[]>(i, 0, slots[start + i].KeyPointer, Math.Min(slots[start + i].KeyNum * 2, MaxNameBytes));
                    scatter.Execute();

                    for (int i = 0; i < n; i++)
                    {
                        if (!scatter.Results[i][0].TryGetResult<byte[]>(out var bytes)) continue;
                        string name = DecodeUtf16(bytes, slots[start + i].KeyNum);
                        if (LooksLikeName(name))
                            _objects[name] = slots[start + i].Value;
                    }
                }
            }
            else
            {
                var ids = slots.Select(s => s.NameId).Distinct().ToList();
                var names = new Dictionary<uint, string>();
                for (int start = 0; start < ids.Count; start += 1500)
                {
                    token.ThrowIfCancellationRequested();
                    foreach (var kv in Memory.GetNamesById(ids.GetRange(start, Math.Min(1500, ids.Count - start))))
                        names[kv.Key] = kv.Value;
                }
                foreach (var s in slots)
                    if (names.TryGetValue(s.NameId, out var name) && LooksLikeName(name))
                        _objects[name] = s.Value;
            }
        }

        private void InferValueLayouts(ulong moduleBase, Action<string> log, CancellationToken token)
        {
            var entries = _objects.ToList();
            var bytes = new Dictionary<ulong, byte[]>();
            for (int start = 0; start < entries.Count; start += 512)
            {
                token.ThrowIfCancellationRequested();
                int n = Math.Min(512, entries.Count - start);
                var map = new ScatterReadMap(n);
                var round = map.AddRound();
                for (int i = 0; i < n; i++)
                    round.AddEntry<byte[]>(i, 0, entries[start + i].Value, (int)ConsoleRegistry.CvarScanSize);
                map.Execute();
                for (int i = 0; i < n; i++)
                    if (map.Results[i][0].TryGetResult<byte[]>(out var buf) && buf.Length >= 0x40)
                        bytes[entries[start + i].Value] = buf;
            }

            foreach (var kv in bytes)
                _vtables[kv.Key] = BitConverter.ToUInt64(kv.Value, 0);

            // group by class (vtable) and look for the offset where the value appears twice in a row
            foreach (var group in _vtables.GroupBy(kv => kv.Value).Where(g => g.Count() >= 3).OrderByDescending(g => g.Count()))
            {
                var members = group.Select(g => g.Key).ToList();
                int bestOffset = -1, bestCount = 0;
                for (int k = 0x20; k + 8 <= (int)ConsoleRegistry.CvarScanSize; k += 4)
                {
                    int count = 0;
                    foreach (var m in members)
                    {
                        int a = BitConverter.ToInt32(bytes[m], k), b = BitConverter.ToInt32(bytes[m], k + 4);
                        if (a == b && a != 0) count++;
                    }
                    if (count > bestCount) { bestCount = count; bestOffset = k; }
                }

                if (bestOffset < 0 || bestCount < 2 || bestCount < members.Count * 0.3)
                {
                    InferIndirectLayout(group.Key, members, bytes, moduleBase, log);
                    continue;
                }

                int floatVotes = 0, intVotes = 0;
                foreach (var m in members)
                {
                    int raw = BitConverter.ToInt32(bytes[m], bestOffset);
                    if (raw != BitConverter.ToInt32(bytes[m], bestOffset + 4) || raw == 0) continue;
                    float f = BitConverter.Int32BitsToSingle(raw);
                    if (!float.IsNaN(f) && Math.Abs(f) >= 1e-4f && Math.Abs(f) <= 1e6f) floatVotes++;
                    if (Math.Abs((long)raw) <= 1_000_000) intVotes++;
                }
                // 1 as an int is a denormal float and 1.0 as a float is a huge int, so one side wins clearly
                bool isFloat = floatVotes > intVotes;
                _layouts[group.Key] = ((uint)bestOffset, isFloat, false);

                var sample = members.Take(4).Select(m =>
                {
                    string n = _objects.First(o => o.Value == m).Key;
                    int raw = BitConverter.ToInt32(bytes[m], bestOffset);
                    return $"{n}={(isFloat ? BitConverter.Int32BitsToSingle(raw).ToString("G6") : raw.ToString())}";
                });
                log($"class vtable+0x{group.Key - moduleBase:X}: {members.Count} variable(s), value at +0x{bestOffset:X} as {(isFloat ? "float" : "int")} ({bestCount} agree), e.g. {string.Join(", ", sample)}");
            }
        }

        /// <summary>
        /// Variables bound to a global (FAutoConsoleVariableRef) keep a pointer to the real value instead of the value.
        /// The offset is the one where most members hold a pointer into the module's own data, and the value they
        /// point at is read to tell int from float.
        /// </summary>
        private void InferIndirectLayout(ulong vtable, List<ulong> members, Dictionary<ulong, byte[]> bytes, ulong moduleBase, Action<string> log)
        {
            const ulong ModuleSpan = 0x20000000;
            for (int k = 0x08; k + 8 <= (int)ConsoleRegistry.CvarScanSize; k += 8)
            {
                var targets = new List<ulong>();
                foreach (var m in members)
                {
                    ulong p = BitConverter.ToUInt64(bytes[m], k);
                    if (p >= moduleBase && p < moduleBase + ModuleSpan && (p & 3) == 0) targets.Add(p);
                }
                if (targets.Count < 3 || targets.Count < members.Count * 0.8) continue;

                var sample = targets.Take(64).ToList();
                var map = new ScatterReadMap(sample.Count);
                var round = map.AddRound();
                for (int i = 0; i < sample.Count; i++) round.AddEntry<int>(i, 0, sample[i], 0);
                map.Execute();
                int floatVotes = 0, intVotes = 0;
                for (int i = 0; i < sample.Count; i++)
                {
                    if (!map.Results[i][0].TryGetResult<int>(out var raw) || raw == 0) continue;
                    float f = BitConverter.Int32BitsToSingle(raw);
                    if (!float.IsNaN(f) && Math.Abs(f) >= 1e-4f && Math.Abs(f) <= 1e6f) floatVotes++;
                    if (Math.Abs((long)raw) <= 1_000_000) intVotes++;
                }
                if (floatVotes + intVotes == 0) continue;
                bool isFloat = floatVotes > intVotes;
                _layouts[vtable] = ((uint)k, isFloat, true);
                log($"class vtable+0x{vtable - moduleBase:X}: {members.Count} variable(s) bound to a global, pointer at +0x{k:X}, value as {(isFloat ? "float" : "int")}");
                return;
            }
        }

        private void WriteDump(ulong moduleBase, Action<string> log)
        {
            try
            {
                Directory.CreateDirectory("Configuration");
                var path = Path.Combine("Configuration", "ConsoleVariables.txt");
                using var writer = new StreamWriter(path, false, Encoding.UTF8);
                writer.WriteLine($"# {_objects.Count} console variable(s) found in the running game, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine("# name | object | class (vtable offset in the module) | value (when the class layout is known)");
                foreach (var kv in _objects.OrderBy(o => o.Key, StringComparer.OrdinalIgnoreCase))
                {
                    string vt = _vtables.TryGetValue(kv.Value, out var v) ? $"+0x{v - moduleBase:X}" : "?";
                    string value = "";
                    if (TryResolve(kv.Key, out var h))
                    {
                        try { value = h.IsFloat ? ReadFloat(h).ToString("G6") : ReadInt(h).ToString(); }
                        catch (DMAShutdown) { throw; }
                        catch { value = "(unreadable)"; }
                    }
                    writer.WriteLine($"{kv.Key} | 0x{kv.Value:X} | {vt} | {value}");
                }
                log($"the full list is in {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[ConsoleVariables] Could not write the list: {ex.Message}");
            }
        }
        #endregion
    }
}
