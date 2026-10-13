using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public enum ObjectiveShape { None, Sphere, Box }

    /// <summary>
    /// One objective on the map: a flag, a main base or a destruction area, plus the bounds that define it.
    /// Position and bounds are read once when it's discovered, the capture state is refreshed every second.
    /// </summary>
    public sealed class ObjectiveInfo
    {
        public ulong Base;
        public uint NameId;
        public string ActorName = "";
        public string DisplayName = "";
        public bool IsMain;

        public Vector3D Position;   // center of the bounds, same world space as UActor.Position
        public ObjectiveShape Shape = ObjectiveShape.None;
        public double Radius;       // sphere radius in cm, scale already applied
        public double ExtentX;      // box half extents in cm, scale already applied
        public double ExtentY;
        public double YawDegrees;   // world yaw in degrees, only matters for boxes

        public ulong CaptureZoneComponent;
        public byte OwningTeam;
        public byte CapturingTeam;
        public float CapturePercent;
        public bool Uncappable;

        /// <summary>How far the bounds reach from the center, in cm. Used to skip objectives that are off screen.</summary>
        public double Reach => Shape switch
        {
            ObjectiveShape.Sphere => Radius,
            ObjectiveShape.Box => Math.Sqrt(ExtentX * ExtentX + ExtentY * ExtentY),
            _ => 0
        };
    }

    /// <summary>
    /// Finds the flags and main bases of the current match (RAAS, AAS, Invasion, TC, plus destruction
    /// objectives) and draws their names and capture bounds on the radar. Read only, and it can be switched
    /// off in the settings.
    ///
    /// There is no flag actor to search for by name. Each game mode has a graph actor (BP_RAASLaneGraph,
    /// BP_InvasionRandomGraph, ...) that keeps a list of capture zone components, and the owner of each
    /// component is the flag. The flag's root is attached to a cluster pivot, so its world position has to
    /// be worked out by walking up the attach chain. The bounds come from a sphere or box component that
    /// hangs off the same root.
    /// </summary>
    public class Objectives
    {
        public const string NAME = "Objectives";

        private const int MaxActorsPerLevel = 100000;
        private const int DiscoveryIntervalMs = 10000;
        private const int EmptyDiscoveryIntervalMs = 2000;
        private const int StateIntervalMs = 1000;

        private readonly ulong _gameWorld;
        private readonly Dictionary<ulong, ObjectiveInfo> _tracked = new();
        private readonly Dictionary<uint, string> _graphNames = new();     // FName index -> name, graph actors only
        private readonly Dictionary<uint, bool> _graphMatches = new();
        private readonly Dictionary<uint, bool> _areaMatches = new();      // FName index -> is it a destruction objective actor
        private readonly Dictionary<uint, string> _classNameCache = new();
        private readonly HashSet<string> _warned = new();
        private readonly Stopwatch _discoverSw = new();
        private readonly Stopwatch _stateSw = new();
        private volatile ObjectiveInfo[] _snapshot = Array.Empty<ObjectiveInfo>();
        private volatile bool _enabled;
        private volatile bool _resetRequested;
        private bool _forceDiscover = true;
        private int _nodeArrayOffset = -1; // where the Nodes array was found on the graph actor, so later reads skip the probe
        private string _lastSummary = "";

        public Objectives(ulong gameWorld)
        {
            _gameWorld = gameWorld;
        }

        public IReadOnlyList<ObjectiveInfo> Current => _snapshot;

        /// <summary>
        /// Called from the UI thread. Turning it on only sets a flag, the actual reset happens on the game
        /// thread in <see cref="Update"/>, since that's the only thread that touches the tracking collections.
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (value == _enabled) return;
                if (value) _resetRequested = true;
                _enabled = value;
            }
        }

        #region Name filters
        private static bool IsGraphActorName(string name)
            => !name.StartsWith("Default__", StringComparison.Ordinal) &&
               !name.Contains("Widget", StringComparison.OrdinalIgnoreCase) &&
               !name.Contains("Anim", StringComparison.OrdinalIgnoreCase) &&
               !name.Contains("Visualizer", StringComparison.OrdinalIgnoreCase) &&
               name.Contains("Graph", StringComparison.OrdinalIgnoreCase);

        /// <summary>Destruction objectives (caches and the like) are regular actors, they aren't part of the capture graph.</summary>
        private static bool IsAreaActorName(string name)
            => !name.StartsWith("Default__", StringComparison.Ordinal) &&
               !name.Contains("Widget", StringComparison.OrdinalIgnoreCase) &&
               !name.Contains("Marker", StringComparison.OrdinalIgnoreCase) &&
               (name.Contains("DestructionObjectiveArea", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("DestructionCache", StringComparison.OrdinalIgnoreCase));
        #endregion

        #region Update
        public void Update()
        {
            if (!_enabled) return;

            if (_resetRequested)
            {
                // Start fresh so the summary gets logged again.
                _resetRequested = false;
                _tracked.Clear();
                _snapshot = Array.Empty<ObjectiveInfo>();
                _warned.Clear();
                _lastSummary = "";
                _forceDiscover = true;
            }

            try
            {
                if (!_discoverSw.IsRunning ||
                    _forceDiscover ||
                    _discoverSw.ElapsedMilliseconds >= (_tracked.Count == 0 ? EmptyDiscoveryIntervalMs : DiscoveryIntervalMs))
                {
                    _forceDiscover = false;
                    try { Discover(); }
                    finally
                    {
                        // Restart the timer even when this throws, otherwise a broken read would be retried on every game tick.
                        _discoverSw.Restart();
                        _stateSw.Restart();
                    }
                    return;
                }

                if (_stateSw.ElapsedMilliseconds >= StateIntervalMs)
                {
                    RefreshStates();
                    _stateSw.Restart();
                }
            }
            catch (DMAShutdown) { throw; }
            catch (GameEnded) { throw; }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Update failed: {ex.Message}");
            }
        }

        /// <summary>Logs each problem once, so a read that keeps failing doesn't spam the log.</summary>
        private void Warn(string key, string message)
        {
            if (_warned.Add(key)) Logger.Error($"[{NAME}] {message}");
        }

        private void Discover()
        {
            // Flags and the like usually sit in streamed sub-levels, so go through every loaded level and not just the persistent one.
            var levels = new List<ulong>();
            var levelsTArray = _gameWorld + Offsets.World.Levels;
            ulong levelsPtr = Memory.ReadPtr(levelsTArray);
            int levelCount = Memory.ReadValue<int>(levelsTArray + 0x8);
            if (levelsPtr != 0 && levelCount > 0 && levelCount <= 256)
            {
                for (int i = 0; i < levelCount; i++)
                {
                    ulong lv = Memory.ReadPtr(levelsPtr + (uint)(i * 0x8));
                    if (IsUserPtr(lv)) levels.Add(lv);
                }
            }
            if (levels.Count == 0)
            {
                ulong persistent = Memory.ReadPtr(_gameWorld + Offsets.World.PersistentLevel);
                if (persistent != 0) levels.Add(persistent);
            }

            var actors = new List<(ulong addr, uint nameId)>();
            var unknownIds = new HashSet<uint>();
            foreach (var level in levels)
            {
                var actorsTArray = level + Offsets.Level.Actors;
                int count = Memory.ReadValue<int>(actorsTArray + 0x8);
                ulong arrayPtr = Memory.ReadPtr(actorsTArray);
                if (count < 1 || count > MaxActorsPerLevel || arrayPtr == 0) continue;

                var scatter = new ScatterReadMap(count);
                var actorRound = scatter.AddRound();
                var idRound = scatter.AddRound();
                for (int i = 0; i < count; i++)
                {
                    var actorAddr = actorRound.AddEntry<ulong>(i, 0, arrayPtr + (uint)(i * 0x8));
                    idRound.AddEntry<uint>(i, 1, actorAddr, null, Offsets.Actor.ID);
                }
                scatter.Execute();

                for (int i = 0; i < count; i++)
                {
                    var r = scatter.Results[i];
                    if (!r.TryGetValue(0, out var ar) || !ar.TryGetResult<ulong>(out var addr) || addr == 0) continue;
                    if (!r.TryGetValue(1, out var ir) || !ir.TryGetResult<uint>(out var nameId) || nameId == 0) continue;
                    actors.Add((addr, nameId));
                    if (!_graphMatches.ContainsKey(nameId)) unknownIds.Add(nameId);
                }
            }

            var names = new Dictionary<uint, string>();
            if (unknownIds.Count > 0)
            {
                names = Memory.GetNamesById(unknownIds.ToList());
                foreach (var id in unknownIds)
                {
                    names.TryGetValue(id, out var n);
                    _graphMatches[id] = n != null && IsGraphActorName(n);
                    _areaMatches[id] = n != null && IsAreaActorName(n);
                    if (n != null && _graphMatches[id]) _graphNames[id] = n;
                }
            }

            var seen = new HashSet<ulong>();
            int graphCount = 0;

            foreach (var (addr, nameId) in actors)
            {
                if (_graphMatches.TryGetValue(nameId, out var isGraph) && isGraph)
                {
                    graphCount++;
                    ReadGraphNodes(addr, nameId, seen);
                }
            }

            foreach (var (addr, nameId) in actors)
            {
                if (!_areaMatches.TryGetValue(nameId, out var isArea) || !isArea) continue;
                seen.Add(addr);

                if (_tracked.TryGetValue(addr, out var existing) && existing.NameId == nameId) continue;

                string actorName = names.TryGetValue(nameId, out var nm) ? nm : Memory.GetActorClassName(addr);
                var info = BuildObjective(addr, nameId, actorName);
                if (info != null) _tracked[addr] = info;
            }

            foreach (var stale in _tracked.Keys.Where(k => !seen.Contains(k)).ToList())
                _tracked.Remove(stale);

            // Not a failure, some modes (training, seeding) simply have no objective graph.
            if (graphCount == 0 && _tracked.Count == 0 && _warned.Add("nograph"))
                Logger.Info($"[{NAME}] No objective graph found among {actors.Count} actors in {levels.Count} level(s); nothing to draw for this mode.");

            RefreshStates();
            _snapshot = _tracked.Values.ToArray();
            LogSummary();
        }

        /// <summary>Logs the objectives found for this match. Mains are drawn but left out of the list.</summary>
        private void LogSummary()
        {
            var listed = _tracked.Values.Where(o => !o.IsMain).OrderBy(o => o.DisplayName, StringComparer.Ordinal).ToList();
            var sb = new StringBuilder();
            foreach (var o in listed)
            {
                string bounds = o.Shape switch
                {
                    ObjectiveShape.Sphere => $"circle r={o.Radius / 100:F0}m",
                    ObjectiveShape.Box => $"box {o.ExtentX * 2 / 100:F0}x{o.ExtentY * 2 / 100:F0}m",
                    _ => "NO BOUNDS"
                };
                sb.AppendLine($"  {o.DisplayName,-20} ({o.Position.X / 100:F0}, {o.Position.Y / 100:F0}) m  {bounds}");
            }

            string summary = sb.ToString();
            if (listed.Count == 0 || summary == _lastSummary) return;
            _lastSummary = summary;
            Logger.Debug($"[{NAME}] {listed.Count} objective(s) found ({_tracked.Count - listed.Count} main base(s) not listed):\n{summary.TrimEnd()}");
        }
        #endregion

        #region Graph nodes
        private static bool IsUserPtr(ulong p) => p > 0x10000 && p < 0x7FFFFFFFFFFF;

        private static (double x, double y, double z) ReadVec(ulong addr)
            => (Memory.ReadValue<double>(addr), Memory.ReadValue<double>(addr + 0x8), Memory.ReadValue<double>(addr + 0x10));

        private string ClassNameOf(ulong obj)
        {
            ulong cls = Memory.ReadPtr(obj + Offsets.UObject.Class);
            if (!IsUserPtr(cls)) return null;
            uint id = Memory.ReadValue<uint>(cls + Offsets.UObject.Name);
            if (id == 0) return null;
            if (!_classNameCache.TryGetValue(id, out var name))
            {
                var r = Memory.GetNamesById(new List<uint> { id });
                _classNameCache[id] = name = r.TryGetValue(id, out var s) ? s : null;
            }
            return name;
        }

        /// <summary>
        /// Finds the node list on the graph actor. Every TArray-looking field after the AActor members gets
        /// checked until one turns up whose first element is a graph node or capture zone component. Once
        /// found, the offset is remembered.
        /// </summary>
        private bool FindNodeArray(ulong graph, string graphName, out ulong arrayPtr, out int count)
        {
            ulong foundPtr = 0; int foundCount = 0;
            arrayPtr = 0; count = 0;
            var seenArrays = new StringBuilder();

            bool TryOffset(uint off, bool record)
            {
                ulong ptr = Memory.ReadPtr(graph + off);
                int num = Memory.ReadValue<int>(graph + off + 0x8);
                int max = Memory.ReadValue<int>(graph + off + 0xC);
                if (!IsUserPtr(ptr) || num < 1 || num > max || max > 4096) return false;

                ulong first = Memory.ReadPtr(ptr);
                if (!IsUserPtr(first)) return false;
                string cn = ClassNameOf(first);
                if (record) seenArrays.AppendLine($"  +0x{off:X3}: TArray num={num} first element class={cn ?? "?"}");
                if (cn == null || !(cn.Contains("GraphNode", StringComparison.Ordinal) || cn.StartsWith("SQCaptureZone", StringComparison.Ordinal)))
                    return false;

                foundPtr = ptr; foundCount = num;
                return true;
            }

            uint firstGuess = _nodeArrayOffset >= 0 ? (uint)_nodeArrayOffset : Offsets.ASQUndirectedGraph.Nodes;
            try
            {
                if (TryOffset(firstGuess, false))
                {
                    _nodeArrayOffset = (int)firstGuess;
                    arrayPtr = foundPtr; count = foundCount;
                    return true;
                }
            }
            catch (DMAShutdown) { throw; }
            catch { }

            for (uint off = Offsets.ObjectiveScan.GraphArrayStart; off < Offsets.ObjectiveScan.GraphArrayEnd; off += 8)
            {
                try
                {
                    if (!TryOffset(off, true)) continue;
                    _nodeArrayOffset = (int)off;
                    arrayPtr = foundPtr; count = foundCount;
                    Logger.Debug($"[{NAME}] {graphName}: node array at +0x{off:X}, {count} node(s)");
                    return true;
                }
                catch (DMAShutdown) { throw; }
                catch { }
            }

            Warn($"nodes:{graph:X}",
                $"{graphName} @ 0x{graph:X}: node array not found (class {ClassNameOf(graph) ?? "?"}). TArray-like fields seen:\n{(seenArrays.Length > 0 ? seenArrays.ToString().TrimEnd() : "  none")}");
            return false;
        }

        /// <summary>
        /// Goes through the nodes of a graph actor. Capture zone nodes turn into objectives, built from the
        /// actor that owns the node. Anything else, like the cluster pivots, is skipped.
        /// </summary>
        private void ReadGraphNodes(ulong graph, uint graphNameId, HashSet<ulong> seen)
        {
            try
            {
                string graphName = _graphNames.TryGetValue(graphNameId, out var gn) ? gn : "Graph";
                if (!FindNodeArray(graph, graphName, out ulong ptr, out int n))
                    return;

                var buf = Memory.ReadBuffer(ptr, n * 8);
                for (int i = 0; i < n; i++)
                {
                    ulong node = BitConverter.ToUInt64(buf.Slice(i * 8, 8));
                    if (!IsUserPtr(node)) continue;

                    try
                    {
                        string className = ClassNameOf(node);
                        if (className == null || !className.StartsWith("SQCaptureZone", StringComparison.Ordinal)) continue;

                        ulong owner = Memory.ReadPtr(node + Offsets.UObject.Outer);
                        if (!IsUserPtr(owner)) continue;
                        seen.Add(owner);

                        if (_tracked.TryGetValue(owner, out var existing) && existing.CaptureZoneComponent == node) continue;

                        uint ownerNameId = Memory.ReadValue<uint>(owner + Offsets.Actor.ID);
                        var nm = Memory.GetNamesById(new List<uint> { ownerNameId });
                        string ownerName = nm.TryGetValue(ownerNameId, out var s) ? s : "Objective";

                        var info = BuildObjective(owner, ownerNameId, ownerName, node);
                        if (info != null) _tracked[owner] = info;
                    }
                    catch (DMAShutdown) { throw; }
                    catch (Exception ex)
                    {
                        Warn($"node:{node:X}", $"Failed reading node 0x{node:X}: {ex.Message}");
                    }
                }
            }
            catch (DMAShutdown) { throw; }
            catch (Exception ex)
            {
                Warn($"graph:{graph:X}", $"Failed reading graph @ 0x{graph:X}: {ex.Message}");
            }
        }
        #endregion

        #region Objective building
        private ObjectiveInfo BuildObjective(ulong actor, uint nameId, string actorName, ulong knownCaptureComp = 0)
        {
            try
            {
                string actorClass = ClassNameOf(actor);
                var info = new ObjectiveInfo
                {
                    Base = actor,
                    NameId = nameId,
                    ActorName = actorName,
                    DisplayName = CleanActorName(actorName),
                    // Main bases are BP_CaptureZoneMain_C actors named like "100-Team1Main".
                    IsMain = (actorClass ?? "").Contains("CaptureZoneMain", StringComparison.Ordinal) ||
                             actorName.EndsWith("Main", StringComparison.Ordinal)
                };

                ulong root = Memory.ReadPtr(actor + Offsets.Actor.RootComponent);
                if (!IsUserPtr(root))
                {
                    Warn($"root:{actor:X}", $"{info.DisplayName}: no root component, skipped");
                    return null;
                }

                var world = ComputeWorldTransform(root);
                info.Position = world.pos;
                info.YawDegrees = world.yaw;

                // Collect the actor's components: plain fields first, then the root's attach children and the
                // actor's component arrays. The flag's sphere or box hangs off the root, it isn't always a field.
                var components = new List<(ulong ptr, string className)>();
                var seenPtrs = new HashSet<ulong>();

                var buf = Memory.ReadBuffer(actor + Offsets.ObjectiveScan.ActorFieldsStart, (int)Offsets.ObjectiveScan.ActorFieldsLength);
                for (int o = 0; o + 8 <= (int)Offsets.ObjectiveScan.ActorFieldsLength; o += 8)
                {
                    ulong p = BitConverter.ToUInt64(buf.Slice(o, 8));
                    if (!IsUserPtr(p) || !seenPtrs.Add(p)) continue;
                    try
                    {
                        string cn = ClassNameOf(p);
                        if (cn != null) components.Add((p, cn));
                    }
                    catch (DMAShutdown) { throw; }
                    catch { }
                }

                foreach (var (ptr, cn) in FindComponentArrays(actor, root))
                    if (seenPtrs.Add(ptr)) components.Add((ptr, cn));

                ulong captureComp = knownCaptureComp;
                foreach (var (ptr, cn) in components)
                {
                    if (captureComp == 0 && cn.StartsWith("SQCaptureZone", StringComparison.Ordinal))
                        captureComp = ptr;
                }
                info.CaptureZoneComponent = captureComp;

                foreach (var (ptr, cn) in components)
                {
                    ObjectiveShape kind =
                        cn.EndsWith("SphereComponent", StringComparison.Ordinal) ? ObjectiveShape.Sphere :
                        cn.EndsWith("BoxComponent", StringComparison.Ordinal) ? ObjectiveShape.Box :
                        ObjectiveShape.None;
                    if (kind == ObjectiveShape.None) continue;

                    if (TryReadShape(info, kind, ptr, root, world.sx, world.sy)) break;
                }

                if (info.Shape == ObjectiveShape.None)
                    Warn($"shape:{actor:X}", $"{info.DisplayName}: no readable bounds, label only");

                return info;
            }
            catch (DMAShutdown) { throw; }
            catch (Exception ex)
            {
                Warn($"build:{actor:X}", $"Failed to read objective {actorName} @ 0x{actor:X}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads the size of a sphere or box component into the objective. A shape attached under the root is
        /// shifted by its relative location, rotated by the world yaw.
        /// </summary>
        private static bool TryReadShape(ObjectiveInfo info, ObjectiveShape kind, ulong shape, ulong root, double rootSx, double rootSy)
        {
            var scl = ReadVec(shape + Offsets.USceneComponent.RelativeScale3D);
            var rel = ReadVec(shape + Offsets.USceneComponent.RelativeLocation);
            double sx = (double.IsFinite(scl.x) && scl.x != 0 ? scl.x : 1) * (shape == root ? 1 : rootSx);
            double sy = (double.IsFinite(scl.y) && scl.y != 0 ? scl.y : 1) * (shape == root ? 1 : rootSy);

            double radius = 0, ex = 0, ey = 0;
            if (kind == ObjectiveShape.Box)
            {
                var ext = ReadVec(shape + Offsets.UBoxComponent.BoxExtent);
                if (!double.IsFinite(ext.x) || !double.IsFinite(ext.y)) return false;
                ex = ext.x * sx;
                ey = ext.y * sy;
                if (!(ex > 0 && ey > 0 && ex < 1e7 && ey < 1e7)) return false;
            }
            else
            {
                radius = ProbeSphereRadius(shape) * Math.Max(sx, sy);
                if (!(radius > 0 && radius < 1e7)) return false;
            }

            info.Shape = kind;
            info.Radius = radius;
            info.ExtentX = ex;
            info.ExtentY = ey;

            if (shape != root)
            {
                double yaw = info.YawDegrees * Math.PI / 180.0;
                double c = Math.Cos(yaw), s = Math.Sin(yaw);
                double ox = rel.x * rootSx, oy = rel.y * rootSy;
                info.Position = new Vector3D(
                    info.Position.X + ox * c - oy * s,
                    info.Position.Y + ox * s + oy * c,
                    info.Position.Z + rel.z);
            }

            if (kind == ObjectiveShape.Box)
                info.YawDegrees += ReadVec(shape + Offsets.USceneComponent.RelativeRotation).y;

            return true;
        }

        private static float ProbeSphereRadius(ulong comp)
        {
            foreach (uint off in new uint[] { Offsets.USphereComponent.SphereRadius, Offsets.USphereComponent.SphereRadius + 0x8, Offsets.USphereComponent.SphereRadius - 0x8, Offsets.USphereComponent.SphereRadius + 0x10 })
            {
                try
                {
                    float v = Memory.ReadValue<float>(comp + off);
                    if (float.IsFinite(v) && v >= 50f && v <= 1_000_000f) return v;
                }
                catch (DMAShutdown) { throw; }
                catch { }
            }
            return 0;
        }

        /// <summary>
        /// Works out the world position, yaw and scale by walking up the attach chain. Only yaw is applied
        /// for rotation, map objects stand upright.
        /// </summary>
        private static (Vector3D pos, double yaw, double sx, double sy) ComputeWorldTransform(ulong root)
        {
            var chain = new List<((double x, double y, double z) loc, double yaw, (double x, double y, double z) scl)>();
            ulong c = root;
            for (int i = 0; i < 12 && IsUserPtr(c); i++)
            {
                var loc = ReadVec(c + Offsets.USceneComponent.RelativeLocation);
                var rot = ReadVec(c + Offsets.USceneComponent.RelativeRotation);
                var scl = ReadVec(c + Offsets.USceneComponent.RelativeScale3D);
                chain.Add((loc, rot.y, scl));
                c = Memory.ReadPtr(c + Offsets.USceneComponent.AttachParent);
            }

            double px = 0, py = 0, pz = 0, yaw = 0, sx = 1, sy = 1, sz = 1;
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                var (loc, ryaw, scl) = chain[i];
                double rad = yaw * Math.PI / 180.0;
                double cs = Math.Cos(rad), sn = Math.Sin(rad);
                double lx = loc.x * sx, ly = loc.y * sy, lz = loc.z * sz;
                px += lx * cs - ly * sn;
                py += lx * sn + ly * cs;
                pz += lz;
                yaw += ryaw;

                sx *= double.IsFinite(scl.x) && scl.x != 0 ? scl.x : 1;
                sy *= double.IsFinite(scl.y) && scl.y != 0 ? scl.y : 1;
                sz *= double.IsFinite(scl.z) && scl.z != 0 ? scl.z : 1;
            }

            return (new Vector3D(px, py, pz), yaw, sx, sy);
        }

        private List<(ulong ptr, string className)> FindComponentArrays(ulong actor, ulong root)
        {
            var result = new List<(ulong, string)>();

            void Scan(ulong obj, uint from, uint to)
            {
                try
                {
                    int len = (int)(to - from);
                    var buf = Memory.ReadBuffer(obj + from, len + 16);
                    for (int o = 0; o < len; o += 8)
                    {
                        ulong ptr = BitConverter.ToUInt64(buf.Slice(o, 8));
                        int num = BitConverter.ToInt32(buf.Slice(o + 8, 4));
                        int max = BitConverter.ToInt32(buf.Slice(o + 12, 4));
                        if (!IsUserPtr(ptr) || num < 1 || num > max || max > 256) continue;

                        try
                        {
                            ulong first = Memory.ReadPtr(ptr);
                            if (!IsUserPtr(first)) continue;
                            string cn = ClassNameOf(first);
                            if (cn == null || !cn.EndsWith("Component", StringComparison.Ordinal)) continue;

                            int take = Math.Min(num, 32);
                            var elems = Memory.ReadBuffer(ptr, take * 8);
                            for (int i = 0; i < take; i++)
                            {
                                ulong e = BitConverter.ToUInt64(elems.Slice(i * 8, 8));
                                if (!IsUserPtr(e)) continue;
                                string ecn = ClassNameOf(e);
                                if (ecn != null) result.Add((e, ecn));
                            }
                        }
                        catch (DMAShutdown) { throw; }
                        catch { }
                    }
                }
                catch (DMAShutdown) { throw; }
                catch { }
            }

            Scan(root, Offsets.ObjectiveScan.SceneComponentArraysStart, Offsets.ObjectiveScan.SceneComponentArraysEnd);
            Scan(actor, Offsets.ObjectiveScan.ActorComponentArraysStart, Offsets.ObjectiveScan.ActorComponentArraysEnd);
            return result;
        }

        private void RefreshStates()
        {
            foreach (var o in _tracked.Values)
            {
                if (o.CaptureZoneComponent == 0) continue;
                try
                {
                    o.CapturingTeam = Memory.ReadValue<byte>(o.CaptureZoneComponent + Offsets.USQCaptureZoneComponent.CapturingTeam);
                    o.OwningTeam = Memory.ReadValue<byte>(o.CaptureZoneComponent + Offsets.USQCaptureZoneComponent.OwningTeam);
                    o.Uncappable = Memory.ReadValue<byte>(o.CaptureZoneComponent + Offsets.USQCaptureZoneComponent.Uncappable) != 0;
                    o.CapturePercent = Memory.ReadValue<float>(o.CaptureZoneComponent + Offsets.USQCaptureZoneComponent.CapturePercent);
                }
                catch (DMAShutdown) { throw; }
                catch { }
            }
        }

        /// <summary>
        /// Flag actors are named like "05W-Servo", "01-BahadurProper" or "100-Team1Main": lane and side first,
        /// then the flag. This drops the prefix and splits the CamelCase so it reads like the in-game name.
        /// </summary>
        private static string CleanActorName(string name)
        {
            if (name.StartsWith("BP_", StringComparison.Ordinal)) name = name[3..];
            if (name.EndsWith("_C", StringComparison.Ordinal)) name = name[..^2];
            name = Regex.Replace(name, @"^\d+[A-Za-z]?-", "");
            name = name.Replace('_', ' ');
            name = Regex.Replace(name, @"(?<=[a-z])(?=[A-Z0-9])|(?<=[0-9])(?=[A-Z])", " ");
            return name.Trim();
        }
        #endregion

        #region Drawing
        private static readonly SKPaint RingOutlinePaint = new()
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3.5f,
            IsAntialias = true
        };

        private static readonly SKPaint RingPaint = new()
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.75f,
            IsAntialias = true
        };

        private static readonly SKPaint FillPaint = new()
        {
            Color = new SKColor(255, 255, 255, 18),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        private static readonly SKPaint LabelOutlinePaint = new()
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            StrokeJoin = SKStrokeJoin.Round,
            TextAlign = SKTextAlign.Center,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            Typeface = SKPaints.TextBase.Typeface
        };

        private static readonly SKPaint LabelFillPaint = new()
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            TextAlign = SKTextAlign.Center,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            Typeface = SKPaints.TextBase.Typeface
        };

        public void Draw(SKCanvas canvas, MapParameters mapParams, Map map, float uiScale)
        {
            if (!_enabled || mapParams == null || map?.ConfigFile == null) return;

            var objectives = _snapshot;
            if (objectives.Length == 0) return;

            double pxPerUnit = map.ConfigFile.Scale * mapParams.XScale;
            var clip = canvas.LocalClipBounds;

            float textSize = 11f * uiScale;
            LabelFillPaint.TextSize = LabelOutlinePaint.TextSize = textSize;

            foreach (var o in objectives)
            {
                var center = o.Position.ToMapPos(map).ToZoomedPos(mapParams).GetPoint();
                float reach = (float)(o.Reach * pxPerUnit);

                if (center.X + reach < clip.Left || center.X - reach > clip.Right ||
                    center.Y + reach < clip.Top || center.Y - reach > clip.Bottom)
                    continue;

                switch (o.Shape)
                {
                    case ObjectiveShape.Sphere:
                    {
                        float r = (float)(o.Radius * pxPerUnit);
                        canvas.DrawCircle(center, r, FillPaint);
                        canvas.DrawCircle(center, r, RingOutlinePaint);
                        canvas.DrawCircle(center, r, RingPaint);
                        break;
                    }
                    case ObjectiveShape.Box:
                        DrawBox(canvas, o, map, mapParams);
                        break;
                }

                if (!string.IsNullOrEmpty(o.DisplayName))
                {
                    // Label sits on the objective, nudged down so it's vertically centered.
                    float ty = center.Y + textSize * 0.35f;
                    canvas.DrawText(o.DisplayName, center.X, ty, LabelOutlinePaint);
                    canvas.DrawText(o.DisplayName, center.X, ty, LabelFillPaint);
                }
            }
        }

        private static void DrawBox(SKCanvas canvas, ObjectiveInfo o, Map map, MapParameters mapParams)
        {
            double yaw = o.YawDegrees * Math.PI / 180.0;
            double c = Math.Cos(yaw), s = Math.Sin(yaw);
            var corners = new (double x, double y)[]
            {
                (-o.ExtentX, -o.ExtentY), (o.ExtentX, -o.ExtentY), (o.ExtentX, o.ExtentY), (-o.ExtentX, o.ExtentY)
            };

            using var path = new SKPath();
            for (int i = 0; i < corners.Length; i++)
            {
                var (dx, dy) = corners[i];
                var world = new Vector3D(
                    o.Position.X + dx * c - dy * s,
                    o.Position.Y + dx * s + dy * c,
                    o.Position.Z);
                var p = world.ToMapPos(map).ToZoomedPos(mapParams).GetPoint();
                if (i == 0) path.MoveTo(p); else path.LineTo(p);
            }
            path.Close();
            canvas.DrawPath(path, FillPaint);
            canvas.DrawPath(path, RingOutlinePaint);
            canvas.DrawPath(path, RingPaint);
        }
        #endregion
    }
}
