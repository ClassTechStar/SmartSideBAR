// Headless 应用宿主 (P1-6), 冒烟与像素基线需要真实渲染:
// UseHeadlessDrawing=false + Skia → CaptureRenderedFrame 出真像素。
// Fluent 主题在此装配 (Button 等模板控件需要), 与生产一致。
using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(SmartSideBAR.Avalonia.Tests.HeadlessAppBuilder))]

namespace SmartSideBAR.Avalonia.Tests;

public sealed class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}

public static class HeadlessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
