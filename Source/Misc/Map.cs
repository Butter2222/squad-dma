using SkiaSharp;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Numerics;

namespace squad_dma
{
    public class Map
    {
        public readonly string Name;
        public readonly MapConfig ConfigFile;
        public readonly string ConfigFilePath;

        public Map(string name, MapConfig config, string configPath)
        {
            Name = name;
            ConfigFile = config;
            ConfigFilePath = configPath;
        }
    }

    public class MapParameters
    {
        public float UIScale;
        public float TechScale;
        public int MapLayerIndex;
        public SKRect Bounds;
        public double XScale;
        public double YScale;
    }

    public class MapConfig
    {
        [JsonIgnore]
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
        };

        [JsonPropertyName("mapID")]
        public List<string> MapID { get; set; } // List of possible map IDs

        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("scale")]
        public double Scale { get; set; }

        [JsonPropertyName("mapLayers")]
        public List<MapLayer> MapLayers { get; set; }

        public static MapConfig LoadFromFile(string file)
        {
            var json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<MapConfig>(json, _jsonOptions);
        }

        public void Save(Map map)
        {
            var json = JsonSerializer.Serialize(this, _jsonOptions);
            File.WriteAllText(map.ConfigFilePath, json);
        }
    }

    public class MapLayer
    {
        [JsonPropertyName("minHeight")]
        public float MinHeight { get; set; }

        [JsonPropertyName("filename")]
        public string Filename { get; set; }
    }

    public struct MapPosition
    {
        public MapPosition() { }
        public float UIScale = 0;
        public float TechScale = 0;
        public double X = 0;
        public double Y = 0;
        public double Height = 0;

        private static SKPaint _projectileOutlinePaint;
        private static SKPaint _projectileFillPaint;
        
        // Cached SKPaint objects for tech markers
        private static SKPaint _techMarkerPaint;
        private static SKPaint _techMarkerFriendlyPaint;
        private static SKPaint _techMarkerEnemyPaint;
        private static SKPaint _techMarkerOutlinePaint;
        private static SKPaint _techMarkerFriendlyOutlinePaint;
        private static SKPaint _techMarkerEnemyOutlinePaint;
        private static bool _paintsInitialized = false;

        public SKPoint GetPoint(float xOff = 0, float yOff = 0)
        {
            double finalX = X + xOff;
            double finalY = Y + yOff;
            return new SKPoint((float)finalX, (float)finalY);
        }

        private static void InitializeTechMarkerPaints()
        {
            if (_paintsInitialized)
                return;

            _techMarkerPaint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };

            _techMarkerFriendlyPaint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High,
                ColorFilter = SKColorFilter.CreateBlendMode(SKPaints.Friendly, SKBlendMode.Modulate)
            };

            _techMarkerEnemyPaint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High,
                ColorFilter = SKColorFilter.CreateBlendMode(SKPaints.EnemyVehicle, SKBlendMode.Modulate)
            };

            // Simple, tight outline paints
            _techMarkerOutlinePaint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High,
                Color = SKColors.Black,
                ImageFilter = SKImageFilter.CreateDropShadow(0, 0, 0.5f, 0.5f, SKColors.Black)
            };

            _techMarkerFriendlyOutlinePaint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High,
                Color = SKColors.White,
                ImageFilter = SKImageFilter.CreateDropShadow(0, 0, 0.5f, 0.5f, SKColors.White)
            };

            _techMarkerEnemyOutlinePaint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High,
                Color = SKColors.Black,
                ImageFilter = SKImageFilter.CreateDropShadow(0, 0, 0.5f, 0.5f, SKColors.Black)
            };

            _paintsInitialized = true;
        }

        private SKPoint GetAimlineEndpoint(double radians, float aimlineLength)
        {
            double scaledLength = aimlineLength * UIScale;
            double finalX = this.X + Math.Cos(radians) * scaledLength;
            double finalY = this.Y + Math.Sin(radians) * scaledLength;
            return new SKPoint((float)finalX, (float)finalY);
        }

        public void DrawPlayerMarker(SKCanvas canvas, UActor player, int aimlineLength, SKColor? color = null)
        {
            var radians = (double)player.Rotation.X.ToRadians();
            SKPaint paint = player.GetActorPaint();

            if (color.HasValue)
            {
                paint.Color = color.Value;
            }

            SKPaint outlinePaint = new SKPaint
            {
                Color = SKColors.Black,
                StrokeWidth = paint.StrokeWidth + 2f * UIScale,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };

            var size = 6 * UIScale;
            canvas.DrawCircle(this.GetPoint(), size, outlinePaint);
            canvas.DrawCircle(this.GetPoint(), size, paint);

            var aimlineEnd = this.GetAimlineEndpoint(radians, aimlineLength);
            canvas.DrawLine(this.GetPoint(), aimlineEnd, outlinePaint);
            canvas.DrawLine(this.GetPoint(), aimlineEnd, paint);
        }
        public void DrawProjectileAA(SKCanvas canvas, UActor projectile) // AntiAir Projectiles for Steel Division
        {
            float size = 16 * UIScale;
            SKPoint center = this.GetPoint();
            string text = "AA";

            var outlinePaint = SKPaints.TextOutline.Clone();
            var textPaint = SKPaints.ProjectileAA.Clone();
            
            textPaint.Color = projectile.GetTextPaint().Color;

            outlinePaint.TextSize = size;
            textPaint.TextSize = size;
            outlinePaint.TextAlign = SKTextAlign.Center;
            textPaint.TextAlign = SKTextAlign.Center;
            outlinePaint.Typeface = textPaint.Typeface;

            SKRect textBounds = new SKRect();
            textPaint.MeasureText(text, ref textBounds);

            float x = center.X;
            float y = center.Y - (textBounds.Height / 2) + (textBounds.Height / 2);

            canvas.DrawText(text, x, y, outlinePaint);
            canvas.DrawText(text, x, y, textPaint);
        }

        public void DrawProjectile(SKCanvas canvas, UActor projectile) // Normal Projectiles Like Mortars / CAS Rockets / etc
        {
            float size = 6 * UIScale;
            SKPoint center = this.GetPoint();

            var outlinePaint = SKPaints.ProjectileOutline.Clone();
            var fillPaint = SKPaints.ProjectileFill.Clone();

            fillPaint.Color = projectile.GetTextPaint().Color;

            outlinePaint.StrokeWidth = 4 * UIScale;
            fillPaint.StrokeWidth = 2 * UIScale;

            canvas.Save();

            canvas.DrawLine(center.X - size, center.Y, center.X + size, center.Y, outlinePaint);
            canvas.DrawLine(center.X - size, center.Y, center.X + size, center.Y, fillPaint);

            canvas.DrawLine(center.X, center.Y - size, center.X, center.Y + size, outlinePaint);
            canvas.DrawLine(center.X, center.Y - size, center.X, center.Y + size, fillPaint);

            canvas.Restore();
        }

        public void DrawTechMarker(SKCanvas canvas, UActor actor)
        {
            if (!Names.BitMaps.TryGetValue(actor.ActorType, out SKBitmap icon))
                return;

            // Initialize cached paints if not already done
            InitializeTechMarkerPaints();

            float scale = 0.2f * TechScale;
            if (actor.ActorType == ActorType.Mine)
                scale /= 1.5f;
            else if (actor.ActorType == ActorType.Drone)
                scale *= 1.5f;

            float iconWidth = icon.Width * scale;
            float iconHeight = icon.Height * scale;
            SKPoint center = this.GetPoint();

            float rotation = Names.DoNotRotate.Contains(actor.ActorType) ? 0 
                           : Names.RotateBy45Degrees.Contains(actor.ActorType) ? (float)(actor.Rotation.X + 45) 
                           : (float)(actor.Rotation.X + 90);

            SKMatrix matrix = SKMatrix.CreateTranslation(center.X, center.Y);
            matrix = SKMatrix.Concat(matrix, SKMatrix.CreateRotationDegrees(rotation));
            matrix = SKMatrix.Concat(matrix, SKMatrix.CreateTranslation(-iconWidth / 2, -iconHeight / 2));

            canvas.Save();
            canvas.SetMatrix(matrix);

            // Simple two-pass rendering: outline first, then main icon
            if (Names.Vehicles.Contains(actor.ActorType) || Names.Deployables.Contains(actor.ActorType))
            {
                if (actor.IsFriendly())
                {
                    // Friendly: white outline, then blue-tinted icon
                    canvas.DrawBitmap(icon, SKRect.Create(iconWidth, iconHeight), _techMarkerFriendlyOutlinePaint);
                    canvas.DrawBitmap(icon, SKRect.Create(iconWidth, iconHeight), _techMarkerFriendlyPaint);
                }
                else if (actor.IsEnemy())
                {
                    // Enemy: black outline, then red-tinted icon
                    canvas.DrawBitmap(icon, SKRect.Create(iconWidth, iconHeight), _techMarkerEnemyOutlinePaint);
                    canvas.DrawBitmap(icon, SKRect.Create(iconWidth, iconHeight), _techMarkerEnemyPaint);
                }
                else
                {
                    // Unclaimed/neutral: black outline, then original icon
                    canvas.DrawBitmap(icon, SKRect.Create(iconWidth, iconHeight), _techMarkerOutlinePaint);
                    canvas.DrawBitmap(icon, SKRect.Create(iconWidth, iconHeight), _techMarkerPaint);
                }
            }
            else
            {
                // Other actor types: simple black outline
                canvas.DrawBitmap(icon, SKRect.Create(iconWidth, iconHeight), _techMarkerOutlinePaint);
                canvas.DrawBitmap(icon, SKRect.Create(iconWidth, iconHeight), _techMarkerPaint);
            }

            canvas.Restore();
        }

        public void DrawActorText(SKCanvas canvas, UActor actor, string[] lines)
        {
            if (lines == null || lines.Length == 0)
                return;

            SKPaint textPaint = SKPaints.TextBase.Clone();
            // Enemy players: #ff6b6b, Enemy vehicles: WHITE, Everything else: white
            if (actor.ActorType == ActorType.Player && actor.IsEnemy())
            {
                textPaint.Color = SKPaints.EnemyPlayer; // #ff6b6b for enemy player distance text
            }
            else
            {
                textPaint.Color = SKColors.White; // White for enemy vehicles and everything else
            }
            textPaint.TextSize = 12 * UIScale * 1.3f;

            SKPaint outlinePaint = SKPaints.TextOutline.Clone();
            outlinePaint.TextSize = 12 * UIScale * 1.3f;
            outlinePaint.StrokeWidth = 2 * UIScale;

            SKPoint iconPosition = this.GetPoint(0, 0);
            SKPoint textPosition = new SKPoint(
                iconPosition.X + (15 * UIScale),
                iconPosition.Y + (5 * UIScale)
            );

            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line?.Trim()))
                    continue;

                canvas.DrawText(line, textPosition, outlinePaint);
                canvas.DrawText(line, textPosition, textPaint);
                textPosition.Y += 12 * UIScale * 1.3f;
            }
        }

        public void DrawVehicleDistance(SKCanvas canvas, UActor actor, string distanceText)
        {
            if (string.IsNullOrEmpty(distanceText) || actor.IsFriendly())
                return;

            SKPaint textPaint = SKPaints.TextBase.Clone();
            textPaint.Color = SKColors.White; // Force WHITE for enemy vehicle distance text
            textPaint.TextSize = 13 * UIScale * 1.1f;
            textPaint.TextAlign = SKTextAlign.Left;
            textPaint.Typeface = CustomFonts.SKFontFamilyRegular; 
            textPaint.SubpixelText = true; 

            SKPaint outlinePaint = SKPaints.TextOutline.Clone();
            outlinePaint.TextSize = 13 * UIScale * 1.1f;
            outlinePaint.StrokeWidth = 2.8f * UIScale; 
            outlinePaint.TextAlign = SKTextAlign.Left;
            outlinePaint.Typeface = CustomFonts.SKFontFamilyRegular; 
            outlinePaint.SubpixelText = true; 

            SKPoint iconPosition = this.GetPoint(0, 0);
            
            float techMarkerSize = 0.2f * TechScale;
            if (Names.BitMaps.TryGetValue(actor.ActorType, out SKBitmap icon))
            {
                float iconWidth = icon.Width * techMarkerSize;
                float iconHeight = icon.Height * techMarkerSize;
                
                // Position at top-right of the tech marker with small offset
                SKPoint textPosition = new SKPoint(
                    iconPosition.X + (iconWidth * 0.5f) + (5 * UIScale), // Right edge + larger offset
                    iconPosition.Y - (iconHeight * 0.5f) + (14 * UIScale * 1.3f) - (4 * UIScale) // Top edge + text height, moved up more
                );

                canvas.DrawText(distanceText, textPosition, outlinePaint);
                canvas.DrawText(distanceText, textPosition, textPaint);
            }
            else
            {
                // Fallback positioning if no icon found
                SKPoint textPosition = new SKPoint(
                    iconPosition.X + (15 * UIScale),
                    iconPosition.Y - (8 * UIScale)
                );

                canvas.DrawText(distanceText, textPosition, outlinePaint);
                canvas.DrawText(distanceText, textPosition, textPaint);
            }
        }

        public void DrawVehicleDistanceIndicator(SKCanvas canvas, UActor actor, string distanceText)
        {
            if (string.IsNullOrEmpty(distanceText))
                return;

            // Only show for enemy vehicles
            if (actor.IsFriendly())
                return;

            SKPoint iconPosition = this.GetPoint(0, 0);
            
            float offsetX = 8 * UIScale;
            float offsetY = -12 * UIScale;
            SKPoint textPosition = new SKPoint(
                iconPosition.X + offsetX,
                iconPosition.Y + offsetY
            );

            using (var textPaint = new SKPaint
            {
                Color = SKColors.White,
                TextSize = 13 * UIScale,
                TextAlign = SKTextAlign.Left,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
                FilterQuality = SKFilterQuality.High
            })
            using (var outlinePaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 13 * UIScale,
                TextAlign = SKTextAlign.Left,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3 * UIScale,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
                FilterQuality = SKFilterQuality.High
            })
            {
                canvas.DrawText(distanceText, textPosition.X, textPosition.Y, outlinePaint);
                canvas.DrawText(distanceText, textPosition.X, textPosition.Y, textPaint);
            }
        }

        private void DrawToolTip(SKCanvas canvas, string tooltipText)
        {
            var lines = tooltipText.Split('\n');
            var maxWidth = 0f;

            foreach (var line in lines)
            {
                var width = SKPaints.TextBase.MeasureText(line);
                maxWidth = Math.Max(maxWidth, width);
            }

            var textSpacing = 12 * UIScale;
            var padding = 3 * UIScale;

            var height = lines.Length * textSpacing;

            var left = (float)X + padding;
            var top = (float)Y - padding;
            var right = left + maxWidth + padding * 2;
            var bottom = top + height + padding * 2;

            var backgroundRect = new SKRect(left, top, right, bottom);
            canvas.DrawRect(backgroundRect, SKPaints.PaintTransparentBacker);

            var y = bottom - (padding * 1.5f);
            foreach (var line in lines)
            {
                canvas.DrawText(line, left + padding, y, SKPaints.TextBase);
                y -= textSpacing;
            }
        }

        public void DrawToolTip(SKCanvas canvas, UActor actor, string distanceText)
        {
            if (!actor.IsAlive)
            {
                return;
            }

            var lines = new List<string>();

            lines.Insert(0, actor.Name);
            lines.Insert(1, $"Distance: {distanceText}");

            DrawToolTip(canvas, string.Join("\n", lines));
        }
    }
}