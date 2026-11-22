// Material Design 3 适配层（2026-11）。
// 规格来源：design-tokens.json 的 m3 段（数值对齐 mochi-compose 的 MochiShapes /
// MochiMotionTokens / MD3 type scale）。这里只做"令牌 → Avalonia 类型"的转换与
// 基础组件工厂，不复制 Compose 的代码，也不改变既有 PanelKit 的视觉基线。
//
// 用法约定：
//   · 新建页面/组件优先用 M3.* 工厂与令牌，不要就地写圆角/阴影/时长字面量；
//   · 既有面板继续走 PanelKit（其数值锁定 v1.2 基线），两者可共存；
//   · 圆角取 M3 刻度、高度不低于 M3.MinTouchDip、阴影取 elevation 级别。
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Avalonia.Ui;

internal static class M3
{
    private static M3Tokens T => DesignTokens.Current.M3;

    // ---- 形状：MD3 Expressive 刻度（mochi MochiShapes 同源）----
    public static CornerRadius CornerExtraSmall => new(T.Shape.ExtraSmall);
    public static CornerRadius CornerSmall => new(T.Shape.Small);
    public static CornerRadius CornerMedium => new(T.Shape.Medium);
    public static CornerRadius CornerLarge => new(T.Shape.Large);
    public static CornerRadius CornerLargeIncreased => new(T.Shape.LargeIncreased);
    public static CornerRadius CornerExtraLarge => new(T.Shape.ExtraLarge);
    public static CornerRadius CornerExtraLargeIncreased => new(T.Shape.ExtraLargeIncreased);
    public static CornerRadius CornerExtraExtraLarge => new(T.Shape.ExtraExtraLarge);
    /// <summary>胶囊（MD3 full）。</summary>
    public static CornerRadius CornerFull => new(T.Shape.Full);

    // ---- 间距 ----
    public static Thickness PadXs => new(T.Spacing.Xs);
    public static Thickness PadS => new(T.Spacing.S);
    public static Thickness PadM => new(T.Spacing.M);
    public static Thickness PadL => new(T.Spacing.L);
    public static Thickness PadXl => new(T.Spacing.Xl);

    // ---- 触控目标 ----
    /// <summary>默认控件高（MD3 48dp）。</summary>
    public static double MinTargetDip => T.Touch.MinTargetDip;
    /// <summary>硬下限（mochi 要求 ≥44dp）。</summary>
    public static double MinTouchDip => T.Touch.MinTouchDip;

    // ---- 海拔 ----
    public static BoxShadows Elevation0 => BoxShadows.Parse(T.Elevation.Level0);
    public static BoxShadows Elevation1 => BoxShadows.Parse(T.Elevation.Level1);
    public static BoxShadows Elevation2 => BoxShadows.Parse(T.Elevation.Level2);
    public static BoxShadows Elevation3 => BoxShadows.Parse(T.Elevation.Level3);
    public static BoxShadows Elevation4 => BoxShadows.Parse(T.Elevation.Level4);
    public static BoxShadows Elevation5 => BoxShadows.Parse(T.Elevation.Level5);

    public static BoxShadows Elevation(int level) => level switch
    {
        <= 0 => Elevation0,
        1 => Elevation1,
        2 => Elevation2,
        3 => Elevation3,
        4 => Elevation4,
        _ => Elevation5,
    };

    // ---- 动效：物理弹簧（解析解，替代此前的 BackEase 近似）----
    // duration 取包络衰减到 0.1% 的时间，easing 是归一化弹簧曲线；
    // 欠阻尼档位会真实过冲（回弹），临界阻尼档位平稳落定。
    public static (TimeSpan Duration, IEasing Easing) Spring(M3MotionSpec spec)
    {
        var curve = new SpringCurve(spec.ResponseSec, spec.Damping);
        return (TimeSpan.FromSeconds(curve.SettleSeconds), new SpringEasingAdapter(curve));
    }

    /// <summary>档位对应的弹簧曲线（供陈列页展示 ω₀/ζ/过冲，也便于测试）。</summary>
    public static SpringCurve Curve(M3MotionSpec spec) => new(spec.ResponseSec, spec.Damping);

    public static TimeSpan PageTime => Spring(T.Motion.Page).Duration;
    public static TimeSpan PresentTime => Spring(T.Motion.Present).Duration;
    public static TimeSpan PopTime => Spring(T.Motion.Pop).Duration;
    public static TimeSpan ControlTime => Spring(T.Motion.Control).Duration;
    public static TimeSpan SettleTime => Spring(T.Motion.Settle).Duration;
    public static TimeSpan BounceTime => Spring(T.Motion.Bounce).Duration;
    public static TimeSpan PressDownTime => Spring(T.Motion.PressDown).Duration;
    public static TimeSpan PressReleaseTime => Spring(T.Motion.PressRelease).Duration;

