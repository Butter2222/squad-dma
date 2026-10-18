using SkiaSharp;
using SkiaSharp.Views.Desktop;
using squad_dma.Source.Misc;
using squad_dma.Source.Squad;

namespace squad_dma
{
    /// <summary>
    /// Ticket counter: friendly and enemy tickets (told apart by color, the same colors as on the radar),
    /// then kills and wounded. The window is exactly as wide as what it shows.
    /// </summary>
    public sealed class TicketsWidget : SKWidget
    {
        private readonly MainForm _mainForm;
        private readonly SKGLControl _canvas;
        private bool _placed;

        private const string IdleText = "Waiting for match";
        private const float TextSize = 12f;
        private const float LabelGap = 5f;
        private const float SeparatorGap = 11f;
        private const float PaddingX = 12f;
        private const float RowHeight = 24f;
        private const float Margin = 8f;

        private static readonly string[] Labels = { null, null, "K", "W" }; // tickets carry no text, only color
        private static readonly SKColor LabelColor = new(150, 156, 166);
        private static readonly SKColor ValueColor = SKColors.White;

        // Measured widths, rebuilt only when a number or the UI scale changes
        private readonly string[] _values = new string[4];
        private readonly float[] _labelWidths = new float[4];
        private readonly float[] _valueWidths = new float[4];
        private float _idleWidth;
        private float _contentWidth;
        private (int, int, int, int) _lastCounts = (-1, -1, -1, -1);
        private float _layoutScale = float.NaN;

        private readonly SKPaint _labelPaint = new()
        {
            IsAntialias = true,
            SubpixelText = true,
            TextEncoding = SKTextEncoding.Utf8,
            Typeface = SKTypeface.FromFamilyName("Segoe UI"),
            Color = LabelColor
        };

        private readonly SKPaint _valuePaint = new()
        {
            IsAntialias = true,
            SubpixelText = true,
            TextEncoding = SKTextEncoding.Utf8,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };

        private readonly SKPaint _separatorPaint = new()
        {
            Color = new SKColor(255, 255, 255, 40),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f
        };

        // A bit more opaque than the default so the numbers stay readable over the map
        protected override SKColor BodyColor => new SKColor(10, 12, 16, 0xCC);

        public TicketsWidget(SKGLControl parent, MainForm mainForm, float scaleFactor)
            : base(parent, "", new SKPoint(Margin, Margin), new SKSize(120, RowHeight), scaleFactor, canResize: false)
        {
            _canvas = parent;
            _mainForm = mainForm;
        }

        public override void Draw(SKCanvas canvas)
        {
            bool live = Memory.GameStatus == GameStatus.InGame && Memory._game != null;
            EnsureLayout();

            var size = new SKSize(
                MathF.Ceiling((live ? _contentWidth : _idleWidth) + PaddingX * 2 * ScaleFactor),
                MathF.Ceiling(RowHeight * ScaleFactor));
            if (size != Size)
                Size = size;

            PlaceOnce();
            base.Draw(canvas);
            if (Minimized)
                return;

            var area = ClientRectangle;
            float baseline = area.MidY - (_valuePaint.FontMetrics.Ascent + _valuePaint.FontMetrics.Descent) / 2;

            if (!live)
            {
                _labelPaint.TextAlign = SKTextAlign.Center;
                canvas.DrawText(IdleText, area.MidX, baseline, _labelPaint);
                _labelPaint.TextAlign = SKTextAlign.Left;
                return;
            }

            float x = area.MidX - _contentWidth / 2;
            float sepGap = SeparatorGap * ScaleFactor, labelGap = LabelGap * ScaleFactor;
            for (int i = 0; i < _values.Length; i++)
            {
                if (Labels[i] != null)
                {
                    canvas.DrawText(Labels[i], x, baseline, _labelPaint);
                    x += _labelWidths[i] + labelGap;
                }

                _valuePaint.Color = ValueColorFor(i);
                canvas.DrawText(_values[i], x, baseline, _valuePaint);
                x += _valueWidths[i];

                if (i < _values.Length - 1)
                {
                    x += sepGap;
                    canvas.DrawLine(x, area.MidY - 7f * ScaleFactor, x, area.MidY + 7f * ScaleFactor, _separatorPaint);
                    x += 1f + sepGap;
                }
            }
        }

        // Friendly and enemy tickets use the colors set in the Colors tab
        private static SKColor ValueColorFor(int index) => index switch
        {
            0 => SKPaints.Friendly,
            1 => SKPaints.EnemyPlayer,
            _ => ValueColor
        };

        /// <summary>Starts bottom-left of the radar, once the canvas has its real size.</summary>
        private void PlaceOnce()
        {
            if (_placed || _canvas.Width < 200 || _canvas.Height < 200)
                return;

            _placed = true;
            Location = new SKPoint(Margin, _canvas.Height - Rectangle.Height - Margin);
        }

        private void EnsureLayout()
        {
            var c = _mainForm.TicketCounts;
            var counts = (c.Friendly, c.Enemy, c.Kills, c.Woundeds);
            if (counts == _lastCounts && ScaleFactor == _layoutScale)
                return;

            _lastCounts = counts;
            _layoutScale = ScaleFactor;
            _labelPaint.TextSize = _valuePaint.TextSize = TextSize * ScaleFactor;

            _values[0] = c.Friendly.ToString();
            _values[1] = c.Enemy.ToString();
            _values[2] = c.Kills.ToString();
            _values[3] = c.Woundeds.ToString();

            _contentWidth = (_values.Length - 1) * (SeparatorGap * 2 * ScaleFactor + 1f);
            for (int i = 0; i < _values.Length; i++)
            {
                _labelWidths[i] = Labels[i] == null ? 0f : _labelPaint.MeasureText(Labels[i]);
                _valueWidths[i] = _valuePaint.MeasureText(_values[i]);
                _contentWidth += _labelWidths[i] + (Labels[i] != null ? LabelGap * ScaleFactor : 0f) + _valueWidths[i];
            }
            _idleWidth = _labelPaint.MeasureText(IdleText);
        }

        public override void Dispose()
        {
            _labelPaint.Dispose();
            _valuePaint.Dispose();
            _separatorPaint.Dispose();
            base.Dispose();
        }
    }
}
