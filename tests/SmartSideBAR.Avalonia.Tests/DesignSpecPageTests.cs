// 设计规范页守卫：这是 M3 组件层的真实使用者，必须可渲染、可点、无硬编码。
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Avalonia.Tests;

public class DesignSpecPageTests
{
    private static Palette Palette() => new()
    {
        Dark = false,
        Glass = false,
        PanelBg = Colors.White,
        Inset = Color.Parse("#F5F7FA"),
        Card = Colors.White,
        Text = Color.Parse("#1A1A1A"),
        Body = Color.Parse("#1B2230"),
        CardText = Color.Parse("#2A3240"),
        Muted = Color.Parse("#5A6478"),
        Divider = Color.Parse("#E8E8E8"),
        Hover = Color.Parse("#F5F7FA"),
        Accent = Color.Parse("#2B6EE0"),
        AccentSoft = Color.Parse("#E8F1FC"),
        RailFrom = Colors.White,
        RailTo = Colors.White,
        RailGlow = Colors.White,
        RimFrom = Colors.White,
        RimTo = Colors.White,
    };

    [AvaloniaFact]
    public void 设计规范页可构建并渲染()
    {
        var page = DesignSpecPage.Build(Palette());
        Assert.IsType<ScrollViewer>(page);

        var window = new Window { Content = page, Width = 900, Height = 700 };
        window.Show();
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        Assert.NotEmpty(window.GetVisualDescendants());
        window.Close();
    }

    [AvaloniaFact]
    public void 页面内按钮全部达到触控下限()
    {
        var window = new Window { Content = DesignSpecPage.Build(Palette()), Width = 900, Height = 700 };
        window.Show();

        var buttons = window.GetVisualDescendants().OfType<Button>()
            .Where(b => Equals(b.Tag, M3Tags.Button)).ToArray();
        Assert.True(buttons.Length >= 4, "设计规范页应陈列按钮四变体");
        foreach (var button in buttons)
        {
            Assert.True(button.MinHeight >= DesignTokens.Current.M3.Touch.MinTouchDip,
                $"按钮高度 {button.MinHeight} 低于 MD3 触控下限");
        }
        window.Close();
    }

    [AvaloniaFact]
    public void 页面包含输入框与列表行()
    {
        var window = new Window { Content = DesignSpecPage.Build(Palette()), Width = 900, Height = 700 };
        window.Show();

        Assert.Contains(window.GetVisualDescendants().OfType<TextBox>(), t => Equals(t.Tag, M3Tags.TextField));
        Assert.Contains(window.GetVisualDescendants(), c => c is Grid);
        window.Close();
    }

    [Fact]
    public void 页面源码无硬编码颜色与圆角()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "SmartSideBAR.Avalonia", "Ui", "DesignSpecPage.cs"));

        Assert.DoesNotContain("\"#", source);
        Assert.DoesNotMatch(@"new CornerRadius\(\s*\d", source);
    }

    private static Palette DarkPalette() => Palette() with
    {
        Dark = true,
        Glass = false,
        PanelBg = M3.Role(M3ColorRole.Surface, dark: true),
        Inset = M3.Role(M3ColorRole.SurfaceContainerLow, dark: true),
        Card = M3.Role(M3ColorRole.SurfaceContainer, dark: true),
        Text = M3.Role(M3ColorRole.OnSurface, dark: true),
        Body = M3.Role(M3ColorRole.OnSurface, dark: true),
        CardText = M3.Role(M3ColorRole.OnSurfaceVariant, dark: true),
        Muted = M3.Role(M3ColorRole.OnSurfaceVariant, dark: true),
        Divider = M3.Role(M3ColorRole.OutlineVariant, dark: true),
        Hover = M3.Role(M3ColorRole.SurfaceContainerHigh, dark: true),
        Accent = M3.Role(M3ColorRole.Primary, dark: true),
        AccentSoft = M3.Role(M3ColorRole.PrimaryContainer, dark: true),
    };

    [AvaloniaFact]
    public void 深色主题下页面可渲染且按钮达标()
    {
        var window = new Window { Content = DesignSpecPage.Build(DarkPalette()), Width = 900, Height = 700 };
        window.Show();
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var buttons = window.GetVisualDescendants().OfType<Button>()
            .Where(b => Equals(b.Tag, M3Tags.Button)).ToArray();
        // 明暗两块各四个变体
        Assert.True(buttons.Length >= 8, $"深色对照应有 8 个以上 M3 按钮，实际 {buttons.Length}");
        foreach (var b in buttons) Assert.True(b.MinHeight >= DesignTokens.Current.M3.Touch.MinTouchDip);

        window.Close();
    }

    [AvaloniaFact]
    public void 页面同时陈列明暗两套色板与组件()
    {
        var window = new Window { Content = DesignSpecPage.Build(Palette()), Width = 900, Height = 900 };
        window.Show();
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // 浅色块 + 深色块 → 至少 16 个色板 Border（8 角色 × 2 套）
        var swatches = window.GetVisualDescendants().OfType<Border>()
            .Where(b => b.Height is > 30 and < 40).ToArray();
        Assert.True(swatches.Length >= 16, $"色板数量不足：{swatches.Length}");

        // 深色组件块的背景应接近深色 surface，而不是白色
        var darkSurface = M3.Role(M3ColorRole.Surface, dark: true);
        Assert.Contains(window.GetVisualDescendants().OfType<Border>(), b =>
            b.Background is SolidColorBrush s && s.Color == darkSurface);

        window.Close();
    }
}
