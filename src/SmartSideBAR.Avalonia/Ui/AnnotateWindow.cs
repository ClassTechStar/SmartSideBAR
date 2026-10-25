// 屏幕批注窗 (§6.3) —— ICC-CE (InkCanvasForClass) 算法移植版:
//   * 速度模拟压感 (慢粗快细, smoothstep 笔锋映射) —— ICC MW_SimulatePressure 公式
//   * 收笔后 Catmull-Rom 平滑 (ICC 的滑动窗五次贝塞尔概念的轻量等价实现)
//   * 荧光笔 = 半透明宽笔 + 单次几何合成 (同一路径自交不叠深)
//   * 激光笔 = 三层描边 (光晕/主体/白芯) + 1.2s 自动消隐, 不进历史不导出
//   * 形状 = 直线/箭头/矩形/椭圆, 预览始终由 (起点, 当前点) 整体重生成 (ICC 不变量)
//   * 橡皮 = 笔迹级命中擦除; 撤销 = 操作前快照 (ICC TimeMachine 的简化语义)
// Avalonia 统一 Pointer 事件 (D1 根修); 导出双模式: 仅笔迹 / 含背景合成 (P1-8)。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Native;
using AloLayout = global::Avalonia.Layout;

namespace SmartSideBAR.Avalonia.Ui;

public enum AnnotateTool { Pen, Highlighter, Laser, Line, Arrow, Rect, Ellipse, Eraser }

/// <summary>笔迹画布: 全类型指针接受, 渲染即状态。</summary>
public sealed class InkCanvas : Control
{
    public enum StrokeKind { Pen, Highlighter, Laser, Shape }

    public sealed class Stroke
    {
        public List<Point> Points { get; init; } = [];
        /// <summary>箭头等多段子路径 (单条笔画 = 一次撤销单元)。</summary>
        public List<List<Point>>? SubPaths { get; init; }
        /// <summary>逐点压感 (0~1), 与 ICC 速度模拟公式对应; 形状笔画为空。</summary>
        public List<double>? Pressure { get; set; }
        public Color Color { get; init; } = Colors.DodgerBlue;
        public double Width { get; init; } = 4;
        public StrokeKind Kind { get; init; } = StrokeKind.Pen;
    }

    private sealed class LaserStroke
    {
        public required List<Point> Points;
        public required Color Color;
        public required double Width;
        public readonly DateTime Born = DateTime.UtcNow;
        public double Age => (DateTime.UtcNow - Born).TotalSeconds;
    }

    private readonly List<Stroke> _strokes = [];
    private readonly List<List<Stroke>> _undo = []; // 操作前快照 (≤50)
    private readonly List<LaserStroke> _lasers = [];
    private readonly DispatcherTimer? _laserTimer;
    private Stroke? _current;
    private Point _shapeStart;
    private Color _color = Color.Parse("#E53935");
    private double _width = 4;
    private AnnotateTool _tool = AnnotateTool.Pen;
    private double _eraserRadius = 16;

    public event EventHandler? StrokesChanged;

    public void SetColor(Color c) => _color = c;
    public void SetWidth(double w) => _width = w;
    public void SetTool(AnnotateTool tool) => _tool = tool;
    public int Count => _strokes.Count;

    // ---- 撤销 (快照语义: 每次提交性操作前压栈) ----

