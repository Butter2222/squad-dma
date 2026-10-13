using SkiaSharp;
using SkiaSharp.Views.Desktop;
using squad_dma.Source.Misc;
using squad_dma.Source.Squad;

namespace squad_dma
{
    public class TInfoWidget : SKWidget
    {
        #region Fields
        private readonly MainForm _mainForm;
        private List<TargetMarkerInfo> _markerInfos = new List<TargetMarkerInfo>();

        // Input signature of the last row build (see UpdateMarkerInfo)
        private readonly List<MainForm.PointOfInterest> _lastMarkers = new();
        private double _lastPlayerX, _lastPlayerY;
        private object _lastWeaponData;
        private bool _inputsValid;

        // Layout values that only change when rows or UI scale change
        private static readonly string[] Headers = { "Mark", "Distance", "Bearing", "Range" };
        private static readonly float[] BaseColumnWidths = { 40, 80, 80, 80 };
        private readonly float[] _columnWidths = new float[4];
        private float _layoutScale = float.NaN;
        private float _rowHeight;
        private float _totalWidth;
        private bool _sizeDirty = true;
        #endregion

        #region Properties
        private float TitleBarHeight => 12.5f * ScaleFactor;
        private SKRect TitleBar => new(Rectangle.Left, Rectangle.Top, Rectangle.Right, Rectangle.Top + TitleBarHeight);
        private SKRect MinimizeButton => new(TitleBar.Right - TitleBarHeight,
            TitleBar.Top, TitleBar.Right, TitleBar.Bottom);
        #endregion

        #region Constants
        private const float PADDING = 5f;
        private const float TEXT_OFFSET_X = 4f;
        private const float TEXT_OFFSET_Y = 6f;
        #endregion

        #region Constructor
        public TInfoWidget(SKGLControl parent, MainForm mainForm, float scaleFactor)
            : base(parent, "Targeting Info", new SKPoint(10, 100), new SKSize(300, 100), scaleFactor, canResize: false)
        {
            _mainForm = mainForm;
        }
        #endregion

        #region Public Methods
        public override void Draw(SKCanvas canvas)
        {
            // Update marker information
            UpdateMarkerInfo();
            
            // Eliminates recomputing column widths (LINQ Select/ToArray/Sum) and re-applying the size
            // (locked setter + resize-triangle rebuild) every frame: only when rows or scale changed.
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

            // Draw custom background with 80% opacity
            DrawCustomBackground(canvas);
            
            // Draw title bar and minimize button
            DrawTitleBar(canvas);

            if (Minimized) return;

            // Draw content
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
            var weaponData = Memory._game?._soldierManager?.WeaponDetector?.CurrentWeaponData;

            // Eliminates rebuilding every row (reflection lookups, trig, string formatting, ranging-table lookups)
            // on every frame: rows only change when the player moved, the weapon changed, or the marker list changed.
            if (_inputsValid &&
                playerPos.X == _lastPlayerX && playerPos.Y == _lastPlayerY &&
                ReferenceEquals(weaponData, _lastWeaponData) &&
                MarkersUnchanged(markers))
                return;

            _inputsValid = true;
            _lastPlayerX = playerPos.X;
            _lastPlayerY = playerPos.Y;
            _lastWeaponData = weaponData;
            _lastMarkers.Clear();

            _markerInfos.Clear();
            for (int i = 0; i < markers.Count && i < 5; i++)
            {
                var marker = markers[i];
                _lastMarkers.Add(marker);
                var info = CalculateMarkerInfo(marker, playerPos, i + 1);
                if (info != null)
                    _markerInfos.Add(info);
            }
            _sizeDirty = true;
        }

        private bool MarkersUnchanged(List<MainForm.PointOfInterest> markers)
        {
            int count = Math.Min(markers.Count, 5);
            if (count != _lastMarkers.Count)
                return false;
            for (int i = 0; i < count; i++)
                if (!ReferenceEquals(markers[i], _lastMarkers[i]))
                    return false;
            return true;
        }

