using SkiaSharp;
using SkiaSharp.Views.Desktop;
using squad_dma.Source.Misc;
using squad_dma.Source.Squad;

namespace squad_dma
{
    /// <summary>
    /// Firing table for the placed markers: distance, bearing, the dial setting for the equipped weapon,
    /// time of flight, and whether the target can be reached at all.
    /// </summary>
    public class TInfoWidget : SKWidget
    {
        #region Fields
        private readonly MainForm _mainForm;
        private readonly List<TargetMarkerInfo> _markerInfos = new List<TargetMarkerInfo>();
        private string _weaponLine = NoWeaponText;
        private bool _hasWeapon;

        // Input signature of the last row build (see UpdateMarkerInfo)
        private readonly List<MainForm.PointOfInterest> _lastMarkers = new();
        private double _lastPlayerX, _lastPlayerY;
        private object _lastWeaponData;
        private TerrainHeightmap _lastTerrain;
        private bool _inputsValid;

        // Layout values that only change when rows or UI scale change
        private static readonly string[] Headers = { "#", "Dist", "Brg", "Elev", "ToF", "" };
        private static readonly float[] BaseColumnWidths = { 26, 92, 66, 92, 56, 44 };
        private readonly float[] _columnWidths = new float[BaseColumnWidths.Length];
        private float _layoutScale = float.NaN;
        private float _layoutTextSize = float.NaN;
        private float _rowHeight;
        private float _totalWidth;
        private bool _sizeDirty = true;
        #endregion

        // A bit more opaque than the default so the table stays readable over the map
        protected override SKColor BodyColor => new SKColor(10, 12, 16, 0xCC);

        #region Constants
        private const float PADDING = 5f;
        private const float TEXT_OFFSET_X = 4f;
        private const float TEXT_OFFSET_Y = 6f;
        private const int MAX_ROWS = 8;
        private const string NoWeaponText = "No ballistic weapon";

        private static readonly SKColor ValueColor = SKColors.White;
        private static readonly SKColor DialColor = new(255, 214, 64);   // The number to dial in
        private static readonly SKColor GoodColor = new(96, 255, 96);
        private static readonly SKColor BadColor = new(255, 80, 80);
        private static readonly SKColor DimColor = new(170, 170, 170);
        #endregion

        #region Constructor
        public TInfoWidget(SKGLControl parent, MainForm mainForm, float scaleFactor)
            : base(parent, "", new SKPoint(10, 100), new SKSize(300, 100), scaleFactor, canResize: false)
        {
            _mainForm = mainForm;
        }
        #endregion

        #region Public Methods
        public override void Draw(SKCanvas canvas)
        {
            UpdateMarkerInfo();

            // Column widths and the size are only recomputed when rows or scale changed, not every frame.
            EnsureLayout();
            if (_sizeDirty)
            {
                var requiredSize = CalculateRequiredSize();
                if (Size.Width != requiredSize.Width || Size.Height != requiredSize.Height)
                {
                    Size = requiredSize;
                }
                _sizeDirty = false;
            }

            base.Draw(canvas);

            if (Minimized) return;

            DrawContent(canvas);
        }

        public void OnMarkerPlaced()
        {
            // Trigger size recalculation on next draw
        }

        public void OnMarkerRemoved()
        {
            // Trigger size recalculation on next draw
        }

        public void ClearAllMarkers()
        {
            _mainForm.ClearAllPointsOfInterest();
        }
        #endregion

        #region Private Methods
        private void UpdateMarkerInfo()
        {
            var markers = _mainForm.GetPointsOfInterest();
            var localPlayer = _mainForm.GetLocalPlayer();

            if (localPlayer == null || markers == null)
            {
                _markerInfos.Clear();
                _inputsValid = false;
                return;
            }

            var playerPos = localPlayer.Position; // single locked read
            var detector = Memory._game?._soldierManager?.WeaponDetector;
            var weaponData = detector?.CurrentWeaponData;
            var terrain = TerrainHeightmap.Current;

            // Rows only change when the player moved, the weapon changed, the terrain finished loading, or the marker list changed.
            if (_inputsValid &&
                playerPos.X == _lastPlayerX && playerPos.Y == _lastPlayerY &&
                ReferenceEquals(weaponData, _lastWeaponData) &&
                ReferenceEquals(terrain, _lastTerrain) &&
                MarkersUnchanged(markers))
                return;

            _inputsValid = true;
            _lastPlayerX = playerPos.X;
            _lastPlayerY = playerPos.Y;
            _lastWeaponData = weaponData;
            _lastTerrain = terrain;
            _lastMarkers.Clear();

            _hasWeapon = detector != null && detector.HasBallisticWeapon;
            _weaponLine = BuildWeaponLine(weaponData);

            _markerInfos.Clear();
            for (int i = 0; i < markers.Count && i < MAX_ROWS; i++)
            {
                var marker = markers[i];
                _lastMarkers.Add(marker);
                var info = CalculateMarkerInfo(marker, playerPos, i + 1, detector);
                if (info != null)
                    _markerInfos.Add(info);
            }
            _sizeDirty = true;
        }

