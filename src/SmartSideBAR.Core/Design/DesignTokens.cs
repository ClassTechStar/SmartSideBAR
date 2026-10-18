// 设计令牌（P0-2）—— 视觉规格的单一真源。
// 背景：重建 Avalonia 版时没有规格基线，近 12 个提交里 8 个是「对齐 v1.2」返工
// （胶囊 72px、rail、玻璃高光没画出来、SVG 不显示、角标、停靠箭头 22→26→30 逐像素手调）。
// 每条用户反馈都是一次像素考古，根因就是规格散落在 XAML/CSS/代码里。
//
// 约定：
//   1) 数值以 design-tokens.json（仓库根）为准，随 Core 程序集嵌入发布；
//   2) Avalonia 的 Theme/窗口几何一律从这里取，不允许就地写死数字；
//   3) 改规格必须先改 JSON —— DesignTokensTests 会把 v1.2 基线钉死，防止"顺手改坏"。
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

    private static DesignTokens? _default;

    /// <summary>随程序集嵌入的规格副本（单一真源的运行时镜像）。</summary>
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
}

public sealed class MotionTokens
{
    public double AnimMs { get; set; } = 220;
    public double AnimFastMs { get; set; } = 120;
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DesignTokens))]
internal partial class DesignTokenJsonContext : JsonSerializerContext;
