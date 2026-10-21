using SkiaSharp;
using SkiaSharp.Views.Desktop;
using squad_dma.Source.Misc;

namespace squad_dma
{
    /// <summary>
    /// Status pill in the top-right corner of the radar (colored dot, FPS and data rate) that opens a detailed
    /// diagnostics panel below it. Both are fixed in place: they cannot be dragged or resized.
    /// Click the pill, use the keybind or the Settings checkbox to open or close the panel; "Copy snapshot" puts
    /// every number on the clipboard as text, ready to paste into a bug report.
    /// The numbers come from <see cref="PerfStats"/> and are refreshed a few times a second, not every frame.
    /// </summary>
    public sealed class DiagnosticsWidget : IDisposable
    {
        private readonly SKGLControl _canvas;
        private float _scale;

        private const float Margin = 8f;
        private const float PillHeight = 22f;
        private const float PanelWidth = 286f;
        private const float Padding = 10f;
        private const float TextSize = 11f;
        private const float RowHeight = 15.5f;
        private const float HeaderHeight = 20f;
        private const float SparkHeight = 26f;
        private const float ButtonHeight = 22f;
        private const int RefreshMs = 250;
        private const int CopiedFeedbackMs = 1500;

        private static readonly SKColor LabelColor = new(150, 156, 166);
        private static readonly SKColor ValueColor = SKColors.White;
        private static readonly SKColor HeaderColor = new(0xBA, 0xBA, 0xFF); // #BABAFF
        private static readonly SKColor Good = new(66, 214, 117);
        private static readonly SKColor Warn = new(255, 196, 66);
        private static readonly SKColor Bad = new(255, 93, 93);
        private static readonly SKColor Idle = new(130, 136, 146);

        private enum RowKind { Header, Line, Spark, Wrap }
        private readonly record struct Row(RowKind Kind, string Label, string Value, SKColor Color);

        private readonly List<Row> _rows = new(40);
        private PerfSnapshot _snapshot = PerfStats.Latest;
        private DateTime _lastRefresh = DateTime.MinValue;
        private DateTime _copiedAt = DateTime.MinValue;
        private string _pillText = "";
        private float _pillTextWidth;
        private bool _layoutDirty = true;
        private float _layoutScale = float.NaN;
        private bool _hoveringControl;

        // Where things were drawn last frame, for the mouse
        private SKRect _pillRect, _copyRect;
        private bool _panelShown;

        private readonly SKPaint _text = new()
        {
            IsAntialias = true,
            SubpixelText = true,
            TextEncoding = SKTextEncoding.Utf8,
            Typeface = SKTypeface.FromFamilyName("Segoe UI")
        };

        private readonly SKPaint _bold = new()
        {
            IsAntialias = true,
            SubpixelText = true,
            TextEncoding = SKTextEncoding.Utf8,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };

        private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
        private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
        private readonly SKPaint _line = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
        private readonly SKPath _sparkPath = new();

        public DiagnosticsWidget(SKGLControl canvas, float scaleFactor)
        {
            _canvas = canvas;
            _scale = scaleFactor;
            _canvas.MouseClick += Canvas_MouseClick;
            _canvas.MouseMove += Canvas_MouseMove;
        }

        public void SetScaleFactor(float scale)
        {
            _scale = scale;
            _layoutDirty = true;
        }

        /// <summary>True when the point is on the pill or the open panel, so the map does not react to the click.</summary>
        public bool Contains(Point p) =>
            Program.Config.ShowDiagnostics && (_pillRect.Contains(p.X, p.Y) || (_panelShown && _panelRect.Contains(p.X, p.Y)));

        private SKRect _panelRect;

