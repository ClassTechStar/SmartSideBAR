// 设计规范陈列页（Material Design 3）。
// 这是 M3 组件层的真实使用者，同时作为可视化自检：形状/色彩/排版/动效/组件五组逐一陈列。
// 数值全部来自 design-tokens.json 的 m3 段 —— 本文件与 M3.cs 一样，不允许出现字面量。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Avalonia.Ui;

internal static class DesignSpecPage
{
    public static Control Build(Palette p)
    {
        var m3 = DesignTokens.Current.M3;
        var root = new StackPanel { Spacing = m3.Spacing.Xl };

        root.Children.Add(M3.Type(new TextBlock
        {
            Text = "Material Design 3",
            Foreground = new SolidColorBrush(p.Body),
            Margin = new Thickness(0, 0, 0, 2),
        }, M3TypeScale.HeadlineSmall));

        root.Children.Add(M3.Type(new TextBlock
        {
            Text = "规格来源 design-tokens.json 的 m3 段（对齐 mochi-compose）。新建页面请优先使用这些组件与令牌。",
            Foreground = new SolidColorBrush(p.Muted),
            TextWrapping = TextWrapping.Wrap,
        }, M3TypeScale.BodyMedium));

        root.Children.Add(ShapeSection(p));
        root.Children.Add(ColorSection(p));
        root.Children.Add(TypeSection(p));
        root.Children.Add(MotionSection(p));
        root.Children.Add(ComponentSection(p));

        return new ScrollViewer { Padding = new Thickness(16, 14, 16, 16), Content = root };
    }

