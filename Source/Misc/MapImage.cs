using SkiaSharp;
using squad_dma.Source.Misc;

namespace squad_dma
{
    /// <summary>
    /// What the radar draws as the map. Width and Height are in map pixels, the space the calibration (x, y, scale)
    /// and the view bounds use, no matter how much detail the image has underneath.
    /// </summary>
    public abstract class MapImage : IDisposable
    {
        public abstract int Width { get; }
        public abstract int Height { get; }

        /// <summary>Draws the part of the map inside <paramref name="source"/> (map pixels) into <paramref name="destination"/>.</summary>
        public abstract void Draw(SKCanvas canvas, SKRect source, SKRect destination, SKPaint paint);

        public abstract void Dispose();
    }

    /// <summary>A map held as one bitmap: the legacy single-image maps, and packs without HD tiles.</summary>
    public sealed class BitmapMapImage : MapImage
    {
        private readonly SKBitmap _bitmap;

        public BitmapMapImage(SKBitmap bitmap) => _bitmap = bitmap;

        public override int Width => _bitmap.Width;
        public override int Height => _bitmap.Height;

        public override void Draw(SKCanvas canvas, SKRect source, SKRect destination, SKPaint paint) =>
            canvas.DrawBitmap(_bitmap, source, destination, paint);

        public override void Dispose() => _bitmap.Dispose();
    }

    /// <summary>
    /// A map held as a pyramid of 256 px tiles (hq/{z}_{x}_{y}.webp, level z has 2^z x 2^z tiles). Only the tiles in
    /// view, at the level that matches the zoom, are decoded and kept. The map is drawn from the coarse levels
    /// until the sharp tiles are ready, so panning and zooming never show gaps.
    /// </summary>
    public sealed class HdTileMapImage : MapImage
    {
        public const int TileSize = 256;
        public const int MaxLevel = 5;

        private const int PinnedLevels = 3;        // levels 0-2 (21 tiles) stay loaded as the fallback
        private const int MaxCachedTiles = 320;    // about 80 MB of decoded tiles on top of the pinned ones
        private const int MaxDecodesPerFrame = 24; // the rest are drawn from the coarser level and decoded on later frames

        private readonly string _directory;
        private readonly int _width, _height;
        private readonly Dictionary<long, Entry> _tiles = new();
        private long _frame;

        private sealed class Entry
        {
            public SKBitmap Bitmap;   // null when the file couldn't be read
            public long LastUsed;
        }

        // Tile edges are filtered and not antialiased, otherwise hairline gaps show between neighbours
        private static readonly SKPaint TilePaint = new() { IsAntialias = false, FilterQuality = SKFilterQuality.High };

        public HdTileMapImage(string directory, int width, int height)
        {
            _directory = directory;
            _width = width;
            _height = height;

            for (int level = 0; level < PinnedLevels; level++)
                for (int x = 0; x < (1 << level); x++)
                    for (int y = 0; y < (1 << level); y++)
                        Load(level, x, y);
        }

        public override int Width => _width;
        public override int Height => _height;

        /// <summary>True when every tile of every level is on disk.</summary>
        public static bool IsComplete(string directory)
        {
            if (!Directory.Exists(directory)) return false;
            for (int level = 0; level <= MaxLevel; level++)
            {
                int n = 1 << level;
                if (!File.Exists(PathOf(directory, level, n - 1, n - 1)) || !File.Exists(PathOf(directory, level, 0, 0)))
                    return false;
            }

            // The last level is the biggest: counting its files is much cheaper than checking each one
            int expected = 1 << (2 * MaxLevel);
            return Directory.EnumerateFiles(directory, $"{MaxLevel}_*_*.webp").Take(expected + 1).Count() == expected;
        }

