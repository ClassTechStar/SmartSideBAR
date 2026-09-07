// v1.1 dock 收起形态: 工作区底角 52×52 液态玻璃小方块 (独立常驻窗口 ——
// 勘误: 对透明窗口做外部 MoveWindow 缩放后 Avalonia 不再出帧, 故 dock 不复用侧栏窗口)。
// 点击 → 展开侧栏 (undock)。AppBar 在 dock 期间注销 (WorkArea 完整归还, v1.1 同语义)。
// v1.1 样式: 浅灰玻璃 + 白色边光环 + 向上箭头 (↑) 图标。
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

        // v1.1 dock-toggle: 向上箭头图标 (展开侧边栏)
        var arrow = new global::Avalonia.Controls.Shapes.Path
        {
            Data = global::Avalonia.Media.StreamGeometry.Parse("M 12 19 L 12 5 M 6 11 L 12 5 L 18 11"),
            Stroke = new SolidColorBrush(Color.Parse("#333333")),
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Width = 24,
            Height = 24,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
        };

        var btn = new Button
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            Content = arrow,
        };
        btn.PointerReleased += (_, _) => _undock();

        // v1.1 v6 dock-toggle 玻璃: 白色渐变玻璃底 + 边光环
        var root = new Border
        {
            CornerRadius = new CornerRadius(14),
            BoxShadow = BoxShadows.Parse("0 6 18 #24171E2A, 0 0 0 0.5 #80FFFFFF, 0 1 3 #40FFFFFF"),
            Child = btn,
        };
        root.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.35, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.65, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#C7FFFFFF"), 0),
                new GradientStop(Color.Parse("#73FFFFFF"), 1),
            },
        };
        // v1.1 v7 边光环: 白色渐变环
        root.BorderBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#D9FFFFFF"), 0),
                new GradientStop(Color.Parse("#47FFFFFF"), 0.4),
                new GradientStop(Color.Parse("#99FFFFFF"), 1),
            },
        };
        root.BorderThickness = new Thickness(1.5);
        Content = root;
    }
}