        private TargetMarkerInfo CalculateMarkerInfo(MainForm.PointOfInterest marker, Vector3D playerPos, int markerNumber)
        {
            try
            {
                // Typed access replaces two reflection GetProperty/GetValue pairs per marker per frame.
                var markerPos = marker.Position;

                // Both positions carry the same absolute offset, which cancels in every delta below,
                // so the MainForm round-trip through GetAbsoluteLocation() is not needed.
                var dx = markerPos.X - playerPos.X;
                var dy = markerPos.Y - playerPos.Y;

                // 2D distance on flat ground (ignore Z height differences)
                var distance = Math.Sqrt(dx * dx + dy * dy);
                int distanceMeters = (int)Math.Round(distance / 100);

                // Bearing using the exact working formula
                double radians = Math.Atan2(-dy, dx);
                double degrees = radians * (180.0 / Math.PI);
                degrees = 90.0 - degrees;

                if (degrees < 0) degrees += 360.0;

                float bearing = (float)degrees;

                // Get weapon-specific ranging
                string rangingInfo = GetWeaponRanging(distanceMeters);

                return new TargetMarkerInfo
                {
                    Number = markerNumber,
                    Distance = distanceMeters,
                    Bearing = bearing,
                    RangingInfo = rangingInfo,
                    Name = marker.Name,
                    // Formatted once when the row is rebuilt, not once per cell per frame.
                    NumberText = markerNumber.ToString(),
                    DistanceText = $"{distanceMeters}m",
                    BearingText = $"{bearing:F1}°"
                };
            }
            catch
            {
                return null;
            }
        }

        private string GetWeaponRanging(int distanceMeters)
        {
            try
            {
                var weaponDetector = Memory._game?._soldierManager?.WeaponDetector;
                if (weaponDetector == null || !weaponDetector.HasBallisticWeapon)
                    return "N/A";

                // Get the database weapon name from the current weapon data
                var weaponData = weaponDetector.CurrentWeaponData;
                if (weaponData == null)
                    return "N/A";

                var weaponName = weaponData.Name;
                var rangingValue = MortarCalculator.GetWeaponRanging(weaponName, distanceMeters);
                var unit = MortarCalculator.GetWeaponRangingUnit(weaponName);
                
                if (rangingValue < 0)
                    return "N/A";
                
                return $"{rangingValue:F1}{unit}";
            }
            catch
            {
                return "N/A";
            }
        }

        private bool HasBallisticWeapon()
        {
            try
            {
                return Memory._game?._soldierManager?.WeaponDetector?.HasBallisticWeapon ?? false;
            }
            catch
            {
                return false;
            }
        }

        private float _layoutTextSize = float.NaN;

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
            _sizeDirty = true;
        }

        private SKSize CalculateRequiredSize()
        {
            float pad = PADDING * ScaleFactor;
            float totalHeight = (_markerInfos.Count + 1) * _rowHeight; // +1 for header

            return new SKSize(_totalWidth + pad * 2, totalHeight + pad * 2);
        }

        private void DrawCustomBackground(SKCanvas canvas)
        {
            if (!Minimized)
                canvas.DrawRect(Rectangle, CustomBackgroundPaint);
        }

        private void DrawTitleBar(SKCanvas canvas)
        {
            canvas.DrawRect(TitleBar, TitleBarPaint);
            float titleCenterY = TitleBar.Top + (TitleBar.Height / 2);
            float titleYOffset = (TitleBarText.FontMetrics.Ascent + TitleBarText.FontMetrics.Descent) / 2;
            canvas.DrawText(Title,
                new(TitleBar.Left + 2.5f * ScaleFactor,
                titleCenterY - titleYOffset),
                TitleBarText);
            canvas.DrawRect(MinimizeButton, ButtonBackgroundPaint);
            DrawMinimizeButton(canvas);
        }

        private void DrawMinimizeButton(SKCanvas canvas)
        {
            float minHalfLength = MinimizeButton.Width / 4;
            if (Minimized)
            {
                canvas.DrawLine(MinimizeButton.MidX - minHalfLength,
                    MinimizeButton.MidY,
                    MinimizeButton.MidX + minHalfLength,
                    MinimizeButton.MidY,
                    SymbolPaint);
                canvas.DrawLine(MinimizeButton.MidX,
                    MinimizeButton.MidY - minHalfLength,
                    MinimizeButton.MidX,
                    MinimizeButton.MidY + minHalfLength,
                    SymbolPaint);
            }
            else
                canvas.DrawLine(MinimizeButton.MidX - minHalfLength,
                    MinimizeButton.MidY,
                    MinimizeButton.MidX + minHalfLength,
                    MinimizeButton.MidY,
                    SymbolPaint);
        }

