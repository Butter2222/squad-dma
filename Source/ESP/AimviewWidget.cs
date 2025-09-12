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
                var game = Game;
                if (game?.LocalPlayer != null)
                {
                    var localPlayer = game.LocalPlayer;
                    var viewInfo = new MinimalViewInfo
                    {
                        Location = localPlayer.Position,
                        Rotation = localPlayer.Rotation3D,
                        FOV = game.CurrentFOV
                    };

                    var visibleActors = new List<(UActor actor, SKPoint panelPos, float distance)>();

                    // Process all actors and collect visible ones
                    foreach (var kvp in game.Actors)
                    {
                        var actor = kvp.Value;
                        if (actor == null || actor.Position == Vector3D.Zero)
                            continue;

                        // Skip local player
                        if (actor == localPlayer)
                            continue;

                        // Skip dead players
                        if (actor.ActorType == ActorType.Player && !actor.IsAlive)
                            continue;

                        // Calculate distance
                        Vector3 localPlayerPosVec = localPlayer.Position.ToVector3();
                        Vector3 actorPosVec = actor.Position.ToVector3();
                        float distance = Vector3.Distance(localPlayerPosVec, actorPosVec) / 100f;
                        
                        // Apply distance limits based on actor type
                        bool isPlayer = actor.ActorType == ActorType.Player;
                        bool isVehicle = IsVehicle(actor.ActorType);
                        float maxDistance = isPlayer ? Program.Config.EspMaxDistance : Program.Config.EspVehicleMaxDistance;
                        if (distance > maxDistance)
                            continue;

                        // Skip allies if not showing them
                        if (!Program.Config.EspShowAllies && actor.IsFriendly())
                            continue;

                        // Skip vehicles if not showing them
                        if (isVehicle && !Program.Config.EspShowVehicles)
                            continue;

                        // Skip unclaimed vehicles (don't show them at all)
                        if (isVehicle && actor.TeamID == -1)
                            continue;

                        // World to screen conversion
                        Vector2 screenPos = Camera.WorldToScreen(viewInfo, actor.Position);
                        if (screenPos == Vector2.Zero)
                            continue;

                        // Convert screen coordinates to panel coordinates 
                        // Simple direct scaling - let's see if this fixes the bunching issue
                        Vector2 panelPos = new Vector2(
                            (screenPos.X / (Screen.PrimaryScreen?.Bounds.Width ?? 1920)) * _espBitmap.Width,
                            (screenPos.Y / (Screen.PrimaryScreen?.Bounds.Height ?? 1080)) * _espBitmap.Height
                        );

                        // Skip if outside panel bounds (with margin for partially visible elements)
                        if (panelPos.X < -50 || panelPos.X > _espBitmap.Width + 50 || 
                            panelPos.Y < -50 || panelPos.Y > _espBitmap.Height + 50)
                            continue;
                            
                        // Skip if coordinates are invalid
                        if (float.IsNaN(panelPos.X) || float.IsNaN(panelPos.Y) || 
                            float.IsInfinity(panelPos.X) || float.IsInfinity(panelPos.Y))
                            continue;

                        visibleActors.Add((actor, new SKPoint(panelPos.X, panelPos.Y), distance));
                    }

                    // Separate players and vehicles, then by friendly status for sophisticated opacity rendering
                    var enemyPlayers = visibleActors.Where(x => x.actor.ActorType == ActorType.Player && !x.actor.IsFriendly()).OrderBy(x => x.distance).ToList();
                    var allyPlayers = visibleActors.Where(x => x.actor.ActorType == ActorType.Player && x.actor.IsFriendly()).ToList();
                    var enemyVehicles = visibleActors.Where(x => IsVehicle(x.actor.ActorType) && !x.actor.IsFriendly()).OrderBy(x => x.distance).ToList();
                    var allyVehicles = visibleActors.Where(x => IsVehicle(x.actor.ActorType) && x.actor.IsFriendly()).ToList();

                    // Draw enemy players with sophisticated FOV-based distance opacity
                    for (int i = 0; i < enemyPlayers.Count; i++)
                    {
                        var (actor, panelPos, distance) = enemyPlayers[i];
                        float opacity = CalculateFieldOfViewOpacity(i, enemyPlayers.Count, distance);
                        
                        if (Program.Config.EspShowBox)
                            DrawPlayerBox(actor, panelPos, distance, opacity);
                        if (Program.Config.EspBones)
                            DrawPlayerBones(actor, panelPos, opacity);
                    }

                    // Draw ally players with full opacity (no distance fading)
                    foreach (var (actor, panelPos, distance) in allyPlayers)
                    {
                        if (Program.Config.EspShowBox)
                            DrawPlayerBox(actor, panelPos, distance, 1.0f);
                        if (Program.Config.EspBones)
                            DrawPlayerBones(actor, panelPos, 1.0f);
                    }

                    // Draw enemy vehicles with FOV-based distance opacity
                    for (int i = 0; i < enemyVehicles.Count; i++)
                    {
                        var (actor, panelPos, distance) = enemyVehicles[i];
                        float opacity = CalculateFieldOfViewOpacity(i, enemyVehicles.Count, distance);
                        
                        if (Program.Config.EspShowBox)
                            DrawVehicleBox(actor, panelPos, distance, opacity);
                    }

                    // Draw ally vehicles with full opacity (no distance fading)
                    if (Program.Config.EspShowAllies)
                    {
                        foreach (var (actor, panelPos, distance) in allyVehicles)
                        {
                            if (Program.Config.EspShowBox)
                                DrawVehicleBox(actor, panelPos, distance, 1.0f);
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

        private void DrawCrosshair()
        {
            if (_espCanvas == null) return;

            try
            {
                // Calculate center of the content panel
                float centerX = _espBitmap.Width / 2.0f;
                float centerY = _espBitmap.Height / 2.0f;

                // Crosshair brush (semi-transparent white)
                using var crosshairBrush = new SKPaint
                {
                    Color = new SKColor(255, 255, 255, (byte)(0.8f * 255)), // Semi-transparent white
                    StrokeWidth = 1.0f,
                    Style = SKPaintStyle.Stroke,
                    IsAntialias = true
                };

                // Draw vertical line (center X)
                _espCanvas.DrawLine(
                    new SKPoint(centerX, 0),
                    new SKPoint(centerX, _espBitmap.Height),
                    crosshairBrush
                );

                // Draw horizontal line (center Y)
                _espCanvas.DrawLine(
                    new SKPoint(0, centerY),
                    new SKPoint(_espBitmap.Width, centerY),
                    crosshairBrush
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Crosshair draw error: {ex.Message}");
            }
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

        private void DrawPlayerBones(UActor actor, SKPoint panelPos, float opacity = 1.0f)
        {
            // Minimal center dot indicator 
            float dotSize = 1.5f;
            var dotRect = new SKRect(
                panelPos.X - dotSize, 
                panelPos.Y - dotSize, 
                panelPos.X + dotSize, 
                panelPos.Y + dotSize);
            
            // Create brush with distance-based opacity for bones - using updated color scheme
            SKPaint dotBrush;
            if (actor.IsInMySquad())
            {
                // Squad members = Green
                dotBrush = new SKPaint
                {
                    Color = SKPaints.Squad.WithAlpha((byte)(255 * opacity)),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
            }
            else if (actor.IsFriendly())
            {
                // Friendly players = SKPaints.Friendly (light blue)
                dotBrush = new SKPaint
                {
                    Color = SKPaints.Friendly.WithAlpha((byte)(255 * opacity)),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
            }
            else
            {
                // Enemy players = White
                dotBrush = new SKPaint
                {
                    Color = SKPaints.EnemyPlayer.WithAlpha((byte)(255 * opacity)),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
            }
            
            _espCanvas.DrawRect(dotRect, dotBrush);
            
            // Dispose dynamic brush to prevent memory leaks
            dotBrush.Dispose();
        }

        private void DrawPlayerBox(UActor actor, SKPoint panelPos, float distance, float opacity = 1.0f)
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

            // Create brush with distance-based opacity - using updated color scheme
            SKPaint boxBrush;
            if (actor.IsInMySquad())
            {
                // Squad members = Green
                boxBrush = new SKPaint
                {
                    Color = SKPaints.Squad.WithAlpha((byte)(255 * opacity)),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.0f,
                    IsAntialias = true
                };
            }
            else if (actor.IsFriendly())
            {
                // Friendly players = SKPaints.Friendly (light blue)
                boxBrush = new SKPaint
                {
                    Color = SKPaints.Friendly.WithAlpha((byte)(255 * opacity)),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.0f,
                    IsAntialias = true
                };
            }
            else
            {
                // Enemy players = White
                boxBrush = new SKPaint
                {
                    Color = SKPaints.EnemyPlayer.WithAlpha((byte)(255 * opacity)),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.0f,
                    IsAntialias = true
                };
            }
            
            _espCanvas.DrawRect(rect, boxBrush);

            // Draw health bar
            DrawHealthBar(actor, rect, opacity);

            // Draw player info text (name and/or distance) 
            var textParts = new List<string>();
            
            if (Program.Config.EspShowNames && !string.IsNullOrEmpty(actor.Name))
            {
                textParts.Add(actor.Name);
            }
            
            if (Program.Config.EspShowDistance)
            {
                textParts.Add($"{distance:F0}m");
            }
            
            if (textParts.Count > 0)
            {
                var text = string.Join(" ", textParts);
                
                // Draw text using same color as box
                using var textPaint = new SKPaint
                {
                    Color = boxBrush.Color,
                    TextSize = 9.0f,
                    IsAntialias = true,
                    Typeface = CustomFonts.SKFontFamilyRegular
                };
                
                _espCanvas.DrawText(text, new SKPoint(rect.Left, rect.Bottom + 15), textPaint);
            }
            
            // Dispose dynamic brush to prevent memory leaks
            boxBrush.Dispose();
        }

        private void DrawHealthBar(UActor actor, SKRect rect, float opacity = 1.0f)
        {
            if (!Program.Config.EspShowHealth || actor.Health <= 0)
                return;

            float healthPercent = actor.Health / 100.0f;
            float barWidth = rect.Right - rect.Left;
            float barHeight = 2.0f; // Thinner health bar

            var healthRect = new SKRect(
                rect.Left,
                rect.Top - barHeight - 1,
                rect.Left + (barWidth * healthPercent),
                rect.Top - 1);

            // Color based on health percentage with opacity 
            // Health bar color like EspOverlay - red to green gradient
            byte healthAlpha = (byte)(255 * opacity);
            var healthColor = new SKColor(
                (byte)(255 * (1.0f - (actor.Health / 100f))), // Red component (high when low health)
                (byte)(255 * (actor.Health / 100f)),          // Green component (high when high health)  
                0,                                             // Blue component (always 0)
                healthAlpha);

            using var healthPaint = new SKPaint
            {
                Color = healthColor,
                Style = SKPaintStyle.Fill
            };
            _espCanvas.DrawRect(healthRect, healthPaint);
        }

        private void DrawVehicleBox(UActor actor, SKPoint panelPos, float distance, float opacity = 1.0f)
        {
            // Skip unclaimed vehicles - they should not be shown at all
            if (actor.TeamID == -1)
                return;

            // Vehicle-type-based sizing with reduced base size 
            float baseSize = 480f; // Reduced by 60% (1200 * 0.4)
            float minSize = 50f;    // Minimum size to ensure visibility in aimview
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

            var rect = new SKRect(
                panelPos.X - boxWidth / 2f,
                panelPos.Y - boxHeight / 2f,
                panelPos.X + boxWidth / 2f,
                panelPos.Y + boxHeight / 2f
            );

            // Create brush with distance-based opacity - using updated color scheme
            SKPaint vehBrush;
            if (actor.IsFriendly())
            {
                // Friendly vehicles = SKPaints.FriendlyVehicle (friendly blue color)
                vehBrush = new SKPaint
                {
                    Color = SKPaints.FriendlyVehicle.WithAlpha((byte)(255 * opacity)),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.0f,
                    IsAntialias = true
                };
            }
            else
            {
                // Enemy vehicles = Yellow
                vehBrush = new SKPaint
                {
                    Color = SKPaints.EnemyVehicle.WithAlpha((byte)(255 * opacity)), // Yellow
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.0f,
                    IsAntialias = true
                };
            }
            
            _espCanvas.DrawRect(rect, vehBrush);

            // Draw vehicle info text if enabled 
            var textParts = new List<string>();
            
            // Always show vehicle name
            var vehicleName = ActorTypeNames.ContainsKey(actor.ActorType) ? ActorTypeNames[actor.ActorType] : actor.ActorType.ToString();
            textParts.Add(vehicleName);
            
            if (Program.Config.EspShowDistance)
            {
                textParts.Add($"{distance:F0}m");
            }
            
            if (textParts.Count > 0)
            {
                var text = string.Join(" ", textParts);
                
                // Draw text using same brush as vehicle box
                using var textPaint = new SKPaint
                {
                    Color = vehBrush.Color,
                    TextSize = 9.0f,
                    IsAntialias = true,
                    Typeface = CustomFonts.SKFontFamilyRegular
                };
                
                _espCanvas.DrawText(text, new SKPoint(rect.Left, rect.Bottom + 15), textPaint);
            }
            
            // Dispose dynamic brush to prevent memory leaks (AFTER using it)
            vehBrush.Dispose();
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
            return ActorTypeNames.ContainsKey(type) ? ActorTypeNames[type] : type.ToString();
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
