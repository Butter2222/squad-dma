using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Diagnostics;
using System.Numerics;

namespace squad_dma
{
    /// <summary>
    /// Second-screen mini view of the game. It mirrors what the player is looking through: the main camera,
    /// or the scope image (circular, with the scope's own FOV) while aimed through a PIP optic.
    /// Tuned for a quick glance: bright enemy colors, a highlighted target nearest the crosshair,
    /// labels only where they help, and nothing faded out.
    /// </summary>
    public sealed class AimviewWidget : SKWidget
    {
        private readonly MainForm _parentForm;
        private SKBitmap _espBitmap;
        private SKCanvas _espCanvas;

        private static readonly Dictionary<ActorType, string> ActorTypeNames = new Dictionary<ActorType, string>
        {
            { ActorType.AntiAir, "Anti-Air" },
            { ActorType.APC, "APC" },
            { ActorType.AttackHelicopter, "Attack Helicopter" },
            { ActorType.LoachCAS, "Loach CAS" },
            { ActorType.LoachScout, "Loach Scout" },
            { ActorType.Boat, "Boat" },
            { ActorType.BoatLogistics, "Boat Logistics" },
            { ActorType.IFV, "IFV" },
            { ActorType.JeepAntiAir, "Jeep Anti-Air" },
            { ActorType.JeepAntitank, "Jeep Antitank" },
            { ActorType.JeepArtillery, "Jeep Artillery" },
            { ActorType.JeepLogistics, "Jeep Logistics" },
            { ActorType.JeepTransport, "Jeep Transport" },
            { ActorType.JeepRWSTurret, "Jeep RWS Turret" },
            { ActorType.JeepTurret, "Jeep Turret" },
            { ActorType.Motorcycle, "Motorcycle" },
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

        private static readonly HashSet<ActorType> VehicleTypes = new HashSet<ActorType>(ActorTypeNames.Keys);

        // Team colors follow the Colors tab
        private static SKColor EnemyColor => SKPaints.EnemyPlayer;
        private static SKColor EnemyVehicleColor => SKPaints.EnemyVehicle;
        private static readonly SKColor FrameColor = new(255, 255, 255, 70);
        private static readonly SKColor PanelTint = new(8, 8, 12, 150);

        private const int MaxLabelledEnemies = 8;
        private const float EdgeArrowLimit = 4f; // Enemies farther than this many view-widths off-center get no arrow

        public AimviewWidget(SKGLControl parent, MainForm parentForm, SKRect location, bool minimized, float scale)
            : base(parent, "", new SKPoint(location.Left, location.Top), new SKSize(location.Width, location.Height), scale)
        {
            _parentForm = parentForm;
            _espBitmap = new SKBitmap((int)location.Width, (int)location.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            _espCanvas = new SKCanvas(_espBitmap);
            Minimized = minimized;
        }

        public override void Draw(SKCanvas canvas)
        {
            // The window fades with Widget Opacity (handled by the base class); targets are drawn on top at full strength.
            base.Draw(canvas);

            if (!Minimized)
            {
                // The view fills the whole body, so it is clipped to the frame's rounded corners and the border goes on last.
                canvas.Save();
                canvas.ClipRoundRect(ClientShape, antialias: true);
                RenderAimviewWidget(canvas, ClientRectangle);
                canvas.Restore();
            }
            DrawBorder(canvas);
        }

        protected override bool DrawsOwnBorder => true;

        private readonly List<AimviewEntry> _players = new(32);
        private readonly List<AimviewEntry> _vehicles = new(16);
        private readonly List<float> _aimDistances = new(32);

        // Box or marker per target, decided from its size in the panel
        private readonly TargetLod _lod = new();
        private readonly SKPaint _haloPaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 4f, IsAntialias = true, Color = new SKColor(0, 0, 0, 200) };

        private readonly SKPaint _boxPaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
        private readonly SKPaint _fillPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
        private readonly SKPaint _labelPaint = new() { TextSize = 12f, IsAntialias = true, Typeface = CustomFonts.SKFontFamilyRegular };
        private readonly SKPaint _labelOutlinePaint = new()
        {
            TextSize = 12f,
            IsAntialias = true,
            Typeface = CustomFonts.SKFontFamilyRegular,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            Color = new SKColor(0, 0, 0, 220),
        };
        private readonly SKPaint _healthPaint = new() { Style = SKPaintStyle.Fill };
        private readonly SKPaint _framePaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true, Color = FrameColor };
        private readonly SKPaint _crosshairPaint = new()
        {
            Color = new SKColor(255, 255, 255, 230),
            StrokeWidth = 1.5f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        private struct AimviewEntry
        {
            public ActorFrameData Data;
            public SKRect Rect;        // panel pixels
            public float Distance;     // meters
            public float AimDistance;  // panel pixels from the crosshair to the box center
            public bool AsBox;         // big enough to read, or under the crosshair; otherwise drawn as a marker
            public int CornerSlot;     // vehicles: index of its 8 projected box corners in _screenCorners
        }

        // Vehicle boxes are drawn as 3D wireframes: 8 projected corners per vehicle, 12 edges
        private readonly Vector2[] _ndcCorners = new Vector2[8];
        private SKPoint[] _screenCorners = new SKPoint[16 * 8];
        private readonly SKPoint[] _edgePoints = new SKPoint[24];

        // Per-frame mapping from NDC to panel pixels
        private bool _optic;
        private float _viewScale, _offsetX, _offsetY, _viewW, _viewH;
        private float _opticCenterX, _opticCenterY, _opticHalf;

        private SKPoint MapNdc(float ndcX, float ndcY)
        {
            if (_optic)
                return new SKPoint(_opticCenterX + ndcX * _opticHalf, _opticCenterY - ndcY * _opticHalf);

            return new SKPoint(
                _offsetX + (0.5f + ndcX * 0.5f) * _viewW * _viewScale,
                _offsetY + (0.5f - ndcY * 0.5f) * _viewH * _viewScale);
        }

        private SKRect MapRect(in NdcRect r)
        {
            var topLeft = MapNdc(r.MinX, r.MaxY);
            var bottomRight = MapNdc(r.MaxX, r.MinY);
            return new SKRect(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        }

        private void RenderAimviewWidget(SKCanvas parent, SKRect dest)
        {
            FitPanelToGameAspect();

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

            // The dark tint behind the targets is part of the window background, so it follows the opacity setting
            _espCanvas.Clear(PanelTint.WithAlpha((byte)(PanelTint.Alpha * WindowOpacity)));
            try
            {
                var frame = _parentForm.FrameSnapshot;
                var localPlayer = frame.LocalPlayer;
                if (localPlayer != null && Camera.ViewportRect.X > 0f && Camera.ViewportRect.Y > 0f)
                {
                    SetupMapping();
                    CollectEntries(frame, localPlayer);

                    _espCanvas.Save();
                    if (_optic)
                    {
                        using var clip = new SKPath();
                        clip.AddCircle(_opticCenterX, _opticCenterY, _opticHalf);
                        _espCanvas.ClipPath(clip, antialias: true);
                    }

                    DrawTargets();
                    _espCanvas.Restore();

                    DrawFrame();
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

        /// <summary>
        /// Main view: the game screen scaled uniformly (letterboxed) so it is never stretched.
        /// Optic view: the scope image is square, so it takes the largest centered circle that fits.
        /// </summary>
        private void SetupMapping()
        {
            float panelW = _espBitmap.Width, panelH = _espBitmap.Height;
            _optic = Camera.OpticActive;

            if (_optic)
            {
                _opticHalf = Math.Min(panelW, panelH) * 0.5f - 2f;
                _opticCenterX = panelW * 0.5f;
                _opticCenterY = panelH * 0.5f;
                return;
            }

            _viewW = Camera.ViewportRect.X;
            _viewH = Camera.ViewportRect.Y;
            _viewScale = Math.Min(panelW / _viewW, panelH / _viewH);
            _offsetX = (panelW - _viewW * _viewScale) * 0.5f;
            _offsetY = (panelH - _viewH * _viewScale) * 0.5f;
        }

        private void CollectEntries(ActorFrameSnapshot frame, UActor localPlayer)
        {
            Vector3 localPos = localPlayer.Position.ToVector3();
            float maxPlayerDistance = Program.Config.EspMaxDistance;
            float maxVehicleDistance = Program.Config.EspVehicleMaxDistance;
            bool showAllies = Program.Config.EspShowAllies;
            bool showVehicles = Program.Config.EspShowVehicles;
            float centerX = _espBitmap.Width * 0.5f, centerY = _espBitmap.Height * 0.5f;

            // Thresholds follow the panel size, so the look stays the same however big the panel is
            float boxEnter = _espBitmap.Height * 0.045f, boxExit = _espBitmap.Height * 0.03f;
            const float aimPad = 8f;
            _lod.BeginFrame();

            _players.Clear();
            _vehicles.Clear();
            _edgeArrows.Clear();

            var items = frame.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var data = items[i];
                if (data.Actor == localPlayer || data.Position == Vector3D.Zero)
                    continue;

                bool isPlayer = data.Type == ActorType.Player;
                if (isPlayer ? !data.IsAlive : !VehicleTypes.Contains(data.Type))
                    continue;

                if (!isPlayer && (!showVehicles || data.TeamID == -1))
                    continue;

                if (!showAllies && data.IsFriendly)
                    continue;

                float distance = Vector3.Distance(localPos, data.Position.ToVector3()) / 100f;
                if (distance > (isPlayer ? maxPlayerDistance : maxVehicleDistance))
                    continue;

                NdcRect ndcRect = default;
                bool projected;
                if (isPlayer)
                {
                    projected = EspGeometry.TryPlayerRect(data.Actor, data.Position, _optic, out ndcRect);
                }
                else
                {
                    projected = EspGeometry.TryVehicleCorners(data.Actor, data.Position, _optic, _ndcCorners);
                    if (projected)
                    {
                        ndcRect = new NdcRect { MinX = float.MaxValue, MinY = float.MaxValue, MaxX = float.MinValue, MaxY = float.MinValue };
                        for (int c = 0; c < 8; c++)
                        {
                            var p = _ndcCorners[c];
                            if (p.X < ndcRect.MinX) ndcRect.MinX = p.X;
                            if (p.X > ndcRect.MaxX) ndcRect.MaxX = p.X;
                            if (p.Y < ndcRect.MinY) ndcRect.MinY = p.Y;
                            if (p.Y > ndcRect.MaxY) ndcRect.MaxY = p.Y;
                        }
                    }
                }

                if (!projected)
                {
                    if (isPlayer && !data.IsFriendly)
                        TryAddEdgeArrow(data);
                    continue;
                }

                // Fully outside the visible area (main view: the screen; optic: the lens)
                if (ndcRect.MaxX < -1f || ndcRect.MinX > 1f || ndcRect.MaxY < -1f || ndcRect.MinY > 1f)
                {
                    if (isPlayer && !data.IsFriendly)
                        TryAddEdgeArrow(data);
                    continue;
                }

                var rect = MapRect(ndcRect);
                if (float.IsNaN(rect.Left) || float.IsNaN(rect.Top) || float.IsInfinity(rect.Width) || float.IsInfinity(rect.Height))
                    continue;

                float dx = rect.MidX - centerX, dy = rect.MidY - centerY;
                bool aimed = TargetLod.IsAimedAt(rect.Left, rect.Top, rect.Right, rect.Bottom, centerX, centerY, aimPad);
                var entry = new AimviewEntry
                {
                    Data = data,
                    Rect = rect,
                    Distance = distance,
                    AimDistance = MathF.Sqrt(dx * dx + dy * dy),
                    AsBox = _lod.UseBox(data.Actor.Base, isPlayer ? rect.Height : Math.Max(rect.Width, rect.Height), boxEnter, boxExit, aimed),
                };

                if (!isPlayer)
                {
                    // Corners are stored by vehicle slot; the sort below reorders entries, so the slot travels with the entry
                    entry.CornerSlot = _vehicles.Count;
                    int baseIndex = entry.CornerSlot * 8;
                    if (_screenCorners.Length < baseIndex + 8)
                        Array.Resize(ref _screenCorners, Math.Max(baseIndex + 8, _screenCorners.Length * 2));
                    for (int c = 0; c < 8; c++)
                        _screenCorners[baseIndex + c] = MapNdc(_ndcCorners[c].X, _ndcCorners[c].Y);
                }
                (isPlayer ? _players : _vehicles).Add(entry);
            }

            // Far to near: close targets are drawn last so they are never hidden behind distant ones.
            SortFarToNear(_players);
            SortFarToNear(_vehicles);

            // Labels are limited to the enemies closest to the crosshair
            _aimDistances.Clear();
            for (int i = 0; i < _players.Count; i++)
                if (!_players[i].Data.IsFriendly)
                    _aimDistances.Add(_players[i].AimDistance);
            _aimDistances.Sort();
            _labelAimLimit = _aimDistances.Count == 0
                ? 0f
                : _aimDistances[Math.Min(MaxLabelledEnemies, _aimDistances.Count) - 1];
        }

        private float _labelAimLimit;

        private readonly List<(SKPoint point, SKPoint direction)> _edgeArrows = new(8);

        // Enemies just outside the main view get a small arrow on the matching panel edge
        private void TryAddEdgeArrow(in ActorFrameData data)
        {
            if (_optic || !Camera.TryProject(data.Position, false, out var ndc, out _))
                return;

            float ax = Math.Abs(ndc.X), ay = Math.Abs(ndc.Y);
            if ((ax <= 1f && ay <= 1f) || ax > EdgeArrowLimit || ay > EdgeArrowLimit)
                return;

            float k = Math.Max(ax, ay);
            var edge = MapNdc(ndc.X / k * 0.97f, ndc.Y / k * 0.97f);
            var dir = new SKPoint(ndc.X / k, -ndc.Y / k);
            _edgeArrows.Add((edge, dir));
        }

        private static void SortFarToNear(List<AimviewEntry> list)
        {
            for (int i = 1; i < list.Count; i++)
            {
                var key = list[i];
                int j = i - 1;
                while (j >= 0 && list[j].Distance < key.Distance)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = key;
            }
        }

        private void DrawTargets()
        {
            bool showBox = Program.Config.EspShowBox;

            for (int i = 0; i < _vehicles.Count; i++)
                DrawVehicle(_vehicles[i], showBox);

            for (int i = 0; i < _players.Count; i++)
                DrawPlayer(_players[i], showBox);
        }


        private void DrawPlayer(in AimviewEntry e, bool showBox)
        {
            var data = e.Data;
            bool enemy = !data.IsFriendly;
            var color = enemy ? EnemyColor : data.IsInMySquad ? SKPaints.Squad : SKPaints.Friendly;
            if (!enemy)
                color = color.WithAlpha(200);

            // Box is always drawn (within the max distance); the "+" marker for small targets is disabled
            SKRect rect = e.Rect;
            if (showBox)
            {
                rect = EnsureMinimumSize(e.Rect, 10f, 16f);
                _boxPaint.Color = color;
                _boxPaint.StrokeWidth = enemy ? 2f : 1.5f;
                _espCanvas.DrawRect(rect, _boxPaint);
                DrawHealthBar(data.Health, rect);
            }
            //else
            //{
            //    rect = DrawMarker(e.Rect.MidX, e.Rect.MidY, color, MarkerArm);
            //}

            if (!enemy || e.AimDistance > _labelAimLimit)
                return;

            string name = Program.Config.EspShowNames && !string.IsNullOrEmpty(data.Actor.Name) ? data.Actor.Name : null;
            string dist = Program.Config.EspShowDistance ? DistanceLabels.ForMeters(RoundedMeters(e.Distance)) : null;
            string text = name != null && dist != null ? string.Concat(name, " ", dist) : name ?? dist;
            if (text != null)
                DrawLabel(text, rect, color);
        }

        private void DrawVehicle(in AimviewEntry e, bool showBox)
        {
            var data = e.Data;
            var color = data.IsFriendly ? SKPaints.FriendlyVehicle : EnemyVehicleColor;

            SKRect rect = e.Rect;
            if (showBox)
            {
                rect = EnsureMinimumSize(e.Rect, 16f, 10f);
                DrawBox3D(e.CornerSlot * 8, color, data.IsFriendly ? 1.5f : 2.5f);
            }
            //else
            //{
            //    rect = DrawMarker(e.Rect.MidX, e.Rect.MidY, color, MarkerArm);
            //}

            // Names are omitted in the aimview; only the distance is shown
            if (Program.Config.EspShowDistance)
                DrawLabel(DistanceLabels.ForMeters(RoundedMeters(e.Distance)), rect, color);
        }

        // Twelve edges of the oriented box: every pair of corners whose indices differ by exactly one bit
        private void DrawBox3D(int cornerStart, SKColor color, float width)
        {
            int n = 0;
            for (int i = 0; i < 8; i++)
            {
                for (int bit = 1; bit <= 4; bit <<= 1)
                {
                    if ((i & bit) != 0) continue;
                    _edgePoints[n++] = _screenCorners[cornerStart + i];
                    _edgePoints[n++] = _screenCorners[cornerStart + (i | bit)];
                }
            }

            // Dark outline first so the edges stay readable over bright scenery
            _haloPaint.StrokeWidth = width + 2f;
            _espCanvas.DrawPoints(SKPointMode.Lines, _edgePoints, _haloPaint);
            _haloPaint.StrokeWidth = 4f;

            _boxPaint.Color = color;
            _boxPaint.StrokeWidth = width;
            _espCanvas.DrawPoints(SKPointMode.Lines, _edgePoints, _boxPaint);
        }

        // A target promoted to a box by the crosshair can still be tiny; keep it large enough to see
        private static SKRect EnsureMinimumSize(SKRect rect, float minWidth, float minHeight)
        {
            float width = Math.Max(rect.Width, minWidth), height = Math.Max(rect.Height, minHeight);
            return new SKRect(rect.MidX - width * 0.5f, rect.MidY - height * 0.5f, rect.MidX + width * 0.5f, rect.MidY + height * 0.5f);
        }

        private float MarkerArm => Math.Clamp(_espBitmap.Height * 0.0128f, 3.2f, 6.4f);

        /// <summary>
        /// Small solid "+" with a dark outline, drawn in place of a box for targets too small to box.
        /// Returns the square around it, which labels are placed against.
        /// </summary>
        private SKRect DrawMarker(float cx, float cy, SKColor color, float arm)
        {
            _boxPaint.Color = color;
            _boxPaint.StrokeWidth = 2f;

            DrawMarkerLines(cx, cy, arm + 1f, _haloPaint);
            DrawMarkerLines(cx, cy, arm, _boxPaint);

            return new SKRect(cx - arm, cy - arm, cx + arm, cy + arm);
        }

        private void DrawMarkerLines(float cx, float cy, float arm, SKPaint paint)
        {
            _espCanvas.DrawLine(cx - arm, cy, cx + arm, cy, paint);
            _espCanvas.DrawLine(cx, cy - arm, cx, cy + arm, paint);
        }

        private void DrawLabel(string text, SKRect rect, SKColor color)
        {
            float x = rect.MidX - _labelPaint.MeasureText(text) * 0.5f;
            float y = rect.Bottom + _labelPaint.TextSize + 2f;

            _espCanvas.DrawText(text, x, y, _labelOutlinePaint);
            _labelPaint.Color = color.WithAlpha(255);
            _espCanvas.DrawText(text, x, y, _labelPaint);
        }

        // Vertical bar on the left edge of the box
        private void DrawHealthBar(float health, SKRect rect)
        {
            if (!Program.Config.EspShowHealth || health <= 0)
                return;

            float percent = Math.Clamp(health / 100f, 0f, 1f);
            float barLeft = rect.Left - 5f;
            float filledTop = rect.Bottom - rect.Height * percent;

            _healthPaint.Color = new SKColor(0, 0, 0, 160);
            _espCanvas.DrawRect(new SKRect(barLeft - 1f, rect.Top - 1f, barLeft + 3f, rect.Bottom + 1f), _healthPaint);
            _healthPaint.Color = new SKColor((byte)(255 * (1f - percent)), (byte)(255 * percent), 0);
            _espCanvas.DrawRect(new SKRect(barLeft, filledTop, barLeft + 2f, rect.Bottom), _healthPaint);
        }

        // Matches the old "{distance:F0}" formatting (round half away from zero)
        private static int RoundedMeters(float distance) => (int)MathF.Round(distance, MidpointRounding.AwayFromZero);

        private void DrawFrame()
        {
            if (_optic)
            {
                _espCanvas.DrawCircle(_opticCenterX, _opticCenterY, _opticHalf, _framePaint);

                float mainHalfTan = MathF.Tan(float.DegreesToRadians(Camera.MainFOV * 0.5f));
                float opticHalfTan = MathF.Tan(float.DegreesToRadians(Camera.OpticFOV * 0.5f));
                string zoom = $"SCOPE {mainHalfTan / opticHalfTan:F1}x";
                _labelPaint.Color = SKColors.White;
                _espCanvas.DrawText(zoom, 6f, _labelPaint.TextSize + 4f, _labelOutlinePaint);
                _espCanvas.DrawText(zoom, 6f, _labelPaint.TextSize + 4f, _labelPaint);
                return;
            }

            _espCanvas.DrawRect(_offsetX, _offsetY, _viewW * _viewScale, _viewH * _viewScale, _framePaint);

            for (int i = 0; i < _edgeArrows.Count; i++)
                DrawEdgeArrow(_edgeArrows[i].point, _edgeArrows[i].direction);
            _edgeArrows.Clear();
        }

        private void DrawEdgeArrow(SKPoint tip, SKPoint dir)
        {
            float len = MathF.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
            if (len < 0.001f) return;
            dir = new SKPoint(dir.X / len, dir.Y / len);
            var perp = new SKPoint(-dir.Y, dir.X);

            using var path = new SKPath();
            path.MoveTo(tip.X + dir.X * 4f, tip.Y + dir.Y * 4f);
            path.LineTo(tip.X - dir.X * 6f + perp.X * 5f, tip.Y - dir.Y * 6f + perp.Y * 5f);
            path.LineTo(tip.X - dir.X * 6f - perp.X * 5f, tip.Y - dir.Y * 6f - perp.Y * 5f);
            path.Close();

            _fillPaint.Color = EnemyColor;
            _espCanvas.DrawPath(path, _fillPaint);
        }

        // A plain "+" at the center of the view
        private void DrawCrosshair()
        {
            float cx = _optic ? _opticCenterX : _offsetX + _viewW * _viewScale * 0.5f;
            float cy = _optic ? _opticCenterY : _offsetY + _viewH * _viewScale * 0.5f;
            const float arm = 11.4f;

            _espCanvas.DrawLine(cx - arm, cy, cx + arm, cy, _crosshairPaint);
            _espCanvas.DrawLine(cx, cy - arm, cx, cy + arm, _crosshairPaint);
        }

        /// <summary>
        /// Keeps the panel the same shape as the game screen so the aimview is a true small copy of it.
        /// The width is the user's choice; the height follows. Skipped while the round scope view is shown.
        /// </summary>
        private void FitPanelToGameAspect()
        {
            if (Camera.OpticActive || Camera.ViewportRect.X <= 0f || Camera.ViewportRect.Y <= 0f)
                return;

            var size = Size;
            float targetHeight = MathF.Round(size.Width * Camera.ViewportRect.Y / Camera.ViewportRect.X);
            if (targetHeight < 60f || Math.Abs(targetHeight - size.Height) < 2f)
                return;

            Size = new SKSize(size.Width, targetHeight);
        }

        public override void SetScaleFactor(float newScale)
        {
            base.SetScaleFactor(newScale);
            _labelPaint.TextSize = 12f * newScale;
            _labelOutlinePaint.TextSize = 12f * newScale;
            _crosshairPaint.StrokeWidth = 1.5f * newScale;
        }

        public override void Dispose()
        {
            _espBitmap?.Dispose();
            _espCanvas?.Dispose();
            _haloPaint.Dispose();
            _boxPaint.Dispose();
            _fillPaint.Dispose();
            _labelPaint.Dispose();
            _labelOutlinePaint.Dispose();
            _healthPaint.Dispose();
            _framePaint.Dispose();
            _crosshairPaint.Dispose();
            base.Dispose();
        }
    }
}
