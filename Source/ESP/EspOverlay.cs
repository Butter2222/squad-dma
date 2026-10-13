using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Runtime.InteropServices;
using System.Numerics;
using SharpDX.DirectWrite;
using System.Diagnostics;
using System;

namespace squad_dma
{


    public static class Vector2Extensions
    {
        public static RawVector2 ToRawVector2(this Vector2 vector)
        {
            return new RawVector2(vector.X, vector.Y);
        }
        
        public static RawVector2 ToRawVector2(this Vector2D vector)
        {
            return new RawVector2((float)vector.X, (float)vector.Y);
        }
    }

    public class EspOverlay : Form
    {
        private WindowRenderTarget renderTarget;
        private SolidColorBrush enemyPlayerBrush;
        private SolidColorBrush friendlyBrush;
        private SolidColorBrush squadBrush;
        private SolidColorBrush enemyVehicleBrush;
        private SolidColorBrush friendlyVehicleBrush;
        private SolidColorBrush healthBrush;
        private SharpDX.DirectWrite.TextFormat textFormat;
        private bool running = true;
        private Game Game => Memory._game;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_EXSTYLE = -20;

        private static readonly Dictionary<ActorType, string> ActorTypeNames = new Dictionary<ActorType, string>
        {
            { ActorType.FOBRadio, "FOB Radio" },
            { ActorType.Hab, "Hab" },
            { ActorType.AntiAir, "Anti-Air" },
            { ActorType.APC, "APC" },
            { ActorType.AttackHelicopter, "Attack Helicopter" },
            { ActorType.LoachCAS, "Loach CAS" },
            { ActorType.LoachScout, "Loach Scout" },
            { ActorType.Boat, "Boat" },
            { ActorType.BoatLogistics, "Boat Logistics" },
            { ActorType.DeployableAntiAir, "Deployable Anti-Air" },
            { ActorType.DeployableAntitank, "Deployable Antitank" },
            { ActorType.DeployableAntitankGun, "Deployable Antitank Gun" },
            { ActorType.DeployableGMG, "Deployable GMG" },
            { ActorType.DeployableHellCannon, "Deployable Hell Cannon" },
            { ActorType.DeployableHMG, "Deployable HMG" },
            { ActorType.DeployableMortars, "Deployable Mortars" },
            { ActorType.DeployableRockets, "Deployable Rockets" },
            { ActorType.Drone, "Drone" },
            { ActorType.IFV, "IFV" },
            { ActorType.JeepAntiAir, "Jeep Anti-Air" },
            { ActorType.JeepAntitank, "Jeep Antitank" },
            { ActorType.JeepArtillery, "Jeep Artillery" },
            { ActorType.JeepLogistics, "Jeep Logistics" },
            { ActorType.JeepTransport, "Jeep Transport" },
            { ActorType.JeepRWSTurret, "Jeep RWS Turret" },
            { ActorType.JeepTurret, "Jeep Turret" },
            { ActorType.Mine, "Mine" },
            { ActorType.Motorcycle, "Motorcycle" },
            { ActorType.RallyPoint, "Rally Point" },
            { ActorType.Tank, "Tank" },
            { ActorType.TankMGS, "Tank MGS" },
            { ActorType.TrackedAPC, "Tracked APC" },
            { ActorType.TrackedAPCArtillery, "Tracked APC Artillery" },
            { ActorType.TrackedIFV, "Tracked IFV" },
            { ActorType.TrackedJeep, "Tracked Jeep" },
            { ActorType.TrackedLogistics, "Tracked Logistics" },
            { ActorType.TransportHelicopter, "Transport Helicopter" },
            { ActorType.TruckAntiAir, "Truck Anti-Air" },
            { ActorType.TruckArtillery, "Truck Artillery" },
            { ActorType.TruckLogistics, "Truck Logistics" },
            { ActorType.TruckTransport, "Truck Transport" },
            { ActorType.TruckTransportArmed, "Truck Transport Armed" }
        };

