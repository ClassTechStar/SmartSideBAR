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
using SmartSideBAR.Windows.Devices;
using SmartSideBAR.Windows.Input;
using SmartSideBAR.Windows.Native;
using SmartSideBAR.Windows.Shell;

namespace SmartSideBAR.Avalonia;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
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

        return AppBuilder.Configure(() => new App(sp))
            .UsePlatformDetect()
            .With(new Win32PlatformOptions())
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
