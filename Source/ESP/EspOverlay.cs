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
        private SolidColorBrush brush;
        private SolidColorBrush vehicleBrush;
        private SolidColorBrush boneBrush;
        private SolidColorBrush healthBrush;
        private SolidColorBrush friendlyBrush;
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
            { ActorType.Tank, "Heavy Tank" },
            { ActorType.TankMGS, "Medium Tank" },
            { ActorType.TrackedAPC, "Tracked APC" },
            { ActorType.TrackedLogistics, "Half Track" },
            { ActorType.TrackedAPCArtillery, "Tracked APC Artillery" },
            { ActorType.TrackedIFV, "Light Tank" },
            { ActorType.TrackedJeep, "Tracked Jeep" },
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
            renderTarget = new WindowRenderTarget(factory, new RenderTargetProperties(), renderProperties);
            brush = new SolidColorBrush(renderTarget, new RawColor4(
                Program.Config.EspTextColor.R / 255f,
                Program.Config.EspTextColor.G / 255f,
                Program.Config.EspTextColor.B / 255f,
                Program.Config.EspTextColor.A / 255f
            ));
            vehicleBrush = new SolidColorBrush(renderTarget, new RawColor4(1.0f, 0.0f, 0.0f, 1.0f));
            boneBrush = brush;
            healthBrush = new SolidColorBrush(renderTarget, new RawColor4(0.0f, 1.0f, 0.0f, 1.0f));
            // Friendly/ally brush using SKPaints.Friendly color (light blue)
            friendlyBrush = new SolidColorBrush(renderTarget, new RawColor4(0f / 255f, 187f / 255f, 254f / 255f, 1.0f));
            textFormat = new SharpDX.DirectWrite.TextFormat(new SharpDX.DirectWrite.Factory(), "Verdana", Program.Config.ESPFontSize)
            {
                TextAlignment = SharpDX.DirectWrite.TextAlignment.Center,
                ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center,
                WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap // Prevent text wrapping
            };
        }

        private void StartRenderLoop()
        {
            Thread renderThread = new Thread(() =>
            {
                Program.Log("Render thread started.");
                bool wasReadyLastFrame = false;
                var stopwatch = Stopwatch.StartNew();
                while (running)
                {
                    stopwatch.Restart();
                    var memoryStart = Stopwatch.StartNew();
                    bool isMemoryReady = Memory.Ready;
                    if (!isMemoryReady)
                    {
                        renderTarget.BeginDraw();
                        renderTarget.Clear(new RawColor4(0, 0, 0, 1));
                        renderTarget.EndDraw();
                        this.Invalidate();
                        Thread.Sleep(wasReadyLastFrame ? 500 : 1500);
                        wasReadyLastFrame = false;
                        continue;
                    }

                    RenderFrame();
                    int elapsedMs = (int)stopwatch.ElapsedMilliseconds;
                    int targetMs = 8;
                    int sleepMs = Math.Max(1, targetMs - elapsedMs);
                    Thread.Sleep(sleepMs);
                    wasReadyLastFrame = true;
                }
                Program.Log("Render thread stopped.");
            });
            renderThread.Priority = ThreadPriority.AboveNormal;
            renderThread.Start();
        }

        private bool IsReadyToRender()
        {
            bool inGame = Game?.InGame ?? false;
            bool localPlayerExists = Game?.LocalPlayer != null;
            bool actorsExist = Game?.Actors != null && Game.Actors.Count > 0;
            return inGame && localPlayerExists && actorsExist;
        }

        private void RenderFrame()
        {
            var frameStart = Stopwatch.StartNew();
            renderTarget.BeginDraw();
            renderTarget.Clear(new RawColor4(0, 0, 0, 0));

            Dictionary<ulong, UActor> actorsCopy;
            if (!IsReadyToRender())
            {
                renderTarget.EndDraw();
                this.Invalidate();
                return;
            }

            var copyStart = Stopwatch.StartNew();
            actorsCopy = new Dictionary<ulong, UActor>(Game.Actors);

            DrawEsp(actorsCopy);
            renderTarget.EndDraw();
        }

        private RawRectangleF CalculatePlayerBox(UActor actor, Vector2 screenPos, MinimalViewInfo viewInfo)
        {
            const float playerHeight = 175f;
            const float halfHeight = playerHeight / 2f;
            const float aspectRatio = 0.4f;

            Vector3D middlePos = actor.Position;
            Vector3D feetPos = new Vector3D(middlePos.X, middlePos.Y, middlePos.Z - halfHeight);
            Vector3D headPos = new Vector3D(middlePos.X, middlePos.Y, middlePos.Z + halfHeight);

            Vector2 feetScreen = Camera.WorldToScreen(viewInfo, feetPos);
            Vector2 headScreen = Camera.WorldToScreen(viewInfo, headPos);

            if (feetScreen == Vector2.Zero || headScreen == Vector2.Zero)
                return new RawRectangleF(0, 0, 0, 0);

            float topY = Math.Min(headScreen.Y, feetScreen.Y);
            float bottomY = Math.Max(headScreen.Y, feetScreen.Y);
            float height = bottomY - topY;
            if (height <= 0)
                return new RawRectangleF(0, 0, 0, 0);

            float width = height * aspectRatio;
            float leftX = screenPos.X - width / 2f;
            float rightX = screenPos.X + width / 2f;

            const float padding = 6f;
            return new RawRectangleF(leftX - padding, topY - padding, rightX + padding, bottomY + padding);
        }

        private (float width, float height) GetTextMetrics(string text)
        {
            using (var textLayout = new TextLayout(new SharpDX.DirectWrite.Factory(), text, textFormat, 1000f, 100f))
            {
                return (textLayout.Metrics.Width, textLayout.Metrics.Height);
            }
        }

        private void DrawEsp(Dictionary<ulong, UActor> actors)
        {
            var espStart = Stopwatch.StartNew();
            if (Game == null || Game.LocalPlayer == null || actors == null || actors.Count < 1)
            {
                Program.Log("DrawEsp: Game, LocalPlayer, or actors not initialized.");
                return;
            }

            var viewInfo = new MinimalViewInfo
            {
                Location = Game.LocalPlayer.Position,
                Rotation = Game.LocalPlayer.Rotation3D,
                FOV = Game.CurrentFOV
            };

            Vector3D camPos = viewInfo.Location;
            float maxDistance = Program.Config.EspMaxDistance;
            float vehicleMaxDistance = Program.Config.EspVehicleMaxDistance;
            bool showAllies = Program.Config.EspShowAllies;
            bool showVehicles = Program.Config.EspShowVehicles;
            var playerColor = brush.Color;

            var visibleActors = new List<(UActor actor, Vector2 screenPos, float distance)>();

            long totalWtsTime = 0;
            int wtsCalls = 0;
            foreach (var actor in actors.Values)
            {
                if (actor == null || actor.Position == Vector3D.Zero)
                    continue;

                if (actor.ActorType == ActorType.Player && !actor.IsAlive)
                    continue;

                Vector3 camPosVec = camPos.ToVector3();
                Vector3 actorPosVec = actor.Position.ToVector3();
                float distance = Vector3.Distance(camPosVec, actorPosVec) / 100f;
                bool isPlayer = actor.ActorType == ActorType.Player;
                if (distance > (isPlayer ? maxDistance : vehicleMaxDistance))
                    continue;

                Vector3 localPlayerPosVec = Memory.LocalPlayer.Position.ToVector3();
                Vector3 actorPosVec2 = actor.Position.ToVector3();
                if (isPlayer && Vector3.Distance(localPlayerPosVec, actorPosVec2) < 1.0f)
                    continue;

                if (!showAllies && actor.IsFriendly())
                    continue;

                // Skip vehicles if not showing them
                if (!isPlayer && !showVehicles)
                    continue;

                var wtsStart = Stopwatch.StartNew();
                Vector2 screenPos = Camera.WorldToScreen(viewInfo, actor.Position);
                totalWtsTime += wtsStart.ElapsedMilliseconds;
                wtsCalls++;
                if (screenPos == Vector2.Zero)
                    continue;

                visibleActors.Add((actor, screenPos, distance));
            }

            foreach (var (actor, screenPos, distance) in visibleActors)
            {
                if (actor.ActorType == ActorType.Player)
                {
                    RawRectangleF boxRect = CalculatePlayerBox(actor, screenPos, viewInfo);
                    if ((boxRect.Left == 0 && boxRect.Top == 0 && boxRect.Right == 0 && boxRect.Bottom == 0) &&
                        Program.Config.EspBones && actor.BoneScreenPositions != null)
                    {
                        boxRect = GetBoxFromBones(actor.BoneScreenPositions);
                    }

                    if (boxRect.Left == 0 && boxRect.Top == 0 && boxRect.Right == 0 && boxRect.Bottom == 0)
                        continue;

                    if (Program.Config.EspShowBox)
                    {
                        var boxBrush = actor.IsFriendly() ? friendlyBrush : boneBrush;
                        renderTarget.DrawRectangle(boxRect, boxBrush);
                    }

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
                        var textBrush = actor.IsFriendly() ? friendlyBrush : brush;
                        renderTarget.DrawText(nameText, textFormat, nameRect, textBrush);
                    }

                    if (Program.Config.EspShowDistance)
                    {
                        string distanceText = $"[{(int)distance}m]";
                        var (textWidth, textHeight) = GetTextMetrics(distanceText);
                        float rectWidth = Math.Max(boxWidth, textWidth);
                        RawRectangleF distanceRect = new RawRectangleF(
                            boxCenterX - rectWidth / 2f, boxRect.Bottom + 5f,
                            boxCenterX + rectWidth / 2f, boxRect.Bottom + textHeight + 5f
                        );
                        var distBrush = actor.IsFriendly() ? friendlyBrush : brush;
                        renderTarget.DrawText(distanceText, textFormat, distanceRect, distBrush);
                    }

                    if (Program.Config.EspShowHealth && actor.Health >= 0)
                        DrawHealthBar(boxRect, actor.Health);

                    if (Program.Config.EspBones && actor.BoneScreenPositions != null)
                        DrawBoneLines(actor, actor.BoneScreenPositions);
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
            string wdistance = Program.Config.EspShowDistance ? $"[{(int)distance}m]" : "";
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

            // Better color differentiation for vehicles
            vehicleBrush.Color = isUnclaimed ? new RawColor4(1.0f, 1.0f, 0.0f, 1.0f) :  // Yellow for unclaimed
                                isEnemy ? new RawColor4(1.0f, 1.0f, 0.0f, 1.0f) :        // Yellow for enemy
                                new RawColor4(0.4f, 0.7f, 1.0f, 1.0f);                   // Light blue for friendly

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

        void DrawBoneLines(UActor actor, Vector2[] screenPositions)
        {
            if (screenPositions == null || screenPositions.Length == 0)
                return;

            int[] boneIds = { 11, 9, 8, 7, 5, 14, 15, 16, 17, 38, 39, 40, 41, 68, 63, 64, 66, 69, 70, 71, 73 };

            var boneConnections = new List<(int, int)>
            {
                (0, 2), (2, 3), (3, 4), (2, 5), (5, 6), (6, 7), (7, 8),
                (2, 9), (9, 10), (10, 11), (11, 12), (4, 13), (13, 14),
                (14, 15), (15, 16), (4, 17), (17, 18), (18, 19), (19, 20),
            };

            foreach (var (startIndex, endIndex) in boneConnections)
            {
                if (startIndex < 0 || startIndex >= screenPositions.Length ||
                    endIndex < 0 || endIndex >= screenPositions.Length ||
                    screenPositions[startIndex] == Vector2.Zero || screenPositions[endIndex] == Vector2.Zero)
                    continue;

                var lineBrush = actor.IsFriendly() ? friendlyBrush : boneBrush;
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
            renderTarget.DrawRectangle(new RawRectangleF(barX, boxRect.Top, barX + barWidth, boxRect.Bottom), boneBrush);
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
            brush.Dispose();
            vehicleBrush.Dispose();
            boneBrush.Dispose();
            healthBrush.Dispose();
            friendlyBrush.Dispose();
            textFormat.Dispose();
            renderTarget.Dispose();
            base.OnClosed(e);
        }
    }
}