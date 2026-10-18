// 视觉规格回归（P0-2）。
// 这些断言把 v1.2 已确认的规格钉死在 design-tokens.json 上：
//   - 想改 UI 规格 → 必须改 JSON → 本测试红 → 强制走"改基线"而不是"改代码"的路径；
//   - 谁手滑把 rail 改成 64、箭头改回 22，这里立刻红。
// 背景：重建期 8 个提交是「对齐 v1.2」返工，停靠箭头被逐像素手调 22→26→30，
// 根因就是没有这份可执行基线。
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
        // 用户连续两次要求放大（22 → 26 → 30），定在 30
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
