// 悬浮球 (§6.14): 透明置顶小窗; 拖拽吸附 (FloatBallLayout 纯算法) + 点击展开扇形快捷菜单。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.FloatBall;
using AloLayout = global::Avalonia.Layout;
using Math = global::System.Math;

namespace SmartSideBAR.Avalonia.Ui;

public sealed class FloatBallWindow : Window
{
    private readonly string[] _actions;
    private readonly Action<string> _dispatch;
    private readonly int _size;
    private bool _dragging;
    private Point _pressPos;
    private PixelPoint _pressWindowPos;
    private bool _moved;
    private readonly Panel _root = new();

    public FloatBallWindow(FloatBallConfig cfg, RectLike workArea, Action<string> dispatch)
    {
        _actions = [.. cfg.Actions];
        _dispatch = dispatch;
        _size = cfg.Size;
        Width = _size;
        Height = _size;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [];
        ShowActivated = false;

        var ball = new Border
        {
            Width = _size,
            Height = _size,
            CornerRadius = new CornerRadius(_size / 2),
            Background = new SolidColorBrush(Color.FromArgb(210, 24, 32, 44)),
            BorderBrush = new SolidColorBrush(Color.Parse("#2B6EE0")),
            BorderThickness = new Thickness(2),
            Child = new TextBlock
            {
                Text = "S",
                Foreground = Brushes.White,
                FontSize = _size * 0.42,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = AloLayout.HorizontalAlignment.Center,
                VerticalAlignment = AloLayout.VerticalAlignment.Center,
            },
        };
        _root.Children.Add(ball);
        Content = _root;

        // 默认位置: workArea 62% 高度、贴配置侧
        var pos = FloatBallLayout.DefaultBallPosition(
            new RectLike(workArea.X, workArea.Y, workArea.Width, workArea.Height), _size, "right");
        Position = new PixelPoint(pos.X, pos.Y);

        PointerPressed += OnPress;
        PointerMoved += OnMove;
        PointerReleased += OnRelease;
    }

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
        var dx = p.X - _pressPos.X;
        var dy = p.Y - _pressPos.Y;
        if (Math.Abs(dx) + Math.Abs(dy) > 6) _moved = true;
        if (_moved)
        {
            Position = new PixelPoint(_pressWindowPos.X + (int)dx, _pressWindowPos.Y + (int)dy);
        }
    }

    private void OnRelease(object? s, PointerReleasedEventArgs e)
    {
        _dragging = false;
        if (_moved) return; // 拖拽结束: 吸附可选 (Wave D 细化 SnapThreshold)
        ToggleFan();
    }

    private bool _fanOpen;

    private void ToggleFan()
    {
        if (_fanOpen)
        {
            Width = _size;
            Height = _size;
            _fanOpen = false;
            RebuildFan(collapsed: true);
            return;
        }
        _fanOpen = true;
        RebuildFan(collapsed: false);
    }

    private void RebuildFan(bool collapsed)
    {
        _root.Children.Clear();
        if (collapsed)
        {
            Width = _size;
            Height = _size;
            var ball = new Border
            {
                Width = _size,
                Height = _size,
                CornerRadius = new CornerRadius(_size / 2),
                Background = new SolidColorBrush(Color.FromArgb(210, 24, 32, 44)),
                BorderBrush = new SolidColorBrush(Color.Parse("#2B6EE0")),
                BorderThickness = new Thickness(2),
                Child = new TextBlock
                {
                    Text = "S",
                    Foreground = Brushes.White,
                    FontSize = _size * 0.42,
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = AloLayout.HorizontalAlignment.Center,
                    VerticalAlignment = AloLayout.VerticalAlignment.Center,
                },
            };
            _root.Children.Add(ball);
            return;
        }

        // 扇形展开: 简化为圆形环绕 (fanRadius 布局算法的 UI 投影)
        var count = Math.Max(1, _actions.Length);
        var radius = FloatBallLayout.FanRadius(count);
        var panel = radius * 2 + 80;
        Width = panel;
        Height = panel;
        Position = new PixelPoint(Position.X - (panel - _size) / 2, Position.Y - (panel - _size) / 2);
        for (var i = 0; i < count; i++)
        {
            var action = _actions[i];
            var angle = Math.PI * 2 * i / count - Math.PI / 2;
            var btn = new Button
            {
                Width = 52,
                Height = 52,
                CornerRadius = new CornerRadius(26),
                Content = ActionLabel(action),
                Background = new SolidColorBrush(Color.FromArgb(225, 24, 32, 44)),
                Foreground = Brushes.White,
            };
            btn.Click += (_, _) =>
            {
                _dispatch(action);
                ToggleFan();
            };
            Canvas.SetLeft(btn, panel / 2 + radius * Math.Cos(angle) - 26);
            Canvas.SetTop(btn, panel / 2 + radius * Math.Sin(angle) - 26);
            _root.Children.Add(btn);
        }
    }

    private static string ActionLabel(string action) => action switch
    {
        "capture" => "📷",
        "annotate" => "✏️",
        "longshot" => "📜",
        "record" => "🎥",
        "ime" => "中",
        "taskmgr" => "📊",
        "sidebar" => "☰",
        "settings" => "⚙️",
        _ => action,
    };
}
