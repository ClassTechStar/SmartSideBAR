// P2-a: 高对比度主题面生成的纯逻辑断言 (AppTheme, 不依赖窗口/线程)。
using Avalonia;
using Avalonia.Media;
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Avalonia.Tests;

public sealed class ThemeTests
{
    [Fact]
    public void 高对比度深色为黑底白字黄强调()
    {
        var p = AppTheme.From(new AppearanceConfig { Theme = "dark", HighContrast = true });
        Assert.True(p.HighContrast);
        Assert.True(p.Dark);
        Assert.False(p.Glass);
        Assert.Equal(Color.Parse("#000000"), p.PanelBg);
        Assert.Equal(Color.Parse("#FFFFFF"), p.Body);
        Assert.Equal(Color.Parse("#FFFFFF"), p.Divider);
        Assert.Equal(Color.Parse("#FFFF00"), p.Accent);
    }

    [Fact]
    public void 高对比度浅色为白底黑字()
    {
        var p = AppTheme.From(new AppearanceConfig { Theme = "light", HighContrast = true });
        Assert.True(p.HighContrast);
        Assert.False(p.Dark);
        Assert.Equal(Color.Parse("#FFFFFF"), p.PanelBg);
        Assert.Equal(Color.Parse("#000000"), p.Body);
        Assert.Equal(Color.Parse("#0000CC"), p.Accent);
    }

    [Fact]
    public void 高对比度深色强调块前景反色()
    {
        var hc = AppTheme.From(new AppearanceConfig { Theme = "dark", HighContrast = true });
        Assert.Same(Brushes.Black, hc.OnAccentBrush);
        var normal = AppTheme.From(new AppearanceConfig { Theme = "dark" });
        Assert.Same(Brushes.White, normal.OnAccentBrush);
    }

    [Fact]
    public void 高对比度优先于玻璃面()
    {
        var p = AppTheme.From(new AppearanceConfig { Theme = "light", LiquidGlass = true, HighContrast = true });
        Assert.True(p.HighContrast);
        Assert.False(p.Glass);
    }

    [Fact]
    public void 默认外观不是高对比度()
    {
        Assert.False(AppTheme.From(null).HighContrast);
        Assert.False(AppTheme.From(new AppearanceConfig()).HighContrast);
    }
}
