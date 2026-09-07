// v1.1 dock 收起形态: 工作区底角 52×52 玻璃小方块 (独立常驻窗口 ——
// 勘误: 对透明窗口做外部 MoveWindow 缩放后 Avalonia 不再出帧, 故 dock 不复用侧栏窗口)。
// 点击 → 展开侧栏 (undock)。AppBar 在 dock 期间注销 (WorkArea 完整归还, v1.1 同语义)。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SmartSideBAR.Avalonia.Views;

namespace SmartSideBAR.Avalonia.Ui;

public sealed class DockWindow : Window
{
    private readonly Action _undock;

    public DockWindow(Action undock)
    {
        _undock = undock;
        Width = 52;
        Height = 52;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        ShowActivated = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [];

        var btn = new Button
        {
            Tag = "svg:sidebar",
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
        };
        btn.PointerReleased += (_, _) => _undock();
        var root = new Border
        {
            CornerRadius = new CornerRadius(12),
            BoxShadow = BoxShadows.Parse("0 6 20 #26000000"),
            Child = btn,
        };
        root.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.3, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.7, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#8AFAFCFF"), 0),
                new GradientStop(Color.Parse("#7AFFFFFF"), 1),
            },
        };
        Content = root;
        Opened += (_, _) => SidebarWindow.LoadSvgIcons(this);
    }
}
