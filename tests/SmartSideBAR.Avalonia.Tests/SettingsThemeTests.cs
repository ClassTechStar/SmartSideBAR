// 设置页深色主题（M3 语义色）守卫。
// 背景：侧栏/面板用 v1.2 玻璃面（半透明卡片），设置页若沿用会在深底上发灰、层级压不开。
// 这里锁定"设置页深色 = M3 不透明 surface 阶梯"的几条硬性质。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Avalonia.Tests;

public class SettingsThemeTests
{
    private static AppearanceConfig Dark(string accent = "#2B6EE0") => new()
    {
        Theme = "dark",
        Accent = accent,
    };

    [Fact]
    public void 深色设置面全部不透明()
    {
        var p = AppTheme.M3Palette(dark: true);
        foreach (var (name, color) in new (string, Color)[]
                 {
                     ("PanelBg", p.PanelBg), ("Inset", p.Inset), ("Card", p.Card),
                     ("Body", p.Body), ("CardText", p.CardText), ("Divider", p.Divider),
                     ("Accent", p.Accent), ("AccentSoft", p.AccentSoft),
                 })
        {
            Assert.Equal(255, color.A);
        }
    }

    [Fact]
    public void 深色surface阶梯有层次()
    {
        var p = AppTheme.M3Palette(dark: true);
        // 卡片必须比页面底亮一档（深色下"浮起来"），hover 再亮一档
        Assert.True(Luma(p.Card) > Luma(p.Inset), "卡片应比页面底亮");
        Assert.True(Luma(p.Hover) > Luma(p.Card), "hover 应比卡片亮");
        Assert.True(Luma(p.Body) > Luma(p.Card), "正文应比卡片亮");
    }

    [Fact]
    public void 深色文字取onSurface而不是强调色()
    {
        var p = AppTheme.M3Palette(dark: true);
        // M3：标题/正文用 onSurface；强调色只用于强调，不铺满文字
        Assert.NotEqual(p.Text, p.Accent);
        Assert.True(Luma(p.Text) > 0.7, $"onSurface 应接近白，实测亮度 {Luma(p.Text):0.00}");
    }

    [Fact]
    public void 过暗的自定义强调色在深色下回退primary()
    {
        // 默认品牌蓝 #2B6EE0 亮度约 0.42，深底上不可读 → 应换成 M3 深色 primary
        var p = AppTheme.M3Palette(dark: true, accent: Color.Parse("#2B6EE0"));
        Assert.Equal(M3.Role(M3ColorRole.Primary, dark: true), p.Accent);

        // 亮色强调色应被保留
        var bright = AppTheme.M3Palette(dark: true, accent: Color.Parse("#B0C6FF"));
        Assert.Equal(Color.Parse("#B0C6FF"), bright.Accent);
    }

    [Fact]
    public void 浅色与深色都走M3()
    {
        var dark = AppTheme.ForSettings(Dark());
        Assert.True(dark.Dark);
        Assert.False(dark.Glass);
        Assert.Equal(255, dark.Card.A);

        // 浅色同样是 M3 不透明 surface 阶梯（2026-11-22 起浅深对称）
        var light = AppTheme.ForSettings(new AppearanceConfig { Theme = "light" });
        Assert.False(light.Dark);
        Assert.Equal(255, light.Card.A);
        Assert.Equal(255, light.Inset.A);
        Assert.True(Luma(light.Card) < Luma(light.Inset), "M3 浅色：白底 + 稍暗卡片");
        Assert.True(Luma(light.Text) < 0.3, "浅色文字应为深色");

        // 设置页与全局 From() 仍完全一致（不引入第二套浅色规格）
        var appearance = new AppearanceConfig { Theme = "light" };
        var baseline = AppTheme.From(appearance);
        Assert.Equal(baseline.Card, AppTheme.ForSettings(appearance).Card);
    }

    [Fact]
    public void 侧栏浅色与设置页浅色同源()
    {
        var appearance = new AppearanceConfig { Theme = "light", Accent = "#2B6EE0" };
        var sidebar = AppTheme.From(appearance);
        var settings = AppTheme.M3Palette(dark: false, accent: Color.Parse("#2B6EE0"));

        Assert.Equal(settings.PanelBg, sidebar.PanelBg);
        Assert.Equal(settings.Inset, sidebar.Inset);
        Assert.Equal(settings.Card, sidebar.Card);
        Assert.Equal(settings.Text, sidebar.Text);
        Assert.Equal(settings.Body, sidebar.Body);
        Assert.Equal(settings.CardText, sidebar.CardText);
        Assert.Equal(settings.Muted, sidebar.Muted);
        Assert.Equal(settings.Divider, sidebar.Divider);
        Assert.Equal(settings.Hover, sidebar.Hover);
        Assert.Equal(settings.Accent, sidebar.Accent);
        Assert.Equal(settings.AccentSoft, sidebar.AccentSoft);
    }

