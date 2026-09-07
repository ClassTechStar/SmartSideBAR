// 悬浮球 (§6.14, 对齐 v1.1): floatball-icon 图元圆球 + 拖拽吸附 + 点击展开玻璃扇形菜单
// (白色 SVG 图标) + 双击开合侧栏。布局算法 = Core.FloatBallLayout (v1.1 逐函数移植)。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.FloatBall;

namespace SmartSideBAR.Avalonia.Ui;

public sealed class FloatBallWindow : Window
{
    private readonly string[] _actions;
    private readonly Action<string> _dispatch;
    private readonly Action _toggleSidebar;
    private readonly int _size;
    private readonly int _snapThreshold;

    private readonly Border _orb;
    private readonly Canvas _root = new();
    private RectLike _workPx;      // 工作区 (物理像素)
    private bool _dragging;
    private bool _moved;
    private bool _expanded;
    private Point _pressPos;
    private PixelPoint _pressWindowPos;
    private DateTime _lastTap = DateTime.MinValue;

    public FloatBallWindow(FloatBallConfig cfg, RectLike workPx, Action<string> dispatch, Action toggleSidebar)
    {
        _actions = [.. cfg.Actions];
        _dispatch = dispatch;
        _toggleSidebar = toggleSidebar;
        _size = cfg.Size;
        _snapThreshold = cfg.SnapThreshold;
        _workPx = workPx;

        Width = _size;
        Height = _size;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [];
        ShowActivated = false;

        _orb = BuildOrb();
        _root.Children.Add(_orb);
        Content = _root;

        var pos = FloatBallLayout.ResolveBallPosition((cfg.X, cfg.Y),
            new RectLike(0, 0, workPx.Width, workPx.Height), _size, "right");
        Position = new PixelPoint(pos.X, pos.Y);

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
        var orb = new Border
        {
            Width = _size,
            Height = _size,
            CornerRadius = new CornerRadius(_size / 2.0),
            BoxShadow = BoxShadows.Parse("0 6 18 #40000000"),
            Child = new Image
            {
                Source = new global::Avalonia.Media.Imaging.Bitmap(
                    global::Avalonia.Platform.AssetLoader.Open(
                        new Uri("avares://SmartSideBAR.Avalonia/Assets/floatball-icon.png"))),
                Stretch = Stretch.UniformToFill,
            },
            ClipToBounds = true,
        };
        orb.CornerRadius = new CornerRadius(_size / 2.0);
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
        e.Pointer.Capture(this);
    }

    private void OnMove(object? s, PointerEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetCurrentPoint(this).Position;
        var dx = (int)(p.X - _pressPos.X);
        var dy = (int)(p.Y - _pressPos.Y);
        if (!_moved && Math.Abs(dx) + Math.Abs(dy) > 6) _moved = true;
        if (_moved && !_expanded)
        {
            Position = new PixelPoint(_pressWindowPos.X + dx, _pressWindowPos.Y + dy);
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
                (Position.X, Position.Y), (_size, _size), _workPx, _snapThreshold);
            Position = new PixelPoint(snapped.X, snapped.Y);
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
        var radius = FloatBallLayout.FanRadius(count);
        var panel = radius * 2 + 72;
        var (ballX, ballY) = (Position.X, Position.Y);
        var centerX = ballX + _size / 2;
        var centerY = ballY + _size / 2;

        // 方向: 远离最近边缘 (v1.1 computeFanLayout dir 语义)
        var dirX = centerX - _workPx.X >= _workPx.X + _workPx.Width - centerX ? -1 : 1;
        var dirY = centerY - _workPx.Y >= _workPx.Y + _workPx.Height - centerY ? -1 : 1;


        Width = panel;
        Height = panel;
        var winX = Math.Clamp(centerX - panel / 2, _workPx.X, _workPx.X + _workPx.Width - panel);
        var winY = Math.Clamp(centerY - panel / 2, _workPx.Y, _workPx.Y + _workPx.Height - panel);
        Position = new PixelPoint(winX, winY);

        _root.Children.Clear();
        _root.Children.Add(_orb);
        Canvas.SetLeft(_orb, panel / 2.0 - _size / 2.0);
        Canvas.SetTop(_orb, panel / 2.0 - _size / 2.0);

        for (var i = 0; i < count; i++)
        {
            var action = _actions[i];
            var (ox, oy) = FloatBallLayout.FanItemOffset(i, count, radius, (dirX, dirY));
            var item = new Button
            {
                Width = 46,
                Height = 46,
                Tag = "svgw:" + action,
                CornerRadius = new CornerRadius(23),
                Background = new SolidColorBrush(Color.FromArgb(215, 24, 32, 44)),
                Padding = new Thickness(0),
                HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Center,

            };
            item.Click += (_, _) =>
            {
                CollapseFan();
                _dispatch(action);
            };
            Canvas.SetLeft(item, panel / 2.0 + ox - 23);
            Canvas.SetTop(item, panel / 2.0 + oy - 23);
            _root.Children.Add(item);
        }
        _expanded = true;
        Views.SidebarWindow.LoadSvgIcons(this);
    }

    private void CollapseFan()
    {
        _expanded = false;
        Width = _size;
        Height = _size;
        _root.Children.Clear();
        _root.Children.Add(_orb);
    }

    private static string ActionTitle(string action) => action switch
    {
        "capture" => "区域截图",
        "annotate" => "屏幕批注",
        "longshot" => "长截图",
        "record" => "录屏",
        "ime" => "输入法",
        "taskmgr" => "任务管理器",
        "sidebar" => "侧边栏",
        "settings" => "设置",
        _ => action,
    };
}
