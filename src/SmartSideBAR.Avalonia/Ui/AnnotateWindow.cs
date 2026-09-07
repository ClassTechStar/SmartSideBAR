// 屏幕批注窗 (§6.3): Avalonia 统一 Pointer 事件 —— 鼠标/触摸/笔天然同一事件流 (D1 根修);
// 撤销栈即时可观察 (D3); 导出双模式: 仅笔迹 (透明 PNG) / 含背景合成 (P1-8)。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Native;
using AloLayout = global::Avalonia.Layout;

namespace SmartSideBAR.Avalonia.Ui;

/// <summary>笔迹画布: 全类型指针接受, 渲染即状态。</summary>
public sealed class InkCanvas : Control
{
    public sealed class Stroke
    {
        public List<Point> Points { get; } = [];
        public Color Color { get; init; } = Colors.DodgerBlue;
        public double Width { get; init; } = 4;
    }

    private readonly List<Stroke> _strokes = [];
    private Stroke? _current;
    private Color _color = Colors.DodgerBlue;
    private double _width = 4;

    public event EventHandler? StrokesChanged;

    public void SetColor(Color c) => _color = c;
    public void SetWidth(double w) => _width = w;

    public int Count => _strokes.Count;

    public void Undo()
    {
        if (_strokes.Count == 0) return;
        _strokes.RemoveAt(_strokes.Count - 1);
        InvalidateVisual();
        StrokesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _strokes.Clear();
        InvalidateVisual();
        StrokesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>导出仅笔迹 (透明底)。width/height 为物理像素, DIP 笔迹按 scale 放大。</summary>
    public Bitmap ExportStrokesOnly(int width, int height, double scale)
    {
        var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96 * scale, 96 * scale));
        using (var ctx = rtb.CreateDrawingContext())
        {
            using (ctx.PushTransform(Matrix.CreateScale(scale, scale)))
            {
                DrawAll(ctx, 1.0);
            }
        }
        return rtb;
    }

    internal void DrawAll(DrawingContext ctx, double opacity)
    {
        using var _ = ctx.PushOpacity(opacity);
        foreach (var stroke in _strokes)
        {
            if (stroke.Points.Count == 1)
            {
                var p = stroke.Points[0];
                ctx.DrawEllipse(new SolidColorBrush(stroke.Color), null, p, stroke.Width / 2, stroke.Width / 2);
                continue;
            }
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(stroke.Points[0], isFilled: false);
                g.LineTo(stroke.Points[0]);
                for (var i = 1; i < stroke.Points.Count; i++) g.LineTo(stroke.Points[i]);
                g.EndFigure(false);
            }
            ctx.DrawGeometry(null, new Pen(new SolidColorBrush(stroke.Color), stroke.Width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
        }
        if (_current is { } cur && cur.Points.Count > 0)
        {
            // 进行中笔迹
            for (var i = 1; i < cur.Points.Count; i++)
            {
                ctx.DrawLine(new Pen(new SolidColorBrush(cur.Color), cur.Width, lineCap: PenLineCap.Round), cur.Points[i - 1], cur.Points[i]);
            }
        }
    }

    public override void Render(DrawingContext context) => DrawAll(context, 1.0);

    // ---- 由宿主 Border (Background=Transparent, 保证可命中) 驱动的笔画 API ----

    public void BeginStroke(Point p, IPointer pointer)
    {
        _current = new Stroke { Color = _color, Width = _width };
        _current.Points.Add(p);
        pointer.Capture(this);
    }

    public void ExtendStroke(Point p)
    {
        if (_current is null) return;
        _current.Points.Add(p);
        InvalidateVisual();
    }

    public void EndStroke()
    {
        if (_current is null) return;
        if (_current.Points.Count > 0) _strokes.Add(_current);
        _current = null;
        InvalidateVisual();
        StrokesChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>批注主窗: 全屏透明置顶; 导出 StrokesOnly | WithBackground。</summary>
public sealed class AnnotateWindow : Window
{
    private readonly InkCanvas _ink = new();
    private readonly string _dir;
    private readonly string _format;
    private readonly byte[]? _backgroundPng; // 打开时的全屏底图 (WithBackground 用)

    public AnnotateWindow(byte[]? backgroundPng, string dir, string format)
    {
        _backgroundPng = backgroundPng;
        _dir = dir;
        _format = format;
    }

    protected override void OnOpened(EventArgs e)
    {
        Topmost = true;
        WindowState = WindowState.FullScreen;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [];

        var toolbar = BuildToolbar();
        var inkHost = new Border { Background = Brushes.Transparent }; // Transparent 笔刷 = 可命中
        inkHost.PointerPressed += (_, e) =>
        {
            var p = e.GetCurrentPoint(inkHost);
            if (!p.Properties.IsLeftButtonPressed) return;
            _ink.BeginStroke(p.Position, e.Pointer);
            e.Handled = true; // Pointer 统一: 触屏/笔/鼠标 (D1 根修)
        };
        inkHost.PointerMoved += (_, e) =>
        {
            if (e.Pointer.Captured is null) return;
            _ink.ExtendStroke(e.GetCurrentPoint(inkHost).Position);
            e.Handled = true;
        };
        inkHost.PointerReleased += (_, e) =>
        {
            _ink.EndStroke();
            e.Handled = true;
        };
        Content = new Panel
        {
            Children = { inkHost, _ink, toolbar },
        };
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape) Close();
        };
        base.OnOpened(e);
    }

    private Control BuildToolbar()
    {
        Button ColorBtn(string hex, double width = 26)
        {
            var btn = new Button
            {
                Width = width,
                Height = 26,
                Background = new SolidColorBrush(Color.Parse(hex)),
                CornerRadius = new CornerRadius(13),
                Tag = hex,
            };
            btn.Click += (_, _) => _ink.SetColor(Color.Parse(hex));
            return btn;
        }
        Button ActionBtn(string label, Action act)
        {
            var b = new Button { Content = label, Padding = new Thickness(10, 4) };
            b.Click += (_, _) => act();
            return b;
        }
        var panel = new StackPanel
        {
            Orientation = AloLayout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = AloLayout.HorizontalAlignment.Center,
            VerticalAlignment = AloLayout.VerticalAlignment.Top,
            Margin = new Thickness(0, 10, 0, 0),
        };
        foreach (var c in new[] { "#2B6EE0", "#E53935", "#111111", "#FFFFFF", "#FBC02D" }) panel.Children.Add(ColorBtn(c));
        panel.Children.Add(ActionBtn("细", () => _ink.SetWidth(2)));
        panel.Children.Add(ActionBtn("中", () => _ink.SetWidth(5)));
        panel.Children.Add(ActionBtn("粗", () => _ink.SetWidth(10)));
        panel.Children.Add(ActionBtn("撤销", () => _ink.Undo()));
        panel.Children.Add(ActionBtn("清空", _ink.Clear));
        panel.Children.Add(ActionBtn("保存(仅笔迹)", () => Save(withBackground: false)));
        panel.Children.Add(ActionBtn("保存(含背景)", () => Save(withBackground: true)));
        panel.Children.Add(ActionBtn("退出", Close));
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(230, 20, 26, 34)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 6),
            Child = panel,
        };
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
                using var composite = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96 * scale, 96 * scale));
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