        private void DrawContent(SKCanvas canvas)
        {
            float pad = PADDING * ScaleFactor;
            float textOffsetX = TEXT_OFFSET_X * ScaleFactor;
            float textOffsetY = TEXT_OFFSET_Y * ScaleFactor;

            // Layout (column widths / row height / total width) comes from the cache built in EnsureLayout();
            // the size was already applied in Draw(), so it is not recomputed or re-set here.
            float rowHeight = _rowHeight;
            float totalWidth = _totalWidth;

            var origin = new SKPoint(ClientRectangle.Left + pad, ClientRectangle.Top + pad);

            if (_markerInfos.Count == 0)
            {
                // Draw standby message
                canvas.DrawText("No markers placed", origin.X + textOffsetX, origin.Y + rowHeight - textOffsetY, TextPaint);
                return;
            }

            // Draw headers
            float x = origin.X;
            float y = origin.Y;

            for (int i = 0; i < Headers.Length; i++)
            {
                canvas.DrawText(Headers[i], x + textOffsetX, y + rowHeight - textOffsetY, TextPaint);
                x += _columnWidths[i];
            }

            // Draw header separator line
            canvas.DrawLine(origin.X, y + rowHeight, origin.X + totalWidth, y + rowHeight, BorderPaint);
            y += rowHeight;

            // Draw marker data rows (cell strings were formatted when the row was built; no per-frame arrays)
            for (int r = 0; r < _markerInfos.Count; r++)
            {
                var info = _markerInfos[r];

                x = origin.X;
                for (int i = 0; i < _columnWidths.Length; i++)
                {
                    string cell = i switch
                    {
                        0 => info.NumberText,
                        1 => info.DistanceText,
                        2 => info.BearingText,
                        _ => info.RangingInfo
                    };
                    canvas.DrawText(cell, x + textOffsetX, y + rowHeight - textOffsetY, TextPaint);
                    canvas.DrawLine(x, y - rowHeight, x, y + rowHeight, BorderPaint);
                    x += _columnWidths[i];
                }

                // Draw right border and bottom separator
                canvas.DrawLine(x, y - rowHeight, x, y + rowHeight, BorderPaint);
                canvas.DrawLine(origin.X, y + rowHeight, origin.X + totalWidth, y + rowHeight, BorderPaint);
                y += rowHeight;
            }
        }

        // Override mouse click handling in parent class
        // This would need to be integrated into the parent's mouse handling system
        #endregion

        #region Data Classes
        private class TargetMarkerInfo
        {
            public int Number { get; set; }
            public int Distance { get; set; }
            public float Bearing { get; set; }
            public string RangingInfo { get; set; } = "";
            public string Name { get; set; } = "";
            public string NumberText { get; set; } = "";
            public string DistanceText { get; set; } = "";
            public string BearingText { get; set; } = "";
        }
        #endregion

        #region Paints
        private static readonly SKPaint CustomBackgroundPaint = new SKPaint()
        {
            Color = SKColors.Black.WithAlpha(0xCC), // 80% Opacity
            StrokeWidth = 1,
            Style = SKPaintStyle.Fill,
        };

        private static readonly SKPaint TitleBarPaint = new SKPaint()
        {
            Color = new SKColor(64, 64, 64), // Dark gray
            StrokeWidth = 0.5f,
            Style = SKPaintStyle.Fill,
        };

        private static readonly SKPaint ButtonBackgroundPaint = new SKPaint()
        {
            Color = new SKColor(80, 80, 80), // Slightly lighter dark gray for button
            StrokeWidth = 0.1f,
            Style = SKPaintStyle.Fill,
        };

        private static readonly SKPaint SymbolPaint = new SKPaint()
        {
            Color = SKColors.White,
            StrokeWidth = 2f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        private static readonly SKPaint TitleBarText = new SKPaint()
        {
            SubpixelText = true,
            Color = SKColors.White,
            IsStroke = false,
            TextSize = 9f,
            TextAlign = SKTextAlign.Left,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            Typeface = CustomFonts.SKFontFamilyRegular,
            FilterQuality = SKFilterQuality.High
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

        internal static SKPaint BorderPaint { get; } = new()
        {
            Color = SKColors.DimGray,
            StrokeWidth = 2f,
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
