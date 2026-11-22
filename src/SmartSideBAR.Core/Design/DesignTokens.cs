// 设计令牌 (P0-2)。视觉规格统一从这里取。
// 以前规格散落在 XAML/CSS/代码里, 改一处漏一处, 界面容易漂。现在集中到一个 JSON。
//
// 约定：
//   1) 数值以 design-tokens.json（仓库根）为准，随 Core 程序集嵌入发布；
//   2) Avalonia 的 Theme/窗口几何一律从这里取，不允许就地写死数字；
//   3) 改规格必须先改 JSON, DesignTokensTests 会把 v1.2 基线固定, 防止改坏。
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartSideBAR.Core.Design;

public sealed class DesignTokens
{
    public string Version { get; set; } = "1.2-derived";
    public RailTokens Rail { get; set; } = new();
    public CapsuleTokens Capsule { get; set; } = new();
    public PanelTokens Panel { get; set; } = new();
    public DockTokens Dock { get; set; } = new();
    public FloatBallTokens FloatBall { get; set; } = new();
    public BadgeTokens Badge { get; set; } = new();
    public ColorTokens Color { get; set; } = new();
    public MotionTokens Motion { get; set; } = new();
    public ClassicTokens Classic { get; set; } = new();
    public ShapeTokens Shape { get; set; } = new();
    public SpacingTokens Spacing { get; set; } = new();
    public ShadowTokens Shadow { get; set; } = new();
    public IndicatorTokens Indicator { get; set; } = new();
    public TouchTokens Touch { get; set; } = new();

    /// <summary>
    /// Material Design 3 / mochi-compose 规格（2026-11）。与旧令牌并存：
    /// 旧令牌锁定既有视觉基线，M3 段供新建组件使用，互不影响。
    /// </summary>
    public M3Tokens M3 { get; set; } = new();

    private static DesignTokens? _default;

    /// <summary>随程序集嵌入的规格副本。</summary>
    public static DesignTokens Current => _default ??= LoadEmbedded();

    public static DesignTokens Parse(string json)
    {
        var tokens = JsonSerializer.Deserialize(json, DesignTokenJsonContext.Default.DesignTokens);
        return tokens ?? new DesignTokens();
    }

