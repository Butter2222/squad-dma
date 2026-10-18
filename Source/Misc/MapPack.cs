using SkiaSharp;
using squad_dma.Source.Misc;
using System.Text.Json;

namespace squad_dma
{
    /// <summary>
    /// The base layer of a map pack: a grid of tiles at the full-resolution zoom level.
    /// </summary>
    public sealed class MapTileSet
    {
        public string Root { get; init; }       // the pack folder
        public string HdDirectory => Path.Combine(Root, "hq"); // optional 256 px tile pyramid of the same map, 8192 px at its finest
        public string Pattern { get; init; }    // e.g. base/{z}/{x}_{y}.png
        public int Level { get; init; }         // the full-resolution zoom level
        public int TileSize { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }

        public int Columns => (Width + TileSize - 1) / TileSize;
        public int Rows => (Height + TileSize - 1) / TileSize;

        public string PathOf(int x, int y) =>
            Path.Combine(Root, Pattern.Replace("{z}", Level.ToString()).Replace("{x}", x.ToString()).Replace("{y}", y.ToString()));

        /// <summary>Draws every tile into one bitmap of the full map size. Tiles are decoded in parallel.</summary>
        public SKBitmap Stitch()
        {
            var tiles = new SKBitmap[Columns * Rows];
            try
            {
                Parallel.For(0, tiles.Length, i =>
                {
                    using var stream = File.OpenRead(PathOf(i % Columns, i / Columns));
                    tiles[i] = SKBitmap.Decode(stream) ?? throw new InvalidDataException($"Unreadable tile {PathOf(i % Columns, i / Columns)}");
                });

                var result = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul));
                using var canvas = new SKCanvas(result);
                for (int i = 0; i < tiles.Length; i++)
                    canvas.DrawBitmap(tiles[i], (i % Columns) * TileSize, (i / Columns) * TileSize);
                return result;
            }
            finally
            {
                foreach (var tile in tiles)
                    tile?.Dispose();
            }
        }

        /// <summary>The first missing tile, or null when the whole grid is on disk.</summary>
        public string FirstMissingTile()
        {
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Columns; x++)
                    if (!File.Exists(PathOf(x, y)))
                        return PathOf(x, y);
            return null;
        }
    }

    /// <summary>
    /// Reads a map pack folder: map.json (names and world bounds), the base layer tiles, and the height data that
    /// <see cref="TerrainHeightmap"/> loads.
    /// </summary>
    public static class MapPack
    {
        public const string BaseLayerId = "base";
        private const string OverrideFile = "mapsetup.json";

        /// <summary>
        /// Names the game or older versions of this tool used for a map that differ from the pack's own aliases.
        /// The SD_ and GE_ prefixed forms of every id are added automatically.
        /// </summary>
        private static readonly Dictionary<string, string[]> LegacyAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AlBasrah"] = new[] { "Al_Basrah" },
            ["BlackCoast"] = new[] { "Black_Coast" },
            ["FoolsRoad"] = new[] { "Fools_Road" },
            ["Jensen"] = new[] { "JensensRange", "Jensens_Range" },
            ["Logar"] = new[] { "Logar_Valley" },
            ["Pacific"] = new[] { "PacificProvingGrounds" },
            ["Sanxian"] = new[] { "Sanxian_Islands" },
            ["Tallil"] = new[] { "Tallil_Outskirts", "Operation_Desert_Storm", "SD_Operation_Desert_Storm", "SD_Tallil_Outskirts_Landscape" },
        };

        /// <summary>Folders of <paramref name="mapsDirectory"/> that look like map packs.</summary>
        public static IEnumerable<string> FindPacks(string mapsDirectory)
        {
            if (!Directory.Exists(mapsDirectory)) return Enumerable.Empty<string>();
            return Directory.EnumerateDirectories(mapsDirectory).Where(d => File.Exists(Path.Combine(d, "map.json"))).OrderBy(d => d, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Why a pack can't be used (no map.json, a missing tile...), or null when it is complete.</summary>
        public static string Validate(string packDirectory)
        {
            try
            {
                var info = Parse(packDirectory);
                if (HdTileMapImage.IsComplete(info.Layer.Tiles.HdDirectory))
                    return null; // the HD tiles are the map, the base tiles are only the fallback

                string missing = info.Layer.Tiles.FirstMissingTile();
                return missing == null ? null : $"missing tile {Path.GetRelativePath(packDirectory, missing)}";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static Map Load(string packDirectory)
        {
            var info = Parse(packDirectory);

            var config = new MapConfig
            {
                MapID = info.Aliases,
                X = info.X,
                Y = info.Y,
                Scale = info.Scale,
                MapLayers = new List<MapLayer> { info.Layer },
                PackDirectory = packDirectory,
            };
            ApplyCalibrationOverride(packDirectory, config);

            return new Map(info.Id.ToUpperInvariant(), config, Path.Combine(packDirectory, "map.json"));
        }

        #region Calibration
        private sealed class PackInfo
        {
            public string Id;
            public List<string> Aliases;
            public double X, Y, Scale;
            public MapLayer Layer;
        }

        private static PackInfo Parse(string packDirectory)
        {
            string path = Path.Combine(packDirectory, "map.json");
            if (!File.Exists(path)) throw new FileNotFoundException("map.json not found");

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            string id = root.GetProperty("Id").GetString();
            var aliases = new List<string> { id };
            AddAliases(aliases, id);
            if (root.TryGetProperty("Aliases", out var listed))
                foreach (var alias in listed.EnumerateArray())
                    AddAliases(aliases, alias.GetString());
            if (LegacyAliases.TryGetValue(id, out var legacy))
                foreach (var alias in legacy)
                    AddAliases(aliases, alias);

            // The layer the radar draws
            JsonElement layer = default;
            foreach (var candidate in root.GetProperty("RasterLayers").EnumerateArray())
            {
                if (candidate.GetProperty("Id").GetString() == BaseLayerId) { layer = candidate; break; }
            }
            if (layer.ValueKind != JsonValueKind.Object) throw new InvalidDataException("no base layer in map.json");

            var tiles = layer.GetProperty("Tiles");
            var tileSet = new MapTileSet
            {
                Root = packDirectory,
                Pattern = tiles.GetProperty("Pattern").GetString(),
                Level = tiles.GetProperty("MaxLevel").GetInt32(),
                TileSize = tiles.GetProperty("TileSize").GetInt32(),
                Width = tiles.GetProperty("Width").GetInt32(),
                Height = tiles.GetProperty("Height").GetInt32(),
            };
            if (tileSet.TileSize <= 0 || tileSet.Width <= 0 || tileSet.Height <= 0)
                throw new InvalidDataException("bad tile size in map.json");

            // The image spans WorldMin..WorldMax (cm, same space as UActor.Position), pixel y growing with world Y
            var cal = root.GetProperty("Calibration");
            double minX = cal.GetProperty("WorldMin").GetProperty("X").GetDouble();
            double minY = cal.GetProperty("WorldMin").GetProperty("Y").GetDouble();
            double maxX = cal.GetProperty("WorldMax").GetProperty("X").GetDouble();
            double maxY = cal.GetProperty("WorldMax").GetProperty("Y").GetDouble();
            if (maxX <= minX || maxY <= minY) throw new InvalidDataException("empty world bounds in map.json");

            double scaleX = tileSet.Width / (maxX - minX);
            double scaleY = tileSet.Height / (maxY - minY);
            double scale = (scaleX + scaleY) / 2.0;

            // SwapXY/InvertX/InvertY (only Jensen sets any) describe SquadCalc's own axis convention, not the picture:
            // Jensen's tiles show the same orientation as its old image, which the old x/y/scale calibration drew
            // unrotated in game. Only a real rotation would need handling here.
            if (cal.TryGetProperty("RotationDegrees", out var rotation) && rotation.GetDouble() != 0)
                Logger.Error($"[Maps] {id}: map.json asks for a rotated map, which the radar doesn't support. Drawn unrotated.");

            return new PackInfo
            {
                Id = id,
                Aliases = aliases,
                X = -minX * scale,
                Y = -minY * scale,
                Scale = scale,
                Layer = new MapLayer { MinHeight = -1_000_000f, Filename = null, Tiles = tileSet },
            };
        }

        private static void AddAliases(List<string> aliases, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            foreach (var form in new[] { name, "SD_" + name, "GE_" + name })
                if (!aliases.Contains(form, StringComparer.OrdinalIgnoreCase))
                    aliases.Add(form);
        }

        private static void ApplyCalibrationOverride(string packDirectory, MapConfig config)
        {
            string path = Path.Combine(packDirectory, OverrideFile);
            if (!File.Exists(path)) return;

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;
                config.X = root.GetProperty("x").GetDouble();
                config.Y = root.GetProperty("y").GetDouble();
                config.Scale = root.GetProperty("scale").GetDouble();
            }
            catch (Exception ex)
            {
                Logger.Error($"[Maps] Ignoring {OverrideFile} in {Path.GetFileName(packDirectory)}: {ex.Message}");
            }
        }

        public static void SaveCalibrationOverride(string packDirectory, MapConfig config)
        {
            var json = JsonSerializer.Serialize(new { x = config.X, y = config.Y, scale = config.Scale }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(packDirectory, OverrideFile), json);
        }
        #endregion
    }
}
