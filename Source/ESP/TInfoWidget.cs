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
            
            // Calculate required size and adjust
            var requiredSize = CalculateRequiredSize();
            if (Size.Width != requiredSize.Width || Size.Height != requiredSize.Height)
            {
                Size = requiredSize;
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
            _markerInfos.Clear();

            var markers = _mainForm.GetPointsOfInterest();
            var localPlayer = _mainForm.GetLocalPlayer();
            
            if (localPlayer == null || markers == null)
                return;

            for (int i = 0; i < markers.Count && i < 5; i++)
            {
                var marker = markers[i];
                var info = CalculateMarkerInfo(marker, localPlayer, i + 1);
                if (info != null)
                    _markerInfos.Add(info);
            }
        }

        private TargetMarkerInfo CalculateMarkerInfo(object marker, object localPlayer, int markerNumber)
        {
            try
            {
                // Get marker position using reflection (since we don't have direct access to PointOfInterest class)
                var positionProp = marker.GetType().GetProperty("Position");
                var nameProp = marker.GetType().GetProperty("Name");
                
                if (positionProp?.GetValue(marker) is not Vector3D markerPos || 
                    nameProp?.GetValue(marker) is not string markerName)
                    return null;

                // Get local player position
                var playerPosProp = localPlayer.GetType().GetProperty("Position");
                if (playerPosProp?.GetValue(localPlayer) is not Vector3D playerPos)
                    return null;

                // Use the same position calculation as MainForm (with AbsoluteLocation adjustment)
                var absoluteLocation = _mainForm.GetAbsoluteLocation();
                var localPlayerPos = new Vector3D(
                    playerPos.X + absoluteLocation.X,
                    playerPos.Y + absoluteLocation.Y,
                    playerPos.Z + absoluteLocation.Z
                );
                
                var poiRenderPos = new Vector3D(
                    markerPos.X + absoluteLocation.X,
                    markerPos.Y + absoluteLocation.Y,
                    markerPos.Z + absoluteLocation.Z
                );

                // Calculate 2D distance on flat ground (ignore Z height differences)
                var dx = poiRenderPos.X - localPlayerPos.X;
                var dy = poiRenderPos.Y - localPlayerPos.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                int distanceMeters = (int)Math.Round(distance / 100);

                // Calculate bearing using the exact working formula with adjusted positions
                double deltaX = poiRenderPos.X - localPlayerPos.X;
                double deltaY = localPlayerPos.Y - poiRenderPos.Y;

                double radians = Math.Atan2(deltaY, deltaX);
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
                    Name = markerName
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

        private SKSize CalculateRequiredSize()
        {
            float pad = PADDING * ScaleFactor;
            var baseColumnWidths = new float[] { 40, 80, 80, 80 };
            var columnWidths = baseColumnWidths.Select(w => w * ScaleFactor).ToArray();
            float totalWidth = columnWidths.Sum();
            
            var rowHeight = (TextPaint.FontSpacing + 4f) * ScaleFactor;
            float totalHeight = (_markerInfos.Count + 1) * rowHeight; // +1 for header

            return new SKSize(totalWidth + pad * 2, totalHeight + pad * 2);
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

            var origin = new SKPoint(ClientRectangle.Left + pad, ClientRectangle.Top + pad);

            // Column headers
            var headers = new[] { "Mark", "Distance", "Bearing", "Range" };
            var baseColumnWidths = new float[] { 40, 80, 80, 80 };
            var columnWidths = baseColumnWidths.Select(w => w * ScaleFactor).ToArray();

            var rowHeight = (TextPaint.FontSpacing + 4f) * ScaleFactor;
            float totalWidth = columnWidths.Sum();
            float totalHeight = (_markerInfos.Count + 1) * rowHeight; // +1 for header

            // Auto-size the widget
            Size = new SKSize(totalWidth + pad * 2, totalHeight + pad * 2);

            if (_markerInfos.Count == 0)
            {
                // Draw standby message
                string standbyMsg = "No markers placed";
                canvas.DrawText(standbyMsg, origin.X + textOffsetX, origin.Y + rowHeight - textOffsetY, TextPaint);
                return;
            }

            // Draw headers
            float x = origin.X;
            float y = origin.Y;

            for (int i = 0; i < headers.Length; i++)
            {
                canvas.DrawText(headers[i], x + textOffsetX, y + rowHeight - textOffsetY, TextPaint);
                x += columnWidths[i];
            }

            // Draw header separator line
            canvas.DrawLine(origin.X, y + rowHeight, origin.X + totalWidth, y + rowHeight, BorderPaint);
            y += rowHeight;

            // Draw marker data rows
            foreach (var info in _markerInfos)
            {
                var values = new[]
                {
                    info.Number.ToString(),
                    $"{info.Distance}m",
                    $"{info.Bearing:F1}°",
                    info.RangingInfo
                };

                x = origin.X;
                for (int i = 0; i < values.Length; i++)
                {
                    canvas.DrawText(values[i], x + textOffsetX, y + rowHeight - textOffsetY, TextPaint);
                    canvas.DrawLine(x, y - rowHeight, x, y + rowHeight, BorderPaint);
                    x += columnWidths[i];
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
