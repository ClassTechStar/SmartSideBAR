// Headless 应用宿主 (P1-6), 冒烟与像素基线需要真实渲染:
// UseHeadlessDrawing=false + Skia → CaptureRenderedFrame 出真像素。
// Fluent 主题在此装配 (Button 等模板控件需要), 与生产一致。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(SmartSideBAR.Avalonia.Tests.HeadlessAppBuilder))]

namespace SmartSideBAR.Avalonia.Tests;

public sealed class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        // 与生产 App.axaml 共用同一份全局字体样式，字体测试验的就是生产配置
        Styles.Add(AvaloniaXamlLoader.Load(
            new Uri("avares://SmartSideBAR.Avalonia/Assets/Fonts/MiSansFont.axaml")) as Styles
            ?? throw new InvalidOperationException("无法加载全局字体样式"));
    }
}

public static class HeadlessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
