using SkiaSharp;

namespace squad_dma
{
    public static class SharedPaints
    {
        public static readonly SKPaint PaintBitmap = new SKPaint()
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };
    }
}
