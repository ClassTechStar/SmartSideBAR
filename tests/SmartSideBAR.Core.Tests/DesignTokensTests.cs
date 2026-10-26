// 视觉规格回归（P0-2）。
// 这些断言把 v1.2 已确认的规格固定在 design-tokens.json 上：
//   - 想改 UI 规格 → 必须改 JSON → 本测试红 → 强制走"改基线"而不是"改代码"的路径；
//   - 谁手滑把 rail 改成 64、箭头改回 22，这里立刻红。
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Core.Tests;

public class DesignTokensTests
{
    private static DesignTokens T => DesignTokens.Current;

    [Fact]
    public void Embedded_tokens_are_loadable()
    {
        // 嵌入副本缺失时会退回类型默认值，这里确保 JSON 真的进了程序集
        Assert.Equal("1.2-derived", T.Version);
    }

    [Fact]
    public void Rail_spec_matches_v12()
    {
        Assert.Equal(72, T.Rail.WidthDip);
        Assert.Equal(52, T.Rail.ItemSizeDip);
        Assert.Equal(28, T.Rail.IconSizeDip);
        Assert.Equal(8, T.Rail.Gap);
        Assert.Equal(12, T.Rail.PaddingTop);
        Assert.Equal(8, T.Rail.PaddingBottom);
    }

    [Fact]
    public void Capsule_and_panel_spec_matches_v12()
    {
        Assert.Equal(28, T.Capsule.Radius);
        Assert.Equal(0.08, T.Capsule.MarginRatio);
        Assert.Equal(21, T.Capsule.Blur);
        Assert.Equal(2.2, T.Capsule.Saturate);

        Assert.Equal(380, T.Panel.WidthDip);
        Assert.Equal(28, T.Panel.Radius);
        Assert.Equal(52, T.Panel.HeaderHeight);
    }

    [Fact]
    public void Dock_toggle_arrow_is_30px()
    {
        // 按用户要求放大过两轮（22 → 26 → 30），现定 30
        Assert.Equal(52, T.Dock.ToggleSizeDip);
        Assert.Equal(30, T.Dock.ArrowSizePx);
    }

    [Fact]
    public void Brand_and_state_colors_match_v12()
    {
        Assert.Equal("#2B6EE0", T.Color.Brand);
        Assert.Equal("#FFFFFF", T.Color.PanelBg);
        Assert.Equal("#2B6EE0", T.Color.Text);
        Assert.Equal("#2E9E5B", T.Color.StateOk);
        Assert.Equal("#D98324", T.Color.StateWarn);
        Assert.Equal("#D64545", T.Color.StateError);
        Assert.Equal("#E74C3C", T.Color.StateRecording);
    }

    [Fact]
    public void Badge_spec_matches_v12()
    {
        // v1.2 .rail-badge: 10px 字号 / 圆角 6 / 右下各 2px
        Assert.Equal(10, T.Badge.FontSize);
        Assert.Equal(6, T.Badge.Radius);
        Assert.Equal(2, T.Badge.OffsetX);
        Assert.Equal(2, T.Badge.OffsetY);
    }

    [Fact]
    public void Motion_vocabulary_matches_feel_baseline()
    {
        // 1.0.0 的动效时长：微反馈 120 / 面板 180 / 窗口 250
        Assert.Equal(220, T.Motion.AnimMs);
        Assert.Equal(120, T.Motion.AnimFastMs);
        Assert.Equal(250, T.Motion.WindowMs);
        Assert.Equal(180, T.Motion.PanelMs);
        Assert.Equal(32, T.Motion.StaggerMs);
        Assert.Equal(0.96, T.Motion.PressScale);
    }

    [Fact]
    public void Classic_surface_matches_v100()
    {
        // 1.0.0「经典快」表面语言：纯白 + 发丝线 + 品牌浅蓝态色
        Assert.Equal("#FFFFFF", T.Classic.Bg);
        Assert.Equal("#F5F7FA", T.Classic.BgHover);
        Assert.Equal("#E8F1FC", T.Classic.BrandLight);
        Assert.Equal("#D9E8FB", T.Classic.PressedHover);
        Assert.Equal("#E8E8E8", T.Classic.Border);
        Assert.Equal("#1A1A1A", T.Classic.Text);
        Assert.Equal("#666666", T.Classic.TextSecondary);
        Assert.Equal("#14000000", T.Classic.Shadow);
    }

    [Fact]
    public void Shape_spacing_shadow_indicator_roles()
    {
        // Seelen 形状/间距角色 + KoBar 海拔阴影与选中指示条
        Assert.Equal(4, T.Shape.RadiusXs);
        Assert.Equal(6, T.Shape.RadiusS);
        Assert.Equal(10, T.Shape.RadiusM);
        Assert.Equal(16, T.Shape.RadiusL);
        Assert.Equal(4, T.Spacing.Xs);
        Assert.Equal(16, T.Spacing.L);
        Assert.Equal("0 2 3 #14000000", T.Shadow.E1);
        Assert.Equal("0 4 12 #29000000", T.Shadow.E2);
        Assert.Equal("0 8 28 #3D000000", T.Shadow.E3);
        Assert.Equal(3, T.Indicator.Width);
        Assert.Equal(18, T.Indicator.Height);
    }

    [Fact]
    public void Parse_roundtrip_preserves_values()
    {
        const string json = """
        {
          "version": "1.2-derived",
          "dock": { "toggleSizeDip": 52, "arrowSizePx": 34 },
          "rail": { "widthDip": 72, "itemSizeDip": 52, "iconSizeDip": 28, "radius": 14, "gap": 8, "paddingTop": 12, "paddingBottom": 8 }
        }
        """;

        var tokens = DesignTokens.Parse(json);

        Assert.Equal(34, tokens.Dock.ArrowSizePx);
        // 未声明的段回落到类型默认值，而不是 null
        Assert.Equal(380, tokens.Panel.WidthDip);
        Assert.Equal("#2B6EE0", tokens.Color.Brand);
    }
}
