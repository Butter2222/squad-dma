using SkiaSharp;

namespace squad_dma
{
    internal static class SKPaints
    {
        #region Configurable Team Colors
        // These colors are now loaded from configuration but have original defaults shown in comments
        
        // Original Default: Green (#008000 - RGB: 0, 128, 0)
        public static SKColor Squad { get; set; } = SKColors.Green;
        
        // Original Default: Light Blue (#00BBFE - RGB: 0, 187, 254)
        public static SKColor Friendly { get; set; } = new SKColor(0, 187, 254);
        
        // Updated: White for enemy players
        public static SKColor EnemyPlayer { get; set; } = SKColors.White;
        
        // Original Default: Purple (#800080 - RGB: 128, 0, 128)
        public static SKColor Unknown { get; set; } = SKColors.Purple;
        
        // White for unclaimed vehicles (used by other parts of software)
        public static SKColor Vehicle { get; set; } = SKColors.White;
        
        // Friendly blue color for friendly vehicles
        public static SKColor FriendlyVehicle { get; set; } = new SKColor(0, 187, 254); // Same as Friendly
        
        // Yellow for enemy vehicles
        public static SKColor EnemyVehicle { get; set; } = SKColors.Yellow;
        
        // Original Default: White (#FFFFFF - RGB: 255, 255, 255)
        public static SKColor UnclaimedVehicle { get; set; } = SKColors.White;
        
        // Original Default: Orange (#FFA500 - RGB: 255, 165, 0)
        public static SKColor RegularProjectile { get; set; } = SKColors.Orange;
        
        // Original Default: Orange (#FFA500 - RGB: 255, 165, 0)
        public static SKColor AAProjectile { get; set; } = SKColors.Orange;
        
        // Original Default: Magenta (#FF00FF - RGB: 255, 0, 255)
        public static SKColor SmallProjectile { get; set; } = SKColors.Magenta;
        
        // Original Default: Light Red (#FF6B6B - RGB: 255, 107, 107)
        public static SKColor EnemyPlayerDistanceText { get; set; } = new SKColor(255, 107, 107);
        
        // Original Default: White (#FFFFFF - RGB: 255, 255, 255)
        public static SKColor VehicleDistanceText { get; set; } = SKColors.White;
        
        // Original Default: White (#FFFFFF - RGB: 255, 255, 255)
        public static SKColor DeadMarker { get; set; } = SKColors.White;
        
        // Original Default: White (#FFFFFF - RGB: 255, 255, 255)
        public static SKColor AdminMarker { get; set; } = SKColors.White;

        // Legacy colors for compatibility
        public static SKColor Enemy => EnemyPlayer;
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
            Typeface = CustomFonts.SKFontFamilyRegular,
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

        #region Color Configuration Methods
        /// <summary>
        /// Loads colors from configuration settings.
        /// </summary>
        public static void LoadColorsFromConfig(RadarColors config)
        {
            Squad = config.SquadMembers.ToSKColor();
            Friendly = config.FriendlyPlayers.ToSKColor();
            EnemyPlayer = config.EnemyPlayers.ToSKColor();
            Unknown = config.UnknownPlayers.ToSKColor();
            FriendlyVehicle = config.FriendlyVehicles.ToSKColor();
            Vehicle = config.UnclaimedVehicles.ToSKColor(); // Vehicle now represents unclaimed vehicles
            EnemyVehicle = config.EnemyVehicles.ToSKColor();
            UnclaimedVehicle = config.UnclaimedVehicles.ToSKColor();
            RegularProjectile = config.RegularProjectiles.ToSKColor();
            AAProjectile = config.AAProjectiles.ToSKColor();
            SmallProjectile = config.SmallProjectiles.ToSKColor();
            EnemyPlayerDistanceText = config.EnemyPlayerDistanceText.ToSKColor();
            VehicleDistanceText = config.VehicleDistanceText.ToSKColor();
            DeadMarker = config.DeadMarkers.ToSKColor();
            AdminMarker = config.AdminMarkers.ToSKColor();
        }

        /// <summary>
        /// Resets colors to default values.
        /// </summary>
        public static void ResetToDefaults()
        {
            var defaultConfig = new RadarColors();
            LoadColorsFromConfig(defaultConfig);
        }
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
