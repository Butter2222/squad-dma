using SkiaSharp;

namespace squad_dma
{
    internal static class SKPaints
    {
        #region Team Colors
        public static readonly SKColor Friendly = new SKColor(0, 187, 254);
        public static readonly SKColor Enemy = SKColors.Red;
        public static readonly SKColor EnemyPlayer = new SKColor(255, 107, 107); // #ff6b6b - Light red for enemy players
        public static readonly SKColor EnemyVehicle = new SKColor(255, 107, 107); // #ff6b6b - Light red for enemy vehicles
        public static readonly SKColor Unknown = SKColors.Purple;
        public static readonly SKColor Squad = SKColors.Green;
        public static readonly SKColor SmallProjectile = SKColors.Magenta; 
        public static readonly SKColor Vehicle = SKColors.White;

        public static readonly SKColor DefaultTextColor = SKColors.White;

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
            TextSize = 12,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            SubpixelText = true,
            Typeface = CustomFonts.SKFontFamilyRegular,
            FilterQuality = SKFilterQuality.High
        };

        public static readonly SKPaint TextOutline = new SKPaint()
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Black,
            StrokeWidth = 2,
            TextSize = 12,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            SubpixelText = true,
            Typeface = CustomFonts.SKFontFamilyRegular,
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
            Typeface = CustomFonts.SKFontFamilyBold,
            TextAlign = SKTextAlign.Center
        };

        public static readonly SKPaint TextActor = new SKPaint()
        {
            SubpixelText = true,
            Color = SKColors.WhiteSmoke,
            IsStroke = false,
            TextSize = 12,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            Typeface = CustomFonts.SKFontFamilyRegular,
            FilterQuality = SKFilterQuality.High
        };

        public static readonly SKPaint ProjectileAA = new SKPaint()
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            FilterQuality = SKFilterQuality.High,
            TextSize = 16f,
            TextAlign = SKTextAlign.Center,
            Typeface = CustomFonts.SKFontFamilyRegular
        };

        public static readonly SKPaint ProjectileOutline = new SKPaint()
        {
            Color = SKColors.Black,
            StrokeWidth = 4,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            FilterQuality = SKFilterQuality.High
        };

        public static readonly SKPaint ProjectileFill = new SKPaint()
        {
            StrokeWidth = 2,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            FilterQuality = SKFilterQuality.High
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