    private void PushUndo()
    {
        _undo.Add([.. _strokes]);
        if (_undo.Count > 50) _undo.RemoveAt(0);
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        _strokes.Clear();
        _strokes.AddRange(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
        InvalidateVisual();
        StrokesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (_strokes.Count == 0) return;
        PushUndo();
        _strokes.Clear();
        InvalidateVisual();
        StrokesChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- 笔画生命周期 (由宿主 inkHost 驱动) ----

    public void BeginStroke(Point p, IPointer pointer, IInputElement? captureTarget = null)
    {
        switch (_tool)
        {
            case AnnotateTool.Eraser:
                PushUndo();
                EraseAt(p);
                break;
            case AnnotateTool.Line or AnnotateTool.Arrow or AnnotateTool.Rect or AnnotateTool.Ellipse:
                _shapeStart = p;
                _current = new Stroke { Color = _color, Width = _width, Kind = StrokeKind.Shape };
                UpdateShape(p);
                break;
            default:
                PushUndo();
                var kind = _tool switch
                {
                    AnnotateTool.Highlighter => StrokeKind.Highlighter,
                    AnnotateTool.Laser => StrokeKind.Laser,
                    _ => StrokeKind.Pen,
                };
                _current = new Stroke
                {
                    Color = _color,
                    Width = kind == StrokeKind.Highlighter ? Math.Max(_width * 2, 14) : _width,
                    Kind = kind,
                    Pressure = kind == StrokeKind.Pen ? [0.7] : null,
                };
                _current.Points.Add(p);
                break;
        }
        // B5 根修: 捕获目标必须是「挂载了 Pointer 处理器的元素」。
        pointer.Capture(captureTarget ?? this);
        InvalidateVisual();
    }

    public void ExtendStroke(Point p)
    {
        switch (_tool)
        {
            case AnnotateTool.Eraser:
                EraseAt(p);
                break;
            case AnnotateTool.Line or AnnotateTool.Arrow or AnnotateTool.Rect or AnnotateTool.Ellipse:
                UpdateShape(p);
                break;
            case AnnotateTool.Laser:
                if (_current is not null)
                {
                    _current.Points.Add(p);
                    // 激光笔实时画到 _lasers (不进历史), 离开即逝
                    _lasers.Add(new LaserStroke { Points = [.. _current.Points], Color = _current.Color, Width = _current.Width });
                    if (_lasers.Count > 12) _lasers.RemoveAt(0);
                    EnsureLaserTimer();
                    InvalidateVisual();
                }
                break;
            default:
                if (_current is null) return;
                _current.Points.Add(p);
                if (_current.Pressure is { } pr)
                {
                    pr.Add(PressureFromSpeed(_current.Points, pr));
                }
                InvalidateVisual();
                break;
        }
    }

    public void EndStroke()
    {
        if (_current is null)
        {
            InvalidateVisual();
            return;
        }
        if (_current.Kind == StrokeKind.Shape)
        {
            if (_current.SubPaths is { Count: > 0 }) _strokes.Add(_current);
        }
        else if (_current.Kind == StrokeKind.Laser)
        {
            // 激光笔已实时进 _lasers, 不持久化
        }
        else if (_current.Points.Count > 0)
        {
            if (_current.Kind == StrokeKind.Pen && _current.Points.Count >= 3)
            {
                SmoothCurrent();
            }
            _strokes.Add(_current);
        }
        _current = null;
        InvalidateVisual();
        StrokesChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- 速度模拟压感 (ICC 公式: 慢 0.9 / 快 0.22, 中速 0.5) ----

    private static double PressureFromSpeed(List<Point> pts, List<double> pressures)
    {
        var n = pts.Count;
        if (n < 3) return 0.7;
        var speed = (Dist(pts[n - 3], pts[n - 2]) + Dist(pts[n - 2], pts[n - 1])) / 20.0;
        double p;
        if (speed >= 0.25) p = 0.5 - 0.3 * (Math.Min(speed, 1.5) - 0.3) / 1.2;
        else if (speed >= 0.05) p = 0.5;
        else p = 0.5 + 0.4 * (0.05 - speed) / 0.05;
        var prev = pressures.Count > 0 ? pressures[^1] : p;
        return (p + prev) / 2; // 相邻均值防抖
    }

    private static double Dist(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    // ---- 收笔平滑 (Catmull-Rom; ICC 滑窗五次贝塞尔的轻量等价) ----

    private void SmoothCurrent()
    {
        var cur = _current!;
        var raw = cur.Points;
        var rawP = cur.Pressure!;
        var smooth = new List<Point> { raw[0] };
        var smoothP = new List<double> { rawP[0] };
        for (var i = 0; i < raw.Count - 1; i++)
        {
            var p0 = raw[Math.Max(i - 1, 0)];
            var p1 = raw[i];
            var p2 = raw[i + 1];
            var p3 = raw[Math.Min(i + 2, raw.Count - 1)];
            var q0 = rawP[Math.Max(i - 1, 0)];
            var q1 = rawP[i];
            var q2 = rawP[i + 1];
            var q3 = rawP[Math.Min(i + 2, rawP.Count - 1)];
            for (var j = 1; j <= 6; j++)
            {
                var t = j / 6.0;
                smooth.Add(CatmullRom(p0, p1, p2, p3, t));
                smoothP.Add(q1 + (q2 - q1) * t); // 标量压感线性插值
            }
        }
        cur.Points.Clear();
        cur.Points.AddRange(smooth);
        cur.Pressure = smoothP;
    }

    private static Point CatmullRom(Point p0, Point p1, Point p2, Point p3, double t)
    {
        double t2 = t * t, t3 = t2 * t;
        return new Point(
            0.5 * ((2 * p1.X) + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3),
            0.5 * ((2 * p1.Y) + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3));
    }

    // ---- 形状 (预览由 (起点, 当前点) 整体重生成 —— ICC 不变量) ----

    private void UpdateShape(Point p)
    {
        var w = _width;
        List<List<Point>> sub = _tool switch
        {
            AnnotateTool.Line => [[_shapeStart, p]],
            AnnotateTool.Rect => [[_shapeStart, new Point(p.X, _shapeStart.Y), new Point(p.X, p.Y), new Point(_shapeStart.X, p.Y), _shapeStart]],
            AnnotateTool.Ellipse => [Ellipse(_shapeStart, p)],
            AnnotateTool.Arrow => Arrow(_shapeStart, p, w),
            _ => [[p]],
        };
        _current = new Stroke { Color = _color, Width = w, Kind = StrokeKind.Shape, SubPaths = sub };
        InvalidateVisual();
    }

    private static List<Point> Ellipse(Point a, Point b)
    {
        var cx = (a.X + b.X) / 2;
        var cy = (a.Y + b.Y) / 2;
        var rx = Math.Abs(b.X - a.X) / 2;
        var ry = Math.Abs(b.Y - a.Y) / 2;
        var pts = new List<Point>(73);
        for (var i = 0; i <= 72; i++)
        {
            var t = i * Math.PI / 36;
            pts.Add(new Point(cx + rx * Math.Cos(t), cy + ry * Math.Sin(t)));
        }
        return pts;
    }

    /// <summary>箭头 = 主轴 + 两支羽翼 (ICC MW_ShapeDrawing 几何, 尺寸随机宽自适应)。</summary>
    private static List<List<Point>> Arrow(Point start, Point end, double w)
    {
        var barb = Math.Max(14, w * 3.5);
        var ang = Math.Atan2(end.Y - start.Y, end.X - start.X);
        var b1 = new Point(end.X - barb * Math.Cos(ang - 0.45), end.Y - barb * Math.Sin(ang - 0.45));
        var b2 = new Point(end.X - barb * Math.Cos(ang + 0.45), end.Y - barb * Math.Sin(ang + 0.45));
        return [[start, end], [end, b1], [end, b2]];
    }

    // ---- 橡皮 (笔迹级: 任一线段进入橡皮半径即整条移除; ICC stroke-eraser 语义) ----

    private void EraseAt(Point p)
    {
        var removed = false;
        for (var i = _strokes.Count - 1; i >= 0; i--)
        {
            if (!HitsStroke(_strokes[i], p, _eraserRadius)) continue;
            _strokes.RemoveAt(i);
            removed = true;
        }
        if (removed)
        {
            InvalidateVisual();
            StrokesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool HitsStroke(Stroke s, Point p, double radius)
    {
        var paths = s.SubPaths ?? [s.Points];
        foreach (var path in paths)
        {
            for (var i = 0; i < path.Count; i++)
            {
                if (Dist(path[i], p) <= radius + s.Width / 2) return true;
            }
        }
        return false;
    }

    // ---- 激光笔自动消隐 (1.2s, ICC fade 语义) ----

    private void EnsureLaserTimer()
    {
        if (_laserTimer is not null) return;
        _laserTimerField = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _laserTimerField.Tick += (_, _) =>
        {
            _lasers.RemoveAll(l => l.Age > 1.2);
            if (_lasers.Count == 0) _laserTimerField!.Stop();
            InvalidateVisual();
        };
        _laserTimerField.Start();
    }

    private DispatcherTimer? _laserTimerField;

    // ---- 渲染 ----

    public override void Render(DrawingContext context) => DrawAll(context, 1.0);

    internal void DrawAll(DrawingContext ctx, double opacity)
    {
        using var _ = ctx.PushOpacity(opacity);
        // 荧光笔在最底层 (ICC 同序), 激光笔最顶层
        foreach (var stroke in _strokes.Where(s => s.Kind == StrokeKind.Highlighter)) DrawStroke(ctx, stroke);
        foreach (var stroke in _strokes.Where(s => s.Kind != StrokeKind.Highlighter)) DrawStroke(ctx, stroke);
        foreach (var laser in _lasers) DrawLaser(ctx, laser);
    }

    private static void DrawStroke(DrawingContext ctx, Stroke stroke)
    {
        var paths = stroke.SubPaths ?? [stroke.Points];
        var isHighlighter = stroke.Kind == StrokeKind.Highlighter;
        using var _ = isHighlighter ? ctx.PushOpacity(0.4) : default(DrawingContext.PushedState);

        if (isHighlighter)
        {
            // 荧光笔: 方头宽笔 (ICC: flat caps + 宽度下限), 单几何一次合成不叠深
            var hw = Math.Max(stroke.Width, 14);
            foreach (var path in paths)
            {
                if (path.Count < 2) continue;
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    g.BeginFigure(path[0], isFilled: false);
                    for (var i = 1; i < path.Count; i++) g.LineTo(path[i]);
                    g.EndFigure(false);
                }
                ctx.DrawGeometry(null, new Pen(new SolidColorBrush(stroke.Color), hw, lineCap: PenLineCap.Flat, lineJoin: PenLineJoin.Miter), geo);
            }
            return;
        }

        if (stroke.Pressure is { Count: > 1 } pressure && stroke.SubPaths is null)
        {
            // 压感逐段渲染 (ICC StrokeVisual: scale = clamp(0.42 + 1.16×p, 0.22, 2.1))
            for (var i = 1; i < stroke.Points.Count; i++)
            {
                var scale = Math.Clamp(0.42 + 1.16 * pressure[i], 0.22, 2.1);
                var pen = new Pen(new SolidColorBrush(stroke.Color), stroke.Width * scale, lineCap: PenLineCap.Round);
                ctx.DrawLine(pen, stroke.Points[i - 1], stroke.Points[i]);
            }
            if (stroke.Points.Count == 1)
            {
                var p0 = stroke.Points[0];
                ctx.DrawEllipse(new SolidColorBrush(stroke.Color), null, p0, stroke.Width / 2, stroke.Width / 2);
            }
            return;
        }

        foreach (var path in paths)
        {
            if (path.Count == 0) continue;
            if (path.Count == 1)
            {
                var p = path[0];
                ctx.DrawEllipse(new SolidColorBrush(stroke.Color), null, p, stroke.Width / 2, stroke.Width / 2);
                continue;
            }
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(path[0], isFilled: false);
                for (var i = 1; i < path.Count; i++) g.LineTo(path[i]);
                g.EndFigure(false);
            }
            ctx.DrawGeometry(null, new Pen(new SolidColorBrush(stroke.Color), stroke.Width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
        }
    }

    /// <summary>激光笔三层渲染 (ICC InkFadeManager 配方: 光晕 2.2×/0.22 + 主体 1.02×/0.92 + 白芯 0.38×/0.98)。</summary>
    private static void DrawLaser(DrawingContext ctx, LaserStroke laser)
    {
        if (laser.Points.Count < 2) return;
        var fade = laser.Age >= 0.8 ? Math.Max(0, 1 - (laser.Age - 0.8) / 0.4) : 1.0;
        using var _ = ctx.PushOpacity(fade);
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(laser.Points[0], isFilled: false);
            for (var i = 1; i < laser.Points.Count; i++) g.LineTo(laser.Points[i]);
            g.EndFigure(false);
        }
        var body = new Pen(new SolidColorBrush(laser.Color), laser.Width, lineCap: PenLineCap.Round);
        ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(0x38, laser.Color.R, laser.Color.G, laser.Color.B)), laser.Width * 2.2, lineCap: PenLineCap.Round), geo);
        ctx.DrawGeometry(null, new Pen(new SolidColorBrush(laser.Color), laser.Width * 1.02, lineCap: PenLineCap.Round), geo);
        ctx.DrawGeometry(null, new Pen(new SolidColorBrush(BlendTowardWhite(laser.Color, 0.94)), laser.Width * 0.38, lineCap: PenLineCap.Round), geo);
    }

    private static Color BlendTowardWhite(Color c, double amount) => Color.FromArgb(
        c.A,
        (byte)(c.R + (255 - c.R) * amount),
        (byte)(c.G + (255 - c.G) * amount),
        (byte)(c.B + (255 - c.B) * amount));

    /// <summary>导出仅笔迹 (透明底, 激光笔除外)。B26 根修: 位图 DPI 固定 96, 缩放只由 PushTransform 施加一次。</summary>
    public Bitmap ExportStrokesOnly(int width, int height, double scale)
    {
        var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        {
            using (ctx.PushTransform(Matrix.CreateScale(scale, scale)))
            {
                DrawAll(ctx, 1.0);
            }
        }
        return rtb;
    }
}

/// <summary>批注主窗: 全屏透明置顶; 工具条 = 工具/颜色/粗细/动作 三组。</summary>
public sealed class AnnotateWindow : Window
{
    private readonly InkCanvas _ink = new();
    private readonly string _dir;
    private readonly string _format;
    private readonly byte[]? _backgroundPng; // 打开时的全屏底图 (WithBackground 用)
    private readonly List<Button> _toolButtons = [];
    private readonly List<Button> _colorButtons = [];
    private readonly List<Button> _widthButtons = [];

    public AnnotateWindow(byte[]? backgroundPng, string dir, string format)
    {
        _backgroundPng = backgroundPng;
        OverlayGate.Enter();
        _dir = dir;
        _format = format;

        // B25 根修: 透明/无边框/置顶必须在 Show 之前设定 (分层窗口语义)。
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        CanResize = false;
        ShowActivated = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [];

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // B25 根修: 以打开瞬间的屏幕快照作底 —— 大面积窗口的分层透明在 Win32 上不可靠。
        var backdrop = new Image { Stretch = Stretch.Fill, IsHitTestVisible = false };
        if (_backgroundPng is not null)
        {
            using var bgStream = new MemoryStream(_backgroundPng);
            backdrop.Source = new Bitmap(bgStream);
        }
        else
        {
            backdrop.Source = null;
            grid.Background = new SolidColorBrush(Color.FromArgb(255, 32, 38, 48));
        }
        Grid.SetRow(backdrop, 0);
        Grid.SetRowSpan(backdrop, 2);
        grid.Children.Add(backdrop);

        _ink.HorizontalAlignment = AloLayout.HorizontalAlignment.Stretch;
        _ink.VerticalAlignment = AloLayout.VerticalAlignment.Stretch;
        Grid.SetRow(_ink, 0);
        Grid.SetRowSpan(_ink, 2);
        grid.Children.Add(_ink);

        var inkHost = new Border
        {
            Background = Brushes.Transparent,
            HorizontalAlignment = AloLayout.HorizontalAlignment.Stretch,
            VerticalAlignment = AloLayout.VerticalAlignment.Stretch,
        };
        inkHost.PointerPressed += (_, e) =>
        {
            var p = e.GetCurrentPoint(inkHost);
            if (!p.Properties.IsLeftButtonPressed) return;
            _ink.BeginStroke(p.Position, e.Pointer, inkHost); // 捕获到 inkHost 自身 (B5)
            e.Handled = true;
        };
        inkHost.PointerMoved += (_, e) =>
        {
            if (e.Pointer.Captured != inkHost) return;
            _ink.ExtendStroke(e.GetCurrentPoint(inkHost).Position);
            e.Handled = true;
        };
        inkHost.PointerReleased += (_, e) =>
        {
            if (e.Pointer.Captured != inkHost) return;
            _ink.EndStroke();
            e.Pointer.Capture(null);
            e.Handled = true;
        };
        Grid.SetRow(inkHost, 0);
        Grid.SetRowSpan(inkHost, 2);
        grid.Children.Add(inkHost);

        var toolbar = BuildToolbar();
        Grid.SetRow(toolbar, 1);
        grid.Children.Add(toolbar);

        Content = grid;

        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape) Close();
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        OverlayGate.Exit();
        base.OnClosed(e);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        FillPrimaryMonitor();
    }

    /// <summary>铺满主屏 (物理像素 → DIP)。用显式几何替代 FullScreen, 保住分层透明。</summary>
    private void FillPrimaryMonitor()
    {
        var (x, y, w, h) = Win32Display.PrimaryMonitorRectPx();
        var scale = Win32Display.GetScaling(TryGetPlatformHandle()?.Handle ?? 0);
        if (scale <= 0) scale = 1.0;
        Position = new PixelPoint(x, y);
        Width = w / scale;
        Height = h / scale;
    }

    // ---- 工具条 (三组: 工具 / 颜色×6 / 粗细 + 动作; 选中项 accent 描边) ----

    private Control BuildToolbar()
    {
        var panel = new StackPanel
        {
            Orientation = AloLayout.Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = AloLayout.HorizontalAlignment.Center,
            VerticalAlignment = AloLayout.VerticalAlignment.Top,
            Margin = new Thickness(0, 10, 0, 0),
        };

        (AnnotateTool Tool, string Label)[] tools =
        [
            (AnnotateTool.Pen, "笔"), (AnnotateTool.Highlighter, "荧"), (AnnotateTool.Laser, "激光"),
            (AnnotateTool.Line, "线"), (AnnotateTool.Arrow, "箭"), (AnnotateTool.Rect, "框"),
            (AnnotateTool.Ellipse, "圆"), (AnnotateTool.Eraser, "擦"),
        ];
        foreach (var (tool, label) in tools)
        {
            var t = tool;
            Button btn = null!;
            btn = ToolBtn(label, () =>
            {
                _ink.SetTool(t);
                MarkActive(_toolButtons, btn);
            });
            _toolButtons.Add(btn);
            panel.Children.Add(btn);
        }
        if (_toolButtons.Count > 0) MarkActive(_toolButtons, _toolButtons[0]);

        panel.Children.Add(Separator());

        // 1.0 调色板: 红/蓝/绿/黄/白/黑
        foreach (var hex in new[] { "#E53935", "#2B6EE0", "#27AE60", "#FBC02D", "#FFFFFF", "#111111" })
        {
            var c = Color.Parse(hex);
            var btn = new Button
            {
                Width = 26,
                Height = 26,
                Background = new SolidColorBrush(c),
                CornerRadius = new CornerRadius(13),
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                Tag = hex,
            };
            btn.Click += (_, _) =>
            {
                _ink.SetColor(c);
                MarkActive(_colorButtons, btn);
            };
            _colorButtons.Add(btn);
            panel.Children.Add(btn);
        }
        if (_colorButtons.Count > 1) MarkActive(_colorButtons, _colorButtons[1]); // 默认蓝

        panel.Children.Add(Separator());

        foreach (var (label, w) in new[] { ("细", 2.0), ("中", 5.0), ("粗", 10.0) })
        {
            var width = w;
            Button btn = null!;
            btn = ToolBtn(label, () =>
            {
                _ink.SetWidth(width);
                MarkActive(_widthButtons, btn);
            }, minWidth: 34);
            _widthButtons.Add(btn);
            panel.Children.Add(btn);
        }
        if (_widthButtons.Count > 1) MarkActive(_widthButtons, _widthButtons[1]);

        panel.Children.Add(Separator());

        panel.Children.Add(ActionBtn("撤销", () => _ink.Undo()));
        panel.Children.Add(ActionBtn("清空", _ink.Clear));
        panel.Children.Add(ActionBtn("存笔迹", () => Save(withBackground: false)));
        panel.Children.Add(ActionBtn("存截图", () => Save(withBackground: true)));
        panel.Children.Add(ActionBtn("退出", Close));
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(230, 20, 26, 34)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 6),
            Child = panel,
        };
    }

    private static Control Separator() => new Border
    {
        Width = 1,
        Height = 20,
        Margin = new Thickness(4, 0),
        Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
    };

    private static Button ToolBtn(string label, Action act, double minWidth = 30)
    {
        var b = new Button
        {
            Content = label,
            Padding = new Thickness(6, 4),
            MinWidth = minWidth,
            FontSize = 12,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(1.5),
            BorderBrush = Brushes.Transparent,
        };
        b.Click += (_, _) => act();
        return b;
    }

    private static Button ActionBtn(string label, Action act)
    {
        var b = new Button
        {
            Content = label,
            Padding = new Thickness(8, 4),
            FontSize = 12,
            Foreground = Brushes.White,
        };
        b.Click += (_, _) => act();
        return b;
    }

    /// <summary>选中态 = accent 描边 (KoBar native-calm: 填充/描边变化, 不发光)。</summary>
    private static void MarkActive(List<Button> group, Button active)
    {
        foreach (var b in group)
        {
            b.BorderBrush = ReferenceEquals(b, active)
                ? new SolidColorBrush(Color.Parse("#4D8DF7"))
                : Brushes.Transparent;
        }
    }

    private void Save(bool withBackground)
    {
        try
        {
            var path = Path.Combine(_dir, $"annotate_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            Directory.CreateDirectory(_dir);
            var scale = Win32Display.GetScaling(TryGetPlatformHandle()?.Handle ?? 0);
            var w = (int)(Bounds.Width * scale);
            var h = (int)(Bounds.Height * scale);

            if (withBackground && _backgroundPng is not null)
            {
                using var bg = new Bitmap(new MemoryStream(_backgroundPng));
                using var strokes = _ink.ExportStrokesOnly(w, h, scale);
                using var composite = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
                using (var ctx = composite.CreateDrawingContext())
                {
                    ctx.DrawImage(bg, new Rect(0, 0, w, h));
                    ctx.DrawImage(strokes, new Rect(0, 0, w, h));
                }
                composite.Save(path);
            }
            else
            {
                using var strokes = _ink.ExportStrokesOnly(w, h, scale);
                strokes.Save(path);
            }
            ToastService.Instance.Show($"批注已保存: {path}", "success");
            Close();
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"批注保存失败: {ex.Message}", "error");
        }
    }
}
