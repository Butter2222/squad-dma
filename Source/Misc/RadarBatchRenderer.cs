using SkiaSharp;
using System.Runtime.InteropServices;

namespace squad_dma
{
    /// <summary>
    /// Batched radar renderer. Owns every paint / path / scratch buffer used while drawing actors so that
    /// nothing is allocated or reconfigured per entity. Entities are bucketed by type in a single pass and
    /// each bucket is flushed with a fixed paint.
    /// </summary>
    internal sealed class RadarBatchRenderer : IDisposable
    {
        private const double LabelMinDistanceUnits = 50 * 100; // labels only past 50m (game units are cm)
        private const double LabelMinDistanceUnitsSq = LabelMinDistanceUnits * LabelMinDistanceUnits;
        private const float PlayerAimlineLength = 15f;
        private const float AlertAimlineLength = 9999f;

        // Player marker: a chevron pointing where the player is looking (sizes are in UI-scale units, facing is +x).
        // The aimline starts at the tip, so the local player gets a long one and high-alert enemies a map-wide one.
        private const float ChevronTip = 7.5f;    // centre -> tip
        private const float ChevronBack = 5.0f;   // centre -> back edge of the wings
        private const float ChevronWing = 5.8f;   // half width across the wings
        private const float ChevronNotch = 3.2f;  // depth of the notch cut into the back

        // Vehicle/deployable icons: constant on-screen size (~2x the chevron length), independent of map zoom, scaled by UI scale and the Tech Marker Scale slider.
        private const float IconBaseSize = (ChevronTip + ChevronBack) * 2f;

        private float _scale = float.NaN;

        #region Paints (created once, reconfigured only when UI scale changes)
        // Chevron fill and outline, plus the aimline that continues from its tip.
        private readonly SKPaint _bodyOutline = new()
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };
        private readonly SKPaint _bodyFill = new() { Style = SKPaintStyle.Fill, IsAntialias = true, FilterQuality = SKFilterQuality.High };
        private readonly SKPaint _aimOutline = new() { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, IsAntialias = true };
        private readonly SKPaint _aimFill = new() { Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, IsAntialias = true };

        private readonly SKPaint _projectileOutline = SKPaints.ProjectileOutline.Clone();
        private readonly SKPaint _projectileFill = SKPaints.ProjectileFill.Clone();

        private readonly SKPaint _labelFill = SKPaints.TextBase.Clone();
        private readonly SKPaint _labelOutline = SKPaints.TextOutline.Clone();

        private readonly SKPaint _aaFill = SKPaints.ProjectileAA.Clone();
        private readonly SKPaint _aaOutline = SKPaints.TextOutline.Clone();

        private static readonly SKTypeface _arial = SKTypeface.FromFamilyName("Arial");
        private readonly SKPaint _adminFill = new() { IsAntialias = true, Typeface = _arial };
        private readonly SKPaint _adminOutline = new() { Color = SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Stroke, Typeface = _arial };
        private float _adminHalfWidth;
        private float _adminHalfHeight;