        private static string BuildWeaponLine(WeaponData weapon)
        {
            if (weapon == null)
                return NoWeaponText;

            return weapon.MinDistance > 0
                ? $"{weapon.Name}"
                : weapon.Name;
        }

        private bool MarkersUnchanged(List<MainForm.PointOfInterest> markers)
        {
            int count = Math.Min(markers.Count, MAX_ROWS);
            if (count != _lastMarkers.Count)
                return false;
            for (int i = 0; i < count; i++)
                if (!ReferenceEquals(markers[i], _lastMarkers[i]))
                    return false;
            return true;
        }

        private TargetMarkerInfo CalculateMarkerInfo(MainForm.PointOfInterest marker, Vector3D playerPos, int markerNumber, MortarCalculator detector)
        {
            try
            {
                var markerPos = marker.Position;

                // Both positions carry the same absolute offset, which cancels in every delta below.
                var dx = markerPos.X - playerPos.X;
                var dy = markerPos.Y - playerPos.Y;

                // Horizontal distance; how much higher or lower the target ground is comes from the heightmap
                var distance = Math.Sqrt(dx * dx + dy * dy);
                int distanceMeters = (int)Math.Round(distance / 100);
                double bearing = MortarCalculator.BearingDegrees(dx, dy);
                double heightMeters = TerrainHeightmap.HeightDifferenceMeters(playerPos.X, playerPos.Y, markerPos.X, markerPos.Y);
                int heightRounded = (int)Math.Round(heightMeters);

                var info = new TargetMarkerInfo
                {
                    NumberText = markerNumber.ToString(),
                    DistanceText = heightRounded == 0 ? $"{distanceMeters}m" : $"{distanceMeters}m {heightRounded:+0;-0}",
                    BearingText = $"{bearing:F1}°",
                    ElevText = "-",
                    TofText = "-",
                    StatusText = "",
                };

                var ranging = _hasWeapon && detector != null ? detector.GetRanging(distanceMeters, heightMeters) : RangingResult.None;
                if (!ranging.HasWeapon)
                    return info;

                switch (ranging.Status)
                {
                    case RangeStatus.TooClose:
                        info.StatusText = "MIN";
                        info.Bad = true;
                        break;
                    case RangeStatus.TooFar:
                        info.StatusText = "MAX";
                        info.Bad = true;
                        break;
                    default:
                        info.StatusText = "OK";
                        info.ElevText = (ranging.IsEstimate ? "~" : "")
                            + ranging.Value.ToString(ranging.Decimals == 0 ? "F0" : "F1") + ranging.Unit;
                        info.TofText = $"{ranging.TimeOfFlight:F1}s";
                        break;
                }

                return info;
            }
            catch
            {
                return null;
            }
        }

        private void EnsureLayout()
        {
            if (_layoutScale == ScaleFactor && _layoutTextSize == TextPaint.TextSize)
                return;

            _layoutScale = ScaleFactor;
            _layoutTextSize = TextPaint.TextSize;
            _totalWidth = 0f;
            for (int i = 0; i < _columnWidths.Length; i++)
            {
                _columnWidths[i] = BaseColumnWidths[i] * ScaleFactor;
                _totalWidth += _columnWidths[i];
            }
            _rowHeight = (TextPaint.FontSpacing + 4f) * ScaleFactor;

            TextOutlinePaint.TextSize = TextPaint.TextSize;
            TextOutlinePaint.StrokeWidth = 2.5f * ScaleFactor;
            BorderPaint.StrokeWidth = Math.Max(1f, ScaleFactor);
            _sizeDirty = true;
        }

        private int RowCount => 1 + (_markerInfos.Count == 0 ? 1 : _markerInfos.Count + 1); // weapon line + (header + rows | standby)

        private SKSize CalculateRequiredSize()
        {
            float pad = PADDING * ScaleFactor;
            return new SKSize(_totalWidth + pad * 2, RowCount * _rowHeight + pad * 2);
        }

        // Dark outline under the fill keeps the text readable over the map
        private void DrawText(SKCanvas canvas, string text, float x, float y, SKColor color)
        {
            if (string.IsNullOrEmpty(text))
                return;

            canvas.DrawText(text, x, y, TextOutlinePaint);
            TextPaint.Color = color;
            canvas.DrawText(text, x, y, TextPaint);
        }