        #region Draw
        public void Draw(SKCanvas canvas)
        {
            var cfg = Program.Config;
            if (!cfg.ShowDiagnostics)
            {
                _pillRect = _copyRect = _panelRect = default;
                _panelShown = false;
                return;
            }

            if ((DateTime.UtcNow - _lastRefresh).TotalMilliseconds >= RefreshMs)
            {
                _lastRefresh = DateTime.UtcNow;
                _snapshot = PerfStats.TakeSnapshot();
                _layoutDirty = true;
            }
            if (_layoutDirty || _scale != _layoutScale)
                Rebuild(cfg.DiagnosticsDetailed);

            float s = _scale;
            float opacity = Math.Clamp(cfg.InfoWidgetOpacity / 100f, 0.1f, 1f);
            var body = new SKColor(10, 12, 16, (byte)(0xD0 * opacity));
            var border = new SKColor(58, 63, 72, (byte)(255 * opacity));

            // Pill: dot + text, pinned to the top-right corner
            float pillH = PillHeight * s;
            float pillW = MathF.Ceiling(_pillTextWidth + (Padding * 2 + 11f) * s);
            _pillRect = new SKRect(_canvas.Width - Margin * s - pillW, Margin * s, _canvas.Width - Margin * s, Margin * s + pillH);

            _fill.Color = body;
            canvas.DrawRoundRect(_pillRect, pillH / 2, pillH / 2, _fill);
            _stroke.Color = border;
            canvas.DrawRoundRect(_pillRect, pillH / 2, pillH / 2, _stroke);

            _fill.Color = HealthColor(_snapshot.Health);
            canvas.DrawCircle(_pillRect.Left + (Padding + 2f) * s, _pillRect.MidY, 4.5f * s, _fill);

            _text.TextSize = 12f * s;
            _text.Color = ValueColor;
            canvas.DrawText(_pillText, _pillRect.Left + (Padding + 12f) * s, Baseline(_pillRect.MidY, _text), _text);

            _panelShown = cfg.DiagnosticsDetailed;
            if (_panelShown)
                DrawPanel(canvas, body, border);
        }

        private void DrawPanel(SKCanvas canvas, SKColor body, SKColor border)
        {
            float s = _scale;
            float width = PanelWidth * s, pad = Padding * s;

            float height = pad * 2 + ButtonHeight * s + 6f * s;
            foreach (var row in _rows)
                height += RowHeightOf(row);

            _panelRect = new SKRect(_canvas.Width - Margin * s - width, _pillRect.Bottom + 6f * s,
                                    _canvas.Width - Margin * s, _pillRect.Bottom + 6f * s + height);

            _fill.Color = body;
            canvas.DrawRoundRect(_panelRect, 8f * s, 8f * s, _fill);
            _stroke.Color = border;
            canvas.DrawRoundRect(_panelRect, 8f * s, 8f * s, _stroke);

            _text.TextSize = _bold.TextSize = TextSize * s;
            float left = _panelRect.Left + pad, right = _panelRect.Right - pad;
            float y = _panelRect.Top + pad;

            foreach (var row in _rows)
            {
                float h = RowHeightOf(row);
                switch (row.Kind)
                {
                    case RowKind.Header:
                        _bold.Color = HeaderColor;
                        canvas.DrawText(row.Label, left, y + h - 6f * s, _bold);
                        _line.Color = new SKColor(255, 255, 255, 28);
                        canvas.DrawLine(left + _bold.MeasureText(row.Label) + 6f * s, y + h - 9f * s, right, y + h - 9f * s, _line);
                        break;

                    case RowKind.Line:
                        {
                            float baseline = Baseline(y + h / 2, _text);
                            _text.Color = LabelColor;
                            _text.TextAlign = SKTextAlign.Left;
                            canvas.DrawText(row.Label, left, baseline, _text);
                            _text.Color = row.Color;
                            _text.TextAlign = SKTextAlign.Right;
                            canvas.DrawText(row.Value, right, baseline, _text);
                            _text.TextAlign = SKTextAlign.Left;
                            break;
                        }

                    case RowKind.Spark:
                        DrawSparkline(canvas, new SKRect(left, y + 2f * s, right, y + h - 2f * s));
                        break;

                    case RowKind.Wrap:
                        _text.Color = row.Color;
                        canvas.DrawText(row.Value, left, Baseline(y + h / 2, _text), _text);
                        break;
                }
                y += h;
            }

            // Copy button
            y += 6f * s;
            _copyRect = new SKRect(left, y, right, y + ButtonHeight * s);
            bool copied = (DateTime.UtcNow - _copiedAt).TotalMilliseconds < CopiedFeedbackMs;
            _fill.Color = copied ? new SKColor(66, 214, 117, 60) : new SKColor(255, 255, 255, _hoveringControl ? (byte)38 : (byte)22);
            canvas.DrawRoundRect(_copyRect, 5f * s, 5f * s, _fill);
            _bold.Color = copied ? Good : ValueColor;
            _bold.TextAlign = SKTextAlign.Center;
            canvas.DrawText(copied ? "Copied to clipboard" : "Copy snapshot", _copyRect.MidX, Baseline(_copyRect.MidY, _bold), _bold);
            _bold.TextAlign = SKTextAlign.Left;
        }

