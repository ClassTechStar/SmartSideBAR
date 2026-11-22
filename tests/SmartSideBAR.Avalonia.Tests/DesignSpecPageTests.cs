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
}