        private readonly SKPaint _deadOutline = new() { Color = SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
        private readonly SKPaint _deadFill = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
        private readonly SKPaint _impactOutline = new() { Color = SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
        private readonly SKPaint _impactFill = new() { Color = new SKColor(80, 255, 120), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };

        private readonly SKPaint _aaMarkOutline = new() { Color = SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
        private readonly SKPaint _aaMarkFill = new() { Color = SKColors.Yellow, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
        private readonly SKPaint _aaMarkTextOutline = new() { Color = SKColors.Black, IsAntialias = true, TextAlign = SKTextAlign.Left, Style = SKPaintStyle.Stroke, Typeface = _arial };
        private readonly SKPaint _aaMarkText = new() { Color = SKColors.Yellow, IsAntialias = true, TextAlign = SKTextAlign.Left, Typeface = _arial };

        // Tech (vehicle / deployable) icon paints: neutral, friendly-tinted, enemy-tinted
        private readonly SKPaint[] _techPaints = new SKPaint[4];
        private bool _techPaintsDirty = true;

        // Overlay paints shared with MainForm POI / range drawing (previously `using var new SKPaint` per call)
        public SKPaint RangeCirclePaint { get; } = new() { Color = SKColors.Black, Style = SKPaintStyle.Stroke, IsAntialias = true };
        public SKPaint PoiCrosshairPaint { get; } = new() { Color = SKColors.Red, StrokeWidth = 1.5f, IsAntialias = true, Style = SKPaintStyle.Stroke };
        public SKPaint SpreadFillPaint { get; } = new() { Color = new SKColor(255, 0, 0, 60), Style = SKPaintStyle.Fill, IsAntialias = true };
        public SKPaint SpreadStrokePaint { get; } = new() { Color = new SKColor(255, 0, 0, 180), Style = SKPaintStyle.Stroke, IsAntialias = true };
        public SKPaint MinDistanceFillPaint { get; } = new() { Color = new SKColor(64, 64, 64, 77), Style = SKPaintStyle.Fill, IsAntialias = true };
        public SKPaint MinDistanceStrokePaint { get; } = new() { Color = new SKColor(0, 0, 0, 255), Style = SKPaintStyle.Stroke, IsAntialias = true };
        public SKPaint PoiNumberOutline { get; } = new()
        {
            Color = SKColors.Black, TextAlign = SKTextAlign.Center, Typeface = CustomFonts.SKFontFamilyRegular,
            IsAntialias = true, SubpixelText = true, FilterQuality = SKFilterQuality.High, Style = SKPaintStyle.Stroke
        };
        public SKPaint PoiNumberFill { get; } = new()
        {
            Color = SKColors.White, TextAlign = SKTextAlign.Center, Typeface = CustomFonts.SKFontFamilyRegular,
            IsAntialias = true, SubpixelText = true, FilterQuality = SKFilterQuality.High, Style = SKPaintStyle.Fill
        };
        /// <summary>Vertical offset that centres POI digits; depends only on the (cached) font size.</summary>
        public float PoiNumberBaselineOffset { get; private set; }
        #endregion

        #region Batch buffers (reused every frame)
        private struct PointLabel { public float X, Y; public string Text; }
        private struct TechItem { public float X, Y, Width, Height, Rotation; public SKBitmap Icon; }

        // Group index: 0 = enemy / other, 1 = friendly, 2 = squad, 3 = admin
        private static readonly int GroupCount = 4;
        private readonly SKPath _bodyOutlinePath = new();
        private readonly SKPath[] _bodyFillPaths = { new(), new(), new(), new() };
        private readonly SKPath _aimOutlinePath = new();
        private readonly SKPath[] _aimFillPaths = { new(), new(), new(), new() };
        private readonly SKPath _projOutlinePath = new();
        private readonly SKPath[] _projFillPaths = { new(), new() };
        private readonly SKPath _deadOutlinePath = new();
        private readonly SKPath _impactPath = new();
        private readonly SKPath _aaMarkPath = new();

        private readonly List<PointLabel> _playerLabels = new(64);
        private readonly List<PointLabel> _vehicleLabels = new(32);
        private readonly List<PointLabel> _adminTexts = new(4);
        private readonly List<TechItem>[] _techItems = { new(64), new(32), new(32), new(16) };

        #endregion

        public RadarBatchRenderer()
        {
            _aaOutline.TextAlign = SKTextAlign.Center;
            _aaOutline.Typeface = _aaFill.Typeface;
            _aaFill.TextAlign = SKTextAlign.Center;
            RefreshTechPaints();
        }

        /// <summary>
        /// Eliminates re-deriving every scale-dependent paint property per entity per frame: they only change
        /// when the user changes UI scale.
        /// </summary>
        public void SetScale(float ui)
        {
            if (ui == _scale)
                return;
            _scale = ui;

            _bodyOutline.StrokeWidth = 2.4f * ui;
            _aimOutline.StrokeWidth = 3.8f * ui;
            _aimFill.StrokeWidth = 1.8f * ui;

            _projectileOutline.StrokeWidth = 4f * ui;
            _projectileFill.StrokeWidth = 2f * ui;

            float labelSize = 13f * ui * 1.1f;
            _labelFill.TextSize = labelSize;
            _labelFill.TextAlign = SKTextAlign.Left;
            _labelFill.SubpixelText = true;
            _labelOutline.TextSize = labelSize;
            _labelOutline.StrokeWidth = 2.8f * ui;
            _labelOutline.TextAlign = SKTextAlign.Left;
            _labelOutline.SubpixelText = true;

            _aaFill.TextSize = 16f * ui;
            _aaOutline.TextSize = 16f * ui;

            float adminSize = 12f * ui;
            _adminFill.TextSize = adminSize;
            _adminOutline.TextSize = adminSize;
            _adminOutline.StrokeWidth = 2f * ui;
            var bounds = new SKRect();
            _adminFill.MeasureText("ADMIN", ref bounds); // measured once per scale, not per admin per frame
            _adminHalfWidth = bounds.Width / 2f;
            _adminHalfHeight = bounds.Height / 2f;

            _deadOutline.StrokeWidth = 4f * ui;
            _deadFill.StrokeWidth = 2f * ui;
            _impactOutline.StrokeWidth = 4f * ui;
            _impactFill.StrokeWidth = 2f * ui;

            _aaMarkOutline.StrokeWidth = 2f * ui + 2f * ui;
            _aaMarkFill.StrokeWidth = 2f * ui;
            _aaMarkTextOutline.TextSize = 12f * ui;
            _aaMarkTextOutline.StrokeWidth = 2f * ui;
            _aaMarkText.TextSize = 12f * ui;

            RangeCirclePaint.StrokeWidth = 2f * ui;
            SpreadStrokePaint.StrokeWidth = 2f * ui;
            MinDistanceStrokePaint.StrokeWidth = 2f * ui;

            float numberSize = 10f * ui;
            PoiNumberOutline.TextSize = numberSize;
            PoiNumberOutline.StrokeWidth = 2.8f * ui;
            PoiNumberFill.TextSize = numberSize;
            PoiNumberBaselineOffset = (PoiNumberFill.FontMetrics.Descent - PoiNumberFill.FontMetrics.Ascent) / 2f;
        }

        /// <summary>Rebuilds the tinted icon paints after the user changes team colors.</summary>
        public void RefreshTechPaints()
        {
            _techPaintsDirty = true;
        }

        private void EnsureTechPaints()
        {
            if (!_techPaintsDirty)
                return;
            for (int i = 0; i < _techPaints.Length; i++)
                _techPaints[i]?.Dispose();

            _techPaints[0] = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
            _techPaints[1] = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High,
                ColorFilter = SKColorFilter.CreateBlendMode(SKPaints.Friendly, SKBlendMode.Modulate)
            };
            _techPaints[2] = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High,
                ColorFilter = SKColorFilter.CreateBlendMode(SKPaints.EnemyVehicle, SKBlendMode.Modulate)
            };
            _techPaints[3] = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High,
                ColorFilter = SKColorFilter.CreateBlendMode(SKPaints.Squad, SKBlendMode.Modulate)
            };
            _techPaintsDirty = false;
        }

        /// <summary>
        /// Off-screen margin (in canvas units) that keeps any marker, rotated icon or label that could still
        /// intersect the viewport from being culled.
        /// </summary>
        private static float ComputeCullMargin(float ui, float iconPx)
        {
            // 1.5 = drone upscale, ~1.5 = rotated diagonal, plus room for label text
            float iconSpan = iconPx * 1.5f * 1.5f;
            return Math.Max(iconSpan, 40f * ui) + 80f * ui + 32f;
        }

        /// <summary>On-screen size (canvas units) of a full-size vehicle/deployable icon; constant at every map zoom.</summary>
        private static float ComputeIconPixels(float ui, MapParameters mapParams) => IconBaseSize * ui * mapParams.TechScale;

        public void DrawActors(
            SKCanvas canvas,
            ActorFrameSnapshot frame,
            MapParameters mapParams,
            Map map,
            int localAimlineLength,
            bool highAlert,
            bool showEnemyDistance,
            float ui,
            DateTime now,
            List<SKPoint> deadMarkers,
            List<ActorFrameData> projectileAAs)
        {
            SetScale(ui);
            EnsureTechPaints();

            var localPlayer = frame.LocalPlayer;
            var cfg = map.ConfigFile;

            // Projection constants hoisted out of the per-entity loop. UActor world positions are
            // (pos + abs) - abs inside ToMapPos, so the absolute offset cancels and is not re-applied per entity.
            double mapScale = cfg.Scale;
            double bx = (cfg.X - mapParams.Bounds.Left) * mapParams.XScale;
            double by = (cfg.Y - mapParams.Bounds.Top) * mapParams.YScale;
            double sx = mapScale * mapParams.XScale;
            double sy = mapScale * mapParams.YScale;

            float iconPx = ComputeIconPixels(ui, mapParams);
            float margin = ComputeCullMargin(ui, iconPx);
            var cull = SKRect.Inflate(canvas.LocalClipBounds, margin, margin);

            var localPos = localPlayer.Position;
            float techUnit = iconPx / SvgIcons.LogicalSize; // logical icon units -> canvas units
            float labelDx = 15f * ui, labelDy = -8f * ui;

            _bodyOutlinePath.Rewind();
            _aimOutlinePath.Rewind();
            for (int i = 0; i < GroupCount; i++)
            {
                _bodyFillPaths[i].Rewind();
                _aimFillPaths[i].Rewind();
            }
            _projOutlinePath.Rewind();
            _projFillPaths[0].Rewind();
            _projFillPaths[1].Rewind();
            _playerLabels.Clear();
            _vehicleLabels.Clear();
            _adminTexts.Clear();
            for (int i = 0; i < _techItems.Length; i++) _techItems[i].Clear();

            // Local player marker (its own aimline length) goes into the same batched player paths.
            // Projected like every other actor (and like the map center), so it sits exactly under the camera.
            {
                float lx = (float)(bx + localPos.X * sx);
                float ly = (float)(by + localPos.Y * sy);
                int lg = localPlayer.IsInMySquad() ? 2 : localPlayer.IsFriendly() ? 1 : 0;
                AddPlayerMarker(lx, ly, localPlayer.Rotation.X, localAimlineLength, lg, ui);
            }

            var items = CollectionsMarshal.AsSpan(frame.Items);
            for (int i = 0; i < items.Length; i++)
            {
                ref readonly var a = ref items[i];
                var type = a.Type;

                if (type == ActorType.ProjectileAA)
                {
                    projectileAAs.Add(a); // drawn in the top-most pass
                    continue;
                }

                var p = a.Position;
                // Actor at the world origin = not yet populated by the update thread.
                if (Math.Abs(p.X) + Math.Abs(p.Y) + Math.Abs(p.Z) < 1.0)
                    continue;

                float x = (float)(bx + p.X * sx);
                float y = (float)(by + p.Y * sy);

                if (type == ActorType.Projectile || type == ActorType.ProjectileSmall)
                {
                    // Screen cull: skip anything that cannot touch the viewport before building any geometry.
                    if (!cull.Contains(x, y))
                        continue;
                    AddProjectile(x, y, ui, type == ActorType.ProjectileSmall ? 1 : 0);
                    continue;
                }

                // Hover hit-testing reads this for every actor, so it must be kept current even when culled.
                a.Actor.ZoomedPosition = new Vector2D(x, y);

                if (type == ActorType.Player && !a.IsAlive)
                {
                    CollectDeadPlayer(a.Actor, mapParams, map, now, deadMarkers);
                    continue;
                }

                if (type == ActorType.Player)
                {
                    // The local player is also in the actor list but was already drawn once above with the
                    // configured aimline; skip it here so it is only drawn once.
                    if (a.Actor == localPlayer)
                        continue;

                    float aim = PlayerAimlineLength;
                    if (highAlert && a.Actor.IsLookingAtPlayer(localPlayer))
                        aim = AlertAimlineLength;

                    // An alert aimline spans the whole map, so only short-aimline players are viewport-culled.
                    if (aim < AlertAimlineLength && !cull.Contains(x, y))
                        continue;

                    // Everyone else is just the chevron. The only line left is the high-alert one for enemies looking at us.
                    AddPlayerMarker(x, y, a.RotationX, aim < AlertAimlineLength ? 0f : aim, a.IsInMySquad ? 2 : a.IsFriendly ? 1 : 0, ui);

                    if (showEnemyDistance && !a.IsFriendly && TryDistanceLabel(localPos, p, out var text))
                        _playerLabels.Add(new PointLabel { X = x + labelDx, Y = y + labelDy, Text = text });
                    continue;
                }

                if (type == ActorType.Admin)
                {
                    AddPlayerMarker(x, y, a.RotationX, AlertAimlineLength, 3, ui);
                    _adminTexts.Add(new PointLabel { X = x, Y = y });
                    continue;
                }

                if (!cull.Contains(x, y))
                    continue;

                bool isVehicle = a.IsVehicle;
                if (isVehicle && a.TeamID == -1)
                    continue; // unclaimed / templated vehicles are not drawn

                Names.BitMaps.TryGetValue(type, out var icon);

                if (isVehicle && !a.IsFriendly && TryDistanceLabel(localPos, p, out var vtext))
                {
                    float lx, ly;
                    if (icon != null)
                    {
                        lx = x + icon.Width / (float)SvgIcons.RasterScale * techUnit * 0.5f + 5f * ui;
                        ly = y - icon.Height / (float)SvgIcons.RasterScale * techUnit * 0.5f + 14f * ui * 1.3f - 4f * ui;
                    }
                    else
                    {
                        lx = x + labelDx;
                        ly = y + labelDy;
                    }
                    _vehicleLabels.Add(new PointLabel { X = lx, Y = ly, Text = vtext });
                }

                // Vehicles with no health are spawners / templates; the icon must also exist.
                if (icon == null || (isVehicle && a.Health <= 0))
                    continue;

                float scale = techUnit;
                if (type == ActorType.Mine) scale /= 1.5f;
                else if (type == ActorType.Drone) scale *= 1.5f;

                float rotation = Names.DoNotRotate.Contains(type) ? 0f
                               : Names.RotateBy45Degrees.Contains(type) ? (float)(a.RotationX + 45)
                               : (float)(a.RotationX + 90);

                int tg = 0;
                if (isVehicle || Names.Deployables.Contains(type))
                    tg = a.IsFriendly ? 1 : a.IsEnemy ? 2 : 0;
                // a vehicle claimed by the local player's squad takes the squad color
                if (isVehicle && tg == 1 && a.Actor.ClaimingSquadID != -1 && a.Actor.ClaimingSquadID == localPlayer.SquadID) tg = 3;

                _techItems[tg].Add(new TechItem
                {
                    X = x, Y = y,
                    Width = icon.Width / (float)SvgIcons.RasterScale * scale, Height = icon.Height / (float)SvgIcons.RasterScale * scale,
                    Rotation = rotation, Icon = icon
                });
            }

            // ---- Flush: one pass per entity type, fixed paint state inside each pass ----

            // Vehicle labels sit under tech icons (matches the previous per-vehicle label-then-icon order).
            FlushLabels(canvas, _vehicleLabels, SKPaints.VehicleDistanceText);

            canvas.Save(); // single Save/Restore around all icon passes instead of one pair per icon
            for (int g = 0; g < _techItems.Length; g++)
            {
                var list = _techItems[g];
                if (list.Count == 0) continue;
                var paint = _techPaints[g];
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    var m = SKMatrix.CreateTranslation(t.X, t.Y);
                    m = SKMatrix.Concat(m, SKMatrix.CreateRotationDegrees(t.Rotation));
                    m = SKMatrix.Concat(m, SKMatrix.CreateTranslation(-t.Width / 2, -t.Height / 2));
                    canvas.SetMatrix(m);
                    canvas.DrawBitmap(t.Icon, SKRect.Create(t.Width, t.Height), paint);
                }
            }
            canvas.Restore();

            // Players are drawn in layers instead of one by one: aimlines first, then the chevrons on top.
            // Each layer is one outline draw plus at most four fills (one per color group).
            canvas.DrawPath(_aimOutlinePath, _aimOutline);
            for (int g = 0; g < GroupCount; g++)
            {
                if (_aimFillPaths[g].IsEmpty) continue;
                _aimFill.Color = GroupColor(g);
                canvas.DrawPath(_aimFillPaths[g], _aimFill);
            }

            canvas.DrawPath(_bodyOutlinePath, _bodyOutline);
            for (int g = 0; g < GroupCount; g++)
            {
                if (_bodyFillPaths[g].IsEmpty) continue;
                _bodyFill.Color = GroupColor(g);
                canvas.DrawPath(_bodyFillPaths[g], _bodyFill);
            }

            FlushLabels(canvas, _playerLabels, SKPaints.EnemyPlayerDistanceText);
            FlushAdminTexts(canvas, ui);

            // Projectiles: 1 outline draw + <= 2 fill draws replace 4 draws per projectile.
            if (!_projOutlinePath.IsEmpty)
            {
                canvas.DrawPath(_projOutlinePath, _projectileOutline);
                if (!_projFillPaths[0].IsEmpty)
                {
                    _projectileFill.Color = SKColors.Orange;
                    canvas.DrawPath(_projFillPaths[0], _projectileFill);
                }
                if (!_projFillPaths[1].IsEmpty)
                {
                    _projectileFill.Color = SKPaints.SmallProjectile;
                    canvas.DrawPath(_projFillPaths[1], _projectileFill);
                }
            }
        }