    private static DesignTokens LoadEmbedded()
    {
        try
        {
            using var stream = typeof(DesignTokens).Assembly
                .GetManifestResourceStream("design-tokens.json");
            if (stream is null) return new DesignTokens();
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
        catch
        {
            // 令牌缺失不能阻断启动：退回类型默认值（与 JSON 默认值一致）
            return new DesignTokens();
        }
    }
}

public sealed class RailTokens
{
    public double WidthDip { get; set; } = 72;
    public double ItemSizeDip { get; set; } = 52;
    public double IconSizeDip { get; set; } = 28;
    public double Radius { get; set; } = 14;
    public double Gap { get; set; } = 8;
    public double PaddingTop { get; set; } = 12;
    public double PaddingBottom { get; set; } = 8;
}

public sealed class CapsuleTokens
{
    public double Radius { get; set; } = 28;
    public double MarginRatio { get; set; } = 0.08;
    public double Blur { get; set; } = 21;
    public double Saturate { get; set; } = 2.2;
}

public sealed class PanelTokens
{
    public double WidthDip { get; set; } = 380;
    public double Radius { get; set; } = 28;
    public double HeaderHeight { get; set; } = 52;
    public double PaddingX { get; set; } = 16;
    public double PaddingY { get; set; } = 12;
}

public sealed class DockTokens
{
    public double ToggleSizeDip { get; set; } = 52;
    public double ArrowSizePx { get; set; } = 30;
}

public sealed class FloatBallTokens
{
    public double SizeDip { get; set; } = 56;
    public double FanItemDip { get; set; } = 46;
    public double FanRadiusMin { get; set; } = 96;
    public double FanRadiusMax { get; set; } = 260;
}

public sealed class BadgeTokens
{
    public double FontSize { get; set; } = 10;
    public double Radius { get; set; } = 6;
    public double OffsetX { get; set; } = 2;
    public double OffsetY { get; set; } = 2;
    public double PaddingX { get; set; } = 4;
    public double PaddingY { get; set; } = 1;
}

public sealed class ColorTokens
{
    public string Brand { get; set; } = "#2B6EE0";
    public string PanelBg { get; set; } = "#FFFFFF";
    public string Text { get; set; } = "#2B6EE0";
    public string TextBody { get; set; } = "#1B2230";
    public string TextMuted { get; set; } = "#5A6478";
    public string TextDark { get; set; } = "#E6EBF3";
    public string BgDark { get; set; } = "#171B22";
    public string StateOk { get; set; } = "#2E9E5B";
    public string StateWarn { get; set; } = "#D98324";
    public string StateError { get; set; } = "#D64545";
    public string StateRecording { get; set; } = "#E74C3C";
    /// <summary>Toast 底色 (白字对比度已校)。此前硬编码在 ToastService。</summary>
    public string ToastOk { get; set; } = "#2E7D32";
    public string ToastWarn { get; set; } = "#B26A00";
    public string ToastError { get; set; } = "#C62828";
    public string ToastInfo { get; set; } = "#243041";
}

/// <summary>
/// 动效时长（参考 1.0.0 与 LanMountain）：微反馈 120ms / 面板 180ms / 窗口 250ms，
/// 扇形菜单交错 32ms，按压缩放 0.96。动效统一从这里取，禁止散落魔法数字。
/// </summary>
public sealed class MotionTokens
{
    public double AnimMs { get; set; } = 220;
    public double AnimFastMs { get; set; } = 120;
    /// <summary>dock/undock 等窗口级动画时长。</summary>
    public double WindowMs { get; set; } = 250;
    /// <summary>面板展开滑出时长（1.0.0 CSS width 180ms ease 同源）。</summary>
    public double PanelMs { get; set; } = 180;
    /// <summary>扇形菜单逐项交错延迟。</summary>
    public double StaggerMs { get; set; } = 32;
    /// <summary>按压反馈缩放（LanMountain SettingsCardStyles 0.985~0.96 档）。</summary>
    public double PressScale { get; set; } = 0.96;
}

/// <summary>
/// 「经典快」表面语言（1.0.0 实测手感最好）：纯白不透明 + 发丝线 + 品牌浅蓝态色，
/// 零持久模糊。appearance.liquidGlass=false 时整套生效；true 时回 v1.2 玻璃规格。
/// </summary>
public sealed class ClassicTokens
{
    public string Bg { get; set; } = "#FFFFFF";
    public string BgHover { get; set; } = "#F5F7FA";
    public string BrandLight { get; set; } = "#E8F1FC";
    /// <summary>按压态（比 hover 深一档，触控可感）。</summary>
    public string PressedHover { get; set; } = "#D9E8FB";
    public string Border { get; set; } = "#E8E8E8";
    public string Text { get; set; } = "#1A1A1A";
    public string TextSecondary { get; set; } = "#666666";
    public string Shadow { get; set; } = "#14000000";
}

/// <summary>Seelen radius.css 的角色语义圆角: xs 小件 / s 按钮·输入 / m 卡片·弹层 / l 对话框·整面板。</summary>
public sealed class ShapeTokens
{
    public double RadiusXs { get; set; } = 4;
    public double RadiusS { get; set; } = 6;
    public double RadiusM { get; set; } = 10;
    public double RadiusL { get; set; } = 16;
}

/// <summary>Seelen spacings.css 间距刻度。</summary>
public sealed class SpacingTokens
{
    public double Xs { get; set; } = 4;
    public double S { get; set; } = 8;
    public double M { get; set; } = 12;
    public double L { get; set; } = 16;
    public double Xl { get; set; } = 20;
}

/// <summary>KoBar 三级海拔阴影 (e1 卡片 / e2 弹层 / e3 模态), 格式同 BoxShadows.Parse。</summary>
public sealed class ShadowTokens
{
    public string E1 { get; set; } = "0 2 3 #14000000";
    public string E2 { get; set; } = "0 4 12 #29000000";
    public string E3 { get; set; } = "0 8 28 #3D000000";
}

/// <summary>KoBar 选中态指示条: rail/导航激活项在贴屏侧的圆角竖条。</summary>
public sealed class IndicatorTokens
{
    public double Width { get; set; } = 3;
    public double Height { get; set; } = 18;
    public double Radius { get; set; } = 2;
}

/// <summary>
/// 触屏可达性 (P0-2): 任意 UI 缩放下可交互元素的物理最小点击区，借鉴 CyreneCompass 的 32px 保底。
/// 单位为物理像素；实际尺寸 = max(设计尺寸 × 缩放, minHitPx)。设为 0 表示不启用该约束。
/// </summary>
public sealed class TouchTokens
{
    public double MinHitPx { get; set; } = 32;
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DesignTokens))]
internal partial class DesignTokenJsonContext : JsonSerializerContext;