    [Fact]
    public void 浅色卡片全不透明且层级有序()
    {
        foreach (var theme in new[] { "light" })
        {
            var p = AppTheme.From(new AppearanceConfig { Theme = theme });
            Assert.Equal(255, p.Card.A);
            Assert.Equal(255, p.Inset.A);
            // M3 浅色：页面底 surface 最亮（近白），卡片 surfaceContainer 稍暗
            Assert.True(Luma(p.Card) < Luma(p.Inset));
        }
    }

    [Fact]
    public void 九条设置页签共用同一深色调色板()
    {
        // 页签颜色全部来自 SettingsWindow 的 _p，这里只需保证 ForSettings 结果稳定可复用
        var a = AppTheme.ForSettings(Dark());
        var b = AppTheme.ForSettings(Dark());
        Assert.Equal(a.Card, b.Card);
        Assert.Equal(a.Body, b.Body);
        Assert.Equal(a.Accent, b.Accent);
    }

    private static double Luma(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

    [AvaloniaFact]
    public void 外观页深色渲染无白块且文字可读()
    {
        var palette = AppTheme.M3Palette(dark: true);
        var page = new AppearancePage();
        page.ApplyPalette(palette);

        var window = new Window
        {
            Content = page,
            Width = 820,
            Height = 740,
            Background = new SolidColorBrush(palette.Inset),
            RequestedThemeVariant = ThemeVariant.Dark,
        };
        window.Show();
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var borders = window.GetVisualDescendants().OfType<Border>()
            .Where(b => b.Background is SolidColorBrush { Color.A: > 0 }).ToArray();
        Assert.NotEmpty(borders);
        foreach (var b in borders)
        {
            var c = ((SolidColorBrush)b.Background!).Color;
            // 排除接近白（1.0）的块；允许 Fluent 的强调高亮（#0078D7 亮度约 0.37）
            Assert.True(Luma(c) < 0.55,
                $"深色页面出现亮底块: {c} (亮度 {Luma(c):0.00})");
        }

        var texts = window.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Foreground is SolidColorBrush).ToArray();
        Assert.NotEmpty(texts);
        foreach (var t in texts)
        {
            var c = ((SolidColorBrush)t.Foreground!).Color;
            Assert.True(Luma(c) > 0.45,
                $"深色页面出现暗字: \"{t.Text}\" 前景 {c} (亮度 {Luma(c):0.00})");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void 外观页浅色仍可渲染()
    {
        var appearance = new AppearanceConfig { Theme = "light" };
        var palette = AppTheme.ForSettings(appearance);
        var page = new AppearancePage();
        page.ApplyPalette(palette);

        var window = new Window
        {
            Content = page,
            Width = 820,
            Height = 740,
            Background = new SolidColorBrush(palette.Inset),
        };
        window.Show();
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Assert.NotEmpty(window.GetVisualDescendants());
        window.Close();
    }

    [Fact]
    public void 侧栏深色与设置页深色同源()
    {
        // 侧栏 AppTheme.From(dark) 与设置页 M3Palette(dark) 必须给出同一套内容色，
        // 否则深色下"侧栏发灰、设置页很清晰"的双轨现象会回来
        var appearance = new AppearanceConfig { Theme = "dark", Accent = "#2B6EE0" };
        var sidebar = AppTheme.From(appearance);
        var settings = AppTheme.M3Palette(dark: true, accent: Color.Parse("#2B6EE0"));

        Assert.Equal(settings.PanelBg, sidebar.PanelBg);
        Assert.Equal(settings.Inset, sidebar.Inset);
        Assert.Equal(settings.Card, sidebar.Card);
        Assert.Equal(settings.Text, sidebar.Text);
        Assert.Equal(settings.Body, sidebar.Body);
        Assert.Equal(settings.CardText, sidebar.CardText);
        Assert.Equal(settings.Muted, sidebar.Muted);
        Assert.Equal(settings.Divider, sidebar.Divider);
        Assert.Equal(settings.Hover, sidebar.Hover);
        Assert.Equal(settings.Accent, sidebar.Accent);
        Assert.Equal(settings.AccentSoft, sidebar.AccentSoft);
    }

    [Fact]
    public void 侧栏保留rail胶囊形态()
    {
        // rail 渐变/边光环是侧栏形态语言，不随配色统一而消失
        var appearance = new AppearanceConfig { Theme = "dark" };
        var sidebar = AppTheme.From(appearance);
        var plain = AppTheme.M3Palette(dark: true);

        Assert.NotEqual(plain.RailFrom, sidebar.RailFrom);
        Assert.NotEqual(plain.RailTo, sidebar.RailTo);
        Assert.True(sidebar.RailFrom.A > 0);
    }

    [Fact]
    public void 深色卡片全部不透明()
    {
        // 统一后侧栏/设置页的深色卡片都不再是半透明叠加
        var p = AppTheme.From(new AppearanceConfig { Theme = "dark" });
        Assert.Equal(255, p.Card.A);
        Assert.Equal(255, p.Inset.A);
        Assert.True(Luma(p.Card) > Luma(p.Inset));
    }
}