    public static IEasing PageEase => Spring(T.Motion.Page).Easing;
    public static IEasing PresentEase => Spring(T.Motion.Present).Easing;
    public static IEasing PopEase => Spring(T.Motion.Pop).Easing;
    public static IEasing ControlEase => Spring(T.Motion.Control).Easing;
    public static IEasing SettleEase => Spring(T.Motion.Settle).Easing;
    public static IEasing BounceEase => Spring(T.Motion.Bounce).Easing;
    public static IEasing PressDownEase => Spring(T.Motion.PressDown).Easing;
    public static IEasing PressReleaseEase => Spring(T.Motion.PressRelease).Easing;

    // 透明度不跟弹簧走（mochi 规则：旧内容先让开，用短 tween）
    public static TimeSpan FadeTime => Ms(T.Motion.Fade);
    public static TimeSpan FadeOutTime => Ms(T.Motion.FadeOut);
    public static IEasing FadeEase => Ease(T.Motion.Fade.Easing);
    public static IEasing FadeOutEase => Ease(T.Motion.FadeOut.Easing);

    private static TimeSpan Ms(M3MotionSpec spec) => TimeSpan.FromMilliseconds(spec.Ms);

    /// <summary>把令牌里的缓动名映射成 Avalonia 缓动实例；未知名字退回线性（不抛异常）。</summary>
    internal static IEasing Ease(string? name) => name switch
    {
        "CubicEaseOut" => new CubicEaseOut(),
        "CubicEaseIn" => new CubicEaseIn(),
        "CubicEaseInOut" => new CubicEaseInOut(),
        "BackEaseOut" => new BackEaseOut(),
        "BackEaseIn" => new BackEaseIn(),
        "SineEaseOut" => new SineEaseOut(),
        "QuadraticEaseOut" => new QuadraticEaseOut(),
        _ => new LinearEasing(),
    };

    // ---- MD3 语义色 ----
    public static Color Role(M3ColorRole role, bool dark = false)
    {
        var set = dark ? T.Color.Dark : T.Color.Light;
        return role switch
        {
            M3ColorRole.Primary => Parse(set.Primary),
            M3ColorRole.OnPrimary => Parse(set.OnPrimary),
            M3ColorRole.PrimaryContainer => Parse(set.PrimaryContainer),
            M3ColorRole.OnPrimaryContainer => Parse(set.OnPrimaryContainer),
            M3ColorRole.Secondary => Parse(set.Secondary),
            M3ColorRole.OnSecondary => Parse(set.OnSecondary),
            M3ColorRole.SecondaryContainer => Parse(set.SecondaryContainer),
            M3ColorRole.Tertiary => Parse(set.Tertiary),
            M3ColorRole.OnTertiary => Parse(set.OnTertiary),
            M3ColorRole.TertiaryContainer => Parse(set.TertiaryContainer),
            M3ColorRole.OnTertiaryContainer => Parse(set.OnTertiaryContainer),
            M3ColorRole.Error => Parse(set.Error),
            M3ColorRole.OnError => Parse(set.OnError),
            M3ColorRole.ErrorContainer => Parse(set.ErrorContainer),
            M3ColorRole.Surface => Parse(set.Surface),
            M3ColorRole.OnSurface => Parse(set.OnSurface),
            M3ColorRole.SurfaceVariant => Parse(set.SurfaceVariant),
            M3ColorRole.OnSurfaceVariant => Parse(set.OnSurfaceVariant),
            M3ColorRole.Outline => Parse(set.Outline),
            M3ColorRole.OutlineVariant => Parse(set.OutlineVariant),
            M3ColorRole.SurfaceContainerLowest => Parse(set.SurfaceContainerLowest),
            M3ColorRole.SurfaceContainerLow => Parse(set.SurfaceContainerLow),
            M3ColorRole.SurfaceContainer => Parse(set.SurfaceContainer),
            M3ColorRole.SurfaceContainerHigh => Parse(set.SurfaceContainerHigh),
            M3ColorRole.SurfaceContainerHighest => Parse(set.SurfaceContainerHighest),
            _ => Parse(set.OnSurface),
        };
    }

    private static Color Parse(string hex)
    {
        try { return Color.Parse(hex); }
        catch { return Colors.Transparent; }
    }

    // ---- 排版 ----
    /// <summary>MD3 字重值 → Avalonia FontWeight（Avalonia 只提供具名档位）。</summary>
    internal static FontWeight Weight(int weight) => weight switch
    {
        <= 300 => FontWeight.Light,
        <= 400 => FontWeight.Normal,
        <= 500 => FontWeight.Medium,
        <= 600 => FontWeight.SemiBold,
        _ => FontWeight.Bold,
    };