        private void DrawContent(SKCanvas canvas)
        {
            float pad = PADDING * ScaleFactor;
            float textOffsetX = TEXT_OFFSET_X * ScaleFactor;
            float textOffsetY = TEXT_OFFSET_Y * ScaleFactor;
            float rowHeight = _rowHeight;
            float totalWidth = _totalWidth;

            float left = ClientRectangle.Left + pad;
            float top = ClientRectangle.Top + pad;
            float right = left + totalWidth;
            float bottom = top + RowCount * rowHeight;

            // Weapon line: what the numbers below are calculated for
            DrawText(canvas, _weaponLine, left + textOffsetX, top + rowHeight - textOffsetY, _hasWeapon ? GoodColor : DimColor);

            float y = top + rowHeight;

            if (_markerInfos.Count == 0)
            {
                DrawText(canvas, "No markers placed", left + textOffsetX, y + rowHeight - textOffsetY, DimColor);
                canvas.DrawLine(left, y, right, y, BorderPaint);
                return;
            }

            // Header row
            canvas.DrawRect(new SKRect(left, y, right, y + rowHeight), HeaderFillPaint);
            float x = left;
            for (int i = 0; i < Headers.Length; i++)
            {
                DrawText(canvas, Headers[i], x + textOffsetX, y + rowHeight - textOffsetY, DimColor);
                x += _columnWidths[i];
            }
            y += rowHeight;

            // Marker rows
            for (int r = 0; r < _markerInfos.Count; r++)
            {
                var info = _markerInfos[r];
                if (info.Bad)
                    canvas.DrawRect(new SKRect(left, y, right, y + rowHeight), BadRowFillPaint);

                float baseline = y + rowHeight - textOffsetY;
                x = left;
                for (int i = 0; i < _columnWidths.Length; i++)
                {
                    string cell;
                    SKColor color;
                    switch (i)
                    {
                        case 0: cell = info.NumberText; color = ValueColor; break;
                        case 1: cell = info.DistanceText; color = ValueColor; break;
                        case 2: cell = info.BearingText; color = ValueColor; break;
                        case 3: cell = info.ElevText; color = info.Bad ? BadColor : DialColor; break;
                        case 4: cell = info.TofText; color = info.Bad ? BadColor : ValueColor; break;
                        default: cell = info.StatusText; color = info.Bad ? BadColor : GoodColor; break;
                    }
                    DrawText(canvas, cell, x + textOffsetX, baseline, color);
                    x += _columnWidths[i];
                }
                y += rowHeight;
            }

            // Grid: a line under every row and a line between every column; the window border is the outer edge
            for (int row = 1; row < RowCount; row++)
            {
                float ly = top + row * rowHeight;
                canvas.DrawLine(left, ly, right, ly, BorderPaint);
            }

            float vx = left;
            float gridTop = top + rowHeight; // the weapon line spans every column
            for (int i = 0; i < _columnWidths.Length - 1; i++)
            {
                vx += _columnWidths[i];
                canvas.DrawLine(vx, gridTop, vx, bottom, BorderPaint);
            }
        }
        #endregion

        #region Data Classes
        private class TargetMarkerInfo
        {
            public string NumberText { get; set; } = "";
            public string DistanceText { get; set; } = "";
            public string BearingText { get; set; } = "";
            public string ElevText { get; set; } = "";
            public string TofText { get; set; } = "";
            public string StatusText { get; set; } = "";
            public bool Bad { get; set; }
        }
        #endregion

        #region Paints
        private static readonly SKPaint HeaderFillPaint = new SKPaint()
        {
            Color = new SKColor(255, 255, 255, 36),
            Style = SKPaintStyle.Fill,
        };

        private static readonly SKPaint BadRowFillPaint = new SKPaint()
        {
            Color = new SKColor(200, 0, 0, 80),
            Style = SKPaintStyle.Fill,
        };

        internal static SKPaint TextPaint { get; } = new()
        {
            SubpixelText = true,
            Color = SKColors.White,
            IsStroke = false,
            TextSize = 12,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas"), // Do NOT change this font
            FilterQuality = SKFilterQuality.High
        };

        private static SKPaint TextOutlinePaint { get; } = new()
        {
            SubpixelText = true,
            Color = new SKColor(0, 0, 0, 230),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.5f,
            StrokeJoin = SKStrokeJoin.Round,
            TextSize = 12,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas"), // Same font as TextPaint so the outline lines up
        };

        internal static SKPaint BorderPaint { get; } = new()
        {
            Color = new SKColor(255, 255, 255, 40),
            StrokeWidth = 1f,
            Style = SKPaintStyle.Stroke,
            IsStroke = true
        };
        #endregion

        public override void SetScaleFactor(float newScale)
        {
            base.SetScaleFactor(newScale);
            TextPaint.TextSize = 12 * newScale;
        }

        #region IDisposable
        public override void Dispose()
        {
            base.Dispose();
        }
        #endregion
    }
}
