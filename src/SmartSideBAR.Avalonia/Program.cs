// 服务装配 (方案 §4.2): 无 IPC —— UI 与服务同进程直调类型安全接口。
using Avalonia;
using Avalonia.Win32;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartSideBAR.Avalonia.Logging;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Links;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Core.Policy;
using SmartSideBAR.Core.Scheduling;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Audio;
using SmartSideBAR.Windows.Capture;
using SmartSideBAR.Windows.Devices;
using SmartSideBAR.Windows.Input;
using SmartSideBAR.Windows.Native;
using SmartSideBAR.Windows.Recording;
using SmartSideBAR.Windows.Shell;

namespace SmartSideBAR.Avalonia;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // 单实例：v1.2 用 app.requestSingleInstanceLock()，Avalonia 版此前缺失，
        // 双击两次图标会起两个进程（两个 AppBar 抢边缘、热键互抢、双托盘图标）。
        if (!SingleInstance.Acquire())
        {
            SingleInstance.Signal();
            return 0;
        }

        // 静默退出取证：任何一层未处理异常都落盘到 logs/crash.log
        CrashLog.InstallProcessWide();

        var services = new ServiceCollection();

        // Core 层 (100% 可单测)
        services.AddSingleton(ConfigPaths.CreateDefault());
        services.AddSingleton<ConfigService>();
        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<PolicyService>();
        services.AddSingleton<LinksService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SchedulerService>();

        // Windows 互操作层 (接口 + 实现成对注册, 测试可替换)
        // D1: 重服务延迟加载 —— 录屏/长截图模块只在首次使用时构造
        services.AddSingleton<IWndProcHook, WndProcHook>();
        services.AddSingleton<IAppBarApi, AppBarApi>();
        services.AddSingleton<AppBarService>();
        services.AddSingleton<IImeService, ImeService>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<IHotkeyService>(sp => sp.GetRequiredService<HotkeyService>());
        services.AddSingleton<IUsbWatcher, UsbDeviceWatcher>();
        services.AddSingleton<IPrinterMonitor, PrinterMonitor>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<ISoundService, SoundService>();
        services.AddSingleton<ICaptureApi, GdiCapture>();
        // D1: 录屏与长截图延迟初始化 (首次调用时才构造 WinRT COM 对象)
        services.AddSingleton<ILongshotService>(sp => ActivatorUtilities.CreateInstance<LongshotService>(sp));
        services.AddSingleton<IRecorderApi>(sp => ActivatorUtilities.CreateInstance<GraphicsCaptureRecorder>(sp));
        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();

        // UI 层
        services.AddSingleton<WindowManager>();

        services.AddLogging(b =>
        {
            b.SetMinimumLevel(LogLevel.Information);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            b.AddProvider(new FileLoggerProvider(Path.Combine(appData, "SmartSideBAR", "logs")));
        });

        using var sp = services.BuildServiceProvider();

        // 与 v1.2.0 main.ts bootstrap 顺序同构: 配置迁移 → 日志 → 服务 → 窗口
        var config = sp.GetRequiredService<ConfigService>();
        config.Load();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap");
        log.LogInformation(
            "SmartSideBAR (Avalonia Wave B) 启动 — config v{Version}, 侧栏 {Side} 侧, 链接 {Links} 条, 提醒 {Reminders} 条",
            config.Current.Version, config.Current.Display.SidebarSide,
            config.Current.Links.Count, config.Current.Reminders.Count);

        // 第二次启动时唤醒已有实例：提示 + 展开侧栏
        var manager = sp.GetRequiredService<WindowManager>();
        SingleInstance.Listen(manager.NotifyAlreadyRunning);

        return AppBuilder.Configure(() => new App(sp))
            .UsePlatformDetect()
            .With(new Win32PlatformOptions())
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
