using SkiaSharp;

namespace squad_dma
{
    internal static class SKPaints
    {
        #region Team Colors
        public static readonly SKColor Friendly = SKColors.Green;
        public static readonly SKColor Enemy = SKColors.Red;
        public static readonly SKColor Unknown = SKColors.Purple;

        public static readonly SKColor DefaultTextColor = new SKColor(233, 0, 255, 255);

        #endregion

        #region Radar Paints
        public static readonly SKPaint PaintBase = new SKPaint() {
            Color = SKColors.WhiteSmoke,
            StrokeWidth = 3,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };

        public static readonly SKPaint TextBase = new SKPaint()
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.WhiteSmoke,
            TextSize = 13,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial"),
            FilterQuality = SKFilterQuality.High
        };

        public static readonly SKPaint TextBaseOutline = new SKPaint()
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Black,
            StrokeWidth = 1,
            TextSize = 13,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial"),
            FilterQuality = SKFilterQuality.High
        };
        #endregion

        #region Aimview Paints
        public static readonly SKPaint PaintTransparentBacker = new SKPaint()
        {
            Color = SKColors.Black.WithAlpha(0xBE), // Transparent backer
            StrokeWidth = 1,
            Style = SKPaintStyle.Fill,
        };
        #endregion

        #region Render/Misc Paints
        public static readonly SKPaint PaintBitmap = new SKPaint()
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };
        public static readonly SKPaint TextRadarStatus = new SKPaint()
        {
            Color = SKColors.Red,
            IsStroke = false,
            TextSize = 48,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        #endregion
}

public class PaintColor {
        public struct Colors {
            public byte A { get; set; }
            public byte R { get; set; }
            public byte G { get; set; }
            public byte B { get; set; }
        }
    }
}
