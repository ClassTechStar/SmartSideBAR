// v1.2 dock 收起形态: 工作区底角 52x52 圆形液态玻璃钮 (独立常驻窗口)
// 点击 -> 展开侧栏 (undock)。AppBar 在 dock 期间注销。
// v1.2 样式: 白色玻璃渐变底 (v6配方) + 向上箭头图标, v7 关闭边光环。
// 动效: hover 微放大 + 按压缩放 (design-tokens.motion, 与 railBtn 同词汇);
// 箭头用与 rail 相同的 dock-up SVG, 手绘 Path 与图标系统不一致。
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media.Transformation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SmartSideBAR.Avalonia.Views;
using SmartSideBAR.Core.Design;

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
                // B23: 需要 Topmost。收起形态恰好发生在「有全屏/最大化窗口」时,
        // 该窗口会把工作区底角的小方块压在下面: 既看不见也点不到 (实测点击落入前台窗口)。
        Topmost = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [];

        // dock 箭头与 rail 停靠钮同源 SVG (单一图标系统), 尺寸走令牌
        var arrowSize = DesignTokens.Current.Dock.ArrowSizePx;
        var arrow = new Image
        {
            Width = arrowSize,
            Height = arrowSize,
            Source = SvgIcons.Load("dock-up", white: false),
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
        };

        var motion = DesignTokens.Current.Motion;
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
            RenderTransform = TransformOperations.Parse("scale(1)"),
            Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(motion.AnimFastMs),
                },
            },
        };
        // Click 而非 PointerReleased: 按下后拖出仍会触发 Released, 属于误触
        btn.Click += (_, _) => _undock();
        btn.PointerEntered += (_, _) => btn.RenderTransform = TransformOperations.Parse("scale(1.06)");
        btn.PointerExited += (_, _) => btn.RenderTransform = TransformOperations.Parse("scale(1)");
        btn.PointerPressed += (_, _) => btn.RenderTransform = TransformOperations.Parse(
            $"scale({motion.PressScale.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        btn.PointerReleased += (_, _) => btn.RenderTransform = TransformOperations.Parse("scale(1.06)");

        // v1.2 v6 dock-toggle: 白色玻璃渐变底 + 圆形 (border-radius: 50%)
        var root = new Border
        {
            CornerRadius = new CornerRadius(26),
            // v1.2 v6: 0 6px 18px rgba(15,23,42,0.14)
            BoxShadow = BoxShadows.Parse("0 6 18 #240F172A"),
            Child = btn,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#120F172A")),
            Transitions = new Transitions
            {
                new BrushTransition
                {
                    Property = Border.BorderBrushProperty,
                    Duration = TimeSpan.FromMilliseconds(motion.AnimFastMs),
                },
            },
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
        // hover: 边框泛品牌蓝 (从 rail hover 语言借来的一层可及性反馈)
        root.PointerEntered += (_, _) => root.BorderBrush = new SolidColorBrush(Color.Parse("#592B6EE0"));
        root.PointerExited += (_, _) => root.BorderBrush = new SolidColorBrush(Color.Parse("#120F172A"));
        Content = root;
    }
}
