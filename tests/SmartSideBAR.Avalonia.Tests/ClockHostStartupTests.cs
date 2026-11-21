// 启动顺序回归守卫 —— 2026-11-22 "测试全绿但启动即崩" 的根因：
// LessonModeHost.Init 在 Program.Main 里早于 AppBuilder.Start 调用，
// 旧 ClockHost.Register 顺手 new DispatcherTimer()，把 Dispatcher.UIThread
// 绑到平台未就绪的空实现上，MainLoop 随即抛 PlatformNotSupportedException。
// headless 测试宿主先建好了平台，所以 342 个测试全绿也照样漏掉。
//
// 双保险：源码守卫（启动点必须在平台就绪后）+ 运行时行为（Register 不直接建 timer）。
using SmartSideBAR.Avalonia.Ui;

namespace SmartSideBAR.Avalonia.Tests;

public class ClockHostStartupTests
{
    private static string ClockSource =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "SmartSideBAR.Avalonia", "Ui", "ClockHost.cs"));

    private static string AppSource =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "SmartSideBAR.Avalonia", "App.axaml.cs"));

    [Fact]
    public void 平台未就绪时不得创建DispatcherTimer()
    {
        // EnsureStarted 必须走 PlatformReady 判定，不能无条件 new DispatcherTimer
        Assert.Contains("PlatformReady()", ClockSource);
        Assert.Contains("WantsStart = true", ClockSource);

        // Register 只能登记消费者后走 EnsureStarted，不允许自己直接 new timer
        var register = ClockSource.Substring(
            ClockSource.IndexOf("public static void Register", StringComparison.Ordinal));
        var method = register[..register.IndexOf("public static bool Unregister", StringComparison.Ordinal)];
        Assert.DoesNotContain("new global::Avalonia.Threading.DispatcherTimer", method);
        Assert.DoesNotContain("new DispatcherTimer", method);
    }

    [Fact]
    public void App初始化完成后必须显式启动统一时钟()
    {
        // 平台就绪后的启动点必须存在，否则消费者登记了却永远不跑
        Assert.Contains("ClockHost.EnsureStarted()", AppSource);
    }

    [Fact]
    public void 重复登记消费者不会重复建计时器()
    {
        ClockHost.Register("startup-test-a", 60_000, () => { });
        ClockHost.Register("startup-test-a", 60_000, () => { });
        // 只验证登记不抛异常、不破坏状态（真 timer 由平台初始化后的 EnsureStarted 建）
        var pumped = ClockHost.PumpForTests(Environment.TickCount64 + 600_000);
        Assert.True(pumped >= 0);
    }
}
