using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public enum FireSupportMode
    {
        /// <summary>The shell launches as aimed and, at the top of its flight, is put on a descent that comes in from the shooter's side.</summary>
        Arc = 0,
        /// <summary>The shell is put above the marker the moment it appears and falls straight onto it.</summary>
        Drop = 1
    }

    /// <summary>
    /// Fire Support: shells from artillery weapons (anything MortarCalculator has ballistic data for) come down on the
    /// newest marker placed on the map. Your aim is never touched, and other weapons are left alone.
    ///
    /// Arc (default): the shell launches the way it was aimed. At the top of its flight it is moved onto a descent
    /// path that ends on the marker, coming in from the shooter's side at a natural angle.
    /// Drop: the shell is moved above the marker right away and falls onto it.
    ///
    /// The ground height under the aim point comes from the map's heightmap, corrected by how far real landings sat
    /// from it. A map without a heightmap falls back to learning the height from the first landing near a marker (a
    /// marker's own height is only the player's when it was placed). Every shell is followed until it lands and the
    /// result is logged and its impact is drawn on the radar for a few seconds.
    /// </summary>
    public class FireSupport : Manager, Weapon
    {
        public const string NAME = "FireSupport";

        #region Constants
        private const int ScanWindowAfterFireMs = 600;   // projectiles are looked for while firing and this long after
        private const int ActorWindow = 64;              // how many of the newest actors are looked at per scan
        private const int OwnerWaitMs = 250;             // owner fields can be filled in a moment after the spawn
        private const int FlightIntervalMs = 30;         // how often the shells in the air are looked at
        private const int MaxFlights = 96;
        private const long MaxFlightMs = 120_000;
        private const long HandledKeepMs = 10_000;

        private const long ArcMinAgeMs = 500;            // a shell is not judged to be descending in its first moments
        private const long ArcLowAngleHopMs = 2_500;     // weapons without a real top of the flight are moved after this long
        private const long ArcHighAngleHopMs = 60_000;   // safety limit for high-angle weapons
        private const double ArcEntryHeightCm = 12_000.0; // the descent starts this high above the ground at the marker
        private const double ArcHighDescentDeg = 70.0;   // how steep the descent is for mortars...
        private const double ArcLowDescentDeg = 25.0;    // ...and for flat-firing rocket weapons
        private const double DropHeightCm = 20_000.0;    // Drop mode: how high above the ground the shell is put

        private const long VerifyForMs = 1_500;          // how long a moved shell is checked for being put back
        private const int MaxRewrites = 12;
        private const double PositionToleranceCm = 3_000.0;
        private const double RewriteAngleDegrees = 4.0;

        private const double GroundLearnRadiusCm = 3_000.0;
        private const double MaxBiasSampleCm = 5_000.0;  // a landing further than this from the heightmap is a roof or a tree, not a correction
        private const double BiasBlend = 0.3;            // how much each new landing moves the learned heightmap offset
        private const double EntryClearanceCm = 3_000.0; // Arc: the descent starts at least this far above the terrain under it
        private const double MaxEntryHeightCm = 60_000.0;
        private const int ImpactHistory = 24;
        private const long ImpactLifetimeMs = 7_000;     // impacts stay on the radar about as long as a death marker
        private const int DiscoveryAttempts = 8;

        private const double GravityCm = 978.0;          // 9.78 m/s², same value MortarCalculator uses
        private const double MinSpeedCm = 100.0;         // below this the projectile has not been launched yet
        private const double MinDropSpeedCm = 5_000.0;
        #endregion

        #region Shared with the UI
        // Set from the UI thread (map markers), read from the feature thread
        private static readonly object SharedLock = new();
        private static Vector3D? _target;
        private static readonly List<(Vector3D Point, long Time)> _impacts = new();

        /// <summary>The artillery target, the newest map marker, or null when there is none. Same world space as UActor.Position.</summary>
        public static void SetTarget(Vector3D? worldPosition)
        {
            lock (SharedLock) _target = worldPosition;
        }

        private static bool TryGetTarget(out Vector3D target)
        {
            lock (SharedLock)
            {
                target = _target.GetValueOrDefault();
                return _target.HasValue;
            }
        }

        /// <summary>
        /// Fills <paramref name="into"/> with where shells came down in the last few seconds, for the radar. Older
        /// impacts are dropped here, the same way dead markers disappear. No allocation per call.
        /// </summary>
        public static void CopyRecentImpacts(List<Vector3D> into)
        {
            into.Clear();
            long now = Environment.TickCount64;
            lock (SharedLock)
            {
                _impacts.RemoveAll(impact => now - impact.Time > ImpactLifetimeMs);
                foreach (var impact in _impacts) into.Add(impact.Point);
            }
        }

        private static void AddImpact(Vector3D point)
        {
            lock (SharedLock)
            {
                _impacts.Add((point, Environment.TickCount64));
                while (_impacts.Count > ImpactHistory) _impacts.RemoveAt(0);
            }
        }

        private static void ClearImpacts()
        {
            lock (SharedLock) _impacts.Clear();
        }
        #endregion

        private enum Phase { Launched, Placed }

        /// <summary>A shell that is being followed until it lands.</summary>
        private sealed class Flight
        {
            public ulong Projectile, Movement, Root, ClassPtr;
            public string Label = "";
            public long Spawn;
            public FireSupportMode Mode;
            public Phase Phase = Phase.Launched;
            public bool HighAngle;

            public Vector3D Origin;      // where the shell was when it was picked up
            public Vector3D Marker;      // the marker as it was when the shot was fired
            public Vector3D Aim;         // marker plus the spread offset, at ground height
            public double Gravity;       // cm/s², 0 when gravity is off
            public double AccelerationLength;
            public double AccelerationSign = 1.0;

            // Where the shell was put and what it was given, to check the engine kept it
            public long PlacedAt;
            public Vector3D PlacedPosition, PlacedVelocity;
            public int Rewrites;
            public bool Warned;

            // What was seen of it in the air
            public Vector3D LastPosition, LastVelocity, ImpactPoint;
            public bool Impacted;
            public int StaleReads;
        }

        private sealed class ClassInfo
        {
            public bool IsProjectile;
            public string Name = "";
        }

        private readonly Game _game;
        private readonly object _sync = new();

        private bool _isEnabled = false;
        private ulong _weapon = 0;
        private long _scanUntil = 0;
        private long _lastFlightUpdate = 0;

        // Where the engine's cached copies of a component's position sit, found by looking for the position in the
        // component (they aren't in the SDK dump). Empty until found; the shell is moved with the engine told to
        // rebuild it from the relative location meanwhile.
        private int[] _worldPositionOffsets = Array.Empty<int>();
        private int _discoveryAttempts = 0;

        // How far real landings sat from the heightmap of the current map (cm), and how many were averaged in
        private TerrainHeightmap _biasTerrain;
        private double _terrainBias;
        private int _biasSamples;

        private readonly Dictionary<ulong, ClassInfo> _classes = new();
        private readonly Dictionary<ulong, long> _handled = new();   // projectile -> when it was handled
        private readonly Dictionary<ulong, long> _pending = new();   // projectile -> when it was first seen without an owner
        private readonly Dictionary<ulong, string> _logged = new();  // projectile -> last reason logged for it
        private readonly Dictionary<(long, long), double> _ground = new(); // marker (metres) -> ground height learned from a landing
        private readonly List<Flight> _flights = new();
        private readonly List<ulong> _scratch = new();

        public bool IsEnabled => _isEnabled;

        public FireSupport(ulong playerController, bool inGame, Game game)
            : base(playerController, inGame)
        {
            _game = game;
        }

        public void SetEnabled(bool enable)
        {
            lock (_sync)
            {
                _isEnabled = enable;
                Logger.Debug($"[{NAME}] Fire support {(enable ? "enabled" : "disabled")}");

                if (!enable)
                {
                    Reset();
                    return;
                }

                UpdateCachedPointers();
                ulong weapon = IsInVehicle() && _cachedVehicleWeapon != 0 ? _cachedVehicleWeapon : _cachedCurrentWeapon;
                _weapon = weapon != 0 && !IsGrenade(weapon) ? weapon : 0;
            }
        }

        public void OnWeaponChanged(ulong newWeapon, ulong oldWeapon)
        {
            lock (_sync)
            {
                // Grenades are thrown from a different path
                _weapon = newWeapon != 0 && !IsGrenade(newWeapon) ? newWeapon : 0;
            }
        }

        public override void Apply()
        {
            lock (_sync)
            {
                if (!_isEnabled) return;

                UpdateCachedPointers(); // owner pointers of the local player, throttled inside
                long now = Environment.TickCount64;

                try
                {
                    UpdateFlights(now);

                    if (_weapon == 0) return;

                    if (IsTriggerHeld(_weapon))
                        _scanUntil = now + ScanWindowAfterFireMs;
                    if (now >= _scanUntil) return;

                    Scan(now);
                }
                catch (Exception ex)
                {
                    Logger.Error($"[{NAME}] Update failed: {ex.Message}");
                }
            }
        }

        private void Reset()
        {
            _handled.Clear();
            _pending.Clear();
            _logged.Clear();
            _flights.Clear();
            _scanUntil = 0;
            ClearImpacts();
        }

        private static bool IsTriggerHeld(ulong weapon)
        {
            try { return Memory.ReadValue<byte>(weapon + ASQWeapon.bFireInput) != 0; }
            catch { return false; }
        }

        // Settings are read when a shell is taken over, so changes apply to the next shot. A hand-edited config can't break it.
        private static FireSupportMode CurrentMode => Program.Config.FireSupportMode == (int)FireSupportMode.Drop ? FireSupportMode.Drop : FireSupportMode.Arc;
        private static double SpreadCm => Math.Clamp(Program.Config.FireSupportSpreadMeters, 0f, 100f) * 100.0;

        /// <summary>
        /// Stops following shells and clears the radar impacts. Called when the game ends, so nothing
        /// from one match is shown in the next one.
        /// </summary>
        public void Shutdown()
        {
            lock (_sync)
            {
                _isEnabled = false;
                Reset();
            }
        }

        /// <summary>
        /// One shell failing (a failed read or write mid-way) must not stop the others from being handled, and must
        /// not be retried forever. It is logged and dropped.
        /// </summary>
        private bool TryTakeOverSafely(ulong projectile, ulong classPtr, ClassInfo info, long now)
        {
            try
            {
                return TryTakeOver(projectile, classPtr, info, now);
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Could not take over projectile 0x{projectile:X}: {ex.Message}");
                return true;
            }
        }

        #region Finding projectiles
        /// <summary>
        /// Looks at the newest actors of the level for projectiles that belong to the local player.
        /// New actors are appended to the list, so the end of it is all that has to be read.
        /// </summary>
        private void Scan(long now)
        {
            ulong level = _game?.PersistentLevel ?? 0;
            if (level == 0) return;

            ulong arrayPtr = Memory.ReadPtr(level + Level.Actors);
            int count = Memory.ReadValue<int>(level + Level.Actors + 0x8);
            if (arrayPtr == 0 || count < 1 || count > 200_000) return;

            PurgeOld(now);

            int n = Math.Min(count, ActorWindow);
            int start = count - n;

            var map = new ScatterReadMap(n);
            var addrRound = map.AddRound();
            var fieldRound = map.AddRound();
            for (int i = 0; i < n; i++)
            {
                var actor = addrRound.AddEntry<ulong>(i, 0, arrayPtr + (uint)((start + i) * 8));
                fieldRound.AddEntry<ulong>(i, 1, actor, null, UObject.Class);
                fieldRound.AddEntry<ulong>(i, 2, actor, null, Actor.Instigator);
                fieldRound.AddEntry<ulong>(i, 3, actor, null, ASQProjectile.DamageInstigatorController);
            }
            map.Execute();

            for (int i = 0; i < n; i++)
            {
                var r = map.Results[i];
                if (!r.TryGetValue(0, out var ar) || !ar.TryGetResult<ulong>(out var actorAddr) || actorAddr == 0) continue;
                if (_handled.ContainsKey(actorAddr)) continue;
                if (!r.TryGetValue(1, out var cr) || !cr.TryGetResult<ulong>(out var cls) || cls == 0) continue;

                var info = GetClassInfo(cls);
                if (info == null || !info.IsProjectile) continue;

                r.TryGetValue(2, out var ir);
                r.TryGetValue(3, out var kr);
                ulong instigator = ir != null && ir.TryGetResult<ulong>(out var ip) ? ip : 0;
                ulong controller = kr != null && kr.TryGetResult<ulong>(out var kp) ? kp : 0;

                bool mine = controller == _playerController ||
                            (instigator != 0 && (instigator == _cachedSoldierActor || instigator == _cachedSeatPawn));

                if (!mine)
                {
                    // The owner can be filled in a moment after the spawn, so it isn't dismissed right away
                    if (!_pending.TryGetValue(actorAddr, out long firstSeen))
                        _pending[actorAddr] = now;
                    else if (now - firstSeen > OwnerWaitMs)
                    {
                        _pending.Remove(actorAddr);
                        _handled[actorAddr] = now;
                    }
                    continue;
                }

                if (TryTakeOverSafely(actorAddr, cls, info, now))
                {
                    _pending.Remove(actorAddr);
                    _handled[actorAddr] = now;
                }
                else
                {
                    // Not launched yet, or no marker: tried again for a short while, then given up on
                    if (!_pending.TryGetValue(actorAddr, out long firstTry))
                        _pending[actorAddr] = now;
                    else if (now - firstTry > OwnerWaitMs)
                    {
                        _pending.Remove(actorAddr);
                        _handled[actorAddr] = now;
                    }
                }
            }
        }

        private void PurgeOld(long now)
        {
            _scratch.Clear();
            foreach (var kv in _handled)
                if (now - kv.Value > HandledKeepMs) _scratch.Add(kv.Key);
            foreach (var key in _scratch) _handled.Remove(key);

            _scratch.Clear();
            foreach (var kv in _pending)
                if (now - kv.Value > HandledKeepMs) _scratch.Add(kv.Key);
            foreach (var key in _scratch) _pending.Remove(key);

            if (_logged.Count > 512) _logged.Clear();
        }

        /// <summary>
        /// A class is a projectile when SQProjectile is in its parent chain. Thrown grenades are projectiles too,
        /// but they aren't this feature's business. Resolved once per class.
        /// </summary>
        private ClassInfo GetClassInfo(ulong cls)
        {
            if (_classes.TryGetValue(cls, out var cached)) return cached;

            try
            {
                var info = new ClassInfo();
                bool grenade = false;
                ulong c = cls;
                for (int depth = 0; depth < 16 && c != 0; depth++)
                {
                    uint nameId = Memory.ReadValue<uint>(c + UObject.Name);
                    if (nameId != 0 && Memory.GetNamesById([nameId]).TryGetValue(nameId, out var name))
                    {
                        if (depth == 0) info.Name = name;
                        if (name == "SQProjectile") info.IsProjectile = true;
                        else if (name.Contains("Grenade", StringComparison.OrdinalIgnoreCase)) grenade = true;
                    }
                    c = Memory.ReadPtr(c + UStruct.SuperStruct);
                }

                if (grenade) info.IsProjectile = false;
                _classes[cls] = info;
                return info;
            }
            catch
            {
                return null; // not cached, tried again next scan
            }
        }
        #endregion

        #region Taking a shell over
        private bool TryTakeOver(ulong projectile, ulong classPtr, ClassInfo info, long now)
        {
            // Only artillery: whatever MortarCalculator has ballistic data for. Anything else is left alone.
            var detector = _game?._soldierManager?.WeaponDetector;
            if (detector == null || !detector.HasBallisticWeapon) return true;

            string label = string.IsNullOrEmpty(info.Name) ? $"projectile 0x{projectile:X}" : $"{info.Name} 0x{projectile:X}";
            string weapon = detector.CurrentWeaponName;

            if (!TryGetTarget(out var marker)) return Skip(projectile, $"{label} ({weapon}): not redirected, no marker");

            ulong movement = Memory.ReadPtr(projectile + ASQProjectile.ProjectileMovement);
            if (movement == 0) return Skip(projectile, $"{label}: no movement component yet");

            var velocity = Memory.ReadValue<Vector3D>(movement + UMovementComponent.Velocity);
            double speed = Length(velocity);
            if (double.IsNaN(speed) || speed < MinSpeedCm) return Skip(projectile, $"{label}: not launched yet, speed {speed / 100.0:F1} m/s");

            ulong root = Memory.ReadPtr(projectile + Actor.RootComponent);
            if (root == 0) return Skip(projectile, $"{label}: no root component");
            var origin = Memory.ReadValue<Vector3D>(root + USceneComponent.RelativeLocation);
            if (origin == Vector3D.Zero) return Skip(projectile, $"{label}: no position yet");

            DiscoverWorldPosition(root, origin);

            // A rocket motor pushes along the way the projectile travels; it has to keep doing that along the new direction
            var acceleration = Memory.ReadValue<Vector3D>(movement + USQProjectileMovement.ConstantAcceleration);
            double accelerationLength = Length(acceleration);
            bool hasMotor = !double.IsNaN(accelerationLength) && accelerationLength > 1.0;
            bool noGravity = Memory.ReadValue<byte>(movement + USQProjectileMovement.bIgnoreGravity) != 0;
            double gravity = noGravity
                ? 0.0
                : GravityCm * Math.Max(0f, Memory.ReadValue<float>(movement + UProjectileMovementComponent.ProjectileGravityScale));

            // Where it is sent: the marker, and a random point inside the spread radius around it
            double aimX = marker.X, aimY = marker.Y;
            double spreadCm = SpreadCm;
            if (spreadCm > 0)
            {
                double angle = Random.Shared.NextDouble() * 2.0 * Math.PI;
                double radius = spreadCm * Math.Sqrt(Random.Shared.NextDouble());
                aimX += Math.Cos(angle) * radius;
                aimY += Math.Sin(angle) * radius;
            }

            // Ground height at that point: the map's heightmap, or without one what earlier landings showed
            // (the marker's own height is only the player's when it was placed)
            double ground = GroundAt(marker, aimX, aimY, out string groundSource);
            var aim = new Vector3D(aimX, aimY, ground);

            var flight = new Flight
            {
                Label = label,
                Projectile = projectile,
                Movement = movement,
                Root = root,
                ClassPtr = classPtr,
                Spawn = now,
                Mode = CurrentMode,
                HighAngle = string.Equals(detector.CurrentWeaponData?.AngleType, "high", StringComparison.OrdinalIgnoreCase),
                Origin = origin,
                Marker = marker,
                Aim = aim,
                Gravity = gravity,
                AccelerationLength = hasMotor ? accelerationLength : 0.0,
                AccelerationSign = hasMotor && Dot(acceleration, velocity) < 0 ? -1.0 : 1.0,
                LastPosition = origin,
                LastVelocity = velocity,
            };

            string distance = $"{Distance2D(origin, marker) / 100.0:F0} m away, ground {groundSource} at {ground / 100.0:F0} m";
            if (flight.Mode == FireSupportMode.Drop)
            {
                Place(flight, speed, origin.Z, now);
                Logger.Debug($"[{NAME}] {label} ({weapon}) [drop] -> marker {distance}, dropped from {DropHeightCm / 100.0:F0} m above it");
            }
            else
            {
                Logger.Debug($"[{NAME}] {label} ({weapon}) [arc] -> marker {distance}, launched as aimed, moved onto the descent at the top of the flight");
            }

            if (_flights.Count >= MaxFlights) _flights.RemoveAt(0);
            _flights.Add(flight);
            return true;
        }

        /// <summary>
        /// A shell that couldn't be taken over (yet). It's retried on the next scan, and each new reason is
        /// logged once per projectile so a retry loop doesn't flood the log.
        /// </summary>
        private bool Skip(ulong projectile, string reason)
        {
            if (!_logged.TryGetValue(projectile, out var last) || last != reason)
            {
                _logged[projectile] = reason;
                Logger.Debug($"[{NAME}] {reason}");
            }
            return false;
        }

        private static (long, long) GroundKey(Vector3D marker) => ((long)Math.Round(marker.X / 100.0), (long)Math.Round(marker.Y / 100.0));

        /// <summary>
        /// Ground height (cm) at a world point. From the map's heightmap plus the offset real landings showed it has;
        /// without a heightmap, the height learned from a landing at the marker; failing that the marker's own height.
        /// </summary>
        private double GroundAt(Vector3D marker, double x, double y, out string source)
        {
            var terrain = TerrainHeightmap.Current;
            if (terrain != null && terrain.TryGetHeightCm(x, y, out double height))
            {
                if (!ReferenceEquals(terrain, _biasTerrain))
                {
                    _biasTerrain = terrain; // another map: what was learned about the old one doesn't apply
                    _terrainBias = 0.0;
                    _biasSamples = 0;
                }
                source = _biasSamples > 0 ? "terrain" : "terrain (uncalibrated)";
                return height + _terrainBias;
            }

            if (_ground.TryGetValue(GroundKey(marker), out double learned))
            {
                source = "learned";
                return learned;
            }

            source = "assumed";
            return marker.Z;
        }

        /// <summary>
        /// A real landing is the true ground height there. How far it was from the heightmap is averaged into an
        /// offset that every later ground height gets, so a constant error in the heightmap corrects itself.
        /// </summary>
        private void LearnTerrainBias(Vector3D landing, out double heightmapError)
        {
            heightmapError = double.NaN;
            var terrain = TerrainHeightmap.Current;
            if (terrain == null || !ReferenceEquals(terrain, _biasTerrain) || !terrain.TryGetHeightCm(landing.X, landing.Y, out double expected))
                return;

            double error = landing.Z - expected;
            heightmapError = error;

            // A shell can land on a roof or a tree, which the heightmap doesn't have
            if (Math.Abs(error) > MaxBiasSampleCm) return;

            _terrainBias = _biasSamples == 0 ? error : _terrainBias + BiasBlend * (error - _terrainBias);
            _biasSamples++;
        }

        /// <summary>
        /// Puts the shell where it has to be: straight above the aim point (Drop), or on the descent path that comes
        /// in from the shooter's side (Arc). The velocity it is given is what carries it onto the aim point.
        /// </summary>
        private void Place(Flight f, double currentSpeed, double currentHeight, long now)
        {
            Vector3D position, velocity;

            if (f.Mode == FireSupportMode.Drop)
            {
                position = new Vector3D(f.Aim.X, f.Aim.Y, f.Aim.Z + DropHeightCm);
                velocity = new Vector3D(0, 0, -Math.Max(currentSpeed, MinDropSpeedCm));
            }
            else
            {
                // Entry point: up and back from the aim point along the line from the shooter, so the shell
                // comes in the way a real one would
                double bx = f.Aim.X - f.Origin.X, by = f.Aim.Y - f.Origin.Y;
                double bl = Math.Sqrt(bx * bx + by * by);
                double ux = bl > 1.0 ? bx / bl : 1.0, uy = bl > 1.0 ? by / bl : 0.0;

                double descent = (f.HighAngle ? ArcHighDescentDeg : ArcLowDescentDeg) * (Math.PI / 180.0);
                double entryHeight = ArcEntryHeightCm;
                position = default;
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    double back = entryHeight / Math.Tan(descent);
                    position = new Vector3D(f.Aim.X - ux * back, f.Aim.Y - uy * back, f.Aim.Z + entryHeight);

                    // The flatter descent starts a long way back: lift it if there is a hill under the entry point
                    var terrain = TerrainHeightmap.Current;
                    if (terrain == null || !terrain.TryGetHeightCm(position.X, position.Y, out double under)) break;
                    double needed = under + _terrainBias + EntryClearanceCm - f.Aim.Z;
                    if (needed <= entryHeight) break;
                    entryHeight = Math.Min(needed, MaxEntryHeightCm);
                }

                // The shell has lost or gained speed on the way to the entry point
                double speed = currentSpeed;
                if (f.Gravity > 1.0)
                    speed = Math.Sqrt(Math.Max(currentSpeed * currentSpeed + 2.0 * f.Gravity * (currentHeight - position.Z), MinDropSpeedCm * MinDropSpeedCm));

                velocity = EntryVelocity(position, f.Aim, speed, f.Gravity, ux, uy);
            }

            MoveTo(f.Root, position);
            WriteVelocity(f, velocity);

            f.Phase = Phase.Placed;
            f.PlacedAt = now;
            f.PlacedPosition = position;
            f.PlacedVelocity = velocity;
        }

        /// <summary>The velocity, at the given speed, that carries a shell from the entry point onto the aim point under gravity.</summary>
        private static Vector3D EntryVelocity(Vector3D entry, Vector3D aim, double speed, double gravity, double ux, double uy)
        {
            double dx = aim.X - entry.X, dy = aim.Y - entry.Y;
            double horizontal = Math.Sqrt(dx * dx + dy * dy);
            double drop = aim.Z - entry.Z; // negative: the aim point is below the entry point

            if (gravity > 1.0 && horizontal > 1.0)
            {
                // tan(θ) = (v² - √(v⁴ - g(g·x² + 2·y·v²))) / (g·x), the flatter of the two solutions
                double v2 = speed * speed;
                double discriminant = v2 * v2 - gravity * (gravity * horizontal * horizontal + 2.0 * drop * v2);
                if (discriminant >= 0)
                {
                    double theta = Math.Atan((v2 - Math.Sqrt(discriminant)) / (gravity * horizontal));
                    return new Vector3D(ux * Math.Cos(theta) * speed, uy * Math.Cos(theta) * speed, Math.Sin(theta) * speed);
                }
            }

            // No gravity, or out of reach: a straight line
            var line = new Vector3D(dx, dy, drop);
            double length = Length(line);
            return length < 1.0 ? new Vector3D(0, 0, -speed) : Scale(line, speed / length);
        }

        /// <summary>
        /// Writes the velocity, and the motor push along it. The motor keeps its strength and its direction relative
        /// to the travel (a braking motor still brakes), otherwise it would drag the shell back.
        /// </summary>
        private static void WriteVelocity(Flight flight, Vector3D velocity)
        {
            Memory.WriteValue(flight.Movement + UMovementComponent.Velocity, velocity);
            if (flight.AccelerationLength > 0)
            {
                double length = Length(velocity);
                if (length > 1e-6)
                    Memory.WriteValue(flight.Movement + USQProjectileMovement.ConstantAcceleration,
                        Scale(velocity, flight.AccelerationSign * flight.AccelerationLength / length));
            }
        }

        /// <summary>
        /// Moves the shell. The engine moves it from cached copies of its position, not from the relative location
        /// that is read everywhere else, so every copy that was found is written as well, and the engine is told its
        /// cached transform is out of date so it rebuilds it from the relative location.
        /// </summary>
        private void MoveTo(ulong root, Vector3D position)
        {
            Memory.WriteValue(root + USceneComponent.RelativeLocation, position);
            foreach (int offset in _worldPositionOffsets)
                Memory.WriteValue(root + (ulong)offset, position);

            byte flags = Memory.ReadValue<byte>(root + USceneComponent.ComponentToWorldFlags);
            Memory.WriteValue<byte>(root + USceneComponent.ComponentToWorldFlags, (byte)(flags & ~USceneComponent.ComponentToWorldUpdatedBit));
        }

        /// <summary>
        /// The engine's cached copies of the position (the world transform and the bounds) aren't in the SDK dump.
        /// A root component with no parent has its relative location in there, so they are found by looking for the
        /// position in the component. A shell that has just spawned may not have them filled in yet, so this is
        /// tried again on the next shells until it works.
        /// </summary>
        private void DiscoverWorldPosition(ulong root, Vector3D position)
        {
            if (_worldPositionOffsets.Length > 0 || _discoveryAttempts >= DiscoveryAttempts) return;
            _discoveryAttempts++;

            try
            {
                var found = new List<int>(2);
                for (uint offset = ComponentTransformScan.Start; offset <= ComponentTransformScan.End; offset += 8)
                {
                    var candidate = Memory.ReadValue<Vector3D>(root + offset);
                    if (Math.Abs(candidate.X - position.X) < 1.0 && Math.Abs(candidate.Y - position.Y) < 1.0 && Math.Abs(candidate.Z - position.Z) < 1.0)
                        found.Add((int)offset);
                }

                if (found.Count > 0)
                {
                    _worldPositionOffsets = found.ToArray();
                    Logger.Debug($"[{NAME}] Cached position found at {string.Join(", ", found.Select(o => $"+0x{o:X}"))} of the root component");
                }
                else if (_discoveryAttempts >= DiscoveryAttempts)
                {
                    Logger.Debug($"[{NAME}] Cached position not found after {DiscoveryAttempts} shells, shells are moved through the relative location only and can't be verified");
                }
            }
            catch
            {
                // tried again with the next shell
            }
        }
        #endregion

        #region Following shells in the air
        /// <summary>
        /// Looks at every shell that was taken over: moves Arc shells at the top of their flight, makes sure a moved
        /// shell stayed where it was put, and finishes the ones that landed.
        /// </summary>
        private void UpdateFlights(long now)
        {
            if (_flights.Count == 0 || now - _lastFlightUpdate < FlightIntervalMs) return;
            _lastFlightUpdate = now;

            bool hasWorld = _worldPositionOffsets.Length > 0;
            int worldOffset = hasWorld ? _worldPositionOffsets[0] : 0;

            int count = _flights.Count;
            var map = new ScatterReadMap(count);
            var round = map.AddRound();
            for (int i = 0; i < count; i++)
            {
                var f = _flights[i];
                round.AddEntry<double>(i, 0, f.Movement + UMovementComponent.Velocity);
                round.AddEntry<double>(i, 1, f.Movement + UMovementComponent.Velocity + 0x8);
                round.AddEntry<double>(i, 2, f.Movement + UMovementComponent.Velocity + 0x10);
                round.AddEntry<double>(i, 3, f.Root + USceneComponent.RelativeLocation);
                round.AddEntry<double>(i, 4, f.Root + USceneComponent.RelativeLocation + 0x8);
                round.AddEntry<double>(i, 5, f.Root + USceneComponent.RelativeLocation + 0x10);
                round.AddEntry<byte>(i, 6, f.Projectile + ASQProjectile.bHasImpacted);
                round.AddEntry<ulong>(i, 7, f.Projectile + UObject.Class);
                round.AddEntry<double>(i, 8, f.Projectile + ASQProjectile.LastImpactPoint);
                round.AddEntry<double>(i, 9, f.Projectile + ASQProjectile.LastImpactPoint + 0x8);
                round.AddEntry<double>(i, 10, f.Projectile + ASQProjectile.LastImpactPoint + 0x10);
                if (hasWorld)
                {
                    // The position the engine really uses, not the one that was written
                    round.AddEntry<double>(i, 11, f.Root + (ulong)worldOffset);
                    round.AddEntry<double>(i, 12, f.Root + (ulong)worldOffset + 0x8);
                    round.AddEntry<double>(i, 13, f.Root + (ulong)worldOffset + 0x10);
                }
            }
            map.Execute();

            for (int i = count - 1; i >= 0; i--)
            {
                var f = _flights[i];
                var r = map.Results[i];

                Vector3D velocity = default, position = default;
                byte impacted = 0;
                ulong cls = 0;
                bool ok = TryVector(r, 0, out velocity) && TryVector(r, 3, out position) &&
                          r.TryGetValue(6, out var ir) && ir.TryGetResult<byte>(out impacted) &&
                          r.TryGetValue(7, out var cr) && cr.TryGetResult<ulong>(out cls);

                Vector3D enginePosition = position;
                if (ok && hasWorld && TryVector(r, 11, out var world) && world != Vector3D.Zero)
                    enginePosition = world;

                if (ok)
                {
                    f.StaleReads = enginePosition == f.LastPosition ? f.StaleReads + 1 : 0;
                    f.LastPosition = enginePosition;
                    f.LastVelocity = velocity;
                    if (impacted != 0)
                    {
                        f.Impacted = true;
                        if (TryVector(r, 8, out var point) && point != Vector3D.Zero) f.ImpactPoint = point;
                    }
                }

                long age = now - f.Spawn;
                if (!ok || cls != f.ClassPtr || f.Impacted || f.StaleReads >= 3 || age > MaxFlightMs)
                {
                    Finish(f, ok, age);
                    _flights.RemoveAt(i);
                    continue;
                }

                try
                {
                    if (f.Phase == Phase.Launched) MoveAtTop(f, velocity, enginePosition, now, age);
                    else Verify(f, velocity, enginePosition, now);
                }
                catch (Exception ex)
                {
                    Logger.Error($"[{NAME}] {f.Label}: {ex.Message}");
                }
            }
        }

        private static bool TryVector(IReadOnlyDictionary<int, IScatterEntry> r, int id, out Vector3D value)
        {
            value = default;
            if (!r.TryGetValue(id, out var x) || !x.TryGetResult<double>(out var vx)) return false;
            if (!r.TryGetValue(id + 1, out var y) || !y.TryGetResult<double>(out var vy)) return false;
            if (!r.TryGetValue(id + 2, out var z) || !z.TryGetResult<double>(out var vz)) return false;
            if (double.IsNaN(vx) || double.IsNaN(vy) || double.IsNaN(vz)) return false;
            value = new Vector3D(vx, vy, vz);
            return true;
        }

        /// <summary>
        /// Arc mode: the shell flies as aimed until it starts coming down (the top of its flight), then it is moved onto
        /// the descent. A weapon without a real top of the flight, like a flat-firing rocket, is moved after a set time.
        /// </summary>
        private void MoveAtTop(Flight f, Vector3D velocity, Vector3D position, long now, long age)
        {
            bool descending = velocity.Z <= 0 && age >= ArcMinAgeMs;
            bool tooLong = age >= (f.HighAngle && f.Gravity > 1.0 ? ArcHighAngleHopMs : ArcLowAngleHopMs);
            if (!descending && !tooLong) return;

            double speed = Length(velocity);
            if (speed < MinSpeedCm) return;

            Place(f, speed, position.Z, now);
            Logger.Debug($"[{NAME}] {f.Label} moved onto the descent {(descending ? "at the top of its flight" : "after the time limit")}, " +
                         $"{age / 1000.0:F1} s in, {Distance2D(position, f.Aim) / 100.0:F0} m from the aim point, entry speed {Length(f.PlacedVelocity) / 100.0:F0} m/s");
        }

        /// <summary>
        /// The game may undo the move right after it. For a while the position the engine really has is compared with
        /// where the shell should be by now, and anything that was put back is written again.
        /// </summary>
        private void Verify(Flight f, Vector3D velocity, Vector3D position, long now)
        {
            long since = now - f.PlacedAt;
            if (since > VerifyForMs || f.Rewrites >= MaxRewrites) return;

            double dt = since / 1000.0;
            var expectedVelocity = new Vector3D(f.PlacedVelocity.X, f.PlacedVelocity.Y, f.PlacedVelocity.Z - f.Gravity * dt);
            var expected = new Vector3D(
                f.PlacedPosition.X + f.PlacedVelocity.X * dt,
                f.PlacedPosition.Y + f.PlacedVelocity.Y * dt,
                f.PlacedPosition.Z + f.PlacedVelocity.Z * dt - 0.5 * f.Gravity * dt * dt);

            double off = Length(Sub(position, expected));
            bool movedAway = off > PositionToleranceCm + Length(f.PlacedVelocity) * 0.05;
            bool turnedBack = AngleBetween(velocity, expectedVelocity) > RewriteAngleDegrees;
            if (!movedAway && !turnedBack) return;

            if (movedAway) MoveTo(f.Root, expected);
            WriteVelocity(f, expectedVelocity);
            f.Rewrites++;

            if (!f.Warned)
            {
                f.Warned = true;
                Logger.Debug($"[{NAME}] {f.Label} was put back ({(movedAway ? $"{off / 100.0:F0} m off" : "direction")}), writing it again");
            }
        }

        /// <summary>Logs where the shell came down next to the marker, remembers the ground height, and adds the impact to the radar.</summary>
        private void Finish(Flight f, bool readable, long age)
        {
            bool real = f.ImpactPoint != Vector3D.Zero;
            var landing = real ? f.ImpactPoint : f.LastPosition;
            string source = real ? "impact point" : readable ? "last seen position" : "last seen position, projectile vanished";
            double missMarker = Distance2D(landing, f.Marker) / 100.0;
            double missAim = Distance2D(landing, f.Aim) / 100.0;

            // Only a real landing near the marker tells where the ground is there
            double heightmapError = double.NaN;
            if (real && Distance2D(landing, f.Marker) <= GroundLearnRadiusCm)
            {
                _ground[GroundKey(f.Marker)] = landing.Z;
                LearnTerrainBias(landing, out heightmapError);
            }

            // Only a point the game reported is worth drawing, a last-seen position can be mid-air
            if (real) AddImpact(landing);

            Logger.Info($"[{NAME}] Landed {f.Label} [{(f.Mode == FireSupportMode.Arc ? "arc" : "drop")}]: {source} " +
                        $"({landing.X / 100.0:F0}, {landing.Y / 100.0:F0}, {landing.Z / 100.0:F0}) m, marker ({f.Marker.X / 100.0:F0}, {f.Marker.Y / 100.0:F0}) m -> " +
                        $"{missMarker:F0} m from the marker, {missAim:F0} m from the aim point, flew {age / 1000.0:F1} s" +
                        (f.Phase == Phase.Launched ? ", was never moved" : "") +
                        (f.Rewrites > 0 ? $", put back {f.Rewrites} time(s)" : "") +
                        (double.IsNaN(heightmapError) ? "" : $", heightmap was {heightmapError / 100.0:+0.0;-0.0} m off here (learned offset {_terrainBias / 100.0:+0.0;-0.0} m over {_biasSamples} landing(s))"));
        }
        #endregion

        #region Vector helpers
        private static double Distance2D(Vector3D a, Vector3D b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static Vector3D Sub(Vector3D a, Vector3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        private static Vector3D Scale(Vector3D a, double s) => new(a.X * s, a.Y * s, a.Z * s);
        private static double Dot(Vector3D a, Vector3D b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        private static double Length(Vector3D a) => Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);

        private static double AngleBetween(Vector3D a, Vector3D b)
        {
            double la = Length(a), lb = Length(b);
            if (la < 1e-6 || lb < 1e-6) return 180.0;
            return Math.Acos(Math.Clamp(Dot(a, b) / (la * lb), -1.0, 1.0)) * (180.0 / Math.PI);
        }
        #endregion
    }
}