    private static Control ShapeSection(Palette p)
    {
        var m3 = DesignTokens.Current.M3;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = m3.Spacing.M };
        (string Name, CornerRadius Radius)[] samples =
        [
            ("XS 4", M3.CornerExtraSmall), ("S 8", M3.CornerSmall), ("M 12", M3.CornerMedium),
            ("L 16", M3.CornerLarge), ("XL 28", M3.CornerExtraLarge), ("Full", M3.CornerFull),
        ];
        foreach (var (name, radius) in samples)
        {
            row.Children.Add(new StackPanel
            {
                Spacing = m3.Spacing.Xs,
                Children =
                {
                    new Border
                    {
                        Width = 52,
                        Height = 52,
                        Background = new SolidColorBrush(p.AccentSoft),
                        BorderBrush = new SolidColorBrush(p.Accent),
                        BorderThickness = new Thickness(1),
                        CornerRadius = radius,
                    },
                    M3.Type(new TextBlock
                    {
                        Text = name,
                        Foreground = new SolidColorBrush(p.Muted),
                        HorizontalAlignment = HorizontalAlignment.Center,
                    }, M3TypeScale.LabelSmall),
                },
            });
        }
        return Card(p, "形状刻度", row);
    }

    private static Control ColorSection(Palette p)
    {
        var m3 = DesignTokens.Current.M3;
        var groups = new StackPanel { Spacing = m3.Spacing.L };
        groups.Children.Add(SwatchGroup("浅色 light", dark: false, p));
        groups.Children.Add(SwatchGroup("深色 dark", dark: true, p));
        return Card(p, "语义色彩（明暗两套）", groups);
    }

    private static Control SwatchGroup(string title, bool dark, Palette p)
    {
        var m3 = DesignTokens.Current.M3;
        (string Name, M3ColorRole Role)[] roles =
        [
            ("primary", M3ColorRole.Primary),
            ("primaryContainer", M3ColorRole.PrimaryContainer),
            ("secondary", M3ColorRole.Secondary),
            ("tertiary", M3ColorRole.Tertiary),
            ("error", M3ColorRole.Error),
            ("surface", M3ColorRole.Surface),
            ("surfaceContainer", M3ColorRole.SurfaceContainer),
            ("outline", M3ColorRole.Outline),
        ];
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (name, role) in roles)
        {
            wrap.Children.Add(new StackPanel
            {
                Width = 96,
                Spacing = m3.Spacing.Xs,
                Margin = new Thickness(0, 0, m3.Spacing.M, m3.Spacing.M),
                Children =
                {
                    new Border
                    {
                        Height = 36,
                        CornerRadius = M3.CornerSmall,
                        Background = new SolidColorBrush(M3.Role(role, dark)),
                        BorderBrush = new SolidColorBrush(M3.Role(M3ColorRole.OutlineVariant, dark)),
                        BorderThickness = new Thickness(1),
                    },
                    M3.Type(new TextBlock
                    {
                        Text = name,
                        Foreground = new SolidColorBrush(p.Muted),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    }, M3TypeScale.LabelSmall),
                },
            });
        }
        return new StackPanel
        {
            Spacing = m3.Spacing.S,
            Children =
            {
                M3.Type(new TextBlock { Text = title, Foreground = new SolidColorBrush(p.Muted) }, M3TypeScale.LabelLarge),
                wrap,
            },
        };
    }

    private static Control TypeSection(Palette p)
    {
        var m3 = DesignTokens.Current.M3;
        var stack = new StackPanel { Spacing = m3.Spacing.S };
        (string Label, M3TypeScale Scale)[] rows =
        [
            ("Headline Small 24", M3TypeScale.HeadlineSmall),
            ("Title Medium 16", M3TypeScale.TitleMedium),
            ("Body Large 16", M3TypeScale.BodyLarge),
            ("Body Medium 14", M3TypeScale.BodyMedium),
            ("Label Large 14", M3TypeScale.LabelLarge),
        ];
        foreach (var (label, scale) in rows)
        {
            stack.Children.Add(M3.Type(new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(p.Text),
            }, scale));
        }
        return Card(p, "排版刻度", stack);
    }

    private static Control MotionSection(Palette p)
    {
        var m = DesignTokens.Current.M3.Motion;
        (string Name, M3MotionSpec Spec)[] rows =
        [
            ("page 页面切换", m.Page),
            ("present 浮层升起", m.Present),
            ("pop 对话框", m.Pop),
            ("control 控件位移", m.Control),
            ("settle 收起落定", m.Settle),
            ("pressDown 按下", m.PressDown),
            ("pressRelease 松开", m.PressRelease),
        ];
        var stack = new StackPanel { Spacing = 2 };
        foreach (var (name, spec) in rows)
        {
            var curve = M3.Curve(spec);
            stack.Children.Add(M3.Type(new TextBlock
            {
                Text = $"{name} · ω₀ {curve.Omega:0.0} rad/s · ζ {spec.Damping:0.00}"
                       + $" · 过冲 {curve.OverShootPercent * 100:0.#}% · 收敛 {curve.SettleSeconds * 1000:0}ms",
                Foreground = new SolidColorBrush(p.Muted),
            }, M3TypeScale.BodySmall));
        }
        stack.Children.Add(M3.Type(new TextBlock
        {
            Text = $"透明度另走 tween：淡入 {m.Fade.Ms:0}ms / 淡出 {m.FadeOut.Ms:0}ms（不跟弹簧）",
            Foreground = new SolidColorBrush(p.Muted),
            Margin = new Thickness(0, DesignTokens.Current.M3.Spacing.S, 0, 0),
        }, M3TypeScale.BodySmall));

        return Card(p, "动效档位（物理弹簧）", stack);
    }

    private static Control ComponentSection(Palette p)
    {
        var m3 = DesignTokens.Current.M3;
        var stack = new StackPanel { Spacing = m3.Spacing.L };
        stack.Children.Add(ComponentBlock("浅色", p, M3.Role(M3ColorRole.Surface)));
        stack.Children.Add(ComponentBlock("深色", AppTheme.M3Palette(dark: true), M3.Role(M3ColorRole.Surface, dark: true)));
        return Card(p, "组件样例（明暗对照）", stack);
    }

    private static Control ComponentBlock(string title, Palette block, Color background)
    {
        var m3 = DesignTokens.Current.M3;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = m3.Spacing.S };
        buttons.Children.Add(M3.Button("Filled", block, () => { }));
        buttons.Children.Add(M3.Button("Tonal", block, () => { }, M3ButtonKind.Tonal));
        buttons.Children.Add(M3.Button("Outlined", block, () => { }, M3ButtonKind.Outlined));
        buttons.Children.Add(M3.Button("Text", block, () => { }, M3ButtonKind.Text));

        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = m3.Spacing.S };
        chips.Children.Add(M3.Chip("全部", block, selected: true));
        chips.Children.Add(M3.Chip("截图", block));
        chips.Children.Add(M3.Chip("录屏", block));

        var listRow = M3.ListRow(
            SvgIcons.Load("capture") is { } icon
                ? new Image { Source = icon, Width = 20, Height = 20 }
                : null,
            "列表行标题",
            "副标题 · 双行高度 56",
            M3.Type(new TextBlock { Text = "12:30", Foreground = new SolidColorBrush(block.Muted) }, M3TypeScale.LabelMedium),
            block);

        return new Border
        {
            Background = new SolidColorBrush(background),
            CornerRadius = M3.CornerMedium,
            Padding = new Thickness(m3.Spacing.L),
            Child = new StackPanel
            {
                Spacing = m3.Spacing.L,
                Children =
                {
                    M3.Type(new TextBlock { Text = title, Foreground = new SolidColorBrush(block.Text) }, M3TypeScale.LabelLarge),
                    buttons,
                    M3.TextField("MD3 输入框", block),
                    chips,
                    new Border
                    {
                        Background = new SolidColorBrush(block.Inset),
                        CornerRadius = M3.CornerMedium,
                        Padding = new Thickness(m3.Spacing.L, m3.Spacing.S),
                        Child = listRow,
                    },
                },
            },
        };
    }

    private static Control Card(Palette p, string title, Control content)
    {
        var m3 = DesignTokens.Current.M3;
        return M3.Card(new StackPanel
        {
            Spacing = m3.Spacing.M,
            Children =
            {
                M3.Type(new TextBlock { Text = title, Foreground = new SolidColorBrush(p.Text) }, M3TypeScale.TitleMedium),
                content,
            },
        }, p);
    }
}
