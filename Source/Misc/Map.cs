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

        /// <summary>Folder of the map pack this config was built from, or null for a legacy single-image map.</summary>
        [JsonIgnore]
        public string PackDirectory { get; set; }

        public static MapConfig LoadFromFile(string file)
        {
            var json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<MapConfig>(json, _jsonOptions);
        }

        public void Save(Map map)
        {
            if (PackDirectory != null)
            {
                // The pack's own map.json is never rewritten: hand-tuned values go to a small file next to it
                MapPack.SaveCalibrationOverride(PackDirectory, this);
                return;
            }

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

        /// <summary>Tile grid of a map pack layer. When set, the image is built from the tiles instead of <see cref="Filename"/>.</summary>
        [JsonIgnore]
        public MapTileSet Tiles { get; set; }

        /// <summary>
        /// The image the radar draws: the HD tile pyramid when the pack has a complete one, otherwise one bitmap
        /// from the base tiles or the single image file.
        /// </summary>
        public MapImage LoadImage()
        {
            if (Tiles != null && HdTileMapImage.IsComplete(Tiles.HdDirectory))
                return new HdTileMapImage(Tiles.HdDirectory, Tiles.Width, Tiles.Height);
            return new BitmapMapImage(LoadBitmap());
        }

        /// <summary>Decodes the layer into one immutable bitmap, from its tiles or its single image file.</summary>
        public SKBitmap LoadBitmap()
        {
            var bitmap = Tiles != null ? Tiles.Stitch() : DecodeFile(Filename);
            bitmap.SetImmutable();
            return bitmap;
        }

        private static SKBitmap DecodeFile(string path)
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
            return SKBitmap.Decode(stream);
        }
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