        private static readonly HashSet<ActorType> VehicleTypes = new HashSet<ActorType>
        {
            ActorType.APC, ActorType.AttackHelicopter, ActorType.Boat, ActorType.BoatLogistics,
            ActorType.IFV, ActorType.JeepAntiAir, ActorType.JeepAntitank, ActorType.JeepArtillery,
            ActorType.JeepLogistics, ActorType.JeepTransport, ActorType.JeepRWSTurret, ActorType.JeepTurret,
            ActorType.LoachCAS, ActorType.LoachScout, ActorType.Motorcycle, ActorType.Tank, ActorType.TankMGS,
            ActorType.TrackedAPC, ActorType.TrackedLogistics, ActorType.TrackedAPCArtillery, ActorType.TrackedIFV,
            ActorType.TrackedJeep, ActorType.TransportHelicopter, ActorType.TruckAntiAir, ActorType.TruckArtillery,
            ActorType.TruckLogistics, ActorType.TruckTransport, ActorType.TruckTransportArmed
        };

        // Vehicle size categories for realistic box sizing
        private enum VehicleSize { Small, Medium, Large, ExtraLarge }
        
        private static readonly Dictionary<ActorType, VehicleSize> VehicleSizes = new Dictionary<ActorType, VehicleSize>
        {
            // Motorcycle
            { ActorType.Motorcycle, VehicleSize.Small },
            
            // Medium vehicles (jeeps, light trucks, boats)
            { ActorType.JeepTransport, VehicleSize.Medium },
            { ActorType.JeepLogistics, VehicleSize.Medium },
            { ActorType.JeepTurret, VehicleSize.Medium },
            { ActorType.JeepArtillery, VehicleSize.Medium },
            { ActorType.JeepAntitank, VehicleSize.Medium },
            { ActorType.JeepAntiAir, VehicleSize.Medium },
            { ActorType.JeepRWSTurret, VehicleSize.Medium },
            { ActorType.Boat, VehicleSize.Medium },
            { ActorType.BoatLogistics, VehicleSize.Medium },
            { ActorType.TrackedJeep, VehicleSize.Medium },
            { ActorType.LoachCAS, VehicleSize.Medium },
            { ActorType.LoachScout, VehicleSize.Medium },
            
            // Large vehicles (trucks, APCs, IFVs)
            { ActorType.TruckTransport, VehicleSize.Large },
            { ActorType.TruckLogistics, VehicleSize.Large },
            { ActorType.TruckTransportArmed, VehicleSize.Large },
            { ActorType.TruckArtillery, VehicleSize.Large },
            { ActorType.TruckAntiAir, VehicleSize.Large },
            { ActorType.APC, VehicleSize.Large },
            { ActorType.TrackedAPC, VehicleSize.Large },
            { ActorType.TrackedAPCArtillery, VehicleSize.Large },
            { ActorType.IFV, VehicleSize.Large },
            { ActorType.TrackedIFV, VehicleSize.Large },
            { ActorType.TrackedLogistics, VehicleSize.Large },
            { ActorType.TransportHelicopter, VehicleSize.Large },
            
            // Extra Large vehicles (tanks, heavy armor)
            { ActorType.Tank, VehicleSize.ExtraLarge },
            { ActorType.TankMGS, VehicleSize.ExtraLarge },
            { ActorType.AttackHelicopter, VehicleSize.ExtraLarge }
        };

        public EspOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            Width = Screen.PrimaryScreen.Bounds.Width;
            Height = Screen.PrimaryScreen.Bounds.Height;
            Location = new System.Drawing.Point(0, 0);
            BackColor = System.Drawing.Color.Black;

            int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
            SetWindowLong(Handle, GWL_EXSTYLE, exStyle);

            InitializeDirect2D();
            StartRenderLoop();
        }

        private void InitializeDirect2D()
        {
            var factory = new SharpDX.Direct2D1.Factory();
            var renderProperties = new HwndRenderTargetProperties
            {
                Hwnd = Handle,
                PixelSize = new Size2(Width, Height),
                PresentOptions = PresentOptions.Immediately
            };
            
            // Create render target with optimized settings for high FPS
            var rtProps = new RenderTargetProperties
            {
                Type = RenderTargetType.Default,
                PixelFormat = new PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied)
            };
            renderTarget = new WindowRenderTarget(factory, rtProps, renderProperties);
            
