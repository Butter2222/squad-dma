using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace squad_dma
{
    public sealed class AimviewWidget : SKWidget
    {
        private readonly MainForm _parentForm;
        private SKBitmap _espBitmap;
        private SKCanvas _espCanvas;

        // Vehicle size categories for realistic box sizing (matching ESP overlay)
        private enum VehicleSize { Small, Medium, Large, ExtraLarge }

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
            ActorType.AntiAir, ActorType.APC, ActorType.AttackHelicopter, ActorType.LoachCAS, ActorType.LoachScout,
            ActorType.Boat, ActorType.BoatLogistics, ActorType.IFV, ActorType.JeepAntiAir, ActorType.JeepAntitank,
            ActorType.JeepArtillery, ActorType.JeepLogistics, ActorType.JeepTransport, ActorType.JeepRWSTurret,
            ActorType.JeepTurret, ActorType.Motorcycle, ActorType.Tank, ActorType.TankMGS, ActorType.TrackedAPC,
            ActorType.TrackedAPCArtillery, ActorType.TrackedIFV, ActorType.TrackedJeep, ActorType.TrackedLogistics,
            ActorType.TransportHelicopter, ActorType.TruckAntiAir, ActorType.TruckArtillery, ActorType.TruckLogistics,
            ActorType.TruckTransport, ActorType.TruckTransportArmed
        };

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

        public AimviewWidget(SKGLControl parent, MainForm parentForm, SKRect location, bool minimized, float scale)
            : base(parent, "Aimview", new SKPoint(location.Left, location.Top), new SKSize(location.Width, location.Height), scale)
        {
            _parentForm = parentForm;
            _espBitmap = new SKBitmap((int)location.Width, (int)location.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            _espCanvas = new SKCanvas(_espBitmap);
            Minimized = minimized;
        }

        /// <summary>
        /// Current game instance
        /// </summary>
        private Game Game => Memory._game;

        public override void Draw(SKCanvas canvas)
        {
            base.Draw(canvas);
            if (!Minimized)
                RenderAimviewWidget(canvas, ClientRectangle);
        }

        private readonly List<AimviewEntry> _enemyPlayers = new(32);
        private readonly List<AimviewEntry> _allyPlayers = new(32);
        private readonly List<AimviewEntry> _enemyVehicles = new(16);
        private readonly List<AimviewEntry> _allyVehicles = new(16);

        private readonly SKPaint _boxPaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 1.0f, IsAntialias = true };
        private readonly SKPaint _dotPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
        private readonly SKPaint _labelPaint = new() { TextSize = 9.0f, IsAntialias = true, Typeface = CustomFonts.SKFontFamilyRegular };
        private readonly SKPaint _healthPaint = new() { Style = SKPaintStyle.Fill };
        private readonly SKPaint _crosshairPaint = new()
        {
            Color = new SKColor(255, 255, 255, (byte)(0.8f * 255)), // Semi-transparent white
            StrokeWidth = 1.0f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        private static readonly Dictionary<ActorType, string> _vehicleNameCache = new();

        private struct AimviewEntry
        {
            public ActorFrameData Data;
            public SKPoint PanelPos;
            public float Distance;
        }

        /// <summary>
        /// Perform Aimview (Mini-ESP) Rendering with sophisticated distance-based opacity fading.
        /// </summary>
        private void RenderAimviewWidget(SKCanvas parent, SKRect dest)
        {
            var size = Size;
            if (_espBitmap is null || _espCanvas is null ||
                _espBitmap.Width != size.Width || _espBitmap.Height != size.Height)
            {
                _espCanvas?.Dispose();
                _espCanvas = null;
                _espBitmap?.Dispose();
                _espBitmap = null;
                _espBitmap = new SKBitmap((int)size.Width, (int)size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
                _espCanvas = new SKCanvas(_espBitmap);
            }

            _espCanvas.Clear(SKColors.Transparent);
            try
            {
                // Read from the frame snapshot MainForm built for this frame instead of re-enumerating the live
                // actor dictionary with per-actor locked Position reads and repeated team lookups.
                var frame = _parentForm.FrameSnapshot;
                var localPlayer = frame.LocalPlayer;
                if (localPlayer != null)
                {
                    // Hoisted out of the per-actor loop: these never change within a frame.
                    Vector3 localPlayerPosVec = localPlayer.Position.ToVector3();
                    float maxPlayerDistance = Program.Config.EspMaxDistance;
                    float maxVehicleDistance = Program.Config.EspVehicleMaxDistance;
                    bool showAllies = Program.Config.EspShowAllies;
                    bool showVehicles = Program.Config.EspShowVehicles;
                    bool showBox = Program.Config.EspShowBox;
                    bool showBones = Program.Config.EspBones;
                    var screenBounds = Screen.PrimaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
                    float scaleX = _espBitmap.Width / (float)screenBounds.Width;
                    float scaleY = _espBitmap.Height / (float)screenBounds.Height;
                    float bmpW = _espBitmap.Width, bmpH = _espBitmap.Height;

                    _enemyPlayers.Clear();
                    _allyPlayers.Clear();
                    _enemyVehicles.Clear();
                    _allyVehicles.Clear();

                    // Single pass: filter AND bucket by type/team (replaces 4 LINQ Where/OrderBy/ToList passes).
                    var items = frame.Items;
                    for (int i = 0; i < items.Count; i++)
                    {
                        var data = items[i];
                        var actor = data.Actor;

                        // Skip local player
                        if (actor == localPlayer)
                            continue;

                        if (data.Position == Vector3D.Zero)
                            continue;

                        bool isPlayer = data.Type == ActorType.Player;

                        // Skip dead players
                        if (isPlayer && !data.IsAlive)
                            continue;

                        bool isVehicle = IsVehicle(data.Type);

                        // Only players and vehicles are ever drawn below; skipping the rest here avoids a
                        // world-to-screen projection for every mine / deployable / spawner in range.
                        if (!isPlayer && !isVehicle)
                            continue;

                        Vector3 actorPosVec = data.Position.ToVector3();
                        float distance = Vector3.Distance(localPlayerPosVec, actorPosVec) / 100f;

                        // Apply distance limits based on actor type
                        if (distance > (isPlayer ? maxPlayerDistance : maxVehicleDistance))
                            continue;

                        // Skip allies if not showing them
                        if (!showAllies && data.IsFriendly)
                            continue;

                        // Skip vehicles if not showing them
                        if (isVehicle && !showVehicles)
                            continue;

                        // Skip unclaimed vehicles (don't show them at all)
                        if (isVehicle && data.TeamID == -1)
                            continue;

                        // World to screen conversion
                        Vector2 screenPos = Camera.WorldToScreen(data.Position);
                        if (screenPos == Vector2.Zero)
                            continue;

                        // Convert screen coordinates to panel coordinates (direct scaling)
                        float panelX = screenPos.X * scaleX;
                        float panelY = screenPos.Y * scaleY;

                        // Cull before bucketing: skip if outside panel bounds (with margin for partially visible elements)
                        if (panelX < -50 || panelX > bmpW + 50 || panelY < -50 || panelY > bmpH + 50)
                            continue;

                        // Skip if coordinates are invalid
                        if (float.IsNaN(panelX) || float.IsNaN(panelY) || float.IsInfinity(panelX) || float.IsInfinity(panelY))
                            continue;

                        var entry = new AimviewEntry { Data = data, PanelPos = new SKPoint(panelX, panelY), Distance = distance };
                        if (isPlayer)
                            (data.IsFriendly ? _allyPlayers : _enemyPlayers).Add(entry);
                        else
                            (data.IsFriendly ? _allyVehicles : _enemyVehicles).Add(entry);
                    }

                    // Enemies are ranked by distance (stable) for the FOV-based opacity falloff.
                    InsertionSortByDistance(_enemyPlayers);
                    InsertionSortByDistance(_enemyVehicles);

                    // Draw enemy players with sophisticated FOV-based distance opacity
                    for (int i = 0; i < _enemyPlayers.Count; i++)
                    {
                        var e = _enemyPlayers[i];
                        float opacity = CalculateFieldOfViewOpacity(i, _enemyPlayers.Count, e.Distance);

                        if (showBox)
                            DrawPlayerBox(e.Data, e.PanelPos, e.Distance, opacity);
                        if (showBones)
                            DrawPlayerBones(e.Data, e.PanelPos, opacity);
                    }

                    // Draw ally players with full opacity (no distance fading)
                    for (int i = 0; i < _allyPlayers.Count; i++)
                    {
                        var e = _allyPlayers[i];
                        if (showBox)
                            DrawPlayerBox(e.Data, e.PanelPos, e.Distance, 1.0f);
                        if (showBones)
                            DrawPlayerBones(e.Data, e.PanelPos, 1.0f);
                    }

                    // Draw enemy vehicles with FOV-based distance opacity
                    for (int i = 0; i < _enemyVehicles.Count; i++)
                    {
                        var e = _enemyVehicles[i];
                        float opacity = CalculateFieldOfViewOpacity(i, _enemyVehicles.Count, e.Distance);

                        if (showBox)
                            DrawVehicleBox(e.Data, e.PanelPos, e.Distance, opacity);
                    }

                    // Draw ally vehicles with full opacity (no distance fading)
                    if (showAllies)
                    {
                        for (int i = 0; i < _allyVehicles.Count; i++)
                        {
                            var e = _allyVehicles[i];
                            if (showBox)
                                DrawVehicleBox(e.Data, e.PanelPos, e.Distance, 1.0f);
                        }
                    }

                    // Draw crosshair
                    DrawCrosshair();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CRITICAL AIMVIEW WIDGET RENDER ERROR: {ex}");
            }

            _espCanvas.Flush();
            parent.DrawBitmap(_espBitmap, dest, SharedPaints.PaintBitmap);
        }

        /// <summary>Stable in-place sort; lists here hold only on-screen targets so insertion sort beats LINQ OrderBy.</summary>
        private static void InsertionSortByDistance(List<AimviewEntry> list)
        {
            for (int i = 1; i < list.Count; i++)
            {
                var key = list[i];
                int j = i - 1;
                while (j >= 0 && list[j].Distance > key.Distance)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = key;
            }
        }

        private void DrawCrosshair()
        {
            if (_espCanvas == null) return;

            float centerX = _espBitmap.Width / 2.0f;
            float centerY = _espBitmap.Height / 2.0f;

            _espCanvas.DrawLine(centerX, 0, centerX, _espBitmap.Height, _crosshairPaint); // vertical
            _espCanvas.DrawLine(0, centerY, _espBitmap.Width, centerY, _crosshairPaint);  // horizontal
        }

        private float CalculateFieldOfViewOpacity(int visibleEnemyIndex, int totalVisibleEnemies, float distance)
        {
            // Closest enemy WITHIN THE AIMVIEW (index 0) gets full opacity
            // This is different from global closest - it's the closest enemy you're actually looking at
            if (visibleEnemyIndex == 0)
                return 1.0f;

            // Calculate opacity based on visible enemy ranking (only enemies in your field of view)
            // Each subsequent visible enemy gets progressively more transparent
            float rankOpacity = Math.Max(0.2f, 1.0f - (visibleEnemyIndex * 0.12f));

            // Additional distance-based opacity reduction for very far enemies
            // Enemies beyond 100m get additional transparency
            float distanceOpacity = distance <= 100f ? 1.0f : Math.Max(0.3f, 1.0f - ((distance - 100f) / 300f));

            // Combine both factors, ensuring reasonable minimum visibility
            return Math.Max(0.2f, rankOpacity * distanceOpacity);
        }

        /// <summary>Squad / friendly / enemy color, with distance-based alpha applied.</summary>
        private static SKColor PlayerColor(in ActorFrameData data, float opacity)
        {
            var color = data.IsInMySquad ? SKPaints.Squad      // Squad members = Green
                      : data.IsFriendly ? SKPaints.Friendly    // Friendly players = light blue
                      : SKPaints.EnemyPlayer;                  // Enemy players = White
            return color.WithAlpha((byte)(255 * opacity));
        }

        private void DrawPlayerBones(in ActorFrameData data, SKPoint panelPos, float opacity = 1.0f)
        {
            // Minimal center dot indicator
            float dotSize = 1.5f;
            var dotRect = new SKRect(
                panelPos.X - dotSize,
                panelPos.Y - dotSize,
                panelPos.X + dotSize,
                panelPos.Y + dotSize);

            _dotPaint.Color = PlayerColor(data, opacity);
            _espCanvas.DrawRect(dotRect, _dotPaint);
        }

        private void DrawPlayerBox(in ActorFrameData data, SKPoint panelPos, float distance, float opacity = 1.0f)
        {
            // Use a simple fixed-size box based on distance
            float boxSize = Math.Max(20f, Math.Min(100f, 2000f / distance)); // Adaptive size based on distance
            float boxWidth = boxSize * 0.6f; // Narrower width for player aspect ratio
            float boxHeight = boxSize;

            var rect = new SKRect(
                panelPos.X - boxWidth / 2f,
                panelPos.Y - boxHeight / 2f,
                panelPos.X + boxWidth / 2f,
                panelPos.Y + boxHeight / 2f
            );

            var color = PlayerColor(data, opacity);
            _boxPaint.Color = color;
            _espCanvas.DrawRect(rect, _boxPaint);

            // Draw health bar
            DrawHealthBar(data.Health, rect, opacity);

            // Draw player info text (name and/or distance)
            string name = Program.Config.EspShowNames && !string.IsNullOrEmpty(data.Actor.Name) ? data.Actor.Name : null;
            string dist = Program.Config.EspShowDistance ? DistanceLabels.ForMeters(RoundedMeters(distance)) : null;
            string text = name != null && dist != null ? string.Concat(name, " ", dist) : name ?? dist;

            if (text != null)
            {
                // Draw text using same color as box
                _labelPaint.Color = color;
                _espCanvas.DrawText(text, rect.Left, rect.Bottom + 15, _labelPaint);
            }
        }

        // Matches the old "{distance:F0}" formatting (round half away from zero)
        private static int RoundedMeters(float distance) => (int)MathF.Round(distance, MidpointRounding.AwayFromZero);

        private void DrawHealthBar(float health, SKRect rect, float opacity = 1.0f)
        {
            if (!Program.Config.EspShowHealth || health <= 0)
                return;

            float healthPercent = health / 100.0f;
            float barWidth = rect.Right - rect.Left;
            float barHeight = 2.0f; // Thinner health bar

            var healthRect = new SKRect(
                rect.Left,
                rect.Top - barHeight - 1,
                rect.Left + (barWidth * healthPercent),
                rect.Top - 1);

            // Health bar color like EspOverlay - red to green gradient, with opacity
            _healthPaint.Color = new SKColor(
                (byte)(255 * (1.0f - (health / 100f))), // Red component (high when low health)
                (byte)(255 * (health / 100f)),          // Green component (high when high health)
                0,                                       // Blue component (always 0)
                (byte)(255 * opacity));
            _espCanvas.DrawRect(healthRect, _healthPaint);
        }

        private void DrawVehicleBox(in ActorFrameData data, SKPoint panelPos, float distance, float opacity = 1.0f)
        {
            // Skip unclaimed vehicles - they should not be shown at all
            if (data.TeamID == -1)
                return;

            // Vehicle-type-based sizing with reduced base size
            float baseSize = 480f; // Reduced by 60% (1200 * 0.4)
            float minSize = 50f;    // Minimum size to ensure visibility in aimview
            float maxSize = 2000f;  // Maximum size for very large vehicles

            // Get vehicle-specific size multiplier
            float sizeMultiplier = GetVehicleSizeMultiplier(data.Type);
            float vehicleBaseSize = baseSize * sizeMultiplier;

            // Distance-based scaling with better curve
            float distanceScale = Math.Max(1f, distance / 10f); // Scale factor based on distance
            float boxSize = Math.Max(minSize, Math.Min(maxSize, vehicleBaseSize / distanceScale));

            // Aspect ratio similar to 1204x754 (approximately 1.6:1)
            float boxWidth = boxSize * 1.6f;  // Width based on 1204x754 aspect ratio
            float boxHeight = boxSize * 1.0f; // Height maintains the calculated box size

            var rect = new SKRect(
                panelPos.X - boxWidth / 2f,
                panelPos.Y - boxHeight / 2f,
                panelPos.X + boxWidth / 2f,
                panelPos.Y + boxHeight / 2f
            );

            // Friendly vehicles = friendly blue, enemy vehicles = yellow, both with distance-based opacity
            var color = (data.IsFriendly ? SKPaints.FriendlyVehicle : SKPaints.EnemyVehicle).WithAlpha((byte)(255 * opacity));
            _boxPaint.Color = color;
            _espCanvas.DrawRect(rect, _boxPaint);

            // Vehicle name always shown, distance if enabled
            string vehicleName = GetVehicleName(data.Type);
            string text = Program.Config.EspShowDistance
                ? string.Concat(vehicleName, " ", DistanceLabels.ForMeters(RoundedMeters(distance)))
                : vehicleName;

            // Draw text using same color as vehicle box
            _labelPaint.Color = color;
            _espCanvas.DrawText(text, rect.Left, rect.Bottom + 15, _labelPaint);
        }

        private bool IsVehicle(ActorType type)
        {
            return VehicleTypes.Contains(type);
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

        private string GetVehicleName(ActorType type)
        {
            // Enum.ToString() allocates; resolve each type's name once.
            if (!_vehicleNameCache.TryGetValue(type, out var name))
            {
                name = ActorTypeNames.TryGetValue(type, out var known) ? known : type.ToString();
                _vehicleNameCache[type] = name;
            }
            return name;
        }


        public override void SetScaleFactor(float newScale)
        {
            base.SetScaleFactor(newScale);
            PaintAimviewCrosshair.StrokeWidth = 1 * newScale;
            PaintAimviewEnemy.StrokeWidth = 1 * newScale;
            PaintAimviewFriendly.StrokeWidth = 1 * newScale;
            PaintAimviewVehicle.StrokeWidth = 1 * newScale;
            TextAimviewInfo.TextSize = 9f * newScale;
        }

        public override void Dispose()
        {
            _espBitmap?.Dispose();
            _espCanvas?.Dispose();
            _boxPaint.Dispose();
            _dotPaint.Dispose();
            _labelPaint.Dispose();
            _healthPaint.Dispose();
            _crosshairPaint.Dispose();
            base.Dispose();
        }

        #region Aimview Paints
        private static SKPaint PaintAimviewCrosshair { get; } = new()
        {
            Color = SKColors.White,
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        private static SKPaint PaintAimviewEnemy { get; } = new()
        {
            Color = SKColors.Red,
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        private static SKPaint PaintAimviewFriendly { get; } = new()
        {
            Color = SKColors.LimeGreen,
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        private static SKPaint PaintAimviewVehicle { get; } = new()
        {
            Color = SKColors.Yellow,
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        private static SKPaint TextAimviewInfo { get; } = new()
        {
            SubpixelText = true,
            Color = SKColors.White,
            IsStroke = false,
            TextSize = 9f,
            TextAlign = SKTextAlign.Center,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            Typeface = CustomFonts.SKFontFamilyRegular,
            FilterQuality = SKFilterQuality.High
        };
        #endregion
    }
}