// ---------------------------------------------------------------------------
// Material Design 3 规格（对齐 mochi-compose 的 MochiShapes / MochiMotionTokens）
// 数值以 design-tokens.json 的 m3 段为准；这里的默认值必须与 JSON 完全一致，
// DesignTokensTests 会双向校验（JSON 改了忘改 C# 会红）。
// ---------------------------------------------------------------------------

public sealed class M3Tokens
{
    public M3ShapeTokens Shape { get; set; } = new();
    public M3SpacingTokens Spacing { get; set; } = new();
    public M3TouchTokens Touch { get; set; } = new();
    public M3ElevationTokens Elevation { get; set; } = new();
    public M3MotionTokens Motion { get; set; } = new();
    public M3TypographyTokens Typography { get; set; } = new();
    public M3ColorTokens Color { get; set; } = new();
}

/// <summary>MD3 Expressive 形状刻度（mochi MochiShapes 同源）。full=999 表示胶囊。</summary>
public sealed class M3ShapeTokens
{
    public double ExtraSmall { get; set; } = 4;
    public double Small { get; set; } = 8;
    public double Medium { get; set; } = 12;
    public double Large { get; set; } = 16;
    public double LargeIncreased { get; set; } = 20;
    public double ExtraLarge { get; set; } = 28;
    public double ExtraLargeIncreased { get; set; } = 32;
    public double ExtraExtraLarge { get; set; } = 48;
    public double Full { get; set; } = 999;
}

public sealed class M3SpacingTokens
{
    public double Xs { get; set; } = 4;
    public double S { get; set; } = 8;
    public double M { get; set; } = 12;
    public double L { get; set; } = 16;
    public double Xl { get; set; } = 24;
    public double Xxl { get; set; } = 32;
}

/// <summary>触控目标：MD3 默认 48，mochi 硬下限 44。</summary>
public sealed class M3TouchTokens
{
    public double MinTargetDip { get; set; } = 48;
    public double MinTouchDip { get; set; } = 44;
}

/// <summary>MD3 六级海拔，字符串格式同 Avalonia BoxShadows.Parse。</summary>
public sealed class M3ElevationTokens
{
    public string Level0 { get; set; } = "0 0 0 0 #00000000";
    public string Level1 { get; set; } = "0 1 2 0 #1F000000, 0 1 3 1 #14000000";
    public string Level2 { get; set; } = "0 1 2 0 #1F000000, 0 2 6 2 #14000000";
    public string Level3 { get; set; } = "0 4 8 3 #1F000000, 0 1 3 0 #14000000";
    public string Level4 { get; set; } = "0 6 10 4 #1F000000, 0 2 3 0 #14000000";
    public string Level5 { get; set; } = "0 8 12 6 #1F000000, 0 4 6 0 #14000000";
}

/// <summary>动效档位：response/damping 是 mochi 原始规格，ms/easing 是 Avalonia 等效实现。</summary>
public sealed class M3MotionSpec
{
    public double ResponseSec { get; set; }
    public double Damping { get; set; } = 1.0;
    public double Ms { get; set; }
    public string Easing { get; set; } = "CubicEaseOut";
}