        /// <summary>Frame times of the last ~2 seconds. The faint line marks 60 fps; a spike is a hitch.</summary>
        private void DrawSparkline(SKCanvas canvas, SKRect area)
        {
            const float CeilingMs = 50f;
            int n = PerfStats.FrameHistoryLength;

            _line.Color = new SKColor(255, 255, 255, 30);
            float y60 = area.Bottom - area.Height * (1000f / 60f / CeilingMs);
            canvas.DrawLine(area.Left, y60, area.Right, y60, _line);

            _sparkPath.Rewind();
            for (int i = 0; i < n; i++)
            {
                float ms = Math.Min(PerfStats.FrameInterval(i), CeilingMs);
                float x = area.Left + area.Width * i / (n - 1);
                float y = area.Bottom - area.Height * (ms / CeilingMs);
                if (i == 0) _sparkPath.MoveTo(x, y);
                else _sparkPath.LineTo(x, y);
            }
            _line.Color = HeaderColor;
            canvas.DrawPath(_sparkPath, _line);
        }

        private float RowHeightOf(Row row) => (row.Kind switch
        {
            RowKind.Header => HeaderHeight,
            RowKind.Spark => SparkHeight,
            _ => RowHeight
        }) * _scale;

        private static float Baseline(float centerY, SKPaint paint) =>
            centerY - (paint.FontMetrics.Ascent + paint.FontMetrics.Descent) / 2;
        #endregion

        #region Content
        private void Rebuild(bool detailed)
        {
            var s = _snapshot;
            _layoutDirty = false;
            _layoutScale = _scale;
            _text.TextSize = _bold.TextSize = TextSize * _scale;

            // Pill text
            if (s.Health == HealthLevel.Idle)
                _pillText = s.HealthReason;
            else if (s.Health == HealthLevel.Good || s.WindowSeconds <= 0)
                _pillText = $"{s.Fps:F0} fps  ·  {s.DataHz:F0} Hz";
            else
                _pillText = $"{s.Fps:F0} fps  ·  {s.HealthReason}";
            _text.TextSize = 12f * _scale;
            _pillTextWidth = _text.MeasureText(_pillText);
            _text.TextSize = TextSize * _scale;

            if (!detailed)
                return;

            _rows.Clear();
            void Header(string name) => _rows.Add(new Row(RowKind.Header, name, "", ValueColor));
            void Line(string label, string value, SKColor? color = null) => _rows.Add(new Row(RowKind.Line, label, value, color ?? ValueColor));

            Header("STATUS");
            Line("Health", s.Health == HealthLevel.Good ? "Good" : s.HealthReason, HealthColor(s.Health));
            Line("Game", s.GameStatus == GameStatus.InGame ? $"In game  ·  {Short(s.Map, 18)}" : s.GameStatus.ToString());
            if (s.GameStatus == GameStatus.InGame)
                Line("Player", s.PlayerState);

            Header("RENDER");
            Line("FPS", $"{s.Fps:F0}", Tone(s.Fps, 60, 30, higherIsWorse: false));
            Line("Frame time", $"{s.FrameAvgMs:F1} avg  /  {s.FrameWorstMs:F0} worst ms", Tone(s.FrameWorstMs, 40, 100));
            Line("Draw cost", $"{s.DrawAvgMs:F1} ms", Tone(s.DrawAvgMs, 8, 16));
            _rows.Add(new Row(RowKind.Spark, "", "", ValueColor));

            Header("RADAR DATA");
            Line("Update rate", $"{s.DataHz:F0} Hz", Tone(s.DataHz, 20, 10, higherIsWorse: false));
            Line("Pass time", $"{s.DataPassMs:F0} ms  (max {s.DataPassMaxMs:F0})", Tone(s.DataPassMs, 60, 150));
            Line("Newest data", double.IsInfinity(s.DataAgeMs) ? "-" : $"{s.DataAgeMs:F0} ms old", Tone(double.IsInfinity(s.DataAgeMs) ? 0 : s.DataAgeMs, 300, 1000));

            Header("DMA");
            Line("Reads", $"{s.ReadsPerSec:N0}/s  ({s.ReadItemsPerSec:N0} items/s)");
            Line("Writes", $"{s.WritesPerSec:N0}/s");
            Line("Read latency", $"{s.ReadAvgMs:F2} avg  /  {s.ReadMaxMs:F1} max ms", Tone(s.ReadMaxMs, 20, 100));
            Line("Failed", $"{s.ReadFailPct:F1}% reads  ·  {s.WriteFailPct:F1}% writes", Tone(Math.Max(s.ReadFailPct, s.WriteFailPct), 3, 20));
            Line("Key reader", s.KeyReaderReady ? "ready" : "not ready", s.KeyReaderReady ? Good : Warn);

            Header("WORLD");
            Line("Actors", $"{s.Players} players  ·  {s.Vehicles} vehicles  ·  {s.Deployables} deployables");
            Line("Actor scan", s.ActorScanAgeSec <= 0 ? "-" : $"{s.ActorScanMs:F0} ms {(s.LastScanWasFull ? "full" : "quick")}, {s.ActorScanAgeSec:F1} s ago", Tone(s.ActorScanMs, 250, 800));
            Line("Objectives scan", s.ObjectivesScanMs <= 0 ? "-" : $"{s.ObjectivesScanMs:F0} ms", Tone(s.ObjectivesScanMs, 400, 1500));

            Header("PROCESS");
            Line("Memory", $"{s.ManagedMb:F0} MB heap  ·  {s.WorkingSetMb:F0} MB total");
            Line("GC", $"{s.Gen0} / {s.Gen1} / {s.Gen2}  ·  {s.GcPauseMs:F0} ms paused");
            Line("Map tiles", $"{s.TilesCached} cached  ·  {s.TileQueue} loading");

            Header("FEATURES");
            AddWrapped(s.Features.Length == 0 ? "none active" : string.Join(", ", s.Features), PanelWidth * _scale - Padding * 2 * _scale);

        }