    /// <summary>按 MD3 type scale 配置 TextBlock（字号/行高/字重/字距）。</summary>
    public static TextBlock Type(TextBlock text, M3TypeScale scale)
    {
        var s = Style(scale);
        text.FontSize = s.Size;
        text.LineHeight = s.LineHeight;
        text.FontWeight = Weight(s.Weight);
        text.LetterSpacing = s.LetterSpacing;
        return text;
    }

    internal static M3TextStyle Style(M3TypeScale scale) => scale switch
    {
        M3TypeScale.DisplayLarge => T.Typography.DisplayLarge,
        M3TypeScale.DisplayMedium => T.Typography.DisplayMedium,
        M3TypeScale.DisplaySmall => T.Typography.DisplaySmall,
        M3TypeScale.HeadlineLarge => T.Typography.HeadlineLarge,
        M3TypeScale.HeadlineMedium => T.Typography.HeadlineMedium,
        M3TypeScale.HeadlineSmall => T.Typography.HeadlineSmall,
        M3TypeScale.TitleLarge => T.Typography.TitleLarge,
        M3TypeScale.TitleMedium => T.Typography.TitleMedium,
        M3TypeScale.TitleSmall => T.Typography.TitleSmall,
        M3TypeScale.BodyLarge => T.Typography.BodyLarge,
        M3TypeScale.BodyMedium => T.Typography.BodyMedium,
        M3TypeScale.BodySmall => T.Typography.BodySmall,
        M3TypeScale.LabelLarge => T.Typography.LabelLarge,
        M3TypeScale.LabelMedium => T.Typography.LabelMedium,
        _ => T.Typography.LabelSmall,
    };

    // ---- 组件工厂（MD3 变体 + mochi 的触控/形态约定）----

    /// <summary>MD3 卡片：surfaceContainerLow + elevation1 + medium 圆角。</summary>
    public static Border Card(Control content, Palette p, int elevation = 1, double pad = 16)
    {
        return new Border
        {
            Background = new SolidColorBrush(p.Card),
            CornerRadius = CornerMedium,
            Padding = new Thickness(pad),
            BoxShadow = Elevation(elevation),
            BorderBrush = new SolidColorBrush(p.Divider),
            BorderThickness = new Thickness(1),
            Child = content,
            Tag = M3Tags.Card,
        };
    }

