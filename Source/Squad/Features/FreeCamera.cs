using System.Diagnostics;
using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// Client-side free camera: the view is detached from the soldier and flown around while the soldier stays put.
    ///
    /// This is NOT the server's admin camera. That one is a pawn the server spawns and possesses, which cannot be
    /// done from memory; this only changes what the local renderer draws.
    ///
    /// How it works: the soldier owns a built-in third-person debug camera. In mode DebugCameraStationary the
    /// engine keeps the camera at ViewPos3p / ViewOrientation3p instead of on the character. The feature switches
    /// the soldier into that mode and moves ViewPos3p with the movement keys; the look direction comes from the
    /// PlayerController's ControlRotation, so the mouse keeps working. The engine builds the view from those values,
    /// so there is nothing to race (overwriting the finished camera pose instead was tried: the engine rewrites it
    /// every frame and the view flickered).
    ///
    /// While active (each part can be switched off in the config):
    ///  - the soldier is held in place: MaxAcceleration 0, so the movement keys cannot walk it away
    ///  - the body is held still: it stops following the look direction and its animations are paused
    ///
    /// Everything it changes is read first and put back when the camera is switched off, the radar closes or the
    /// game ends. Server side the soldier still turns with the mouse; memory writes cannot hide that.
    ///
    /// Logging ([FreeCamera]): Info for what the user did, Error for what went wrong, Debug for everything needed to
    /// diagnose it: the addresses and values found at start, what the engine did after the first write, and a
    /// heartbeat line every few seconds (see <see cref="LogHeartbeat"/>).
    /// </summary>
    public class FreeCamera : Manager
    {
        public const string NAME = "FreeCamera";

        private const int TickActiveMs = 3;        // while keys are held or the view is turning
        private const int TickIdleMs = 8;          // nothing is changing: leave the DMA link to the radar
        private const int SoldierCheckMs = 250;
        private const int HeartbeatMs = 5000;
        private const int VerifyAfterMs = 150;     // how long after the first write to check what the engine did
        private const int ErrorBackoffMs = 250;
        private const int ErrorLogEvery = 50;
        private const int StopJoinMs = 1500;
        private const double PositionToleranceCm = 0.5;
        private const double MaxPlausibleFollowErrorCm = 50_000; // a bigger "error" is a failed read, not a real offset

        private readonly Game _game;
        private readonly object _lifecycleLock = new();
        private CancellationTokenSource _cts;
        private Thread _worker;
        private DateTime _lastRestartLog = DateTime.MinValue;
        private volatile bool _gaveUp;             // the worker ended on its own; Apply() must not restart it
        private bool _loggedKeyConflict;

        public bool IsEnabled { get; private set; }

        /// <summary>Diagnostic detail for troubleshooting; only written when freeCameraDebugLog is on in the config.</summary>
        private static void Verbose(string message)
        {
            if (Program.Config.FreeCameraDebugLog)
                Logger.Debug(message);
        }

        public FreeCamera(ulong playerController, bool inGame, Game game)
            : base(playerController, inGame)
        {
            _game = game;
        }

        #region Control
        public void SetEnabled(bool enable)
        {
            lock (_lifecycleLock)
            {
                Verbose($"[{NAME}] SetEnabled({enable}) (was {IsEnabled}, workerAlive={_worker?.IsAlive ?? false})");
                IsEnabled = enable;

                if (enable)
                {
                    _gaveUp = false;
                    Start();
                }
                else
                {
                    Stop("disabled");
                }
            }
        }

        /// <summary>Called by the feature timer: only makes sure the worker is still running while the feature is on.</summary>
        public override void Apply()
        {
            if (!IsEnabled || _gaveUp) return;

            lock (_lifecycleLock)
            {
                if (_worker != null && _worker.IsAlive) return;

                if ((DateTime.UtcNow - _lastRestartLog).TotalSeconds >= 5)
                {
                    _lastRestartLog = DateTime.UtcNow;
                    Logger.Error($"[{NAME}] The worker is not running while the feature is enabled, restarting it");
                }
                Start();
            }
        }

        /// <summary>Stops the camera and waits for the soldier to be put back, so closing the radar never leaves it frozen.</summary>
        public void Shutdown()
        {
            lock (_lifecycleLock)
            {
                IsEnabled = false;
                Stop("shutdown");
            }
        }

        private void Start()
        {
            if (_worker != null && _worker.IsAlive)
            {
                Verbose($"[{NAME}] Start requested but the worker is already running");
                return;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _worker = new Thread(() => Run(token)) { IsBackground = true, Name = NAME };
            _worker.Start();
            Logger.Info("[FreeCamera] On");
        }

        private void Stop(string reason)
        {
            var cts = _cts;
            var worker = _worker;
            _cts = null;
            _worker = null;
            if (cts == null)
                return;

            cts.Cancel();
            if (worker?.IsAlive == true)
                Logger.Info("[FreeCamera] Off");
            Verbose($"[{NAME}] Stopped ({reason})");

            // The worker puts the soldier back as it exits; wait for that unless we are that thread
            if (worker != null && worker != Thread.CurrentThread && !worker.Join(StopJoinMs))
                Logger.Error($"[{NAME}] The worker did not finish within {StopJoinMs} ms, the soldier may not have been restored yet");
        }

        /// <summary>The worker ended by itself (not on foot, bad data, ...): make the config and the UI say so.</summary>
        private void GiveUp(string reason, bool expected = true)
        {
            _gaveUp = true;
            IsEnabled = false;
            if (expected)
                Logger.Info($"[FreeCamera] Free camera stopped: {reason}");
            else
                Logger.Error($"[{NAME}] Stopping on its own: {reason}");
            try
            {
                Program.Config.SetFreeCamera = false;
                Config.SaveConfig(Program.Config);
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Could not save the config after stopping: {ex.Message}");
            }
        }
        #endregion

        #region Session
        /// <summary>Addresses of one run, and everything the soldier looked like before it was touched.</summary>
        private sealed class Session
        {
            public ulong Soldier, CharacterMovement, PawnFlags, AnimFlags, RootYaw, PovLocation, ViewTargetPov;
            public byte OrigFlags, OrigMode, OrigPawnFlags, OrigAnimFlags;
            public Vector3D OrigViewPos, OrigViewOrientation;
            public float OrigAcceleration;
            public double StartYaw;
            public bool FreezeSoldier, FreezeBody, PauseAnims;
            public byte WantPawnFlags;
        }

        /// <summary>Per-heartbeat counters, reset every time the heartbeat line is written.</summary>
        private sealed class Stats
        {
            public long Ticks, Writes, ReadErrors, WriteErrors, BadRotations, BadReads;
            public long ModeResets, FlagResets, PositionResets, AccelerationResets, BodyResets;
            public long FollowSamples;
            public double TickTimeMs, FollowErrorSum, FollowErrorMax, YawDriftMax;

            public void Reset()
            {
                Ticks = Writes = ReadErrors = WriteErrors = BadRotations = BadReads = 0;
                ModeResets = FlagResets = PositionResets = AccelerationResets = BodyResets = 0;
                FollowSamples = 0;
                TickTimeMs = FollowErrorSum = FollowErrorMax = YawDriftMax = 0;
            }
        }

        private static bool IsFinite(Vector3D v) => double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z);
        private static bool IsZero(Vector3D v) => v.X == 0 && v.Y == 0 && v.Z == 0; // what a failed scatter read looks like

        private ulong ReadPawn() => Memory.ReadPtr(_playerController + Controller.Pawn);

        private ulong ReadSoldier()
        {
            ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
            return playerState == 0 ? 0 : Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
        }

        /// <summary>Validates that the camera can run and reads everything it will touch. Null (with the reason logged) if not.</summary>
        private Session Begin(Config cfg, out Vector3D startPosition, out Vector3D controlRotation)
        {
            startPosition = default;
            controlRotation = default;

            ulong soldier = ReadSoldier();
            ulong pawn = ReadPawn();
            Verbose($"[{NAME}] Pawn=0x{pawn:X} soldier=0x{soldier:X} player state={_game?.GetPlayerState()}");

            if (soldier == 0 || pawn != soldier)
            {
                GiveUp(soldier == 0
                    ? "there is no soldier (dead or not spawned yet)"
                    : "the controlled pawn is not the soldier (in a vehicle or other seat?); the camera only works on foot");
                return null;
            }

            ulong camMgr = Memory.ReadPtr(_playerController + PlayerController.PlayerCameraManager);
            var s = new Session
            {
                Soldier = soldier,
                ViewTargetPov = camMgr == 0 ? 0 : camMgr + PlayerCameraManager.ViewTarget + FTViewTarget.POV,
                FreezeSoldier = cfg.FreeCameraFreezeSoldier,
                FreezeBody = cfg.FreeCameraFreezeBody,
            };
            s.PovLocation = s.ViewTargetPov == 0 ? 0 : s.ViewTargetPov + FMinimalViewInfo.Location;

            s.OrigFlags = Memory.ReadValue<byte>(soldier + ASQSoldier.ThirdPersonFlags);
            s.OrigMode = Memory.ReadValue<byte>(soldier + ASQSoldier.ViewMode3p);
            s.OrigViewPos = Memory.ReadValue<Vector3D>(soldier + ASQSoldier.ViewPos3p);
            s.OrigViewOrientation = Memory.ReadValue<Vector3D>(soldier + ASQSoldier.ViewOrientation3p);
            controlRotation = Memory.ReadValue<Vector3D>(_playerController + Controller.ControlRotation);
            startPosition = s.PovLocation != 0 ? Memory.ReadValue<Vector3D>(s.PovLocation) : default;

            Verbose($"[{NAME}] Soldier 0x{soldier:X}: flags@0x{ASQSoldier.ThirdPersonFlags:X}=0x{s.OrigFlags:X2} (inThirdPerson={(s.OrigFlags & ASQSoldier.ThirdPersonViewBit) != 0}), " +
                         $"ViewMode3p@0x{ASQSoldier.ViewMode3p:X}={s.OrigMode}, ViewPos3p@0x{ASQSoldier.ViewPos3p:X}=({s.OrigViewPos.X:F1},{s.OrigViewPos.Y:F1},{s.OrigViewPos.Z:F1}), " +
                         $"ViewOrientation3p@0x{ASQSoldier.ViewOrientation3p:X}=(p{s.OrigViewOrientation.X:F1} y{s.OrigViewOrientation.Y:F1} r{s.OrigViewOrientation.Z:F1})");
            Verbose($"[{NAME}] Camera now: POV loc=({startPosition.X:F1},{startPosition.Y:F1},{startPosition.Z:F1}) (POV addr 0x{s.PovLocation:X}), " +
                         $"ControlRotation@0x{Controller.ControlRotation:X}=(p{controlRotation.X:F1} y{controlRotation.Y:F1} r{controlRotation.Z:F1})");

            if (s.PovLocation == 0 || !IsFinite(startPosition) || IsZero(startPosition) || !IsFinite(controlRotation))
            {
                GiveUp("the current camera location or control rotation could not be read", expected: false);
                return null;
            }

            // The soldier: no acceleration, so the movement keys cannot walk it away
            if (s.FreezeSoldier)
            {
                s.CharacterMovement = Memory.ReadPtr(soldier + Character.CharacterMovement);
                if (s.CharacterMovement == 0)
                {
                    Logger.Error($"[{NAME}] Could not find the soldier's movement component, the soldier will NOT be held in place");
                    s.FreezeSoldier = false;
                }
                else
                {
                    s.OrigAcceleration = Memory.ReadValue<float>(s.CharacterMovement + CharacterMovementComponent.MaxAcceleration);
                    var vel = Memory.ReadValue<Vector3D>(s.CharacterMovement + UMovementComponent.Velocity);
                    Verbose($"[{NAME}] Movement component 0x{s.CharacterMovement:X}: MaxAcceleration@0x{CharacterMovementComponent.MaxAcceleration:X}={s.OrigAcceleration:F1}, " +
                                 $"Velocity@0x{UMovementComponent.Velocity:X}=({vel.X:F1},{vel.Y:F1},{vel.Z:F1}); the soldier will be held in place");
                    if (cfg.SetAirStuck)
                        Logger.Error($"[{NAME}] Air Stuck is active as well: it writes the same movement values and the two will fight");
                }
            }
            else
            {
                Verbose($"[{NAME}] Holding the soldier in place is off (freeCameraFreezeSoldier=false): the movement keys will walk it");
            }

            // The body: no longer follows the look direction, animations paused
            if (s.FreezeBody)
            {
                s.PawnFlags = soldier + Pawn.ControllerRotationFlags;
                ulong mesh = Memory.ReadPtr(soldier + ACharacter.Mesh);
                s.AnimFlags = mesh == 0 ? 0 : mesh + USkeletalMeshComponent.AnimFlags;
                ulong root = Memory.ReadPtr(soldier + Actor.RootComponent);
                s.RootYaw = root == 0 ? 0 : root + USceneComponent.RelativeRotation + 0x8; // FRotator.Yaw (double)

                s.OrigPawnFlags = Memory.ReadValue<byte>(s.PawnFlags);
                s.WantPawnFlags = (byte)(s.OrigPawnFlags & ~(Pawn.UseControllerRotationPitchBit | Pawn.UseControllerRotationYawBit));
                if (s.AnimFlags != 0)
                {
                    s.OrigAnimFlags = Memory.ReadValue<byte>(s.AnimFlags);
                    s.PauseAnims = true;
                }
                else
                {
                    Logger.Error($"[{NAME}] Could not find the soldier's mesh, its animations will NOT be paused");
                }
                if (s.RootYaw != 0)
                    s.StartYaw = Memory.ReadValue<double>(s.RootYaw);

                Verbose($"[{NAME}] Body: pawn rotation flags@0x{Pawn.ControllerRotationFlags:X}=0x{s.OrigPawnFlags:X2} (follows look yaw={(s.OrigPawnFlags & Pawn.UseControllerRotationYawBit) != 0}), " +
                             $"mesh=0x{mesh:X} anim flags@0x{USkeletalMeshComponent.AnimFlags:X}=0x{s.OrigAnimFlags:X2} (paused={(s.OrigAnimFlags & USkeletalMeshComponent.PauseAnimsBit) != 0}), " +
                             $"actor yaw={s.StartYaw:F1}; the body will be held still");
            }
            else
            {
                Verbose($"[{NAME}] Holding the body still is off (freeCameraFreezeBody=false): it will turn with the camera");
            }

            return s;
        }

        /// <summary>Puts back everything <see cref="Begin"/> read. Only if it is still the same soldier.</summary>
        private void Restore(Session s)
        {
            try
            {
                if (ReadSoldier() != s.Soldier)
                {
                    Verbose($"[{NAME}] Restore skipped: the soldier is not 0x{s.Soldier:X} any more");
                    return;
                }

                var writes = new List<IScatterWriteEntry>
                {
                    new ScatterWriteDataEntry<byte>(s.Soldier + ASQSoldier.ViewMode3p, s.OrigMode),
                    new ScatterWriteDataEntry<byte>(s.Soldier + ASQSoldier.ThirdPersonFlags, s.OrigFlags),
                    new ScatterWriteDataEntry<Vector3D>(s.Soldier + ASQSoldier.ViewPos3p, s.OrigViewPos),
                    new ScatterWriteDataEntry<Vector3D>(s.Soldier + ASQSoldier.ViewOrientation3p, s.OrigViewOrientation),
                };
                if (s.FreezeSoldier)
                    writes.Add(new ScatterWriteDataEntry<float>(s.CharacterMovement + CharacterMovementComponent.MaxAcceleration, s.OrigAcceleration));
                if (s.FreezeBody)
                    writes.Add(new ScatterWriteDataEntry<byte>(s.PawnFlags, s.OrigPawnFlags));
                if (s.PauseAnims)
                    writes.Add(new ScatterWriteDataEntry<byte>(s.AnimFlags, s.OrigAnimFlags));

                Memory.WriteScatter(writes);
                Verbose($"[{NAME}] Restored the soldier: camera mode={s.OrigMode}, flags=0x{s.OrigFlags:X2}" +
                             (s.FreezeSoldier ? $", MaxAcceleration={s.OrigAcceleration:F1}" : "") +
                             (s.FreezeBody ? $", body flags=0x{s.OrigPawnFlags:X2}" : "") +
                             (s.PauseAnims ? $", anim flags=0x{s.OrigAnimFlags:X2}" : ""));
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Could not restore the soldier: {ex.Message}");
            }
        }
        #endregion

        #region Worker
        private void Run(CancellationToken token)
        {
            Verbose($"[{NAME}] Worker thread started");
            Session session = null;
            bool restoreNeeded = false;
            try
            {
                var cfg = Program.Config;
                LogControls(cfg);

                session = Begin(cfg, out var position, out var rotation);
                if (session == null)
                    return;

                restoreNeeded = true; // from the first write on, the soldier has to be put back however this ends
                Loop(token, session, cfg, position, rotation, ref restoreNeeded);
            }
            catch (DMAShutdown)
            {
                restoreNeeded = false; // nothing left to write to
                Verbose($"[{NAME}] DMA shutdown, stopping");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Worker crashed: {ex}");
            }
            finally
            {
                if (session != null && restoreNeeded)
                    Restore(session);
                Verbose($"[{NAME}] Worker thread exiting");
            }
        }

        private void Loop(CancellationToken token, Session s, Config cfg, Vector3D position, Vector3D rotation, ref bool restoreNeeded)
        {
            var stats = new Stats();
            long lastTick = Stopwatch.GetTimestamp();
            long lastHeartbeat = lastTick;
            long firstWrite = 0;
            bool verified = false;
            bool first = true;
            DateTime lastSoldierCheck = DateTime.UtcNow;
            int consecutiveErrors = 0;
            long totalErrors = 0;
            double speed = 0;

            Vector3D lastPosition = position;      // what ViewPos3p was last set to
            Vector3D lastRotation = rotation;      // what ViewOrientation3p was last set to
            const byte wantMode = ThirdPersonCameraMode.DebugCameraStationary;

            while (!token.IsCancellationRequested)
            {
                long tickStart = Stopwatch.GetTimestamp();
                double dt = Math.Clamp((tickStart - lastTick) / (double)Stopwatch.Frequency, 0.0, 0.1);
                lastTick = tickStart;
                bool active = false;

                try
                {
                    if (_game == null || !_game.InGame)
                    {
                        Verbose($"[{NAME}] The game is no longer in progress, stopping");
                        return;
                    }

                    // Still the same soldier, still on foot? (death, respawn, got into a vehicle)
                    if ((DateTime.UtcNow - lastSoldierCheck).TotalMilliseconds >= SoldierCheckMs)
                    {
                        lastSoldierCheck = DateTime.UtcNow;
                        ulong soldierNow = ReadSoldier();
                        if (soldierNow != s.Soldier)
                        {
                            Verbose($"[{NAME}] The soldier is gone (was 0x{s.Soldier:X}, now 0x{soldierNow:X}); nothing to restore");
                            restoreNeeded = false;
                            GiveUp("the soldier died or respawned");
                            return;
                        }
                        ulong pawnNow = ReadPawn();
                        if (pawnNow != s.Soldier)
                        {
                            GiveUp($"the soldier is no longer controlled (pawn is now 0x{pawnNow:X}, in a vehicle?)");
                            return; // the soldier still exists, so it is restored on the way out
                        }
                    }

                    // Everything this tick needs, in one DMA round trip
                    var map = new ScatterReadMap(1);
                    var round = map.AddRound();
                    round.AddEntry<Vector3D>(0, 0, _playerController + Controller.ControlRotation);
                    round.AddEntry<byte>(0, 1, s.Soldier + ASQSoldier.ThirdPersonFlags);
                    round.AddEntry<byte>(0, 2, s.Soldier + ASQSoldier.ViewMode3p);
                    round.AddEntry<Vector3D>(0, 3, s.Soldier + ASQSoldier.ViewPos3p);
                    round.AddEntry<Vector3D>(0, 4, s.PovLocation);
                    if (s.FreezeSoldier)
                        round.AddEntry<float>(0, 5, s.CharacterMovement + CharacterMovementComponent.MaxAcceleration);
                    if (s.FreezeBody)
                    {
                        round.AddEntry<byte>(0, 6, s.PawnFlags);
                        if (s.PauseAnims)
                            round.AddEntry<byte>(0, 7, s.AnimFlags);
                        if (s.RootYaw != 0)
                            round.AddEntry<double>(0, 8, s.RootYaw);
                    }
                    map.Execute();
                    var r = map.Results[0];

                    // A failed scatter read comes back as zeros; a zero look direction is not a real one
                    if (!r[0].TryGetResult(out rotation) || !IsFinite(rotation) || IsZero(rotation) || Math.Abs(rotation.X) > 360.0 || Math.Abs(rotation.Y) > 100000.0)
                    {
                        stats.BadRotations++;
                        if (stats.BadRotations % ErrorLogEvery == 1)
                            Logger.Error($"[{NAME}] The look direction could not be read or looks wrong (seen {stats.BadRotations}x)");
                        rotation = lastRotation;
                        Thread.Sleep(TickActiveMs);
                        continue;
                    }

                    bool haveFlags = r[1].TryGetResult<byte>(out var flagsNow);
                    bool haveMode = r[2].TryGetResult<byte>(out var modeNow);
                    bool havePos = r[3].TryGetResult<Vector3D>(out var viewPosNow) && !IsZero(viewPosNow);
                    bool havePov = r[4].TryGetResult<Vector3D>(out var povNow) && !IsZero(povNow);

                    // What the game changed behind our back since the last write
                    bool writeMode = first || (haveMode && modeNow != wantMode);
                    bool writeFlags = first || (haveFlags && flagsNow != 0 && (flagsNow & ASQSoldier.ThirdPersonViewBit) == 0);
                    bool writeView = first;
                    if (!first)
                    {
                        if (writeMode) stats.ModeResets++;
                        if (writeFlags) stats.FlagResets++;
                        if (havePos && (Math.Abs(viewPosNow.X - lastPosition.X) > PositionToleranceCm ||
                                        Math.Abs(viewPosNow.Y - lastPosition.Y) > PositionToleranceCm ||
                                        Math.Abs(viewPosNow.Z - lastPosition.Z) > PositionToleranceCm))
                        {
                            stats.PositionResets++;
                            writeView = true;
                        }
                    }

                    // How far the drawn camera is from where we asked it to be
                    if (!first && havePov)
                    {
                        double ex = povNow.X - lastPosition.X, ey = povNow.Y - lastPosition.Y, ez = povNow.Z - lastPosition.Z;
                        double err = Math.Sqrt(ex * ex + ey * ey + ez * ez);
                        if (err < MaxPlausibleFollowErrorCm)
                        {
                            stats.FollowErrorSum += err;
                            stats.FollowErrorMax = Math.Max(stats.FollowErrorMax, err);
                            stats.FollowSamples++;
                        }
                        else
                        {
                            stats.BadReads++;
                        }
                    }

                    bool writeAcceleration = false, writePawnFlags = false, writeAnimFlags = false;
                    byte animFlagsNow = 0;
                    if (s.FreezeSoldier)
                    {
                        writeAcceleration = first || (r.TryGetValue(5, out var accelEntry) && accelEntry.TryGetResult<float>(out var accelNow) && Math.Abs(accelNow) > 0.001f);
                        if (writeAcceleration && !first) stats.AccelerationResets++;
                    }
                    if (s.FreezeBody)
                    {
                        writePawnFlags = first || (r.TryGetValue(6, out var pfEntry) && pfEntry.TryGetResult<byte>(out var pawnFlagsNow) && pawnFlagsNow != s.WantPawnFlags);
                        if (s.PauseAnims)
                        {
                            writeAnimFlags = first || (r.TryGetValue(7, out var afEntry) && afEntry.TryGetResult(out animFlagsNow) && animFlagsNow != 0 && (animFlagsNow & USkeletalMeshComponent.PauseAnimsBit) == 0);
                        }
                        if (!first && (writePawnFlags || writeAnimFlags)) stats.BodyResets++;

                        if (s.RootYaw != 0 && r.TryGetValue(8, out var yawEntry) && yawEntry.TryGetResult<double>(out var yawNow) && yawNow != 0)
                        {
                            double drift = Math.Abs(((yawNow - s.StartYaw + 540.0) % 360.0 + 360.0) % 360.0 - 180.0);
                            stats.YawDriftMax = Math.Max(stats.YawDriftMax, drift);
                        }
                    }

                    // Movement
                    cfg = Program.Config;
                    var newPosition = Move(position, rotation, dt, cfg, out speed);
                    active = newPosition != position || rotation != lastRotation;
                    position = newPosition;
                    if (position != lastPosition || rotation != lastRotation)
                        writeView = true;

                    // Only what changed is written, so an idle camera costs almost nothing
                    var writes = new List<IScatterWriteEntry>(6);
                    if (writeView)
                    {
                        writes.Add(new ScatterWriteDataEntry<Vector3D>(s.Soldier + ASQSoldier.ViewPos3p, position));
                        writes.Add(new ScatterWriteDataEntry<Vector3D>(s.Soldier + ASQSoldier.ViewOrientation3p, rotation));
                    }
                    if (writeMode)
                        writes.Add(new ScatterWriteDataEntry<byte>(s.Soldier + ASQSoldier.ViewMode3p, wantMode));
                    if (writeFlags)
                    {
                        // keep the other bits as the game has them (the original byte on the very first write)
                        byte baseFlags = first || !haveFlags || flagsNow == 0 ? s.OrigFlags : flagsNow;
                        writes.Add(new ScatterWriteDataEntry<byte>(s.Soldier + ASQSoldier.ThirdPersonFlags, (byte)(baseFlags | ASQSoldier.ThirdPersonViewBit)));
                    }
                    if (writeAcceleration)
                    {
                        writes.Add(new ScatterWriteDataEntry<float>(s.CharacterMovement + CharacterMovementComponent.MaxAcceleration, 0f));
                        if (first)
                            writes.Add(new ScatterWriteDataEntry<Vector3D>(s.CharacterMovement + UMovementComponent.Velocity, default)); // stop any slide
                    }
                    if (writePawnFlags)
                        writes.Add(new ScatterWriteDataEntry<byte>(s.PawnFlags, s.WantPawnFlags));
                    if (writeAnimFlags)
                    {
                        byte baseAnim = first || animFlagsNow == 0 ? s.OrigAnimFlags : animFlagsNow;
                        writes.Add(new ScatterWriteDataEntry<byte>(s.AnimFlags, (byte)(baseAnim | USkeletalMeshComponent.PauseAnimsBit)));
                    }

                    if (writes.Count > 0)
                    {
                        try
                        {
                            Memory.WriteScatter(writes);
                            stats.Writes++;
                            if (writeView)
                            {
                                lastPosition = position;
                                lastRotation = rotation;
                            }
                        }
                        catch (Exception ex)
                        {
                            stats.WriteErrors++;
                            if (stats.WriteErrors % ErrorLogEvery == 1)
                                Logger.Error($"[{NAME}] Write failed (seen {stats.WriteErrors}x): {ex.Message}");
                            throw;
                        }
                    }

                    if (first)
                    {
                        first = false;
                        firstWrite = Stopwatch.GetTimestamp();
                        Verbose($"[{NAME}] First write: mode {s.OrigMode}->{wantMode}, flags 0x{s.OrigFlags:X2}->0x{(byte)(s.OrigFlags | ASQSoldier.ThirdPersonViewBit):X2}, " +
                                     $"ViewPos3p=({position.X:F1},{position.Y:F1},{position.Z:F1})");
                    }
                    else if (!verified && (Stopwatch.GetTimestamp() - firstWrite) * 1000.0 / Stopwatch.Frequency >= VerifyAfterMs)
                    {
                        verified = true;
                        VerifyEngineAccepted(s, wantMode, position);
                    }

                    consecutiveErrors = 0;
                    stats.Ticks++;
                    stats.TickTimeMs += (Stopwatch.GetTimestamp() - tickStart) * 1000.0 / Stopwatch.Frequency;

                    long now = Stopwatch.GetTimestamp();
                    if ((now - lastHeartbeat) * 1000.0 / Stopwatch.Frequency >= HeartbeatMs)
                    {
                        LogHeartbeat(stats, s, cfg, position, rotation, speed, (now - lastHeartbeat) / (double)Stopwatch.Frequency);
                        lastHeartbeat = now;
                        stats.Reset();
                    }
                }
                catch (DMAShutdown)
                {
                    restoreNeeded = false;
                    throw;
                }
                catch (Exception ex)
                {
                    consecutiveErrors++;
                    totalErrors++;
                    stats.ReadErrors++;
                    if (consecutiveErrors == 1 || consecutiveErrors % ErrorLogEvery == 0)
                        Logger.Error($"[{NAME}] Tick failed ({consecutiveErrors} in a row, {totalErrors} total): {ex.Message}");
                    if (token.WaitHandle.WaitOne(ErrorBackoffMs))
                        return;
                    continue;
                }

                if (token.WaitHandle.WaitOne(active ? TickActiveMs : TickIdleMs))
                    return;
            }
        }
        #endregion

        #region Movement and input
        private Vector3D Move(Vector3D pos, Vector3D rot, double dt, Config cfg, out double speed)
        {
            double forward = Axis(cfg, cfg.FreeCameraKeyForward, cfg.FreeCameraKeyBack);
            double right = Axis(cfg, cfg.FreeCameraKeyRight, cfg.FreeCameraKeyLeft);
            double up = Axis(cfg, cfg.FreeCameraKeyUp, cfg.FreeCameraKeyDown);

            speed = Math.Max(cfg.FreeCameraSpeed, 0f);
            if (IsDown(cfg, cfg.FreeCameraKeyFast))
                speed *= cfg.FreeCameraFastMultiplier;
            else if (IsDown(cfg, cfg.FreeCameraKeySlow))
                speed *= cfg.FreeCameraSlowMultiplier;

            if (forward == 0 && right == 0 && up == 0)
                return pos;

            double pitch = rot.X * Math.PI / 180.0;
            double yaw = rot.Y * Math.PI / 180.0;
            double cp = Math.Cos(pitch), sp = Math.Sin(pitch);
            double cy = Math.Cos(yaw), sy = Math.Sin(yaw);

            // Forward follows where the camera looks (including pitch), strafe stays level, up/down is world Z
            double mx = cp * cy * forward - sy * right;
            double my = cp * sy * forward + cy * right;
            double mz = sp * forward + up;

            double length = Math.Sqrt(mx * mx + my * my + mz * mz);
            if (length < 1e-6)
                return pos;

            double step = speed * dt / length;
            return new Vector3D(pos.X + mx * step, pos.Y + my * step, pos.Z + mz * step);
        }

        /// <summary>A control that is also the toggle keybind is ignored, otherwise the press that switches the camera on or off would move it.</summary>
        private bool IsDown(Config cfg, Keys key)
        {
            if (key == Keys.None || !InputManager.IsKeyDown((int)key))
                return false;

            if (key == cfg.KeybindFreeCamera)
            {
                if (!_loggedKeyConflict)
                {
                    _loggedKeyConflict = true;
                    Logger.Info($"[{NAME}] {key} is both the toggle keybind and a camera control, so it is ignored as a control. Change one of them (freeCameraKey* in the config file).");
                }
                return false;
            }
            return true;
        }

        private double Axis(Config cfg, Keys positive, Keys negative)
        {
            double v = 0;
            if (IsDown(cfg, positive)) v += 1;
            if (IsDown(cfg, negative)) v -= 1;
            return v;
        }
        #endregion

        #region Diagnostics
        private static void LogControls(Config cfg)
        {
            Verbose($"[{NAME}] Controls: forward={cfg.FreeCameraKeyForward} back={cfg.FreeCameraKeyBack} left={cfg.FreeCameraKeyLeft} " +
                        $"right={cfg.FreeCameraKeyRight} up={cfg.FreeCameraKeyUp} down={cfg.FreeCameraKeyDown} " +
                        $"fast={cfg.FreeCameraKeyFast} (x{cfg.FreeCameraFastMultiplier}) slow={cfg.FreeCameraKeySlow} (x{cfg.FreeCameraSlowMultiplier}), " +
                        $"speed={cfg.FreeCameraSpeed} cm/s, toggle={cfg.KeybindFreeCamera}");
            if (!InputManager.IsReady)
                Logger.Error($"[{NAME}] The key reader is not ready: the camera will look around but cannot be moved (keys are read from the game PC)");
        }

        private static string DescribeKeys(Config cfg)
        {
            var down = new List<string>();
            void Check(Keys k, string name)
            {
                if (k != Keys.None && InputManager.IsKeyDown((int)k)) down.Add(name);
            }
            Check(cfg.FreeCameraKeyForward, "fwd");
            Check(cfg.FreeCameraKeyBack, "back");
            Check(cfg.FreeCameraKeyLeft, "left");
            Check(cfg.FreeCameraKeyRight, "right");
            Check(cfg.FreeCameraKeyUp, "up");
            Check(cfg.FreeCameraKeyDown, "down");
            Check(cfg.FreeCameraKeyFast, "fast");
            Check(cfg.FreeCameraKeySlow, "slow");
            return down.Count == 0 ? "none" : string.Join("+", down);
        }

        /// <summary>One line, every few seconds, that answers "is it working": what the game undid, and how closely the view follows.</summary>
        private static void LogHeartbeat(Stats st, Session s, Config cfg, Vector3D pos, Vector3D rot, double speed, double seconds)
        {
            Verbose($"[{NAME}] heartbeat: {st.Ticks / seconds:F0} ticks/s (avg {(st.Ticks > 0 ? st.TickTimeMs / st.Ticks : 0):F1} ms), {st.Writes} writes, " +
                         $"errors read/write/rotation/bad-read {st.ReadErrors}/{st.WriteErrors}/{st.BadRotations}/{st.BadReads}, " +
                         $"undone by the game: mode {st.ModeResets}, 3rd-person flag {st.FlagResets}, ViewPos3p {st.PositionResets}, " +
                         $"acceleration {st.AccelerationResets} (held={s.FreezeSoldier}), body {st.BodyResets} (held={s.FreezeBody}), " +
                         $"drawn camera vs ViewPos3p avg {(st.FollowSamples > 0 ? st.FollowErrorSum / st.FollowSamples : 0):F1} / max {st.FollowErrorMax:F1} cm, actor yaw drift max {st.YawDriftMax:F1} deg, " +
                         $"pos=({pos.X:F0},{pos.Y:F0},{pos.Z:F0}) look=(p{rot.X:F1} y{rot.Y:F1}) speed={speed:F0} cm/s, keys: {DescribeKeys(cfg)}, key reader ready={InputManager.IsReady}");
        }

        /// <summary>One line that says whether the engine accepted the camera: did the mode stick, is the view where we put it.</summary>
        private static void VerifyEngineAccepted(Session s, byte wantMode, Vector3D wantPos)
        {
            try
            {
                byte mode = Memory.ReadValue<byte>(s.Soldier + ASQSoldier.ViewMode3p);
                byte flags = Memory.ReadValue<byte>(s.Soldier + ASQSoldier.ThirdPersonFlags);
                var viewPos = Memory.ReadValue<Vector3D>(s.Soldier + ASQSoldier.ViewPos3p);
                var pov = Memory.ReadValue<Vector3D>(s.PovLocation);
                double dx = pov.X - viewPos.X, dy = pov.Y - viewPos.Y, dz = pov.Z - viewPos.Z;
                double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                Verbose($"[{NAME}] After the first write: ViewMode3p={mode} ({(mode == wantMode ? "stuck" : "RESET by the game")}), inThirdPerson={(flags & ASQSoldier.ThirdPersonViewBit) != 0}, " +
                             $"ViewPos3p=({viewPos.X:F1},{viewPos.Y:F1},{viewPos.Z:F1}) (we wrote {wantPos.X:F1},{wantPos.Y:F1},{wantPos.Z:F1}), " +
                             $"drawn camera=({pov.X:F1},{pov.Y:F1},{pov.Z:F1}), {d:F1} cm from ViewPos3p => " +
                             (d < 50 ? "the engine IS using ViewPos3p" : "the camera is NOT at ViewPos3p (mode ignored, or an offset is applied)"));
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Could not verify what the engine did: {ex.Message}");
            }
        }
        #endregion
    }
}
