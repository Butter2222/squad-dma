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

        public SKPoint GetPoint(float xOff = 0, float yOff = 0)
        {
            double finalX = X + xOff;
            double finalY = Y + yOff;
            return new SKPoint((float)finalX, (float)finalY);
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