            // Initialize brushes - NO ANTIALIASING for performance
            // Enemy players = White (SKPaints.EnemyPlayer)
            enemyPlayerBrush = new SolidColorBrush(renderTarget, new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));
            
            // Friendly players = Light Blue (SKPaints.Friendly - RGB: 0, 187, 254)
            friendlyBrush = new SolidColorBrush(renderTarget, new RawColor4(0f / 255f, 187f / 255f, 254f / 255f, 1.0f));
            
            // Squad members = Green (SKPaints.Squad)
            squadBrush = new SolidColorBrush(renderTarget, new RawColor4(0f, 128f / 255f, 0f, 1.0f));
            
            // Enemy vehicles = Yellow (SKPaints.EnemyVehicle)
            enemyVehicleBrush = new SolidColorBrush(renderTarget, new RawColor4(1.0f, 1.0f, 0.0f, 1.0f));
            
            // Friendly vehicles = Light Blue (SKPaints.FriendlyVehicle)
            friendlyVehicleBrush = new SolidColorBrush(renderTarget, new RawColor4(0f / 255f, 187f / 255f, 254f / 255f, 1.0f));
            
            // Health bar brush (dynamic color based on health percentage)
            healthBrush = new SolidColorBrush(renderTarget, new RawColor4(0.0f, 1.0f, 0.0f, 1.0f));
            
            // Optimized text format - no antialiasing
            textFormat = new SharpDX.DirectWrite.TextFormat(new SharpDX.DirectWrite.Factory(), "Verdana", Program.Config.ESPFontSize)
            {
                TextAlignment = SharpDX.DirectWrite.TextAlignment.Center,
                ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center,
                WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap
            };
            
            // Set render target to Aliased mode for performance (no antialiasing)
            renderTarget.AntialiasMode = AntialiasMode.Aliased;
            renderTarget.TextAntialiasMode = SharpDX.Direct2D1.TextAntialiasMode.Aliased;
        }

        private void StartRenderLoop()
        {
            Thread renderThread = new Thread(() =>
            {
                Program.Log("Render thread started - Target: 144 FPS");
                bool wasReadyLastFrame = false;
                var stopwatch = Stopwatch.StartNew();
                while (running)
                {
                    stopwatch.Restart();
                    bool isMemoryReady = Memory.Ready;
                    if (!isMemoryReady)
                    {
                        renderTarget.BeginDraw();
                        renderTarget.Clear(new RawColor4(0, 0, 0, 0)); // Transparent black background only
                        renderTarget.EndDraw();
                        Thread.Sleep(wasReadyLastFrame ? 500 : 1500);
                        wasReadyLastFrame = false;
                        continue;
                    }

                    RenderFrame();
                    
                    // Target 144 FPS = ~6.9ms per frame
                    int elapsedMs = (int)stopwatch.ElapsedMilliseconds;
                    int targetMs = 7; // 144 FPS target
                    int sleepMs = targetMs - elapsedMs;
                    if (sleepMs > 0)
                        Thread.Sleep(sleepMs);
                    
                    wasReadyLastFrame = true;
                }
                Program.Log("Render thread stopped.");
            });
            renderThread.Priority = ThreadPriority.Highest; // Highest priority for 144 FPS
            renderThread.Start();
        }

        private bool IsReadyToRender()
        {
            bool inGame = Game?.InGame ?? false;
            bool localPlayerExists = Game?.LocalPlayer != null;
            bool actorsExist = Game?.Actors != null && Game.Actors.Count > 0;
            return inGame && localPlayerExists && actorsExist;
        }

        // Reused every frame: eliminates a full Dictionary copy (bucket arrays + entries) at up to 144 Hz.
        private readonly List<UActor> _actorBuffer = new(256);
        private readonly List<(UActor actor, Vector2 screenPos, float distance)> _visibleBuffer = new(128);

        private void RenderFrame()
        {
            // (Two unused Stopwatch.StartNew() heap allocations per frame removed.)
            renderTarget.BeginDraw();
            renderTarget.Clear(new RawColor4(0, 0, 0, 0));

            if (!IsReadyToRender())
            {
                renderTarget.EndDraw();
                this.Invalidate();
                return;
            }

            // Snapshot of the actor references for this frame; the dictionary's values are enumerated once.
            _actorBuffer.Clear();
            _actorBuffer.AddRange(Game.Actors.Values);

            DrawEsp(_actorBuffer);
            renderTarget.EndDraw();
        }

        private RawRectangleF CalculatePlayerBox(UActor actor, Vector2 screenPos, float distance)
        {
            // Use EXACT same method as AimviewWidget - simple distance-based box
            float boxSize = Math.Max(20f, Math.Min(100f, 2000f / distance)); // Adaptive size based on distance
            float boxWidth = boxSize * 0.6f; // Narrower width for player aspect ratio
            float boxHeight = boxSize;

            return new RawRectangleF(
                screenPos.X - boxWidth / 2f,
                screenPos.Y - boxHeight / 2f,
                screenPos.X + boxWidth / 2f,
                screenPos.Y + boxHeight / 2f
            );
        }

        // Cache text metrics to avoid expensive TextLayout creation - MAJOR PERFORMANCE BOOST
        private Dictionary<string, (float width, float height)> _textMetricsCache = new Dictionary<string, (float width, float height)>();
        private const int MaxMetricsCacheEntries = 2048;

        // One DirectWrite factory for the overlay's lifetime. The old code constructed (and never disposed) a new
        // native factory on every cache miss, and the cache was cleared every 60 *lookups* rather than every 60
        // frames, so with many actors nearly every label missed.
        private readonly SharpDX.DirectWrite.Factory _dwFactory = new SharpDX.DirectWrite.Factory();

        // "[Nm]" labels by whole meters, built once per distinct value instead of per actor per frame.
        private static readonly string[] _bracketDistanceLabels = new string[4096];
        private static string BracketDistance(int meters)
        {
            if ((uint)meters >= (uint)_bracketDistanceLabels.Length)
                return $"[{meters}m]";
            return _bracketDistanceLabels[meters] ??= $"[{meters}m]";
        }

        private (float width, float height) GetTextMetrics(string text)
        {
            if (_textMetricsCache.TryGetValue(text, out var cached))
                return cached;

            // Bound memory by size (metrics for a given string never change), not by a lookup counter.
            if (_textMetricsCache.Count >= MaxMetricsCacheEntries)
                _textMetricsCache.Clear();

            using (var textLayout = new TextLayout(_dwFactory, text, textFormat, 1000f, 100f))
            {
                var metrics = (textLayout.Metrics.Width, textLayout.Metrics.Height);
                _textMetricsCache[text] = metrics;
                return metrics;
            }
        }

        // Squad / friendly / enemy brush, resolved once per actor per frame (was 3-5 IsInMySquad/IsFriendly calls per actor).
        private SolidColorBrush TeamBrush(UActor actor)
        {
            return actor.IsInMySquad() ? squadBrush :
                   actor.IsFriendly() ? friendlyBrush : enemyPlayerBrush;
        }

        private void DrawEsp(List<UActor> actors)
        {
            if (Game == null || Game.LocalPlayer == null || actors == null || actors.Count < 1)
            {
                Program.Log("DrawEsp: Game, LocalPlayer, or actors not initialized.");
                return;
            }

            Vector3 camPosVec = Camera.Location.ToVector3();
            Vector3 localPlayerPosVec = Memory.LocalPlayer.Position.ToVector3();
            float maxDistance = Program.Config.EspMaxDistance;
            float vehicleMaxDistance = Program.Config.EspVehicleMaxDistance;
            bool showAllies = Program.Config.EspShowAllies;
            bool showVehicles = Program.Config.EspShowVehicles;

            // Reused list: eliminates a per-frame List allocation (and its growth reallocations).
            var visibleActors = _visibleBuffer;
            visibleActors.Clear();

            // Players whose anchor is far outside the screen cannot have a visible box/label/bones.
            // The margin is generous because close players span far more pixels than their anchor offset.
            float cullW = Width + 1000f, cullH = Height + 1000f;

            foreach (var actor in actors)
            {
                if (actor == null || actor.Position == Vector3D.Zero)
                    continue;

                if (actor.ActorType == ActorType.Player && !actor.IsAlive)
                    continue;

                if (!showAllies && actor.IsFriendly())
                    continue;

                Vector3 actorPosVec = actor.Position.ToVector3();
                float distance = Vector3.Distance(camPosVec, actorPosVec) / 100f;
                bool isPlayer = actor.ActorType == ActorType.Player;
                if (distance > (isPlayer ? maxDistance : vehicleMaxDistance))
                    continue;

                if (isPlayer && distance < 1.0f)
                    continue;

                // Skip vehicles if not showing them
                if (!isPlayer && !showVehicles)
                    continue;

                Vector2 screenPos = Camera.WorldToScreen(actor.Position);
                if (screenPos == Vector2.Zero)
                    continue;

                // Eliminates box / text-metric / brush work for every off-screen player.
                if (isPlayer && (screenPos.X < -1000f || screenPos.X > cullW || screenPos.Y < -1000f || screenPos.Y > cullH))
                    continue;

                visibleActors.Add((actor, screenPos, distance));
            }

            foreach (var (actor, screenPos, distance) in visibleActors)
            {
                if (actor.ActorType == ActorType.Player)
                {
                    RawRectangleF boxRect = CalculatePlayerBox(actor, screenPos, distance);
                    if ((boxRect.Left == 0 && boxRect.Top == 0 && boxRect.Right == 0 && boxRect.Bottom == 0) &&
                        Program.Config.EspBones && actor.BoneScreenPositions != null)
                    {
                        boxRect = GetBoxFromBones(actor.BoneScreenPositions);
                    }

                    if (boxRect.Left == 0 && boxRect.Top == 0 && boxRect.Right == 0 && boxRect.Bottom == 0)
                        continue;

                    // Squad / friendly / enemy brush resolved once for box, name, distance and bones.
                    var teamBrush = TeamBrush(actor);

                    if (Program.Config.EspShowBox)
                        renderTarget.DrawRectangle(boxRect, teamBrush);

                    float boxCenterX = (boxRect.Left + boxRect.Right) / 2f;
                    float boxWidth = boxRect.Right - boxRect.Left;

                    if (Program.Config.EspShowNames)
                    {
                        string nameText = GetNameText(actor);
                        var (textWidth, textHeight) = GetTextMetrics(nameText);
                        float rectWidth = Math.Max(boxWidth, textWidth);
                        RawRectangleF nameRect = new RawRectangleF(
                            boxCenterX - rectWidth / 2f, boxRect.Top - textHeight - 5f,
                            boxCenterX + rectWidth / 2f, boxRect.Top - 5f
                        );
                        renderTarget.DrawText(nameText, textFormat, nameRect, teamBrush);
                    }

                    if (Program.Config.EspShowDistance)
                    {
                        string distanceText = BracketDistance((int)distance);
                        var (textWidth, textHeight) = GetTextMetrics(distanceText);
                        float rectWidth = Math.Max(boxWidth, textWidth);
                        RawRectangleF distanceRect = new RawRectangleF(
                            boxCenterX - rectWidth / 2f, boxRect.Bottom + 5f,
                            boxCenterX + rectWidth / 2f, boxRect.Bottom + textHeight + 5f
                        );
                        renderTarget.DrawText(distanceText, textFormat, distanceRect, teamBrush);
                    }

                    if (Program.Config.EspShowHealth && actor.Health >= 0)
                        DrawHealthBar(boxRect, actor.Health);

                    if (Program.Config.EspBones && actor.BoneScreenPositions != null)
                        DrawBoneLines(teamBrush, actor.BoneScreenPositions);
                }
                else if (IsVehicle(actor))
                {
                    DrawVehicleBox(actor, screenPos, distance);
                }
            }
        }

        private string GetEspText(UActor actor, float distance)
        {
            string name = actor.ActorType == ActorType.Player
                ? (Program.Config.EspShowNames ? actor.Name : "")
                : (ActorTypeNames.TryGetValue(actor.ActorType, out var typeName) ? typeName : "");
            string wdistance = Program.Config.EspShowDistance ? BracketDistance((int)distance) : "";
            string whealth = Program.Config.EspShowHealth && actor.Health >= 0 ? $"[{(int)actor.Health}❤]" : "";
            return $"{name}{(string.IsNullOrEmpty(name) ? "" : " ")}{wdistance}{(string.IsNullOrEmpty(wdistance) ? "" : " ")}{whealth}";
        }

        private bool IsVehicle(UActor actor)
        {
            bool isVehicle = VehicleTypes.Contains(actor.ActorType);
            return isVehicle;
        }

        private float GetVehicleSizeMultiplier(ActorType vehicleType)
        {
            if (!VehicleSizes.TryGetValue(vehicleType, out VehicleSize size))
                return 1.0f; // Default size for unknown vehicles
            
            return size switch
            {
                VehicleSize.Small => 0.7f,        // 70% of base size (motorcycles)
                VehicleSize.Medium => 1.0f,       // 100% of base size (jeeps, boats)
                VehicleSize.Large => 1.4f,        // 140% of base size (trucks, APCs)
                VehicleSize.ExtraLarge => 1.8f,   // 180% of base size (tanks)
                _ => 1.0f
            };
        }

        private void DrawVehicleBox(UActor actor, Vector2 screenPos, float distance)
        {
            bool isEnemy = actor.TeamID != -1 && actor.TeamID != Memory.LocalPlayer.TeamID;
            bool isFriendly = actor.TeamID != -1 && actor.TeamID == Memory.LocalPlayer.TeamID;
            bool isUnclaimed = actor.TeamID == -1;

            if (!Program.Config.EspShowAllies && !isEnemy)
                return;

            // Select brush based on vehicle ownership - matching AimviewWidget color scheme
            var vehicleBrush = (isUnclaimed || isEnemy) ? enemyVehicleBrush : friendlyVehicleBrush;

            // Vehicle-type-based sizing with reduced base size
            float baseSize = 480f; // Reduced by 60% (1200 * 0.4)
            float minSize = 50f;    // Minimum size to ensure visibility
            float maxSize = 2000f;  // Maximum size for very large vehicles
            
            // Get vehicle-specific size multiplier
            float sizeMultiplier = GetVehicleSizeMultiplier(actor.ActorType);
            float vehicleBaseSize = baseSize * sizeMultiplier;
            
            // Distance-based scaling with better curve
            float distanceScale = Math.Max(1f, distance / 10f); // Scale factor based on distance
            float boxSize = Math.Max(minSize, Math.Min(maxSize, vehicleBaseSize / distanceScale));
            
            // Aspect ratio similar to 1204x754 (approximately 1.6:1)
            float boxWidth = boxSize * 1.6f;  // Width based on 1204x754 aspect ratio
            float boxHeight = boxSize * 1.0f; // Height maintains the calculated box size
            
            RawRectangleF vehicleRect = new RawRectangleF(
                screenPos.X - boxWidth / 2f, screenPos.Y - boxHeight / 2f,
                screenPos.X + boxWidth / 2f, screenPos.Y + boxHeight / 2f
            );

            // Eliminates the rectangle draw, string build and text-metrics lookup for fully off-screen vehicles
            // (text sits just above the box and is at most a few hundred px wide, hence the margin).
            if (vehicleRect.Right < -400f || vehicleRect.Left > Width + 400f ||
                vehicleRect.Bottom < -100f || vehicleRect.Top > Height + 100f)
                return;

            renderTarget.DrawRectangle(vehicleRect, vehicleBrush);

            string vehicleText = GetEspText(actor, distance);
            var (textWidth, textHeight) = GetTextMetrics(vehicleText);
            float rectWidth = Math.Max(boxWidth, textWidth);
            float boxCenterX = (vehicleRect.Left + vehicleRect.Right) / 2f;
            RawRectangleF textRect = new RawRectangleF(
                boxCenterX - rectWidth / 2f, vehicleRect.Top - textHeight - 5f,
                boxCenterX + rectWidth / 2f, vehicleRect.Top - 5f
            );
            renderTarget.DrawText(vehicleText, textFormat, textRect, vehicleBrush);
        }

        // Static: eliminates a List<(int,int)> and an unused int[] allocated per player per frame.
        private static readonly (int, int)[] BoneConnections =
        {
            (0, 2), (2, 3), (3, 4), (2, 5), (5, 6), (6, 7), (7, 8),
            (2, 9), (9, 10), (10, 11), (11, 12), (4, 13), (13, 14),
            (14, 15), (15, 16), (4, 17), (17, 18), (18, 19), (19, 20),
        };

        void DrawBoneLines(SolidColorBrush lineBrush, Vector2[] screenPositions)
        {
            if (screenPositions == null || screenPositions.Length == 0)
                return;

            // Brush is resolved once by the caller instead of two team lookups per bone segment.
            foreach (var (startIndex, endIndex) in BoneConnections)
            {
                if (startIndex < 0 || startIndex >= screenPositions.Length ||
                    endIndex < 0 || endIndex >= screenPositions.Length ||
                    screenPositions[startIndex] == Vector2.Zero || screenPositions[endIndex] == Vector2.Zero)
                    continue;

                renderTarget.DrawLine(
                    screenPositions[startIndex].ToRawVector2(),
                    screenPositions[endIndex].ToRawVector2(),
                    lineBrush
                );
            }
        }

        private RawRectangleF GetBoxFromBones(Vector2[] screenPositions)
        {
            if (screenPositions == null || screenPositions.Length < 19)
                return new RawRectangleF(0, 0, 0, 0);

            Vector2 head = screenPositions[0];
            Vector2 rightFoot = screenPositions[15];
            Vector2 leftFoot = screenPositions[18];

            if (head == Vector2.Zero || rightFoot == Vector2.Zero || leftFoot == Vector2.Zero)
                return new RawRectangleF(0, 0, 0, 0);

            float topY = Math.Min(head.Y, Math.Min(rightFoot.Y, leftFoot.Y));
            float bottomY = Math.Max(head.Y, Math.Max(rightFoot.Y, leftFoot.Y));
            float leftX = Math.Min(head.X, Math.Min(rightFoot.X, leftFoot.X));
            float rightX = Math.Max(head.X, Math.Max(rightFoot.X, leftFoot.X));

            const float padding = 6f;
            return new RawRectangleF(leftX - padding, topY - padding, rightX + padding, bottomY + padding);
        }

        private void DrawHealthBar(RawRectangleF boxRect, float health)
        {
            if (health < 0) return;

            const float barWidth = 5f;
            float barHeight = boxRect.Bottom - boxRect.Top;
            float healthHeight = (health / 100f) * barHeight;
            float barX = boxRect.Right + 2f;
            float barY = boxRect.Top + (barHeight - healthHeight);

            healthBrush.Color = new RawColor4(1.0f - (health / 100f), health / 100f, 0.0f, 1.0f);
            renderTarget.FillRectangle(new RawRectangleF(barX, barY, barX + barWidth, barY + healthHeight), healthBrush);
            renderTarget.DrawRectangle(new RawRectangleF(barX, boxRect.Top, barX + barWidth, boxRect.Bottom), enemyPlayerBrush);
        }

        private string GetNameText(UActor actor)
        {
            return actor.ActorType == ActorType.Player
                ? (Program.Config.EspShowNames ? actor.Name : "")
                : (ActorTypeNames.TryGetValue(actor.ActorType, out var typeName) ? typeName : "");
        }

        protected override void OnClosed(EventArgs e)
        {
            running = false;
            enemyPlayerBrush.Dispose();
            friendlyBrush.Dispose();
            squadBrush.Dispose();
            enemyVehicleBrush.Dispose();
            friendlyVehicleBrush.Dispose();
            healthBrush.Dispose();
            textFormat.Dispose();
            _dwFactory.Dispose();
            renderTarget.Dispose();
            base.OnClosed(e);
        }
    }
}
