using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace voboX.Controls;

/// <summary>
/// 波形显示控件：暗色风格。
/// 支持：播放头（已播放/未播放分色）、鼠标拖动选择裁剪选区。
/// </summary>
public class WaveformView : FrameworkElement
{
    public static readonly DependencyProperty PeaksProperty =
        DependencyProperty.Register(nameof(Peaks), typeof(double[]), typeof(WaveformView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PlayheadProperty =
        DependencyProperty.Register(nameof(Playhead), typeof(double), typeof(WaveformView),
            new FrameworkPropertyMetadata(0.0, OnPlayheadChanged));

    public double[]? Peaks
    {
        get => (double[]?)GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    /// <summary>播放头位置（秒）</summary>
    public double Playhead
    {
        get => (double)GetValue(PlayheadProperty);
        set => SetValue(PlayheadProperty, value);
    }

    /// <summary>总时长（秒）</summary>
    public double Duration { get; set; } = 1;

    /// <summary>裁剪选区起止（秒），-1 表示未选择</summary>
    public double SelectionStart { get; private set; } = -1;
    public double SelectionEnd { get; private set; } = -1;

    /// <summary>选区变化事件（起止秒）</summary>
    public event Action<double, double>? SelectionChanged;

    private bool _dragging;

    private static readonly Brush BgBrush = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
    private static readonly Brush PlayedBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
    private static readonly Brush UnplayedBrush = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xAA));
    private static readonly Brush SelectionFill = new SolidColorBrush(Color.FromArgb(0x2E, 0x25, 0x63, 0xEB));
    private static readonly Brush SelectionLine = new SolidColorBrush(Color.FromArgb(0xE6, 0x60, 0xA5, 0xFA));
    private static readonly Pen PlayedPen;
    private static readonly Pen UnplayedPen;
    private static readonly Pen PlayheadPen;

    // 播放头用独立视觉绘制，避免每帧重绘整条波形导致卡顿
    private readonly VisualCollection _visuals;
    private readonly DrawingVisual _playheadVisual = new();

    static WaveformView()
    {
        PlayedBrush.Freeze();
        UnplayedBrush.Freeze();
        PlayedPen = new Pen(PlayedBrush, 1);
        UnplayedPen = new Pen(UnplayedBrush, 1);
        var ph = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));
        ph.Freeze();
        PlayheadPen = new Pen(ph, 1.2);
        PlayedPen.Freeze();
        UnplayedPen.Freeze();
        PlayheadPen.Freeze();
    }

    private static void OnPlayheadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((WaveformView)d).UpdatePlayheadVisual();

    protected override int VisualChildrenCount => _visuals.Count;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    /// <summary>只重绘播放头（独立轻量视觉，每帧仅一条线）</summary>
    private void UpdatePlayheadVisual()
    {
        var dc = _playheadVisual.RenderOpen();
        double w = ActualWidth, h = ActualHeight;
        if (Playhead > 0 && w > 2 && h > 2)
        {
            double x = Playhead / Math.Max(Duration, 1e-6) * w;
            dc.DrawLine(PlayheadPen, new Point(x, 0), new Point(x, h));
        }
        dc.Close();
    }

    public WaveformView()
    {
        ClipToBounds = true;
        Cursor = Cursors.Cross;
        Focusable = true;
        _visuals = new VisualCollection(this) { _playheadVisual };
    }

    public double TimeToX(double seconds)
        => Duration <= 0 ? 0 : seconds / Duration * ActualWidth;

    public double XToTime(double x)
        => Duration <= 0 ? 0 : Math.Clamp(x, 0, ActualWidth) / Math.Max(ActualWidth, 1) * Duration;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth, h = ActualHeight;
        if (w <= 2 || h <= 2) return;

        dc.DrawRectangle(BgBrush, null, new Rect(0, 0, w, h));

        var peaks = Peaks;
        if (peaks is null || peaks.Length == 0)
        {
            // 占位提示
            var ft = new FormattedText("（无波形）",
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"), 12,
                new SolidColorBrush(Color.FromRgb(0x71, 0x71, 0x7A)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(10, (h - ft.Height) / 2));
            return;
        }

        double mid = h / 2;
        double amp = Math.Max(2, h / 2 - 4);

        // 选中的区域显示为蓝色波形，其余灰色（不再按播放进度染色）
        double selSx = SelectionStart >= 0 ? TimeToX(SelectionStart) : -1;
        double selEx = SelectionEnd > SelectionStart ? TimeToX(SelectionEnd) : -1;
        bool hasSelection = selSx >= 0 && selEx > selSx;

        int cols = (int)Math.Min(peaks.Length, Math.Max(1, w / 2));
        for (int i = 0; i < cols; i++)
        {
            int start = i * peaks.Length / cols;
            int end = Math.Max(start + 1, (i + 1) * peaks.Length / cols);
            double peak = 0;
            for (int j = start; j < end && j < peaks.Length; j++)
                peak = Math.Max(peak, peaks[j]);
            peak = Math.Clamp(peak, 0, 1) * amp;

            double x = (i + 0.5) * w / cols;
            bool inSel = hasSelection && x >= selSx && x <= selEx;
            dc.DrawLine(inSel ? PlayedPen : UnplayedPen, new Point(x, mid - peak), new Point(x, mid + peak));
        }

        // 裁剪选区
        if (SelectionStart >= 0 && SelectionEnd > SelectionStart)
        {
            double sx = TimeToX(SelectionStart);
            double ex = TimeToX(SelectionEnd);
            if (ex - sx > 0)
            {
                dc.DrawRectangle(SelectionFill, null, new Rect(sx, 0, ex - sx, h));
                dc.DrawRectangle(SelectionLine, null, new Rect(sx - 2, 0, 4, h));
                dc.DrawRectangle(SelectionLine, null, new Rect(ex - 2, 0, 4, h));
            }
        }

        // 播放头改由独立视觉 UpdatePlayheadVisual() 绘制（避免整条波形每帧重绘）
        UpdatePlayheadVisual();
    }

    // ================= 鼠标交互：拖动选择裁剪范围 =================

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        double t = XToTime(e.GetPosition(this).X);
        SelectionStart = t;
        SelectionEnd = t;
        _dragging = true;
        CaptureMouse();
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            SelectionEnd = XToTime(e.GetPosition(this).X);
            if (SelectionEnd < SelectionStart)
            {
                (SelectionStart, SelectionEnd) = (SelectionEnd, SelectionStart);
            }
            InvalidateVisual();
            SelectionChanged?.Invoke(SelectionStart, SelectionEnd);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _dragging = false;
        ReleaseMouseCapture();
        if (SelectionEnd - SelectionStart < 0.05)
        {
            SelectionStart = -1;
            SelectionEnd = -1;
            SelectionChanged?.Invoke(-1, -1);
        }
        InvalidateVisual();
    }

    /// <summary>清除选区</summary>
    public void ClearSelection()
    {
        SelectionStart = -1;
        SelectionEnd = -1;
        InvalidateVisual();
    }
}
