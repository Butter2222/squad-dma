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
    /// A main base protection zone (SQProtectionZone): the volume that kills enemies who stay inside it.
    /// Read once when discovered.
    /// </summary>
    public sealed class MainZoneInfo
    {
        public ulong Base;
        public byte DeclaredTeam;   // TeamId of the zone, 0 when unset or not a team
        public byte Team;           // 1 or 2 once known, 0 while the zone has no team
        public bool IsSphere;
        public Vector3D Position;
        public double Radius;       // cm, scale applied
        public double ExtentX;      // box half extents in cm, scale applied
        public double ExtentY;
        public double YawDegrees;

        public double Reach => IsSphere ? Radius : Math.Sqrt(ExtentX * ExtentX + ExtentY * ExtentY);

        public bool Contains(double x, double y)
        {
            double dx = x - Position.X, dy = y - Position.Y;
            if (IsSphere) return dx * dx + dy * dy <= Radius * Radius;

            double yaw = YawDegrees * Math.PI / 180.0;
            double c = Math.Cos(yaw), s = Math.Sin(yaw);
            double lx = dx * c + dy * s, ly = -dx * s + dy * c;
            return Math.Abs(lx) <= ExtentX && Math.Abs(ly) <= ExtentY;
        }
    }

    /// <summary>
    /// Finds the flags and main bases of the current match (RAAS, AAS, Invasion, TC, plus destruction
    /// objectives) and draws them on the radar: the capture bounds of each flag, a line connecting the flags
    /// in order, and the protection zone of each main base. Read only, and it can be switched off in the settings.
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
        private readonly Dictionary<ulong, bool> _zoneClassMatch = new();  // UClass* -> is it SQProtectionZone or a subclass
        private readonly Dictionary<ulong, MainZoneInfo> _zones = new();   // only zones whose volume was read
        private volatile MainZoneInfo[] _zoneSnapshot = Array.Empty<MainZoneInfo>();
        private volatile ObjectiveInfo[] _route = Array.Empty<ObjectiveInfo>(); // flags in the order the line connects them
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
                _zones.Clear();
                _zoneSnapshot = Array.Empty<MainZoneInfo>();
                _route = Array.Empty<ObjectiveInfo>();
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
                    long scanStart = PerfStats.Begin();
                    try { Discover(); }
                    finally
                    {
                        // Restart the timer even when this throws, otherwise a broken read would be retried on every game tick.
                        PerfStats.RecordObjectivesScan(scanStart);
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
            var actorClasses = new List<(ulong addr, ulong cls)>();
            var unknownClasses = new HashSet<ulong>();
            var unknownIds = new HashSet<uint>();
            foreach (var level in levels)
            {
                var actorsTArray = level + Offsets.Level.Actors;
                int count = Memory.ReadValue<int>(actorsTArray + 0x8);
                ulong arrayPtr = Memory.ReadPtr(actorsTArray);
                if (count < 1 || count > MaxActorsPerLevel || arrayPtr == 0) continue;

                var pointers = RegisteredActors.ReadActorPointers(arrayPtr, count);

                // Chunked so a scan of a big level never holds the DMA link for long
                for (int start = 0; start < count; start += 2048)
                {
                    int n = Math.Min(2048, count - start);
                    var scatter = new ScatterReadMap(n);
                    var round = scatter.AddRound();
                    for (int i = 0; i < n; i++)
                    {
                        ulong actorAddr = pointers[start + i];
                        if (actorAddr == 0) continue;
                        round.AddEntry<uint>(i, 1, actorAddr, null, Offsets.Actor.ID);
                        round.AddEntry<ulong>(i, 2, actorAddr, null, Offsets.UObject.Class);
                    }
                    scatter.Execute();

                    for (int i = 0; i < n; i++)
                    {
                        ulong addr = pointers[start + i];
                        if (addr == 0) continue;
                        var r = scatter.Results[i];
                        if (!r.TryGetValue(1, out var ir) || !ir.TryGetResult<uint>(out var nameId) || nameId == 0) continue;
                        actors.Add((addr, nameId));
                        if (!_graphMatches.ContainsKey(nameId)) unknownIds.Add(nameId);

                        if (r.TryGetValue(2, out var cr) && cr.TryGetResult<ulong>(out var cls) && IsUserPtr(cls))
                        {
                            actorClasses.Add((addr, cls));
                            if (!_zoneClassMatch.ContainsKey(cls)) unknownClasses.Add(cls);
                        }
                    }

                    if (start + 2048 < count)
                        Thread.Sleep(1);
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

            foreach (var cls in unknownClasses)
                if (TryClassifyZoneClass(cls, out bool isZone)) _zoneClassMatch[cls] = isZone;

            var zoneActors = actorClasses.Where(a => _zoneClassMatch.TryGetValue(a.cls, out var m) && m).Select(a => a.addr).ToList();
            if (zoneActors.Count == 0 && _warned.Add("nozones"))
                Logger.Info($"[{NAME}] No protection zone actors found among {actors.Count} actors in {levels.Count} level(s).");
            CollectZones(zoneActors);
            AssignZoneTeams();

            _snapshot = _tracked.Values.ToArray();
            _zoneSnapshot = _zones.Values.ToArray();
            _route = _tracked.Values
                .Where(o => !o.IsMain && o.CaptureZoneComponent != 0)
                .OrderBy(FlagNumber)
                .ThenBy(o => o.ActorName, StringComparer.Ordinal)
                .ToArray();
            LogSummary();
        }

        /// <summary>Logs the objectives found for this match. Mains are left out of the list, they show as protection zones.</summary>
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
            return IsUserPtr(cls) ? ClassPtrName(cls) : null;
        }

        /// <summary>Name of a UClass, looked up once per FName.</summary>
        private string ClassPtrName(ulong cls)
        {
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

        #region Main zones
        /// <summary>
        /// Whether a class is SQProtectionZone or derives from it. Matching by class and not by the instance name
        /// matters: placed actors are often renamed per map, the class chain is what stays put. Returns false when
        /// a read fails, so the caller doesn't cache a wrong answer.
        /// </summary>
        private bool TryClassifyZoneClass(ulong cls, out bool isZone)
        {
            isZone = false;
            try
            {
                ulong c = cls;
                for (int depth = 0; depth < 16 && IsUserPtr(c); depth++)
                {
                    if (ClassPtrName(c) == "SQProtectionZone") { isZone = true; break; }
                    c = Memory.ReadPtr(c + Offsets.UStruct.SuperStruct);
                }
                return true;
            }
            catch (DMAShutdown) { throw; }
            catch { return false; }
        }

        /// <summary>
        /// Reads the volume of every zone that isn't known yet. A zone that can't be read is retried on the next
        /// discovery, since its components may not exist yet right after the match loads.
        /// </summary>
        private void CollectZones(List<ulong> zoneActors)
        {
            var present = new HashSet<ulong>(zoneActors);
            foreach (var addr in zoneActors)
            {
                if (_zones.ContainsKey(addr)) continue;

                var zone = ReadZone(addr);
                if (zone != null) _zones[addr] = zone;
            }

            foreach (var stale in _zones.Keys.Where(k => !present.Contains(k)).ToList())
                _zones.Remove(stale);
        }

        private MainZoneInfo ReadZone(ulong actor)
        {
            try
            {
                string className = ClassNameOf(actor) ?? "";
                byte declared = Memory.ReadValue<byte>(actor + Offsets.ASQProtectionZone.TeamId);
                if (declared is not (1 or 2)) declared = 0;

                // The zone volume: the class's own box first, then the root and any other component.
                ulong root = Memory.ReadPtr(actor + Offsets.Actor.RootComponent);
                var candidates = new List<(ulong ptr, string className)>();
                ulong primary = Memory.ReadPtr(actor + Offsets.ASQProtectionZone.CollisionBox);
                if (IsUserPtr(primary)) candidates.Add((primary, ClassNameOf(primary) ?? ""));
                if (IsUserPtr(root))
                {
                    candidates.Add((root, ClassNameOf(root) ?? ""));
                    candidates.AddRange(CollectComponents(actor, root));
                }

                // A placeholder collision component can sit next to the real volume, so every shape is measured
                // and the largest one wins.
                MainZoneInfo best = null;
                var seen = new HashSet<ulong>();
                foreach (var (ptr, cn) in candidates)
                {
                    if (!seen.Add(ptr)) continue;
                    bool box = cn.EndsWith("BoxComponent", StringComparison.Ordinal);
                    bool sphere = cn.EndsWith("SphereComponent", StringComparison.Ordinal);
                    if (!box && !sphere) continue;

                    var world = ComputeWorldTransform(ptr);
                    var zone = new MainZoneInfo
                    {
                        Base = actor,
                        DeclaredTeam = declared,
                        IsSphere = sphere,
                        Position = world.pos,
                        YawDegrees = world.yaw
                    };

                    if (box)
                    {
                        var ext = ReadVec(ptr + Offsets.UBoxComponent.BoxExtent);
                        zone.ExtentX = ext.x * Math.Abs(world.sx);
                        zone.ExtentY = ext.y * Math.Abs(world.sy);
                        if (!(zone.ExtentX > 0 && zone.ExtentY > 0 && zone.ExtentX < 1e7 && zone.ExtentY < 1e7)) continue;
                    }
                    else
                    {
                        zone.Radius = ProbeSphereRadius(ptr) * Math.Max(Math.Abs(world.sx), Math.Abs(world.sy));
                        if (!(zone.Radius > 0 && zone.Radius < 1e7)) continue;
                    }

                    if (best == null || zone.Reach > best.Reach) best = zone;
                }

                if (best == null)
                {
                    Warn($"zone:{actor:X}", $"{className} @ 0x{actor:X}: no readable zone volume");
                    return null;
                }

                Logger.Debug($"[{NAME}] Protection zone {className}: " +
                             (best.IsSphere ? $"circle r={best.Radius / 100:F0}m" : $"box {best.ExtentX * 2 / 100:F0}x{best.ExtentY * 2 / 100:F0}m") +
                             $" at ({best.Position.X / 100:F0}, {best.Position.Y / 100:F0}) m, team {declared}");
                return best;
            }
            catch (DMAShutdown) { throw; }
            catch (Exception ex)
            {
                Warn($"zoneread:{actor:X}", $"Failed reading zone @ 0x{actor:X}: {ex.Message}");
                return null;
            }
        }

        /// <summary>The team a main base belongs to. The actor name ("100-Team1Main") is stable, the capture state is the fallback.</summary>
        private static byte MainTeam(ObjectiveInfo main)
        {
            var m = Regex.Match(main.ActorName, @"Team([12])");
            if (m.Success) return (byte)(m.Groups[1].Value[0] - '0');
            return main.OwningTeam is 1 or 2 ? main.OwningTeam : (byte)0;
        }

        /// <summary>
        /// Zones carry their own team. When the field isn't a team, the zone takes the team of the main base
        /// it covers. A zone with no team stays at 0 and is drawn gray.
        /// </summary>
        private void AssignZoneTeams()
        {
            List<ObjectiveInfo> mains = null;
            foreach (var z in _zones.Values)
            {
                if (z.DeclaredTeam != 0) { z.Team = z.DeclaredTeam; continue; }

                mains ??= _tracked.Values.Where(o => o.IsMain).ToList();
                z.Team = 0;
                foreach (var main in mains)
                {
                    byte t = MainTeam(main);
                    if (t != 0 && z.Contains(main.Position.X, main.Position.Y)) { z.Team = t; break; }
                }
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

                var components = CollectComponents(actor, root);

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
        /// Collects the actor's components: plain fields first, then the root's attach children and the
        /// actor's component arrays. A sphere or box often hangs off the root, it isn't always a field.
        /// </summary>
        private List<(ulong ptr, string className)> CollectComponents(ulong actor, ulong root)
        {
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

            return components;
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

        /// <summary>Flag order from the actor name: "C3-Russian Outpost" and "05W-Servo" both give the number after the lane letters.</summary>
        private static int FlagNumber(ObjectiveInfo o)
        {
            var m = Regex.Match(o.ActorName, @"^\D*(\d+)");
            return m.Success && int.TryParse(m.Groups[1].Value, out int n) ? n : int.MaxValue;
        }
        #endregion

        #region Drawing
        // Colors are RGB plus alpha (0 invisible, 255 solid).
        // Objective rings and labels share one opacity.
        private const byte ObjectiveAlpha = 130;

        private static readonly SKPaint RingOutlinePaint = new()
        {
            Color = new SKColor(0, 0, 0, ObjectiveAlpha),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3.5f,
            IsAntialias = true
        };

        // Objective status colors: gray until a team has the objective, then that team's color from the Colors tab
        private static readonly SKColor NeutralColor = new SKColor(180, 180, 180);

        // The ring color is set per objective when drawing
        private static readonly SKPaint RingPaint = new()
        {
            Color = new SKColor(255, 255, 255, ObjectiveAlpha),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.75f,
            IsAntialias = true
        };

        // Line connecting the flags in order, colored per segment when drawing
        private const byte RouteNeutralAlpha = 100;
        private const byte RouteTeamAlpha = 170;
        private static readonly SKPaint RoutePaint = new()
        {
            Color = new SKColor(255, 255, 255, RouteNeutralAlpha),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };

        // Protection zone shading, one color per team, no outline. The color is set per zone when drawing.
        private const byte ZoneFillAlpha = 30;
        private static readonly SKPaint ZoneFillPaint = new()
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        private static readonly SKPaint LabelOutlinePaint = new()
        {
            Color = new SKColor(0, 0, 0, ObjectiveAlpha),
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
            Color = new SKColor(255, 255, 255, ObjectiveAlpha),
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
            var zones = _zoneSnapshot;
            if (objectives.Length == 0 && zones.Length == 0) return;

            double pxPerUnit = map.ConfigFile.Scale * mapParams.XScale;
            var clip = canvas.LocalClipBounds;

            DrawZones(canvas, zones, mapParams, map, pxPerUnit, clip);
            DrawRoute(canvas, _route, mapParams, map);

            float textSize = 11f * uiScale;
            LabelFillPaint.TextSize = LabelOutlinePaint.TextSize = textSize;

            foreach (var o in objectives)
            {
                if (o.IsMain) continue; // a main is shown by its protection zone, not by its capture bounds

                var center = o.Position.ToMapPos(map).ToZoomedPos(mapParams).GetPoint();
                float reach = (float)(o.Reach * pxPerUnit);

                if (center.X + reach < clip.Left || center.X - reach > clip.Right ||
                    center.Y + reach < clip.Top || center.Y - reach > clip.Bottom)
                    continue;

                RingPaint.Color = StatusColor(StatusTeam(o)).WithAlpha(ObjectiveAlpha);

                switch (o.Shape)
                {
                    case ObjectiveShape.Sphere:
                    {
                        float r = (float)(o.Radius * pxPerUnit);
                        canvas.DrawCircle(center, r, RingOutlinePaint);
                        canvas.DrawCircle(center, r, RingPaint);
                        break;
                    }
                    case ObjectiveShape.Box:
                        DrawBox(canvas, o, map, mapParams);
                        break;
                }

                // Bounds are enough on their own; the name is only there for objectives that have none.
                if (o.Shape == ObjectiveShape.None && !string.IsNullOrEmpty(o.DisplayName))
                {
                    // Label sits on the objective, nudged down so it's vertically centered.
                    float ty = center.Y + textSize * 0.35f;
                    canvas.DrawText(o.DisplayName, center.X, ty, LabelOutlinePaint);
                    canvas.DrawText(o.DisplayName, center.X, ty, LabelFillPaint);
                }
            }
        }

        /// <summary>
        /// A thin line through the flags in order, whether or not they have bounds. A segment takes a team's color
        /// when that team owns both flags it joins, and stays gray otherwise.
        /// </summary>
        private static void DrawRoute(SKCanvas canvas, ObjectiveInfo[] route, MapParameters mapParams, Map map)
        {
            if (route.Length < 2) return;

            var from = route[0].Position.ToMapPos(map).ToZoomedPos(mapParams).GetPoint();
            for (int i = 1; i < route.Length; i++)
            {
                var to = route[i].Position.ToMapPos(map).ToZoomedPos(mapParams).GetPoint();

                byte a = route[i - 1].OwningTeam, b = route[i].OwningTeam;
                bool owned = a == b && a is 1 or 2;
                RoutePaint.Color = owned
                    ? StatusColor(a).WithAlpha(RouteTeamAlpha)
                    : NeutralColor.WithAlpha(RouteNeutralAlpha);

                canvas.DrawLine(from, to, RoutePaint);
                from = to;
            }
        }

        /// <summary>The team an objective is shown as: its owner, or while it has no owner the team that is capturing it. 0 for an unclaimed one.</summary>
        private static byte StatusTeam(ObjectiveInfo o)
            => o.OwningTeam is 1 or 2 ? o.OwningTeam : o.CapturingTeam is 1 or 2 ? o.CapturingTeam : (byte)0;

        /// <summary>Gray for an unclaimed objective, otherwise friendly or enemy relative to the local player's team, like the players on the radar.</summary>
        private static SKColor StatusColor(byte team)
        {
            int local = Memory.LocalPlayer?.TeamID ?? -1;
            if (team is not (1 or 2) || local is not (1 or 2)) return NeutralColor;
            return team == local ? SKPaints.Friendly : SKPaints.Enemy;
        }

        /// <summary>Same colors as the players on the radar: friendly or enemy relative to the local player's team.</summary>
        private static SKColor TeamColor(byte team)
        {
            int local = Memory.LocalPlayer?.TeamID ?? -1;
            if (team is not (1 or 2) || local is not (1 or 2)) return SKColors.Gray;
            return team == local ? SKPaints.Friendly : SKPaints.Enemy;
        }

        private static void DrawZones(SKCanvas canvas, MainZoneInfo[] zones, MapParameters mapParams, Map map, double pxPerUnit, SKRect clip)
        {
            foreach (var z in zones)
            {
                var center = z.Position.ToMapPos(map).ToZoomedPos(mapParams).GetPoint();
                float reach = (float)(z.Reach * pxPerUnit);
                if (center.X + reach < clip.Left || center.X - reach > clip.Right ||
                    center.Y + reach < clip.Top || center.Y - reach > clip.Bottom)
                    continue;

                ZoneFillPaint.Color = TeamColor(z.Team).WithAlpha(ZoneFillAlpha);

                if (z.IsSphere)
                {
                    canvas.DrawCircle(center, (float)(z.Radius * pxPerUnit), ZoneFillPaint);
                }
                else
                {
                    using var path = new SKPath();
                    AddBoxPath(path, z.Position, z.ExtentX, z.ExtentY, z.YawDegrees, map, mapParams);
                    canvas.DrawPath(path, ZoneFillPaint);
                }
            }
        }

        /// <summary>Adds the outline of a yawed box, given by its center and half extents in world cm, to the path.</summary>
        private static void AddBoxPath(SKPath path, Vector3D center, double extentX, double extentY, double yawDegrees, Map map, MapParameters mapParams)
        {
            double yaw = yawDegrees * Math.PI / 180.0;
            double c = Math.Cos(yaw), s = Math.Sin(yaw);
            var corners = new (double x, double y)[]
            {
                (-extentX, -extentY), (extentX, -extentY), (extentX, extentY), (-extentX, extentY)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                var (dx, dy) = corners[i];
                var world = new Vector3D(
                    center.X + dx * c - dy * s,
                    center.Y + dx * s + dy * c,
                    center.Z);
                var p = world.ToMapPos(map).ToZoomedPos(mapParams).GetPoint();
                if (i == 0) path.MoveTo(p); else path.LineTo(p);
            }
            path.Close();
        }

        private static void DrawBox(SKCanvas canvas, ObjectiveInfo o, Map map, MapParameters mapParams)
        {
            using var path = new SKPath();
            AddBoxPath(path, o.Position, o.ExtentX, o.ExtentY, o.YawDegrees, map, mapParams);
            canvas.DrawPath(path, RingOutlinePaint);
            canvas.DrawPath(path, RingPaint);
        }
        #endregion
    }
}
