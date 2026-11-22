// 独立窗口（崩溃提示 / OOBE）的深色主题守卫。
// 这些窗口不注入配置，此前用 Brushes.White/DimGray 硬编码，深色下会出现白块。
// 现在统一走 AppTheme.M3Palette / CurrentM3，与设置页同源。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using SmartSideBAR.Avalonia.Ui;

namespace SmartSideBAR.Avalonia.Tests;

public class IndependentWindowThemeTests
{
    [AvaloniaFact]
    public void CurrentM3跟随应用主题变体()
    {
        if (Application.Current is not { } app) return;

        app.RequestedThemeVariant = ThemeVariant.Dark;
        var dark = AppTheme.CurrentM3();
        Assert.True(dark.Dark);
        Assert.Equal(255, dark.Card.A);

        app.RequestedThemeVariant = ThemeVariant.Light;
        var light = AppTheme.CurrentM3();
        Assert.False(light.Dark);

        app.RequestedThemeVariant = ThemeVariant.Light; // 恢复
    }

    [AvaloniaFact]
    public void 崩溃报告窗深色无白块且文字可读()
    {
        var palette = AppTheme.M3Palette(dark: true);
        var w = new CrashReportWindow("测试异常信息", @"C:/logs/crash.log", palette);
        w.Show();
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // M3.Button 的 Filled 变体用 primary 亮蓝作主操作色，属允许的亮色；
        // 这里只查容器类 Border（卡片/窗体底），排除带 M3 标记的按钮
        var borders = w.GetVisualDescendants().OfType<Border>()
            .Where(b => b.Background is SolidColorBrush { Color.A: > 0 }
                        && !Equals(b.Tag, M3Tags.Button)).ToArray();
        Assert.NotEmpty(borders);
        foreach (var b in borders)
        {
            var c = ((SolidColorBrush)b.Background!).Color;
            var luma = Luma(c);
            Assert.True(luma < 0.55, $"崩溃提示窗出现亮底块: {c} (亮度 {luma:0.00})");
        }

        // Filled 按钮用 primary 亮蓝底 + onPrimary 深字，是 M3 正确对比，不计入"暗字"检查
        var texts = w.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Foreground is SolidColorBrush)
            .Where(t => t.FindAncestorOfType<Button>() is not { } btn || !Equals(btn.Tag, M3Tags.Button))
            .ToArray();
        Assert.NotEmpty(texts);
        foreach (var t in texts)
        {
            var c = ((SolidColorBrush)t.Foreground!).Color;
            Assert.True(Luma(c) > 0.45,
                $"崩溃提示窗出现暗字: \"{t.Text}\" 前景 {c} (亮度 {Luma(c):0.00})");
        }

        // 按钮应是 M3 按钮（带标记），而非裸 Button
        var m3Buttons = w.GetVisualDescendants().OfType<Button>()
            .Where(b => Equals(b.Tag, M3Tags.Button)).ToArray();
        Assert.True(m3Buttons.Length >= 3, "崩溃提示窗应有 3 个 M3 按钮");

        w.Close();
    }

    [AvaloniaFact]
    public void 崩溃报告窗浅色仍可渲染()
    {
        var w = new CrashReportWindow("测试", @"C:/logs/a.log", AppTheme.M3Palette(dark: false));
        w.Show();
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Assert.NotEmpty(w.GetVisualDescendants());
        w.Close();
    }

    private static double Luma(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
}
