using System.Diagnostics;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// Client-side render tweaks made through the engine's own console variables, the same ones the in-game
    /// console would change: view distance, fog and foliage. Nothing is sent to the server, it only changes what
    /// this client draws.
    ///
    /// The variables are found by <see cref="ConsoleVariables"/> (the first start of a game takes a few seconds
    /// while the registry is located). While a tweak is on its values are re-written every half second, because the
    /// game applies its own graphics settings again on events such as a map change. Every original value is read
    /// first and put back when the tweak is switched off, the radar closes or the game ends.
    ///
    /// Logging ([RenderTweaks]): Info for what the user did and what went wrong, Debug (renderTweaksDebugLog) for
    /// every variable that was found, its original value, and every time the game changed one back.
    /// </summary>
    public class RenderTweaks : Manager
    {
        public const string NAME = "RenderTweaks";

        private const int TickMs = 500;
        private const int StopJoinMs = 3000;

        private readonly Game _game;
        private readonly object _lifecycleLock = new();
        private CancellationTokenSource _cts;
        private Thread _worker;

        public RenderTweaks(ulong playerController, bool inGame, Game game)
            : base(playerController, inGame)
        {
            _game = game;
        }

        private static void Log(string message)
        {
            if (Program.Config.RenderTweaksDebugLog)
                Logger.Debug($"[{NAME}] {message}");
        }

        #region Control
        /// <summary>Starts the worker while at least one tweak is switched on in the config, and stops it (restoring everything) when none is.</summary>
        public void Refresh()
        {
            var cfg = Program.Config;
            bool any = cfg.RenderNoFoliage;

            lock (_lifecycleLock)
            {
                if (any)
                {
                    if (_worker == null || !_worker.IsAlive)
                    {
                        _cts = new CancellationTokenSource();
                        var token = _cts.Token;
                        _worker = new Thread(() => Run(token)) { IsBackground = true, Name = NAME };
                        _worker.Start();
                    }
                }
                else
                {
                    Stop();
                }
            }
        }

        public void Shutdown()
        {
            lock (_lifecycleLock) Stop();
        }

        private void Stop()
        {
            var cts = _cts;
            var worker = _worker;
            _cts = null;
            _worker = null;
            if (cts == null) return;

            cts.Cancel();
            if (worker != null && worker != Thread.CurrentThread && !worker.Join(StopJoinMs))
                Logger.Error($"[{NAME}] The worker did not finish within {StopJoinMs} ms, values may not be restored yet");
        }
        #endregion

        #region Worker
        private sealed class Applied
        {
            public CvarHandle Handle;
            public float OriginalFloat, WantFloat;
            public int OriginalInt, WantInt;
            public int Resets;
            public bool Written;        // the wanted value has been written at least once
        }

        private readonly Dictionary<string, Applied> _applied = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _reportedMissing = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>What the config asks for right now: variable name and value.</summary>
        private static List<(string Name, double Value)> Desired()
        {
            var cfg = Program.Config;
            var list = new List<(string, double)>();
            if (cfg.RenderNoFoliage)
            {
                list.Add(("foliage.DensityScale", 0));
                list.Add(("grass.DensityScale", 0));
                list.Add(("grass.Enable", 0));
            }
            return list;
        }

        private void Run(CancellationToken token)
        {
            Log("worker started");
            try
            {
                Logger.Info("[RenderTweaks] Locating the engine's console variables (a few seconds the first time)...");
                if (!ConsoleVariables.Instance.EnsureReady(Memory._squadBase, Log, token))
                {
                    if (!token.IsCancellationRequested)
                        Logger.Error("[RenderTweaks] The engine's console variables could not be found, the render tweaks cannot be used (see the log above)");
                    return;
                }
                Logger.Info($"[RenderTweaks] Ready ({ConsoleVariables.Instance.Count} console variables found)");

                while (!token.IsCancellationRequested)
                {
                    if (_game == null || !_game.InGame)
                    {
                        Log("the game is no longer in progress, stopping");
                        return;
                    }

                    try
                    {
                        Reconcile();
                    }
                    catch (DMAShutdown)
                    {
                        _applied.Clear();
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[{NAME}] Update failed: {ex.Message}");
                    }

                    if (token.WaitHandle.WaitOne(TickMs))
                        return;
                }
            }
            catch (DMAShutdown)
            {
                Log("DMA shutdown, stopping");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Worker crashed: {ex}");
            }
            finally
            {
                RestoreAll("worker exit");
                Log("worker exiting");
            }
        }

        /// <summary>Makes the game match the config: applies what is wanted, puts back what no longer is, and re-writes what the game changed.</summary>
        private void Reconcile()
        {
            var console = ConsoleVariables.Instance;
            var desired = Desired();
            var wanted = new HashSet<string>(desired.Select(d => d.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var name in _applied.Keys.Where(n => !wanted.Contains(n)).ToList())
                Restore(name, "switched off");

            foreach (var (name, value) in desired)
            {
                if (!console.TryResolve(name, out var handle))
                {
                    if (_reportedMissing.Add(name))
                        Log(console.Exists(name)
                            ? $"{name}: found, but the place of its value is not known (see Configuration\\ConsoleVariables.txt)"
                            : $"{name}: not in this game's console variables");
                    continue;
                }

                if (!_applied.TryGetValue(name, out var applied))
                {
                    applied = new Applied { Handle = handle };
                    if (handle.IsFloat) applied.OriginalFloat = console.ReadFloat(handle);
                    else applied.OriginalInt = console.ReadInt(handle);
                    _applied[name] = applied;
                    Log($"{name} @0x{handle.Object:X}+0x{handle.ValueOffset:X} ({(handle.IsFloat ? "float" : "int")}): original {(handle.IsFloat ? applied.OriginalFloat.ToString("G6") : applied.OriginalInt.ToString())}, " +
                        $"setting {value:G6}");
                }

                if (handle.IsFloat)
                {
                    float want = (float)value;
                    float now = console.ReadFloat(handle);
                    if (!applied.Written || now != want || applied.WantFloat != want)
                    {
                        if (applied.Written && applied.WantFloat == want && now != want)
                        {
                            applied.Resets++;
                            Log($"the game changed {name} back to {now:G6}, writing {want:G6} again (reset #{applied.Resets})");
                        }
                        applied.WantFloat = want;
                        console.WriteFloat(handle, want);
                        applied.Written = true;
                    }
                }
                else
                {
                    int want = (int)Math.Round(value);
                    int now = console.ReadInt(handle);
                    if (!applied.Written || now != want || applied.WantInt != want)
                    {
                        if (applied.Written && applied.WantInt == want && now != want)
                        {
                            applied.Resets++;
                            Log($"the game changed {name} back to {now}, writing {want} again (reset #{applied.Resets})");
                        }
                        applied.WantInt = want;
                        console.WriteInt(handle, want);
                        applied.Written = true;
                    }
                }
            }
        }

        private void Restore(string name, string reason)
        {
            if (!_applied.TryGetValue(name, out var applied)) return;
            try
            {
                var console = ConsoleVariables.Instance;
                if (applied.Handle.IsFloat) console.WriteFloat(applied.Handle, applied.OriginalFloat);
                else console.WriteInt(applied.Handle, applied.OriginalInt);
                Log($"restored {name} to {(applied.Handle.IsFloat ? applied.OriginalFloat.ToString("G6") : applied.OriginalInt.ToString())} ({reason})");
            }
            catch (DMAShutdown) { throw; }
            catch (Exception ex)
            {
                Log($"could not restore {name}: {ex.Message}");
            }
            _applied.Remove(name);
        }

        private void RestoreAll(string reason)
        {
            foreach (var name in _applied.Keys.ToList())
            {
                try { Restore(name, reason); }
                catch (DMAShutdown) { _applied.Clear(); return; }
            }
        }
        #endregion
    }
}
