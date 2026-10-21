using System.Reflection;
using Svg.Skia;
using SkiaSharp;

namespace squad_dma
{
    /// <summary>
    /// Rasterizes the white-fill SVG icons in Icons/default (embedded as resources) into bitmaps.
    /// The fill stays white so the radar's Modulate color filter can tint it to the team color,
    /// while the dark outline is left untouched.
    /// </summary>
    public static class SvgIcons
    {
        /// <summary>Icons are authored on a 128 unit canvas and rasterized at this multiple of it.</summary>
        public const int RasterScale = 2;
        public const int LogicalSize = 128;

        public static SKBitmap Load(string relativePath)
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("Icons/" + relativePath.Replace('\\', '/'));
            if (stream == null)
                return null;

            using var svg = new SKSvg();
            var picture = svg.Load(stream);
            if (picture == null)
                return null;

            int size = LogicalSize * RasterScale;
            var cull = picture.CullRect;
            var bitmap = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(size / cull.Width, size / cull.Height);
            canvas.Translate(-cull.Left, -cull.Top);
            canvas.DrawPicture(picture);
            return bitmap;
        }
    }
}
