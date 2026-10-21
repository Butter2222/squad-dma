using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace squad_dma
{
    public enum HealthLevel
    {
        /// <summary>Nothing to measure yet (no game, or sitting in a menu).</summary>
        Idle,
        Good,
        Warning,
        Bad
    }

    /// <summary>Everything the diagnostics widget shows, computed once per refresh from <see cref="PerfStats"/>.</summary>
    public sealed class PerfSnapshot
    {
        public DateTime Time;
        public double WindowSeconds;

        // Status
        public HealthLevel Health;
        public string HealthReason = "";
        public GameStatus GameStatus;
        public string Map = "";
        public string PlayerState = "";

        // Render
        public double Fps;
        public double FrameAvgMs, FrameWorstMs, DrawAvgMs;

        // Data loop
        public double DataHz, DataPassMs, DataPassMaxMs, DataAgeMs;

        // DMA
        public double ReadsPerSec, ReadItemsPerSec, WritesPerSec;
        public double ReadAvgMs, ReadMaxMs, ReadFailPct, WriteFailPct;
        public bool KeyReaderReady;

        // World
        public int Players, Vehicles, Deployables;
        public double ActorScanMs, ActorScanAgeSec, ObjectivesScanMs;
        public bool LastScanWasFull;

        // Process
        public double ManagedMb, WorkingSetMb, GcPauseMs;
        public int Gen0, Gen1, Gen2;
        public int TileQueue, TilesCached;

        // Session
        public string[] Features = Array.Empty<string>();
        public string Version = "";

        public string ToText()
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine($"Squad DMA diagnostics {Time:yyyy-MM-dd HH:mm:ss}  v{Version}");
            sb.AppendLine($"Status: {Health}{(string.IsNullOrEmpty(HealthReason) ? "" : " - " + HealthReason)}");
            sb.AppendLine($"Game: {GameStatus}  Map: {(string.IsNullOrEmpty(Map) ? "-" : Map)}  Player: {(string.IsNullOrEmpty(PlayerState) ? "-" : PlayerState)}");
            sb.AppendLine($"Window: {WindowSeconds:F1} s");
            sb.AppendLine();
            sb.AppendLine("Render");
            sb.AppendLine($"  FPS {Fps:F0}   frame avg {FrameAvgMs:F1} ms   worst {FrameWorstMs:F1} ms   draw {DrawAvgMs:F1} ms");
            sb.AppendLine("Data loop");
            sb.AppendLine($"  {DataHz:F0} Hz   pass {DataPassMs:F1} ms (max {DataPassMaxMs:F1})   newest data {DataAgeMs:F0} ms old");
            sb.AppendLine("DMA");
            sb.AppendLine($"  reads {ReadsPerSec:F0}/s ({ReadItemsPerSec:F0} items/s)   writes {WritesPerSec:F0}/s");
            sb.AppendLine($"  read latency avg {ReadAvgMs:F2} ms   max {ReadMaxMs:F1} ms   failed reads {ReadFailPct:F1}%   failed writes {WriteFailPct:F1}%");
            sb.AppendLine($"  key reader {(KeyReaderReady ? "ready" : "NOT ready")}");
            sb.AppendLine("World");
            sb.AppendLine($"  players {Players}   vehicles {Vehicles}   deployables {Deployables}");
            sb.AppendLine($"  actor scan {ActorScanMs:F0} ms ({(LastScanWasFull ? "full" : "quick")}, {ActorScanAgeSec:F1} s ago)   objectives scan {ObjectivesScanMs:F0} ms");
            sb.AppendLine("Process");
            sb.AppendLine($"  managed {ManagedMb:F0} MB   working set {WorkingSetMb:F0} MB");
            sb.AppendLine($"  GC gen0/1/2 {Gen0}/{Gen1}/{Gen2}   total pause {GcPauseMs:F0} ms");
            sb.AppendLine($"  map tiles cached {TilesCached}   decode queue {TileQueue}");
            sb.AppendLine("Features");
            sb.AppendLine($"  {(Features.Length == 0 ? "none" : string.Join(", ", Features))}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Lock-free performance counters fed from the hot paths (DMA reads and writes, the data loop, the render loop)
    /// and read by the diagnostics widget a few times a second. Recording costs a timestamp and an interlocked add.
    /// </summary>
    public static class PerfStats
    {
        private static readonly double TicksPerMs = Stopwatch.Frequency / 1000.0;
        private static readonly string AppVersion =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

        #region DMA
        private static long _readOps, _readItems, _readFailedItems, _readTicks, _readMaxTicks;
        private static long _writeOps, _writeFailedOps;

        public static long Begin() => Stopwatch.GetTimestamp();

        /// <summary>One read call. <paramref name="items"/> is the number of reads in it (a scatter read carries many).</summary>
        public static void EndRead(long start, int items, int failedItems)
        {
            long elapsed = Stopwatch.GetTimestamp() - start;
            Interlocked.Increment(ref _readOps);
            Interlocked.Add(ref _readItems, items);
            if (failedItems > 0)
                Interlocked.Add(ref _readFailedItems, failedItems);
            Interlocked.Add(ref _readTicks, elapsed);

            long seen;
            while (elapsed > (seen = Volatile.Read(ref _readMaxTicks)))
                if (Interlocked.CompareExchange(ref _readMaxTicks, elapsed, seen) == seen)
                    break;
        }

        public static void EndWrite(bool ok)
        {
            Interlocked.Increment(ref _writeOps);
            if (!ok)
                Interlocked.Increment(ref _writeFailedOps);
        }
        #endregion

        #region Render (UI thread only)
        private const int FrameHistory = 120;
        private static readonly float[] _frameIntervals = new float[FrameHistory];
        private static int _frameIndex;
        private static long _frames, _lastFrameStart, _frameStart;
        private static double _windowIntervalSum, _windowIntervalMax, _windowDrawSum;
        private static long _windowFrames;

        /// <summary>The last frame times in ms, oldest first, for the sparkline. Valid on the UI thread.</summary>
        public static float FrameInterval(int ageFromOldest) => _frameIntervals[(_frameIndex + ageFromOldest) % FrameHistory];
        public static int FrameHistoryLength => FrameHistory;

        public static void FrameBegin()
        {
            _frameStart = Stopwatch.GetTimestamp();
            if (_lastFrameStart != 0)
            {
                double interval = (_frameStart - _lastFrameStart) / TicksPerMs;
                _frameIntervals[_frameIndex] = (float)interval;
                _frameIndex = (_frameIndex + 1) % FrameHistory;
                _windowIntervalSum += interval;
                if (interval > _windowIntervalMax) _windowIntervalMax = interval;
                _windowFrames++;
            }
            _lastFrameStart = _frameStart;
            _frames++;
        }

        public static void FrameEnd()
        {
            _windowDrawSum += (Stopwatch.GetTimestamp() - _frameStart) / TicksPerMs;
        }
        #endregion

        #region Data loop and scans
        private static long _dataPasses, _dataPassTicks, _dataPassMaxTicks, _lastDataPassAt;
        private static long _actorScanTicks, _actorScanAt, _objectivesScanTicks;
        private static volatile bool _actorScanFull;

        /// <summary>One completed pass of the position loop (all tracked actors read and applied).</summary>
        public static void RecordDataPass(long start)
        {
            long now = Stopwatch.GetTimestamp();
            long elapsed = now - start;
            Interlocked.Increment(ref _dataPasses);
            Interlocked.Add(ref _dataPassTicks, elapsed);
            long seen;
            while (elapsed > (seen = Volatile.Read(ref _dataPassMaxTicks)))
                if (Interlocked.CompareExchange(ref _dataPassMaxTicks, elapsed, seen) == seen)
                    break;
            Interlocked.Exchange(ref _lastDataPassAt, now);
        }

        public static void RecordActorScan(long start, bool full)
        {
            long now = Stopwatch.GetTimestamp();
            Interlocked.Exchange(ref _actorScanTicks, now - start);
            Interlocked.Exchange(ref _actorScanAt, now);
            _actorScanFull = full;
        }

        public static void RecordObjectivesScan(long start) =>
            Interlocked.Exchange(ref _objectivesScanTicks, Stopwatch.GetTimestamp() - start);
        #endregion

        #region Map tiles
        public static volatile int TileQueue;
        public static volatile int TilesCached;
        #endregion

        #region Snapshot
        // Totals at the previous snapshot, to turn the counters into per-second rates
        private static long _prevTimestamp;
        private static long _prevFrames, _prevReadOps, _prevReadItems, _prevReadFailed, _prevReadTicks;
        private static long _prevWriteOps, _prevWriteFailed, _prevDataPasses, _prevDataPassTicks;

        public static PerfSnapshot Latest { get; private set; } = new PerfSnapshot { Version = AppVersion, Time = DateTime.Now };

        /// <summary>Call from the UI thread, a few times a second.</summary>
        public static PerfSnapshot TakeSnapshot()
        {
            long now = Stopwatch.GetTimestamp();
            double seconds = _prevTimestamp == 0 ? 0 : (now - _prevTimestamp) / (double)Stopwatch.Frequency;
            var s = new PerfSnapshot { Time = DateTime.Now, WindowSeconds = seconds, Version = AppVersion };

            // Totals first, so every rate in this snapshot covers the same moment
            long frames = _frames;
            long readOps = Volatile.Read(ref _readOps), readItems = Volatile.Read(ref _readItems), readFailed = Volatile.Read(ref _readFailedItems);
            long readTicks = Volatile.Read(ref _readTicks);
            long writeOps = Volatile.Read(ref _writeOps), writeFailed = Volatile.Read(ref _writeFailedOps);
            long passes = Volatile.Read(ref _dataPasses), passTicks = Volatile.Read(ref _dataPassTicks);
            long readMax = Interlocked.Exchange(ref _readMaxTicks, 0);
            long passMax = Interlocked.Exchange(ref _dataPassMaxTicks, 0);

            if (seconds > 0)
            {
                long dFrames = frames - _prevFrames;
                long dReadOps = readOps - _prevReadOps, dReadItems = readItems - _prevReadItems, dReadFailed = readFailed - _prevReadFailed;
                long dWriteOps = writeOps - _prevWriteOps, dWriteFailed = writeFailed - _prevWriteFailed;
                long dPasses = passes - _prevDataPasses;

                s.Fps = dFrames / seconds;
                s.FrameAvgMs = _windowFrames > 0 ? _windowIntervalSum / _windowFrames : 0;
                s.FrameWorstMs = _windowIntervalMax;
                s.DrawAvgMs = _windowFrames > 0 ? _windowDrawSum / _windowFrames : 0;

                s.DataHz = dPasses / seconds;
                s.DataPassMs = dPasses > 0 ? (passTicks - _prevDataPassTicks) / TicksPerMs / dPasses : 0;
                s.DataPassMaxMs = passMax / TicksPerMs;

                s.ReadsPerSec = dReadOps / seconds;
                s.ReadItemsPerSec = dReadItems / seconds;
                s.WritesPerSec = dWriteOps / seconds;
                s.ReadAvgMs = dReadOps > 0 ? (readTicks - _prevReadTicks) / TicksPerMs / dReadOps : 0;
                s.ReadMaxMs = readMax / TicksPerMs;
                s.ReadFailPct = dReadItems > 0 ? 100.0 * dReadFailed / dReadItems : 0;
                s.WriteFailPct = dWriteOps > 0 ? 100.0 * dWriteFailed / dWriteOps : 0;
            }
            else
            {
                // First snapshot: nothing to compare with yet
                s.Fps = Latest.Fps;
            }

            _windowIntervalSum = _windowIntervalMax = _windowDrawSum = 0;
            _windowFrames = 0;
            _prevTimestamp = now;
            _prevFrames = frames;
            _prevReadOps = readOps; _prevReadItems = readItems; _prevReadFailed = readFailed; _prevReadTicks = readTicks;
            _prevWriteOps = writeOps; _prevWriteFailed = writeFailed;
            _prevDataPasses = passes; _prevDataPassTicks = passTicks;

            long lastPass = Interlocked.Read(ref _lastDataPassAt);
            s.DataAgeMs = lastPass == 0 ? double.PositiveInfinity : (now - lastPass) / TicksPerMs;

            long scanAt = Interlocked.Read(ref _actorScanAt);
            s.ActorScanMs = Interlocked.Read(ref _actorScanTicks) / TicksPerMs;
            s.ActorScanAgeSec = scanAt == 0 ? 0 : (now - scanAt) / (double)Stopwatch.Frequency;
            s.LastScanWasFull = _actorScanFull;
            s.ObjectivesScanMs = Interlocked.Read(ref _objectivesScanTicks) / TicksPerMs;

            s.KeyReaderReady = InputManager.IsReady;
            s.TileQueue = TileQueue;
            s.TilesCached = TilesCached;

            // Process
            s.Gen0 = GC.CollectionCount(0);
            s.Gen1 = GC.CollectionCount(1);
            s.Gen2 = GC.CollectionCount(2);
            s.GcPauseMs = GC.GetTotalPauseDuration().TotalMilliseconds;
            s.ManagedMb = GC.GetTotalMemory(false) / 1048576.0;
            s.WorkingSetMb = Environment.WorkingSet / 1048576.0;

            FillSession(s);
            Classify(s);
            Latest = s;
            return s;
        }

        private static void FillSession(PerfSnapshot s)
        {
            s.GameStatus = Memory.GameStatus;
            var game = Memory._game;
            s.Map = game?.MapName ?? "";
            s.PlayerState = game?.GetPlayerState().ToString() ?? "";

            int players = 0, vehicles = 0, deployables = 0;
            var actors = game?.Actors;
            if (actors != null && s.GameStatus == GameStatus.InGame)
            {
                foreach (var kv in actors)
                {
                    var type = kv.Value.ActorType;
                    if (type == ActorType.Player) players++;
                    else if (Names.Vehicles.Contains(type)) vehicles++;
                    else if (Names.Deployables.Contains(type)) deployables++;
                }
            }
            s.Players = players;
            s.Vehicles = vehicles;
            s.Deployables = deployables;

            var c = Program.Config;
            var features = new List<string>(8);
            void Add(bool on, string name) { if (on) features.Add(name); }
            Add(c.SetFreeCamera, "Free Camera");
            Add(c.SetAirStuck, "Air Stuck");
            Add(c.SetSpeedHack, "Speed");
            Add(c.DisableCollision, "No Collision");
            Add(c.NoRecoil, "No Recoil");
            Add(c.NoSpread, "No Spread");
            Add(c.NoSway, "No Sway");
            Add(c.NoCameraShake, "No Shake");
            Add(c.DisableSuppression, "No Suppression");
            Add(c.InfiniteAmmo, "Infinite Ammo");
            Add(c.RapidFire, "Rapid Fire");
            Add(c.ForceFullAuto, "Full Auto");
            Add(c.QuickSwap, "Quick Swap");
            Add(c.QuickAds, "Quick ADS");
            Add(c.QuickZoom, "Quick Zoom");
            Add(c.InstantGrenade, "Instant Grenade");
            Add(c.FireSupport, "Fire Support");
            Add(c.SetInteractionDistances, "Interaction Dist.");
            Add(c.AllowShootingInMainBase, "Main Base Fire");
            s.Features = features.ToArray();
        }

        /// <summary>One verdict for the status dot. The first problem found is the reason shown next to it.</summary>
        private static void Classify(PerfSnapshot s)
        {
            if (s.GameStatus != GameStatus.InGame)
            {
                s.Health = HealthLevel.Idle;
                s.HealthReason = s.GameStatus == GameStatus.NotFound ? "Waiting for game" : "In menu";
                return;
            }

            if (s.DataAgeMs > 1000)
            {
                s.Health = HealthLevel.Bad;
                s.HealthReason = double.IsInfinity(s.DataAgeMs) ? "no data yet" : $"data {s.DataAgeMs / 1000:F1} s old";
            }
            else if (s.ReadFailPct >= 20)
            {
                s.Health = HealthLevel.Bad;
                s.HealthReason = $"{s.ReadFailPct:F0}% DMA reads failing";
            }
            else if (s.DataAgeMs > 300)
            {
                s.Health = HealthLevel.Warning;
                s.HealthReason = $"data {s.DataAgeMs:F0} ms old";
            }
            else if (s.ReadFailPct >= 3)
            {
                s.Health = HealthLevel.Warning;
                s.HealthReason = $"{s.ReadFailPct:F0}% DMA reads failing";
            }
            else if (s.WindowSeconds > 0 && s.DataHz < 10)
            {
                s.Health = HealthLevel.Warning;
                s.HealthReason = $"slow updates ({s.DataHz:F0} Hz)";
            }
            else if (s.WindowSeconds > 0 && s.Fps < 30)
            {
                s.Health = HealthLevel.Warning;
                s.HealthReason = $"low fps ({s.Fps:F0})";
            }
            else
            {
                s.Health = HealthLevel.Good;
                s.HealthReason = "";
            }
        }
        #endregion
    }
}