public sealed class M3MotionTokens
{
    public M3MotionSpec Page { get; set; } = new() { ResponseSec = 0.42, Damping = 1.0, Ms = 420, Easing = "CubicEaseOut" };
    public M3MotionSpec Present { get; set; } = new() { ResponseSec = 0.42, Damping = 0.86, Ms = 420, Easing = "BackEaseOut" };
    public M3MotionSpec Pop { get; set; } = new() { ResponseSec = 0.36, Damping = 0.78, Ms = 360, Easing = "BackEaseOut" };
    public M3MotionSpec Control { get; set; } = new() { ResponseSec = 0.34, Damping = 0.86, Ms = 340, Easing = "CubicEaseOut" };
    public M3MotionSpec Settle { get; set; } = new() { ResponseSec = 0.28, Damping = 1.0, Ms = 280, Easing = "CubicEaseOut" };
    public M3MotionSpec Bounce { get; set; } = new() { ResponseSec = 0.34, Damping = 0.55, Ms = 340, Easing = "BackEaseOut" };
    public M3MotionSpec PressDown { get; set; } = new() { ResponseSec = 0.22, Damping = 0.62, Ms = 220, Easing = "CubicEaseOut" };
    public M3MotionSpec PressRelease { get; set; } = new() { ResponseSec = 0.38, Damping = 0.62, Ms = 380, Easing = "BackEaseOut" };
    public M3MotionSpec Fade { get; set; } = new() { ResponseSec = 0.15, Damping = 1.0, Ms = 150, Easing = "CubicEaseOut" };
    public M3MotionSpec FadeOut { get; set; } = new() { ResponseSec = 0.12, Damping = 1.0, Ms = 120, Easing = "CubicEaseIn" };
}

/// <summary>MD3 type scale 单档（单位 dip）。</summary>
public sealed class M3TextStyle
{
    public double Size { get; set; }
    public double LineHeight { get; set; }
    public int Weight { get; set; } = 400;
    public double LetterSpacing { get; set; }
}

public sealed class M3TypographyTokens
{
    public M3TextStyle DisplayLarge { get; set; } = new() { Size = 57, LineHeight = 64, Weight = 400, LetterSpacing = -0.25 };
    public M3TextStyle DisplayMedium { get; set; } = new() { Size = 45, LineHeight = 52, Weight = 400 };
    public M3TextStyle DisplaySmall { get; set; } = new() { Size = 36, LineHeight = 44, Weight = 400 };
    public M3TextStyle HeadlineLarge { get; set; } = new() { Size = 32, LineHeight = 40, Weight = 400 };
    public M3TextStyle HeadlineMedium { get; set; } = new() { Size = 28, LineHeight = 36, Weight = 400 };
    public M3TextStyle HeadlineSmall { get; set; } = new() { Size = 24, LineHeight = 32, Weight = 400 };
    public M3TextStyle TitleLarge { get; set; } = new() { Size = 22, LineHeight = 28, Weight = 400 };
    public M3TextStyle TitleMedium { get; set; } = new() { Size = 16, LineHeight = 24, Weight = 500, LetterSpacing = 0.15 };
    public M3TextStyle TitleSmall { get; set; } = new() { Size = 14, LineHeight = 20, Weight = 500, LetterSpacing = 0.1 };
    public M3TextStyle BodyLarge { get; set; } = new() { Size = 16, LineHeight = 24, Weight = 400, LetterSpacing = 0.5 };
    public M3TextStyle BodyMedium { get; set; } = new() { Size = 14, LineHeight = 20, Weight = 400, LetterSpacing = 0.25 };
    public M3TextStyle BodySmall { get; set; } = new() { Size = 12, LineHeight = 16, Weight = 400, LetterSpacing = 0.4 };
    public M3TextStyle LabelLarge { get; set; } = new() { Size = 14, LineHeight = 20, Weight = 500, LetterSpacing = 0.1 };
    public M3TextStyle LabelMedium { get; set; } = new() { Size = 12, LineHeight = 16, Weight = 500, LetterSpacing = 0.5 };
    public M3TextStyle LabelSmall { get; set; } = new() { Size = 11, LineHeight = 16, Weight = 500, LetterSpacing = 0.5 };
}