        /// <summary>
        /// Fire Support impact marks: a green "+" with a black outline, the same stroke style as the dead markers.
        /// The points are in canvas pixels. All of them are drawn as one path per layer.
        /// </summary>
        public void DrawImpactMarkers(SKCanvas canvas, List<SKPoint> points, float ui)
        {
            if (points.Count == 0)
                return;

            SetScale(ui);

            float arm = 5f * ui;
            _impactPath.Rewind();
            foreach (var p in points)
            {
                _impactPath.MoveTo(p.X - arm, p.Y);
                _impactPath.LineTo(p.X + arm, p.Y);
                _impactPath.MoveTo(p.X, p.Y - arm);
                _impactPath.LineTo(p.X, p.Y + arm);
            }
            canvas.DrawPath(_impactPath, _impactOutline);
            canvas.DrawPath(_impactPath, _impactFill); // identical geometry: reuse the path
        }

        /// <summary>Dead markers + AA projectiles, drawn above everything else.</summary>
        public void DrawTopMost(
            SKCanvas canvas,
            MapParameters mapParams,
            Map map,
            float ui,
            List<SKPoint> deadMarkers,
            List<ActorFrameData> projectileAAs,
            Dictionary<UActor, Vector3D> aaOrigins)
        {
            SetScale(ui);

            if (deadMarkers.Count > 0)
            {
                float size = 5f * ui;
                _deadOutlinePath.Rewind();
                foreach (var pos in deadMarkers)
                {
                    _deadOutlinePath.MoveTo(pos.X - size, pos.Y - size);
                    _deadOutlinePath.LineTo(pos.X + size, pos.Y + size);
                    _deadOutlinePath.MoveTo(pos.X + size, pos.Y - size);
                    _deadOutlinePath.LineTo(pos.X - size, pos.Y + size);
                }
                _deadFill.Color = SKPaints.DeadMarker;
                canvas.DrawPath(_deadOutlinePath, _deadOutline);
                canvas.DrawPath(_deadOutlinePath, _deadFill); // identical geometry: reuse the path
            }

            if (projectileAAs.Count == 0)
                return;

            var cfg = map.ConfigFile;
            double bx = (cfg.X - mapParams.Bounds.Left) * mapParams.XScale;
            double by = (cfg.Y - mapParams.Bounds.Top) * mapParams.YScale;
            double sx = cfg.Scale * mapParams.XScale;
            double sy = cfg.Scale * mapParams.YScale;
            var cull = SKRect.Inflate(canvas.LocalClipBounds, 64f * ui + 32f, 64f * ui + 32f);

            _aaFill.Color = SKColors.Orange;
            _aaOutline.Color = SKColors.Black;

            foreach (var aa in projectileAAs)
            {
                float x = (float)(bx + aa.Position.X * sx);
                float y = (float)(by + aa.Position.Y * sy);
                if (!cull.Contains(x, y))
                    continue;

                // "AA" text is centred on the projectile; the old MeasureText height terms cancelled to y.
                canvas.DrawText("AA", x, y, _aaOutline);
                canvas.DrawText("AA", x, y, _aaFill);

                if (aaOrigins.Count > 0 && aaOrigins.TryGetValue(aa.Actor, out var origin))
                {
                    float ox = (float)(bx + origin.X * sx);
                    float oy = (float)(by + origin.Y * sy);
                    DrawAAStartMarker(canvas, ox, oy, ui);
                }
            }
        }

