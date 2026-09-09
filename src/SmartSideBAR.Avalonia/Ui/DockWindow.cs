// v1.2 dock 收起形态: 工作区底角 52x52 圆形液态玻璃钮 (独立常驻窗口)
// 点击 -> 展开侧栏 (undock)。AppBar 在 dock 期间注销。
// v1.2 样式: 白色玻璃渐变底 (v6配方) + 向上箭头图标, v7 关闭边光环。
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

        // v1.2 dock-toggle: 向上箭头图标 (展开侧边栏)
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
            Width = 52,
            Height = 52,
            CornerRadius = new CornerRadius(26),
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            Content = arrow,
        };
        btn.PointerReleased += (_, _) => _undock();

        // v1.2 v6 dock-toggle: 白色玻璃渐变底 + 圆形 (border-radius: 50%)
        var root = new Border
        {
            CornerRadius = new CornerRadius(26),
            // v1.2 v6: 0 6px 18px rgba(15,23,42,0.14)
            BoxShadow = BoxShadows.Parse("0 6 18 #240F172A"),
            Child = btn,
        };
        // v1.2 v6: linear-gradient(135deg, rgba(255,255,255,0.78) 0%, rgba(255,255,255,0.45) 100%)
        root.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#C7FFFFFF"), 0),
                new GradientStop(Color.Parse("#73FFFFFF"), 1),
            },
        };
        // v1.2 v6: inset 0 0 0 1px rgba(15,23,42,0.07) 近似为薄暗边框 (v7 关闭边光环)
        root.BorderBrush = new SolidColorBrush(Color.Parse("#120F172A"));
        root.BorderThickness = new Thickness(1);
        Content = root;
    }
}
