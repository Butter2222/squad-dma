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
        public int MapLayerIndex;
        public SKRect Bounds;
        public float XScale;
        public float YScale;
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
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("scale")]
        public float Scale { get; set; }

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
        public float X = 0;
        public float Y = 0;
        public float Height = 0;


        public SKPoint GetPoint(float xOff = 0, float yOff = 0)
        {
            return new SKPoint(X + xOff, Y + yOff);
        }

        private SKPoint GetAimlineEndpoint(double radians, float aimlineLength)
        {
            aimlineLength *= UIScale;
            return new SKPoint((float)(this.X + Math.Cos(radians) * aimlineLength), (float)(this.Y + Math.Sin(radians) * aimlineLength));
        }

        public void DrawPlayerMarker(SKCanvas canvas, UActor player, int aimlineLength, SKColor? color = null)
        {
            var radians = player.Rotation.X.ToRadians();
            SKPaint paint = player.GetEntityPaint();

            if (color.HasValue)
            {
                paint.Color = color.Value;
            }

            // Draw the outline for the player marker
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

            using (var outlinePaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = size,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2 * UIScale,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            })
            using (var textPaint = new SKPaint
            {
                Color = SKColors.Cyan,
                TextSize = size,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            })
            {
                SKRect textBounds = new SKRect();
                textPaint.MeasureText(text, ref textBounds);

                float yOffset = textBounds.Height / 2 - textBounds.Bottom;

                canvas.DrawText(text, center.X, center.Y - yOffset, outlinePaint);
                canvas.DrawText(text, center.X, center.Y - yOffset, textPaint);
            }
        }

        public void DrawProjectile(SKCanvas canvas, UActor projectile) // Normal Projectiles Like Mortars / CAS Rockets / etc
        {
            SKPaint paint = projectile.GetProjectilePaint();

            float crosshairSize = 8 * UIScale;
            float crosshairThickness = 2 * UIScale;

            SKPoint center = this.GetPoint();

            canvas.DrawLine(
                center.X - crosshairSize, center.Y,
                center.X + crosshairSize, center.Y,
                new SKPaint
                {
                    Color = paint.Color,
                    StrokeWidth = crosshairThickness,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke
                }
            );

            canvas.DrawLine(
                center.X, center.Y - crosshairSize,
                center.X, center.Y + crosshairSize,
                new SKPaint
                {
                    Color = paint.Color,
                    StrokeWidth = crosshairThickness,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke
                }
            );
        }

        public void DrawTechMarker(SKCanvas canvas, UActor actor)
        {
            var scale = 0.2f; // Default scale for most icons

            if (actor.ActorType == ActorType.Mine)
            {
                scale /= 1.5f; // Shrink
            }
            else if (actor.ActorType == ActorType.Drone)
            {
                scale *= 1.5f; // Enlarge
            }

            if (!Names.BitMaps.TryGetValue(actor.ActorType, out SKBitmap skBitMap))
            {
                return;
            }
            var icon = skBitMap;

            var iconWidth = icon.Width * scale;
            var iconHeight = icon.Height * scale;
            var point = this.GetPoint();
            var rotation = actor.Rotation.X + 90;
            if (Names.DoNotRotate.Contains(actor.ActorType))
            {
                rotation = 0;
            }
            else if (Names.RotateBy45Degrees.Contains(actor.ActorType))
            {
                rotation -= 45;
            }
            SKMatrix matrix = SKMatrix.CreateTranslation(point.X, point.Y);
            matrix = SKMatrix.Concat(matrix, SKMatrix.CreateRotationDegrees(rotation));
            matrix = SKMatrix.Concat(matrix, SKMatrix.CreateTranslation(-iconWidth / 2, -iconHeight / 2));

            canvas.Save();
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(icon, SKRect.Create(iconWidth, iconHeight), SKPaints.PaintBitmap);
            canvas.Restore();
        }

        public void DrawActorText(SKCanvas canvas, UActor actor, string[] lines)
        {
            if (lines == null || lines.Length == 0)
                return;

            SKPaint textPaint = actor.GetTextPaint();
            SKPaint outlinePaint = Extensions.GetTextOutlinePaint();
            SKPoint iconPosition = this.GetPoint(0, 0);

            float horizontalOffset = 15 * UIScale; // Adjust this value for horizontal separation
            float verticalOffset = 5 * UIScale;   // Adjust this value for vertical separation

            SKPoint textPosition = new SKPoint(iconPosition.X + horizontalOffset, iconPosition.Y + verticalOffset);

            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line?.Trim()))
                    continue;

                canvas.DrawText(line, textPosition, outlinePaint);
                canvas.DrawText(line, textPosition, textPaint);

                textPosition.Y += 12 * UIScale;
            }
        }

        public void DrawToolTip(SKCanvas canvas, UActor actor)
        {
            if (!actor.IsAlive)
            {
                //DrawCorpseTooltip(canvas, player);
                return;
            }

            DrawHostileTooltip(canvas, actor);
        }

        private void DrawHostileTooltip(SKCanvas canvas, UActor actor)
        {
            var lines = new List<string>();

            lines.Insert(0, actor.Name);

            DrawToolTip(canvas, string.Join("\n", lines));
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

            var left = X + padding;
            var top = Y - padding;
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
                //DrawCorpseTooltip(canvas, player);
                return;
            }

            var lines = new List<string>();

            // Add the player name and distance to the tooltip
            lines.Insert(0, actor.Name);
            lines.Insert(1, $"Distance: {distanceText}");

            DrawToolTip(canvas, string.Join("\n", lines));
        }
    }
}