        private void AddWrapped(string text, float maxWidth)
        {
            var words = text.Split(' ');
            var current = "";
            foreach (var word in words)
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (current.Length > 0 && _text.MeasureText(candidate) > maxWidth)
                {
                    _rows.Add(new Row(RowKind.Wrap, "", current, ValueColor));
                    current = word;
                }
                else
                {
                    current = candidate;
                }
            }
            if (current.Length > 0)
                _rows.Add(new Row(RowKind.Wrap, "", current, ValueColor));
        }

        private static string Short(string text, int max) =>
            string.IsNullOrEmpty(text) ? "-" : text.Length <= max ? text : text[..(max - 1)] + "…";

        private static SKColor HealthColor(HealthLevel level) => level switch
        {
            HealthLevel.Good => Good,
            HealthLevel.Warning => Warn,
            HealthLevel.Bad => Bad,
            _ => Idle
        };

        /// <summary>Value color: white normally, amber past <paramref name="warn"/>, red past <paramref name="bad"/>.</summary>
        private static SKColor Tone(double value, double warn, double bad, bool higherIsWorse = true)
        {
            if (higherIsWorse)
                return value >= bad ? Bad : value >= warn ? Warn : ValueColor;
            return value <= bad ? Bad : value <= warn ? Warn : ValueColor;
        }
        #endregion

        #region Mouse
        private void Canvas_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !Program.Config.ShowDiagnostics)
                return;

            if (_pillRect.Contains(e.X, e.Y))
            {
                Program.Config.DiagnosticsDetailed = !Program.Config.DiagnosticsDetailed;
                _layoutDirty = true;
                Config.SaveConfig(Program.Config);
                _canvas.Invalidate();
            }
            else if (_panelShown && _copyRect.Contains(e.X, e.Y))
            {
                CopySnapshot();
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            bool over = Program.Config.ShowDiagnostics && (_pillRect.Contains(e.X, e.Y) || (_panelShown && _copyRect.Contains(e.X, e.Y)));
            if (over == _hoveringControl)
                return;

            _hoveringControl = over;
            _canvas.Cursor = over ? Cursors.Hand : Cursors.Default;
        }

        /// <summary>Puts every number on the clipboard as plain text.</summary>
        private void CopySnapshot()
        {
            try
            {
                Clipboard.SetText(_snapshot.ToText());
                _copiedAt = DateTime.UtcNow;
                Logger.Info("[Diagnostics] Snapshot copied to the clipboard");
            }
            catch (Exception ex)
            {
                // The clipboard can be locked by another program for a moment
                Logger.Error($"[Diagnostics] Could not copy the snapshot: {ex.Message}");
            }
        }
        #endregion

        public void Dispose()
        {
            _canvas.MouseClick -= Canvas_MouseClick;
            _canvas.MouseMove -= Canvas_MouseMove;
            _text.Dispose();
            _bold.Dispose();
            _fill.Dispose();
            _stroke.Dispose();
            _line.Dispose();
            _sparkPath.Dispose();
        }
    }
}
