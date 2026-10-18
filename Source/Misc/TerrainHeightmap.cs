using SkiaSharp;
using squad_dma.Source.Misc;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace squad_dma
{
    /// <summary>
    /// Ground height of the selected map, sampled from the height grid that ships with the map pack.
    ///
    /// Two sources, best first:
    ///  - GeometryV2/Ground/ground.f32: raw float metres, row-major (row = world Y, column = world X), 1 m (0.75 m on Anvil) steps.
    ///  - heightmap.png: 8-bit pack, height = R * RedScale + B * BlueScale + HeightOffset, 1 pixel per step.
    ///
    /// Positions are in the same space as UActor.Position (cm); heights come back in cm too. The grid is the bare
    /// landscape: buildings, trees and other props are not in it.
    /// </summary>
    public sealed class TerrainHeightmap
    {
        /// <summary>The terrain of the map on screen, or null while it loads or when the map has no height data.</summary>
        public static TerrainHeightmap Current => _current;

        private static volatile TerrainHeightmap _current;
        private static int _loadGeneration;

        private readonly float[] _heights;   // metres
        private readonly int _width, _height;
        private readonly double _minX, _minY, _stepX, _stepY; // metres

        public string MapId { get; }
        public string Source { get; }

        private TerrainHeightmap(string mapId, string source, float[] heights, int width, int height,
            double minX, double minY, double stepX, double stepY)
        {
            MapId = mapId;
            Source = source;
            _heights = heights;
            _width = width;
            _height = height;
            _minX = minX;
            _minY = minY;
            _stepX = stepX;
            _stepY = stepY;
        }

        #region Selecting
        /// <summary>
        /// Makes <paramref name="map"/>'s terrain the current one. The grid is read in the background: the previous
        /// map's terrain is dropped right away and the new one appears when it is ready.
        /// </summary>
        public static void Select(Map map)
        {
            int generation = Interlocked.Increment(ref _loadGeneration);
            _current = null;

            string dir = map?.ConfigFile?.PackDirectory;
            if (string.IsNullOrEmpty(dir)) return;

            Task.Run(() =>
            {
                try
                {
                    var terrain = Load(map.Name, dir);
                    if (terrain == null)
                    {
                        Logger.Info($"[Terrain] {map.Name}: no height data in the map pack, ground is treated as flat");
                        return;
                    }

                    if (generation == Volatile.Read(ref _loadGeneration))
                    {
                        _current = terrain;
                        Logger.Info($"[Terrain] {map.Name}: loaded {terrain._width}x{terrain._height} grid from {terrain.Source}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"[Terrain] {map.Name}: could not load height data: {ex.Message}");
                }
            });
        }

        public static void Clear()
        {
            Interlocked.Increment(ref _loadGeneration);
            _current = null;
        }
        #endregion

        #region Sampling
        /// <summary>Ground height in cm at a world position in cm. False outside the grid or where the grid has no data.</summary>
        public bool TryGetHeightCm(double worldXCm, double worldYCm, out double heightCm)
        {
            heightCm = 0;

            double gx = (worldXCm / 100.0 - _minX) / _stepX;
            double gy = (worldYCm / 100.0 - _minY) / _stepY;
            if (double.IsNaN(gx) || double.IsNaN(gy) || gx < 0 || gy < 0 || gx > _width - 1 || gy > _height - 1)
                return false;

            int x0 = (int)gx, y0 = (int)gy;
            int x1 = Math.Min(x0 + 1, _width - 1), y1 = Math.Min(y0 + 1, _height - 1);
            double fx = gx - x0, fy = gy - y0;

            float h00 = _heights[y0 * _width + x0], h10 = _heights[y0 * _width + x1];
            float h01 = _heights[y1 * _width + x0], h11 = _heights[y1 * _width + x1];
            if (!float.IsFinite(h00) || !float.IsFinite(h10) || !float.IsFinite(h01) || !float.IsFinite(h11))
                return false;

            double top = h00 + (h10 - h00) * fx;
            double bottom = h01 + (h11 - h01) * fx;
            heightCm = (top + (bottom - top) * fy) * 100.0;
            return true;
        }

        /// <summary>
        /// Ground height difference in metres, target minus shooter, between two world positions. Zero (flat ground)
        /// when there is no terrain, or either position is off the grid.
        /// </summary>
        public static double HeightDifferenceMeters(double shooterXCm, double shooterYCm, double targetXCm, double targetYCm)
        {
            var terrain = _current;
            if (terrain == null) return 0.0;
            if (!terrain.TryGetHeightCm(shooterXCm, shooterYCm, out double shooter)) return 0.0;
            if (!terrain.TryGetHeightCm(targetXCm, targetYCm, out double target)) return 0.0;
            return (target - shooter) / 100.0;
        }
        #endregion

        #region Loading
        private static TerrainHeightmap Load(string mapId, string dir)
        {
            return TryLoadGround(mapId, dir) ?? TryLoadHeightmapPng(mapId, dir);
        }

        private static TerrainHeightmap TryLoadGround(string mapId, string dir)
        {
            string raw = Path.Combine(dir, "GeometryV2", "Ground", "ground.f32");
            string meta = Path.Combine(dir, "GeometryV2", "Ground", "runtime.json");
            if (!File.Exists(raw) || !File.Exists(meta)) return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(meta));
            var root = doc.RootElement;
            int width = root.GetProperty("Width").GetInt32();
            int height = root.GetProperty("Height").GetInt32();
            double minX = root.GetProperty("MinX").GetDouble();
            double minY = root.GetProperty("MinY").GetDouble();
            double stepX = root.GetProperty("StepX").GetDouble();
            double stepY = root.GetProperty("StepY").GetDouble();

            long expected = (long)width * height * sizeof(float);
            if (width < 2 || height < 2 || stepX <= 0 || stepY <= 0 || new FileInfo(raw).Length != expected)
            {
                Logger.Error($"[Terrain] {mapId}: ground.f32 does not match its runtime.json, ignored");
                return null;
            }

            var heights = new float[width * height];
            using (var stream = new FileStream(raw, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan))
                stream.ReadExactly(MemoryMarshal.AsBytes(heights.AsSpan()));

            return new TerrainHeightmap(mapId, "ground.f32", heights, width, height, minX, minY, stepX, stepY);
        }

        private static TerrainHeightmap TryLoadHeightmapPng(string mapId, string dir)
        {
            string png = Path.Combine(dir, "heightmap.png");
            string meta = Path.Combine(dir, "heightmap.json");
            if (!File.Exists(png) || !File.Exists(meta)) return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(meta));
            var root = doc.RootElement;
            double minX = root.GetProperty("MinX").GetDouble();
            double minY = root.GetProperty("MinY").GetDouble();
            double maxX = root.GetProperty("MaxX").GetDouble();
            double maxY = root.GetProperty("MaxY").GetDouble();
            double redScale = root.GetProperty("RedScale").GetDouble();
            double blueScale = root.GetProperty("BlueScale").GetDouble();
            double offset = root.GetProperty("HeightOffset").GetDouble();

            using var bitmap = SKBitmap.Decode(png);
            if (bitmap == null || bitmap.Width < 2 || bitmap.Height < 2) return null;

            int width = bitmap.Width, height = bitmap.Height;
            var heights = new float[width * height];

            // Both decoded layouts keep 4 bytes per pixel; the slow per-pixel path covers anything else
            bool bgra = bitmap.ColorType == SKColorType.Bgra8888;
            bool rgba = bitmap.ColorType == SKColorType.Rgba8888;
            var pixels = bitmap.GetPixelSpan();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    double r, b;
                    if (bgra) { r = pixels[i * 4 + 2]; b = pixels[i * 4]; }
                    else if (rgba) { r = pixels[i * 4]; b = pixels[i * 4 + 2]; }
                    else { var c = bitmap.GetPixel(x, y); r = c.Red; b = c.Blue; }
                    heights[i] = (float)(r * redScale + b * blueScale + offset);
                }
            }

            return new TerrainHeightmap(mapId, "heightmap.png", heights, width, height,
                minX, minY, (maxX - minX) / (width - 1), (maxY - minY) / (height - 1));
        }
        #endregion
    }
}
