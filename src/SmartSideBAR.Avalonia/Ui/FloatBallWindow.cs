// 悬浮球 (§6.14, 对齐 v1.1): floatball-icon 图元圆球 + 拖拽吸附 + 点击展开玻璃扇形菜单
// (白色 SVG 图标) + 双击开合侧栏。布局算法 = Core.FloatBallLayout (v1.1 逐函数移植)。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.FloatBall;
using SmartSideBAR.Windows.Native;

namespace SmartSideBAR.Avalonia.Ui;

public sealed class FloatBallWindow : Window
{
    private readonly ConfigService _config;
    private readonly string[] _actions;
    private readonly Action<string> _dispatch;
    private readonly Action _toggleSidebar;
    private readonly int _sizeDip;
    private readonly int _sizePx;
    private readonly double _scale;
    private readonly int _snapThreshold;

    private readonly Border _orb;
    private readonly Canvas _root = new();
    private readonly RectLike _workPx;      // 工作区 (物理像素)
    private bool _dragging;
    private bool _moved;
    private bool _expanded;
    private Point _pressPos;
    private PixelPoint _pressWindowPos;
    private PixelPoint _grabOffsetPx;
    private PixelPoint _restPosition;        // 收起态窗口位置 (物理像素) —— 展开/收起不得漂移
    private DateTime _lastTap = DateTime.MinValue;

    /// <param name="workPx">侧栏所在显示器的工作区 (物理像素)。</param>
    /// <param name="scale">窗口 DPI 缩放 (物理 = DIP × scale)。</param>
    public FloatBallWindow(ConfigService config, FloatBallConfig cfg, RectLike workPx, double scale,
        Action<string> dispatch, Action toggleSidebar)
    {
        _config = config;
        _actions = [.. cfg.Actions];
        _dispatch = dispatch;
        _toggleSidebar = toggleSidebar;
        _sizeDip = cfg.Size;
        _scale = scale > 0 ? scale : 1.0;
        _sizePx = (int)Math.Round(_sizeDip * _scale);
        _snapThreshold = cfg.SnapThreshold;
        _workPx = workPx;

        // Avalonia 的 Width/Height 是 DIP, Position 是物理像素 —— 两者不可混用 (B3 根修)。
        Width = _sizeDip;
        Height = _sizeDip;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [];
        ShowActivated = false;

        _orb = BuildOrb();
        _root.Children.Add(_orb);
        Content = _root;

        var pos = FloatBallLayout.ResolveBallPosition((cfg.X, cfg.Y), _workPx, _sizePx, "right");
        Position = new PixelPoint(pos.X, pos.Y);
        _restPosition = Position;

        PointerPressed += OnPress;
        PointerMoved += OnMove;
        PointerReleased += OnRelease;
        DoubleTapped += (_, _) =>
        {
            _toggleSidebar();
            if (_expanded) CollapseFan(); // 双击的第一击可能已展开, 一并收起
        };
    }