        public override void Draw(SKCanvas canvas, SKRect source, SKRect destination, SKPaint paint)
        {
            if (source.Width <= 0 || source.Height <= 0 || destination.Width <= 0) return;
            _frame++;

            // Screen pixels per map pixel decides the level: one tile texel per screen pixel or finer
            double scale = destination.Width / source.Width;
            double texelsPerMapPixel = 1.0 / _width * TileSize;
            int level = (int)Math.Ceiling(Math.Log2(Math.Max(scale / texelsPerMapPixel, 1.0)));
            level = Math.Clamp(level, 0, MaxLevel);

            int count = 1 << level;
            double tileMapW = (double)_width / count, tileMapH = (double)_height / count;
            int x0 = Math.Clamp((int)Math.Floor(source.Left / tileMapW), 0, count - 1);
            int x1 = Math.Clamp((int)Math.Floor((source.Right - 1e-4) / tileMapW), 0, count - 1);
            int y0 = Math.Clamp((int)Math.Floor(source.Top / tileMapH), 0, count - 1);
            int y1 = Math.Clamp((int)Math.Floor((source.Bottom - 1e-4) / tileMapH), 0, count - 1);

            double sx = destination.Width / source.Width, sy = destination.Height / source.Height;
            int decodes = 0;

            canvas.Save();
            canvas.ClipRect(destination);
            for (int ty = y0; ty <= y1; ty++)
            {
                for (int tx = x0; tx <= x1; tx++)
                {
                    var bitmap = GetTile(level, tx, ty, ref decodes, out SKRect part);
                    if (bitmap == null) continue;

                    float left = (float)(destination.Left + (tx * tileMapW - source.Left) * sx);
                    float top = (float)(destination.Top + (ty * tileMapH - source.Top) * sy);
                    var dest = new SKRect(left, top, left + (float)(tileMapW * sx), top + (float)(tileMapH * sy));
                    dest.Inflate(0.5f, 0.5f);
                    canvas.DrawBitmap(bitmap, part, dest, TilePaint);
                }
            }
            canvas.Restore();

            if (_tiles.Count > MaxCachedTiles + 21) Evict();
        }

        /// <summary>
        /// The tile, or while it isn't loaded yet the part of the nearest coarser loaded tile that covers the same
        /// area (<paramref name="part"/> is the region of that bitmap to draw).
        /// </summary>
        private SKBitmap GetTile(int level, int x, int y, ref int decodes, out SKRect part)
        {
            long key = Key(level, x, y);
            if (_tiles.TryGetValue(key, out var entry))
            {
                entry.LastUsed = _frame;
                if (entry.Bitmap != null)
                {
                    part = new SKRect(0, 0, TileSize, TileSize);
                    return entry.Bitmap;
                }
            }
            else if (decodes < MaxDecodesPerFrame)
            {
                decodes++;
                var loaded = Load(level, x, y);
                if (loaded != null)
                {
                    part = new SKRect(0, 0, TileSize, TileSize);
                    return loaded;
                }
            }

            // Coarser levels, which are loaded
            for (int up = level - 1; up >= 0; up--)
            {
                int shift = level - up;
                if (_tiles.TryGetValue(Key(up, x >> shift, y >> shift), out var parent) && parent.Bitmap != null)
                {
                    parent.LastUsed = _frame;
                    float size = (float)TileSize / (1 << shift);
                    float px = (x & ((1 << shift) - 1)) * size, py = (y & ((1 << shift) - 1)) * size;
                    part = new SKRect(px, py, px + size, py + size);
                    return parent.Bitmap;
                }
            }

            part = default;
            return null;
        }

        private SKBitmap Load(int level, int x, int y)
        {
            SKBitmap bitmap = null;
            try
            {
                string path = PathOf(_directory, level, x, y);
                if (File.Exists(path))
                {
                    using var stream = File.OpenRead(path);
                    bitmap = SKBitmap.Decode(stream);
                    bitmap?.SetImmutable();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Maps] HD tile {level}_{x}_{y} could not be read: {ex.Message}");
            }

            _tiles[Key(level, x, y)] = new Entry { Bitmap = bitmap, LastUsed = _frame };
            return bitmap;
        }

        /// <summary>Drops the tiles that were drawn longest ago, keeping the pinned coarse levels.</summary>
        private void Evict()
        {
            var victims = _tiles
                .Where(kv => LevelOf(kv.Key) >= PinnedLevels)
                .OrderBy(kv => kv.Value.LastUsed)
                .Take(_tiles.Count - MaxCachedTiles - 21)
                .ToList();

            foreach (var kv in victims)
            {
                kv.Value.Bitmap?.Dispose();
                _tiles.Remove(kv.Key);
            }
        }

        private static long Key(int level, int x, int y) => ((long)level << 40) | ((long)x << 20) | (long)y;
        private static int LevelOf(long key) => (int)(key >> 40);
        private static string PathOf(string directory, int level, int x, int y) => Path.Combine(directory, $"{level}_{x}_{y}.webp");

        public override void Dispose()
        {
            foreach (var entry in _tiles.Values)
                entry.Bitmap?.Dispose();
            _tiles.Clear();
        }
    }
}