    /// <summary>
    /// MD3 按钮四变体（Filled / Tonal / Outlined / Text）。
    /// 高度取 MinTargetDip，水平内边距 24、垂直 12（mochi MochiButtonDefaults 同源）。
    /// </summary>
    public static Button Button(string text, Palette p, Action click, M3ButtonKind kind = M3ButtonKind.Filled)
    {
        IBrush bg;
        IBrush fg;
        IBrush border;
        switch (kind)
        {
            case M3ButtonKind.Tonal:
                bg = new SolidColorBrush(p.AccentSoft);
                fg = new SolidColorBrush(p.Text);
                border = Brushes.Transparent;
                break;
            case M3ButtonKind.Outlined:
                bg = Brushes.Transparent;
                fg = new SolidColorBrush(p.Text);
                border = new SolidColorBrush(p.Divider);
                break;
            case M3ButtonKind.Text:
                bg = Brushes.Transparent;
                fg = new SolidColorBrush(p.Accent);
                border = Brushes.Transparent;
                break;
            default: // Filled
                bg = new SolidColorBrush(p.Accent);
                // MD3: primary 上的前景取 onPrimary；深色主题 primary 是浅蓝，白字对比不足
                fg = new SolidColorBrush(Role(M3ColorRole.OnPrimary, p.Dark));
                border = Brushes.Transparent;
                break;
        }

        var button = new Button
        {
            Content = text,
            Background = bg,
            Foreground = fg,
            BorderBrush = border,
            BorderThickness = new Thickness(kind == M3ButtonKind.Outlined ? 1 : 0),
            CornerRadius = CornerFull,
            Padding = new Thickness(24, 12),
            MinHeight = MinTargetDip,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Tag = M3Tags.Button,
        };
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>MD3 输入框：small 圆角 + 48 高 + outline 边框。</summary>
    public static TextBox TextField(string watermark, Palette p, string? text = null)
    {
        return new TextBox
        {
            Watermark = watermark,
            Text = text ?? string.Empty,
            MinHeight = MinTargetDip,
            CornerRadius = CornerSmall,
            Padding = new Thickness(16, 12),
            Background = new SolidColorBrush(p.Inset),
            Foreground = new SolidColorBrush(p.Text),
            BorderBrush = new SolidColorBrush(p.Divider),
            BorderThickness = new Thickness(1),
            Tag = M3Tags.TextField,
        };
    }

    /// <summary>MD3 Chip：小圆角、32 高（不占满 48，但外层点击区由调用方保底）。</summary>
    public static Border Chip(string label, Palette p, bool selected = false)
    {
        return new Border
        {
            Background = new SolidColorBrush(selected ? p.AccentSoft : p.Inset),
            BorderBrush = new SolidColorBrush(selected ? p.Accent : p.Divider),
            BorderThickness = new Thickness(1),
            CornerRadius = CornerSmall,
            Padding = new Thickness(12, 6),
            MinHeight = 32,
            Tag = M3Tags.Chip,
            Child = new TextBlock
            {
                Text = label,
                FontSize = T.Typography.LabelLarge.Size,
                FontWeight = Weight(T.Typography.LabelLarge.Weight),
                Foreground = new SolidColorBrush(selected ? p.Text : p.Muted),
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }

    /// <summary>
    /// MD3 列表行：最小高 48（双行 56），左侧 leading、中间标题/副标题、右侧 trailing。
    /// 行内不用分隔线上色，交给外层 List 容器（MD3 list 约定）。
    /// </summary>
    public static Grid ListRow(Control? leading, string title, string? subtitle, Control? trailing, Palette p)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = T.Typography.BodyLarge.Size,
            LineHeight = T.Typography.BodyLarge.LineHeight,
            Foreground = new SolidColorBrush(p.Text),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            text.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = T.Typography.BodySmall.Size,
                LineHeight = T.Typography.BodySmall.LineHeight,
                Foreground = new SolidColorBrush(p.Muted),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }

        var grid = new Grid
        {
            MinHeight = string.IsNullOrWhiteSpace(subtitle) ? MinTargetDip : 56,
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
        };
        if (leading is not null)
        {
            leading.VerticalAlignment = VerticalAlignment.Center;
            leading.Margin = new Thickness(0, 0, T.Spacing.L, 0);
            Grid.SetColumn(leading, 0);
            grid.Children.Add(leading);
        }
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        if (trailing is not null)
        {
            trailing.VerticalAlignment = VerticalAlignment.Center;
            trailing.Margin = new Thickness(T.Spacing.L, 0, 0, 0);
            Grid.SetColumn(trailing, 2);
            grid.Children.Add(trailing);
        }
        return grid;
    }

    /// <summary>保证任意控件的点击区不低于 M3 硬下限（44dip）。</summary>
    public static T EnsureTouchTarget<T>(T control) where T : Control
    {
        if (control.MinHeight < MinTouchDip) control.MinHeight = MinTouchDip;
        if (control.MinWidth < MinTouchDip) control.MinWidth = MinTouchDip;
        return control;
    }
}

/// <summary>MD3 形状语义（供样式层按角色取圆角）。</summary>
internal enum M3ShapeRole
{
    ExtraSmall, Small, Medium, Large, LargeIncreased,
    ExtraLarge, ExtraLargeIncreased, ExtraExtraLarge, Full,
}

/// <summary>MD3 色彩角色子集（按现有 UI 需要的部分）。</summary>
internal enum M3ColorRole
{
    Primary, OnPrimary, PrimaryContainer, OnPrimaryContainer,
    Secondary, OnSecondary, SecondaryContainer,
    Tertiary, OnTertiary, TertiaryContainer, OnTertiaryContainer,
    Error, OnError, ErrorContainer,
    Surface, OnSurface, SurfaceVariant, OnSurfaceVariant,
    Outline, OutlineVariant,
    SurfaceContainerLowest, SurfaceContainerLow, SurfaceContainer, SurfaceContainerHigh, SurfaceContainerHighest,
}

/// <summary>MD3 type scale 档位。</summary>
internal enum M3TypeScale
{
    DisplayLarge, DisplayMedium, DisplaySmall,
    HeadlineLarge, HeadlineMedium, HeadlineSmall,
    TitleLarge, TitleMedium, TitleSmall,
    BodyLarge, BodyMedium, BodySmall,
    LabelLarge, LabelMedium, LabelSmall,
}

/// <summary>MD3 按钮变体。</summary>
internal enum M3ButtonKind
{
    Filled, Tonal, Outlined, Text,
}

/// <summary>
/// 把物理弹簧曲线包装成 Avalonia 的 IEasing。
/// Ease 的入参是归一化时间 0..1，映射到 [0, SettleSeconds] 的物理时间。
/// </summary>
internal sealed class SpringEasingAdapter : Easing
{
    public SpringEasingAdapter(SpringCurve curve) => Curve = curve;

    public SpringCurve Curve { get; }

    public override double Ease(double progress)
    {
        if (progress <= 0) return 0;
        if (progress >= 1) return 1;
        return Curve.ValueAt(progress * Curve.SettleSeconds);
    }
}

/// <summary>M3 组件标记：UI 测试与自动化定位用（避免命中控件模板内部元素）。</summary>
internal static class M3Tags
{
    public const string Button = "m3.button";
    public const string Card = "m3.card";
    public const string TextField = "m3.textfield";
    public const string Chip = "m3.chip";
}
