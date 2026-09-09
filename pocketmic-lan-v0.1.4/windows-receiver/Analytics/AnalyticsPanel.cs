using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PocketMicReceiver;

/// <summary>
/// Reusable diagnostics strip for Advanced mode: current values plus three sparklines (loss %,
/// interarrival jitter p95, buffer depth) built from the 10 s windows an
/// <see cref="AnalyticsCollector"/> emits. Pure GDI+ — no charting package — because the whole
/// point of this control is three thin polylines and a header line of text, which is well
/// inside what <see cref="Graphics.DrawLines(Pen, PointF[])"/> already does.
///
/// This control does no threading of its own. <see cref="AnalyticsCollector.WindowClosed"/>
/// fires on a background thread; the host must bridge it to <see cref="PushWindow"/> with
/// <c>Control.BeginInvoke</c>, exactly as <c>MainForm</c> already does for every other
/// receive-loop-driven UI update.
/// </summary>
public sealed class AnalyticsPanel : UserControl
{
    // 180 x 10s = 30 minutes of history, long enough to see a trend without holding an
    // unbounded amount of data for a receiver that might run for hours.
    private const int HistoryCapacity = 180;

    private static readonly Color PanelBackground = Color.FromArgb(242, 245, 247);
    private static readonly Color GridColor = Color.FromArgb(220, 224, 227);
    private static readonly Color LossColor = Color.FromArgb(214, 69, 69);
    private static readonly Color JitterColor = Color.FromArgb(53, 116, 209);
    private static readonly Color BufferColor = Color.FromArgb(56, 158, 107);
    private static readonly Color TextColor = Color.FromArgb(38, 42, 46);
    private static readonly Color MutedColor = Color.FromArgb(118, 126, 134);

    private readonly List<WindowStats> _history = new(HistoryCapacity);
    private WindowStats? _latestOneMinute;
    private WindowStats? _latestSession;

    public AnalyticsPanel()
    {
        DoubleBuffered = true;
        BackColor = PanelBackground;
        MinimumSize = new Size(360, 220);
        Font = new Font("Segoe UI", 9F);
    }

    /// <summary>
    /// Feeds one closed window into the panel and repaints. Call on the UI thread only — see
    /// the class remarks for why this control does not marshal for itself.
    /// </summary>
    public void PushWindow(WindowStats window)
    {
        switch (window.WindowKind)
        {
            case "10s":
                if (_history.Count == HistoryCapacity) _history.RemoveAt(0);
                _history.Add(window);
                break;
            case "1m":
                _latestOneMinute = window;
                break;
            case "session":
                _latestSession = window;
                break;
            default:
                return;
        }

        Invalidate();
    }

    /// <summary>Clears history without disposing the control, so it survives a stop/start cycle.</summary>
    public void ResetHistory()
    {
        _history.Clear();
        _latestOneMinute = null;
        _latestSession = null;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var current = _latestSession ?? _latestOneMinute ?? (_history.Count > 0 ? _history[^1] : null);
        const int headerHeight = 42;
        DrawHeader(g, current);

        if (_history.Count < 2)
        {
            using var mutedBrush = new SolidBrush(MutedColor);
            g.DrawString("Waiting for data...", Font, mutedBrush, 8, headerHeight + 4);
            return;
        }

        var chartsHeight = Math.Max(0, Height - headerHeight);
        var chartHeight = chartsHeight / 3;
        var lossValues = _history.ConvertAll(w => w.LossPercent);
        var jitterValues = _history.ConvertAll(w => w.InterarrivalP95Ms);
        var bufferValues = _history.ConvertAll(w => w.BufferDepthAvgMs);

        DrawSparkline(
            g, new Rectangle(0, headerHeight, Width, chartHeight), lossValues, 0, null,
            LossColor, "Loss %", $"{current?.LossPercent ?? 0:F2}%");

        DrawSparkline(
            g, new Rectangle(0, headerHeight + chartHeight, Width, chartHeight), jitterValues, 0, null,
            JitterColor, "Jitter p95", $"{current?.InterarrivalP95Ms ?? 0:F0} ms");

        DrawSparkline(
            g, new Rectangle(0, headerHeight + (2 * chartHeight), Width, chartsHeight - (2 * chartHeight)), bufferValues, 0, null,
            BufferColor, "Buffer depth", $"{current?.BufferDepthAvgMs ?? 0:F0} ms");
    }

    private void DrawHeader(Graphics g, WindowStats? current)
    {
        using var textBrush = new SolidBrush(TextColor);
        using var mutedBrush = new SolidBrush(MutedColor);
        using var headerFont = new Font(Font, FontStyle.Bold);

        if (current is null)
        {
            g.DrawString("PocketMic diagnostics — waiting for the receiver to start", headerFont, mutedBrush, 8, 6);
            return;
        }

        var summary =
            $"Loss {current.LossPercent:F2}%   ·   " +
            $"p50/p95/p99 {current.InterarrivalP50Ms:F0}/{current.InterarrivalP95Ms:F0}/{current.InterarrivalP99Ms:F0} ms   ·   " +
            $"Buffer {current.BufferDepthAvgMs:F0} ms   ·   " +
            $"Level {current.LevelDbfsAvg:F1} dBFS   ·   " +
            $"Gate {current.NoiseGateActivePercent:F0}%";
        g.DrawString(summary, headerFont, textBrush, 8, 6);

        var detail = current.DroppedSamples > 0
            ? $"{current.WindowKind} window ending {current.WindowEnd.ToLocalTime():HH:mm:ss}   ·   {current.DroppedSamples} analytics samples dropped"
            : $"{current.WindowKind} window ending {current.WindowEnd.ToLocalTime():HH:mm:ss}";
        g.DrawString(detail, Font, mutedBrush, 8, 24);
    }

    /// <summary>
    /// One labelled strip: a baseline, the series as a polyline scaled to the strip, and the
    /// metric name plus current value in the corner. A null <paramref name="maxValue"/>
    /// auto-scales to the series' own peak with headroom, since loss/jitter/buffer depth have
    /// very different natural ranges and a shared fixed scale would flatten whichever is smallest.
    /// </summary>
    private void DrawSparkline(
        Graphics g,
        Rectangle bounds,
        List<double> values,
        double minValue,
        double? maxValue,
        Color color,
        string label,
        string valueText)
    {
        if (bounds.Height <= 4 || bounds.Width <= 4 || values.Count == 0) return;

        var plotBounds = Rectangle.Inflate(bounds, -2, -2);
        using var gridPen = new Pen(GridColor);
        g.DrawLine(gridPen, plotBounds.Left, plotBounds.Bottom, plotBounds.Right, plotBounds.Bottom);

        var peak = maxValue ?? Math.Max(values.Max() * 1.15, 0.001);
        var range = Math.Max(peak - minValue, 0.001);

        var points = new PointF[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var x = plotBounds.Left + (values.Count == 1 ? 0f : (float)i / (values.Count - 1) * plotBounds.Width);
            var normalized = (values[i] - minValue) / range;
            var y = plotBounds.Bottom - ((float)Math.Clamp(normalized, 0, 1) * plotBounds.Height);
            points[i] = new PointF(x, y);
        }

        if (points.Length >= 2)
        {
            using var linePen = new Pen(color, 1.6f);
            g.DrawLines(linePen, points);
        }

        using var labelBrush = new SolidBrush(MutedColor);
        g.DrawString($"{label}  {valueText}", Font, labelBrush, plotBounds.Left, plotBounds.Top);
    }
}