/// <summary>MD3 语义色角色（seed=#2B6EE0）。两套配色都必须可用。</summary>
public sealed class M3ColorRoleTokens
{
    public string Primary { get; set; } = "#2B6EE0";
    public string OnPrimary { get; set; } = "#FFFFFF";
    public string PrimaryContainer { get; set; } = "#D8E2FF";
    public string OnPrimaryContainer { get; set; } = "#001945";
    public string Secondary { get; set; } = "#565E71";
    public string OnSecondary { get; set; } = "#FFFFFF";
    public string SecondaryContainer { get; set; } = "#DAE2F9";
    public string OnSecondaryContainer { get; set; } = "#131C2B";
    public string Tertiary { get; set; } = "#6F5575";
    public string OnTertiary { get; set; } = "#FFFFFF";
    public string TertiaryContainer { get; set; } = "#F8D8FE";
    public string OnTertiaryContainer { get; set; } = "#28132E";
    public string Error { get; set; } = "#BA1A1A";
    public string OnError { get; set; } = "#FFFFFF";
    public string ErrorContainer { get; set; } = "#FFDAD6";
    public string OnErrorContainer { get; set; } = "#410002";
    public string Background { get; set; } = "#FEFBFF";
    public string OnBackground { get; set; } = "#1B1B1F";
    public string Surface { get; set; } = "#FEFBFF";
    public string OnSurface { get; set; } = "#1B1B1F";
    public string SurfaceVariant { get; set; } = "#E1E2EC";
    public string OnSurfaceVariant { get; set; } = "#44474F";
    public string Outline { get; set; } = "#74777F";
    public string OutlineVariant { get; set; } = "#C4C6D0";
    public string SurfaceContainerLowest { get; set; } = "#FFFFFF";
    public string SurfaceContainerLow { get; set; } = "#F7F2FA";
    public string SurfaceContainer { get; set; } = "#F1ECF4";
    public string SurfaceContainerHigh { get; set; } = "#ECE6F0";
    public string SurfaceContainerHighest { get; set; } = "#E6E0E9";
    public string InverseSurface { get; set; } = "#303034";
    public string InverseOnSurface { get; set; } = "#F3EFF4";
    public string Scrim { get; set; } = "#000000";
}

public sealed class M3ColorTokens
{
    public M3ColorRoleTokens Light { get; set; } = new();
    public M3ColorRoleTokens Dark { get; set; } = new()
    {
        Primary = "#B0C6FF",
        OnPrimary = "#002E6A",
        PrimaryContainer = "#004494",
        OnPrimaryContainer = "#D8E2FF",
        Secondary = "#BEC6DC",
        OnSecondary = "#283141",
        SecondaryContainer = "#3E4759",
        OnSecondaryContainer = "#DAE2F9",
        Tertiary = "#DDBEE3",
        OnTertiary = "#3E2844",
        TertiaryContainer = "#563E5C",
        OnTertiaryContainer = "#F8D8FE",
        Error = "#FFB4AB",
        OnError = "#690005",
        ErrorContainer = "#93000A",
        OnErrorContainer = "#FFDAD6",
        Background = "#131316",
        OnBackground = "#E5E1E6",
        Surface = "#131316",
        OnSurface = "#E5E1E6",
        SurfaceVariant = "#44474F",
        OnSurfaceVariant = "#C4C6D0",
        Outline = "#8E9099",
        OutlineVariant = "#44474F",
        SurfaceContainerLowest = "#0D0D10",
        SurfaceContainerLow = "#1B1B1F",
        SurfaceContainer = "#1F1F23",
        SurfaceContainerHigh = "#2A2A2E",
        SurfaceContainerHighest = "#353539",
        InverseSurface = "#E5E1E6",
        InverseOnSurface = "#303034",
    };
}
