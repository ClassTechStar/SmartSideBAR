// M1 主题资源化: Palette → 资源字典的映射契约 (键齐全、模式差异、可覆盖)。
using Avalonia.Controls;
using Avalonia.Media;
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Avalonia.Tests;

public sealed class ThemeResourcesTests
{
    private static Color ColorOf(IResourceDictionary dict, string key) =>
        Assert.IsType<SolidColorBrush>(dict[key]).Color;

    private static readonly string[] AllKeys =
    [
        ThemeResources.RailHover, ThemeResources.RailPressed, ThemeResources.RailActive,
        ThemeResources.BadgeBg, ThemeResources.BadgeFg, ThemeResources.IndicatorBar,
        ThemeResources.PanelBg, ThemeResources.PanelHeaderBorder, ThemeResources.PanelTitleFg,
        ThemeResources.PanelCloseFg, ThemeResources.BodyFg, ThemeResources.MutedFg,
        ThemeResources.CardBg, ThemeResources.CardBorder, ThemeResources.TextFg,
        ThemeResources.CardTextFg, ThemeResources.HoverBg, ThemeResources.AccentFg,
        ThemeResources.AccentSoftBg, ThemeResources.OnAccentFg, ThemeResources.OnAccentSoftFg,
        ThemeResources.DividerFg,
        ThemeResources.OkFg, ThemeResources.WarnFg, ThemeResources.ErrFg,
        ThemeResources.RecFg, ThemeResources.RecSoftBg, ThemeResources.RecBorderFg,
    ];

    [Fact]
    public void 经典浅色写入全部主题键()
    {
        var dict = new ResourceDictionary();
        var p = AppTheme.From(new AppearanceConfig { Theme = "light", LiquidGlass = false });

        ThemeResources.Apply(dict, p);

        foreach (var key in AllKeys) Assert.NotNull(dict[key]);
        Assert.Equal(p.PanelBg, ColorOf(dict, ThemeResources.PanelBg));
        Assert.Equal(p.Body, ColorOf(dict, ThemeResources.BodyFg));
        Assert.Equal(p.Muted, ColorOf(dict, ThemeResources.MutedFg));
        Assert.Equal(p.Card, ColorOf(dict, ThemeResources.CardBg));
        Assert.Equal(p.Divider, ColorOf(dict, ThemeResources.CardBorder));
        Assert.Equal(p.Accent, ColorOf(dict, ThemeResources.BadgeBg));
        Assert.Equal(p.Accent, ColorOf(dict, ThemeResources.IndicatorBar));

        // M5: 面板/设置页共用内容色
        Assert.Equal(p.Text, ColorOf(dict, ThemeResources.TextFg));
        Assert.Equal(p.CardText, ColorOf(dict, ThemeResources.CardTextFg));
        Assert.Equal(p.Hover, ColorOf(dict, ThemeResources.HoverBg));
        Assert.Equal(p.Accent, ColorOf(dict, ThemeResources.AccentFg));
        Assert.Equal(p.AccentSoft, ColorOf(dict, ThemeResources.AccentSoftBg));
        Assert.Equal(p.Divider, ColorOf(dict, ThemeResources.DividerFg));
        var rec = ColorOf(dict, ThemeResources.RecFg);
        var recBorder = ColorOf(dict, ThemeResources.RecBorderFg);
        Assert.Equal((rec.R, rec.G, rec.B), (recBorder.R, recBorder.G, recBorder.B));
        Assert.Equal(0x33, recBorder.A);
        Assert.Equal(0x14, ((ISolidColorBrush)dict[ThemeResources.RecSoftBg]!).Color.A);
        var onAccent = ((ISolidColorBrush)dict[ThemeResources.OnAccentFg]!).Color;
        var onAccentSoft = ((ISolidColorBrush)dict[ThemeResources.OnAccentSoftFg]!).Color;
        Assert.Equal((onAccent.R, onAccent.G, onAccent.B), (onAccentSoft.R, onAccentSoft.G, onAccentSoft.B));
        Assert.Equal(0xD9, onAccentSoft.A);
    }

    [Fact]
    public void 经典面标题用正文色玻璃面用强调色()
    {
        var classic = AppTheme.From(new AppearanceConfig { Theme = "light", LiquidGlass = false });
        var glass = AppTheme.From(new AppearanceConfig { Theme = "light", LiquidGlass = true });

        var classicDict = new ResourceDictionary();
        ThemeResources.Apply(classicDict, classic);
        var glassDict = new ResourceDictionary();
        ThemeResources.Apply(glassDict, glass);

        Assert.Equal(classic.Text, ColorOf(classicDict, ThemeResources.PanelTitleFg));
        Assert.Equal(glass.Accent, ColorOf(glassDict, ThemeResources.PanelTitleFg));
        Assert.Equal(classic.Divider, ColorOf(classicDict, ThemeResources.PanelHeaderBorder));
    }

    [Fact]
    public void 经典面交互色取设计令牌()
    {
        var dict = new ResourceDictionary();
        var p = AppTheme.From(new AppearanceConfig { Theme = "light", LiquidGlass = false });
        ThemeResources.Apply(dict, p);

        var classic = DesignTokens.Current.Classic;
        Assert.Equal(Color.Parse(classic.BrandLight!), ColorOf(dict, ThemeResources.RailHover));
        Assert.Equal(Color.Parse(classic.PressedHover!), ColorOf(dict, ThemeResources.RailPressed));
        Assert.Equal(Color.Parse(classic.BrandLight!), ColorOf(dict, ThemeResources.RailActive));
    }

    [Fact]
    public void 重复应用覆盖旧值()
    {
        var dict = new ResourceDictionary();
        ThemeResources.Apply(dict, AppTheme.From(new AppearanceConfig { Theme = "light", LiquidGlass = false }));
        var lightBody = ColorOf(dict, ThemeResources.BodyFg);

        var dark = AppTheme.From(new AppearanceConfig { Theme = "dark", LiquidGlass = true });
        ThemeResources.Apply(dict, dark);

        Assert.NotEqual(lightBody, ColorOf(dict, ThemeResources.BodyFg));
        Assert.Equal(dark.Body, ColorOf(dict, ThemeResources.BodyFg));
        Assert.Equal(dark.Accent, ColorOf(dict, ThemeResources.PanelTitleFg));
    }
}