    private Border BuildOrb()
    {
        // v1.1 v7: 悬浮球 = floatball-icon.png 铺满正圆 + 边光环
        var orbIcon = new Image
        {
            Source = new Bitmap(AssetLoader.Open(new Uri("avares://SmartSideBAR.Avalonia/Assets/floatball-icon.png"))),
            Stretch = Stretch.UniformToFill,
        };

        // 边光环: 1.5px 白色渐变环 (v1.1 v7 ::after rim light 近似)
        var rim = new Border
        {
            CornerRadius = new CornerRadius(_sizeDip / 2.0),
            BorderThickness = new Thickness(1.5),
            Child = orbIcon,
            ClipToBounds = true,
        };
        rim.BorderBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#E6FFFFFF"), 0),
                new GradientStop(Color.Parse("#52FFFFFF"), 0.18),
                new GradientStop(Color.Parse("#1AFFFFFF"), 0.4),
                new GradientStop(Color.Parse("#33FFFFFF"), 0.62),
                new GradientStop(Color.Parse("#ADFFFFFF"), 1),
            },
        };

        var orb = new Border
        {
            Width = _sizeDip,
            Height = _sizeDip,
            CornerRadius = new CornerRadius(_sizeDip / 2.0),
            // v1.1 v7: 0 12px 40px 0.25 + 0 2px 8px 0.12
            BoxShadow = BoxShadows.Parse("0 12 40 #40000000, 0 2 8 #1F000000"),
            Child = rim,
            ClipToBounds = true,
        };
        return orb;
    }

    // ---- 拖拽 / 点击 ----

    private void OnPress(object? s, PointerPressedEventArgs e)
    {
        var p = e.GetCurrentPoint(this);
        if (!p.Properties.IsLeftButtonPressed) return;
        _dragging = true;
        _moved = false;
        _pressPos = p.Position;
        _pressWindowPos = Position;
        // 抓取点在窗口内的物理偏移 —— 拖动时窗口位置 = 光标屏幕坐标 - 该偏移
        _grabOffsetPx = new PixelPoint(
            (int)Math.Round(_pressPos.X * _scale),
            (int)Math.Round(_pressPos.Y * _scale));
        e.Pointer.Capture(this);
    }

    private void OnMove(object? s, PointerEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetCurrentPoint(this).Position;
        // B22 根修: 原来用「相对窗口的指针位移」累加位置, 而窗口本身又跟着指针移动,
        // 于是每次移动算出的都是"相对按下点的绝对差", 窗口被反复拉回 → 拖动只能走一小段。
        // 改用「指针屏幕坐标 - 抓取偏移」直接定位 (物理像素)。
        var screen = this.PointToScreen(p);
        var dxPx = screen.X - _pressWindowPos.X - _grabOffsetPx.X;
        var dyPx = screen.Y - _pressWindowPos.Y - _grabOffsetPx.Y;
        if (!_moved && Math.Abs(dxPx) + Math.Abs(dyPx) > 6 * _scale) _moved = true;
        if (_moved && !_expanded)
        {
            Position = new PixelPoint(screen.X - _grabOffsetPx.X, screen.Y - _grabOffsetPx.Y);
        }
    }

    private void OnRelease(object? s, PointerReleasedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        if (_moved && !_expanded)
        {
            // 吸附 (v1.1 SnapToEdges 语义, 物理像素)
            var snapped = FloatBallLayout.SnapToEdges(
                (Position.X, Position.Y), (_sizePx, _sizePx), _workPx, _snapThreshold);
            Position = new PixelPoint(snapped.X, snapped.Y);
            _restPosition = Position;
            // P1: 持久化位置到配置 —— 存的是「相对工作区偏移」, 与 ResolveBallPosition 的读法对称
            var rel = FloatBallLayout.ToRelativePosition((snapped.X, snapped.Y), _workPx);
            _ = _config.Set("floatBall.x", rel.X);
            _ = _config.Set("floatBall.y", rel.Y);
            return;
        }
        // 双击的第一击: 250ms 内第二击由 DoubleTapped 处理
        var sinceLast = DateTime.UtcNow - _lastTap;
        _lastTap = DateTime.UtcNow;
        if (sinceLast.TotalMilliseconds < 260) return;
        ToggleFan();
    }

    // ---- 扇形菜单 ----

    private void ToggleFan() { if (_expanded) CollapseFan(); else ExpandFan(); }

    private void ExpandFan()
    {
        var count = Math.Max(1, _actions.Length);
        // Core 已提供带 DPI 补偿的布局求解 (物理像素窗口 + DIP 球体偏移), 不再手算 (B3 根修)
        var (layout, win) = FanLayoutComputer.ComputeFanLayout(
            (Position.X, Position.Y), _sizeDip, _workPx, count, _scale);

        Width = win.Width / _scale;
        Height = win.Height / _scale;
        Position = new PixelPoint(win.X, win.Y);

        _root.Children.Clear();
        _root.Children.Add(_orb);
        Canvas.SetLeft(_orb, layout.BallOffset.X);
        Canvas.SetTop(_orb, layout.BallOffset.Y);

        var centerX = layout.BallOffset.X + _sizeDip / 2.0;
        var centerY = layout.BallOffset.Y + _sizeDip / 2.0;

        for (var i = 0; i < count; i++)
        {
            var action = _actions[i];
            var (ox, oy) = FloatBallLayout.FanItemOffset(i, count, layout.Radius, layout.Dir);
            var backgroundColor = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#57FFFFFF"), 0),
                    new GradientStop(Color.Parse("#1FFFFFFF"), 0.42),
                    new GradientStop(Color.Parse("#0DFFFFFF"), 0.58),
                    new GradientStop(Color.Parse("#38FFFFFF"), 1),
                },
            };
            var button = new Button
            {
                Width = FloatBallLayout.FanItemSize,
                Height = FloatBallLayout.FanItemSize,
                CornerRadius = new CornerRadius(FloatBallLayout.FanItemSize / 2.0),
                Padding = new Thickness(0),
                Background = backgroundColor,
                BorderThickness = new Thickness(1.5),
                HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                // B4 根修: 图标在构造时直接赋值, 不依赖布局完成后的可视树遍历
                Content = new Image
                {
                    Width = 26,
                    Height = 26,
                    Source = SvgIcons.Load(action, white: true),
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                },
            };
            // v1.2 v7 边光环: 1.5px 白色渐变环 (Button 无 BoxShadow, 靠 BorderBrush 近似)
            button.BorderBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#D9FFFFFF"), 0),
                    new GradientStop(Color.Parse("#47FFFFFF"), 0.18),
                    new GradientStop(Color.Parse("#14FFFFFF"), 0.4),
                    new GradientStop(Color.Parse("#29FFFFFF"), 0.62),
                    new GradientStop(Color.Parse("#99FFFFFF"), 1),
                },
            };
            button.Click += (_, _) =>
            {
                CollapseFan();
                _dispatch(action);
            };
            Canvas.SetLeft(button, centerX + ox - FloatBallLayout.FanItemSize / 2.0);
            Canvas.SetTop(button, centerY + oy - FloatBallLayout.FanItemSize / 2.0);
            _root.Children.Add(button);
        }
        _expanded = true;
    }

    /// <summary>收起: 窗口缩回球体尺寸, 位置与 Canvas 偏移都必须复位 ——
    /// 否则球会被画在 (panel/2 - r, panel/2 - r) 而窗口只有球那么大 → 球不可见 (B2 根修)。</summary>
    private void CollapseFan()
    {
        _expanded = false;
        Width = _sizeDip;
        Height = _sizeDip;
        Position = _restPosition;
        _root.Children.Clear();
        _root.Children.Add(_orb);
        Canvas.SetLeft(_orb, 0);
        Canvas.SetTop(_orb, 0);
    }
}
