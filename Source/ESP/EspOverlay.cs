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
        private SolidColorBrush markerShadowBrush;

        // Box or marker per target, decided from its on-screen size
        private readonly TargetLod _lod = new();
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

        private int _teamColorVersion = -1;

        private static RawColor4 ToRaw(SkiaSharp.SKColor c) => new(c.Red / 255f, c.Green / 255f, c.Blue / 255f, 1f);

        /// <summary>Copies the colors from the Colors tab into the team brushes whenever they have changed.</summary>
        private void SyncTeamColors()
        {
            int version = SKPaints.ColorVersion;
            if (version == _teamColorVersion)
                return;
            _teamColorVersion = version;

            enemyPlayerBrush.Color = ToRaw(SKPaints.EnemyPlayer);
            friendlyBrush.Color = ToRaw(SKPaints.Friendly);
            squadBrush.Color = ToRaw(SKPaints.Squad);
            enemyVehicleBrush.Color = ToRaw(SKPaints.EnemyVehicle);
            friendlyVehicleBrush.Color = ToRaw(SKPaints.FriendlyVehicle);
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
            // The team brushes take their colors from the Colors tab (SKPaints); SyncTeamColors keeps them current.
            enemyPlayerBrush = new SolidColorBrush(renderTarget, default);
            
            friendlyBrush = new SolidColorBrush(renderTarget, default);
            
            squadBrush = new SolidColorBrush(renderTarget, default);
            
            enemyVehicleBrush = new SolidColorBrush(renderTarget, default);
            
            friendlyVehicleBrush = new SolidColorBrush(renderTarget, default);
            SyncTeamColors();
            
            // Health bar brush (dynamic color based on health percentage)
            healthBrush = new SolidColorBrush(renderTarget, new RawColor4(0.0f, 1.0f, 0.0f, 1.0f));

            // Dark outline under the markers so they stay readable on bright backgrounds
            markerShadowBrush = new SolidColorBrush(renderTarget, new RawColor4(0f, 0f, 0f, 0.75f));

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

        // Empty (all zero) when the box cannot be projected
        private static RawRectangleF CalculatePlayerBox(UActor actor)
        {
            return EspGeometry.TryPlayerRect(actor, actor.Position, false, out var ndc)
                ? NdcToScreenRect(ndc)
                : new RawRectangleF(0, 0, 0, 0);
        }

        private static RawRectangleF NdcToScreenRect(in NdcRect ndc)
        {
            var topLeft = Camera.NdcToViewport(new Vector2(ndc.MinX, ndc.MaxY));
            var bottomRight = Camera.NdcToViewport(new Vector2(ndc.MaxX, ndc.MinY));
            return new RawRectangleF(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
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

        // Box or marker thresholds scale with the screen height; the aim pad is how close the center of the screen
        // must be to a target for it to count as aimed at.
        private float BoxEnterSize => Height * 0.025f;
        private float BoxExitSize => Height * 0.017f;
        private float MarkerArm => Math.Max(4f, Height * 0.0036f);
        private const float AimPad = 14f;

        /// <summary>Small solid "+" with a dark outline, used in place of a box for small targets.</summary>
        private void DrawMarker(SolidColorBrush brush, float cx, float cy, float arm)
        {
            DrawMarkerLines(markerShadowBrush, cx, cy, arm + 1f, 4f);
            DrawMarkerLines(brush, cx, cy, arm, 2f);
        }

        private void DrawMarkerLines(SolidColorBrush brush, float cx, float cy, float arm, float width)
        {
            renderTarget.DrawLine(new RawVector2(cx - arm, cy), new RawVector2(cx + arm, cy), brush, width);
            renderTarget.DrawLine(new RawVector2(cx, cy - arm), new RawVector2(cx, cy + arm), brush, width);
        }

        /// <summary>
        /// Draws the target as a box or a marker and returns the rectangle the labels should sit around:
        /// the box itself, or a small square around the marker.
        /// </summary>
        private RawRectangleF DrawBoxOrMarker(ulong key, RawRectangleF rect, SolidColorBrush brush, float markerScale, bool draw, out bool asBox)
        {
            float width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
            float midX = (rect.Left + rect.Right) * 0.5f, midY = (rect.Top + rect.Bottom) * 0.5f;
            bool aimed = TargetLod.IsAimedAt(rect.Left, rect.Top, rect.Right, rect.Bottom, Width * 0.5f, Height * 0.5f, AimPad);

            asBox = _lod.UseBox(key, Math.Max(width, height), BoxEnterSize, BoxExitSize, aimed);
            if (asBox)
            {
                // A target promoted by aim can still be tiny; keep the box large enough to see
                float minW = Math.Max(width, 14f * markerScale), minH = Math.Max(height, 14f * markerScale);
                rect = new RawRectangleF(midX - minW * 0.5f, midY - minH * 0.5f, midX + minW * 0.5f, midY + minH * 0.5f);
                if (draw) renderTarget.DrawRectangle(rect, brush);
                return rect;
            }

            float arm = MarkerArm * markerScale;
            if (draw) DrawMarker(brush, midX, midY, arm);
            return new RawRectangleF(midX - arm, midY - arm, midX + arm, midY + arm);
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

            SyncTeamColors();
            _lod.BeginFrame();

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
                    RawRectangleF boxRect = CalculatePlayerBox(actor);
                    if ((boxRect.Left == 0 && boxRect.Top == 0 && boxRect.Right == 0 && boxRect.Bottom == 0) &&
                        Program.Config.EspBones && actor.BoneScreenPositions != null)
                    {
                        boxRect = GetBoxFromBones(actor.BoneScreenPositions);
                    }

                    if (boxRect.Left == 0 && boxRect.Top == 0 && boxRect.Right == 0 && boxRect.Bottom == 0)
                        continue;

                    // Squad / friendly / enemy brush resolved once for box, name, distance and bones.
                    var teamBrush = TeamBrush(actor);

                    // Box when big enough or aimed at, otherwise a small marker
                    boxRect = DrawBoxOrMarker(actor.Base, boxRect, teamBrush, 1f, Program.Config.EspShowBox, out bool drawnAsBox);

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

                    if (drawnAsBox && Program.Config.EspShowHealth && actor.Health >= 0)
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

        private void DrawVehicleBox(UActor actor, Vector2 screenPos, float distance)
        {
            bool isEnemy = actor.TeamID != -1 && actor.TeamID != Memory.LocalPlayer.TeamID;
            bool isFriendly = actor.TeamID != -1 && actor.TeamID == Memory.LocalPlayer.TeamID;
            bool isUnclaimed = actor.TeamID == -1;

            if (!Program.Config.EspShowAllies && !isEnemy)
                return;

            // Select brush based on vehicle ownership - matching AimviewWidget color scheme
            var vehicleBrush = (isUnclaimed || isEnemy) ? enemyVehicleBrush : friendlyVehicleBrush;

            // Box from the vehicle's real mesh bounds, oriented with the vehicle
            if (!EspGeometry.TryVehicleRect(actor, actor.Position, false, out var vehicleNdc))
                return;

            RawRectangleF vehicleRect = NdcToScreenRect(vehicleNdc);
            float boxWidth = vehicleRect.Right - vehicleRect.Left;

            // Eliminates the rectangle draw, string build and text-metrics lookup for fully off-screen vehicles
            // (text sits just above the box and is at most a few hundred px wide, hence the margin).
            if (vehicleRect.Right < -400f || vehicleRect.Left > Width + 400f ||
                vehicleRect.Bottom < -100f || vehicleRect.Top > Height + 100f)
                return;

            // Same marker size as players; only the color differs
            vehicleRect = DrawBoxOrMarker(actor.Base, vehicleRect, vehicleBrush, 1f, true, out _);
            boxWidth = vehicleRect.Right - vehicleRect.Left;

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
            markerShadowBrush.Dispose();
            textFormat.Dispose();
            _dwFactory.Dispose();
            renderTarget.Dispose();
            base.OnClosed(e);
        }
    }
}