        #region Bucketing helpers
        private void AddPlayerMarker(float x, float y, double rotationX, float aimlineLength, int group, float ui)
        {
            double radians = rotationX.ToRadians();
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);

            // Chevron outline in local space (facing +x), rotated around (x, y): tip, right wing, back notch, left wing.
            Span<(float lx, float ly)> local = stackalloc (float, float)[]
            {
                (ChevronTip * ui, 0f),
                (-ChevronBack * ui, ChevronWing * ui),
                ((-ChevronBack + ChevronNotch) * ui, 0f),
                (-ChevronBack * ui, -ChevronWing * ui)
            };

            var fill = _bodyFillPaths[group];
            for (int i = 0; i < local.Length; i++)
            {
                float px = x + local[i].lx * cos - local[i].ly * sin;
                float py = y + local[i].lx * sin + local[i].ly * cos;
                if (i == 0)
                {
                    _bodyOutlinePath.MoveTo(px, py);
                    fill.MoveTo(px, py);
                }
                else
                {
                    _bodyOutlinePath.LineTo(px, py);
                    fill.LineTo(px, py);
                }
            }
            _bodyOutlinePath.Close();
            fill.Close();

            // The aimline starts at the tip. If the configured length doesn't reach past the chevron there's
            // nothing to draw.
            float tipDistance = ChevronTip * ui;
            float lineLength = aimlineLength * ui;
            if (lineLength > tipDistance)
            {
                float sx = x + cos * tipDistance, sy = y + sin * tipDistance;
                float ex = x + cos * lineLength, ey = y + sin * lineLength;
                _aimOutlinePath.MoveTo(sx, sy);
                _aimOutlinePath.LineTo(ex, ey);
                _aimFillPaths[group].MoveTo(sx, sy);
                _aimFillPaths[group].LineTo(ex, ey);
            }
        }

        private void AddProjectile(float x, float y, float ui, int colorGroup)
        {
            float s = 6f * ui;
            var fill = _projFillPaths[colorGroup];
            _projOutlinePath.MoveTo(x - s, y); _projOutlinePath.LineTo(x + s, y);
            _projOutlinePath.MoveTo(x, y - s); _projOutlinePath.LineTo(x, y + s);
            fill.MoveTo(x - s, y); fill.LineTo(x + s, y);
            fill.MoveTo(x, y - s); fill.LineTo(x, y + s);
        }

        private static SKColor GroupColor(int group) => group switch
        {
            1 => SKPaints.Friendly,
            2 => SKPaints.Squad,
            3 => SKPaints.AdminMarker,
            _ => SKPaints.EnemyPlayer
        };

        private void CollectDeadPlayer(UActor actor, MapParameters mp, Map map, DateTime now, List<SKPoint> deadMarkers)
        {
            var death = actor.DeathPosition;
            if (death.X == 0 && death.Y == 0 && death.Z == 0)
                return;

            if ((now - actor.TimeOfDeath).TotalSeconds <= 8)
            {
                var cfg = map.ConfigFile;
                float x = (float)((cfg.X + death.X * cfg.Scale - mp.Bounds.Left) * mp.XScale);
                float y = (float)((cfg.Y + death.Y * cfg.Scale - mp.Bounds.Top) * mp.YScale);
                deadMarkers.Add(new SKPoint(x, y));
            }
            else
            {
                actor.DeathPosition = Vector3D.Zero;
                actor.TimeOfDeath = DateTime.MinValue;
            }
        }

        /// <summary>
        /// Past-50m distance label. Squared-distance gate first so the sqrt, rounding and string lookup
        /// only happen for entities that actually get a label.
        /// </summary>
        private static bool TryDistanceLabel(Vector3D from, Vector3D to, out string text)
        {
            double dx = from.X - to.X, dy = from.Y - to.Y, dz = from.Z - to.Z;
            double d2 = dx * dx + dy * dy + dz * dz;
            if (d2 <= LabelMinDistanceUnitsSq)
            {
                text = null;
                return false;
            }
            text = DistanceLabels.ForMeters((int)Math.Round(Math.Sqrt(d2) / 100));
            return true;
        }

        private void FlushLabels(SKCanvas canvas, List<PointLabel> labels, SKColor color)
        {
            if (labels.Count == 0)
                return;
            // Outlines first, then fills: two paint setups per pass instead of alternating per label.
            for (int i = 0; i < labels.Count; i++)
                canvas.DrawText(labels[i].Text, labels[i].X, labels[i].Y, _labelOutline);
            _labelFill.Color = color;
            for (int i = 0; i < labels.Count; i++)
                canvas.DrawText(labels[i].Text, labels[i].X, labels[i].Y, _labelFill);
        }

        private void FlushAdminTexts(SKCanvas canvas, float ui)
        {
            if (_adminTexts.Count == 0)
                return;
            _adminFill.Color = SKPaints.AdminMarker;
            float offset = 15f * ui;
            for (int i = 0; i < _adminTexts.Count; i++)
            {
                float x = _adminTexts[i].X - _adminHalfWidth;
                float y = _adminTexts[i].Y + offset + _adminHalfHeight;
                canvas.DrawText("ADMIN", x, y, _adminOutline);
                canvas.DrawText("ADMIN", x, y, _adminFill);
            }
        }

        private void DrawAAStartMarker(SKCanvas canvas, float x, float y, float ui)
        {
            float size = 8f * ui;
            _aaMarkPath.Rewind();
            _aaMarkPath.MoveTo(x - size, y - size); _aaMarkPath.LineTo(x + size, y + size);
            _aaMarkPath.MoveTo(x + size, y - size); _aaMarkPath.LineTo(x - size, y + size);
            canvas.DrawPath(_aaMarkPath, _aaMarkOutline);
            canvas.DrawPath(_aaMarkPath, _aaMarkFill);

            var bounds = new SKRect();
            _aaMarkText.MeasureText("AA", ref bounds);
            float tx = x + size + 4f * ui;
            float ty = y + bounds.Height / 2f;
            canvas.DrawText("AA", tx, ty, _aaMarkTextOutline);
            canvas.DrawText("AA", tx, ty, _aaMarkText);
        }
        #endregion

        public void Dispose()
        {
            foreach (var p in new[] { _bodyOutline, _bodyFill, _aimOutline, _aimFill, _projectileOutline, _projectileFill, _labelFill, _labelOutline,
                _aaFill, _aaOutline, _adminFill, _adminOutline, _deadOutline, _deadFill, _impactOutline, _impactFill, _aaMarkOutline, _aaMarkFill,
                _aaMarkTextOutline, _aaMarkText, RangeCirclePaint, PoiCrosshairPaint, SpreadFillPaint, SpreadStrokePaint,
                MinDistanceFillPaint, MinDistanceStrokePaint, PoiNumberOutline, PoiNumberFill })
                p.Dispose();
            foreach (var p in _techPaints) p?.Dispose();
            _bodyOutlinePath.Dispose();
            _aimOutlinePath.Dispose();
            foreach (var p in _bodyFillPaths) p.Dispose();
            foreach (var p in _aimFillPaths) p.Dispose();
            _projOutlinePath.Dispose();
            foreach (var p in _projFillPaths) p.Dispose();
            _deadOutlinePath.Dispose();
            _impactPath.Dispose();
            _aaMarkPath.Dispose();
        }
    }
}
