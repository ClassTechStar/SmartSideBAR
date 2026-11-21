// DI 图回归守卫 —— 本轮 P0 的直接教训:
// LessonStatsService 曾从 Program.cs 的注册清单里丢失 (后续迭代覆盖),
// 353 个测试全绿但应用启动即崩 —— 因为所有测试都手工构造服务, 绕过了容器。
// 双保险:
//   1) 源扫描: Program.cs 必须包含关键注册/接线 (改注册名必须同步改这里)
//   2) 容器级真解析: 复刻注册清单后 GetRequiredService<WindowManager>()
using SmartSideBAR.Avalonia.Tests;

namespace SmartSideBAR.Avalonia.Tests;

using Microsoft.Extensions.DependencyInjection;

public class DiGraphTests
{
    private static string ProgramSource =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "SmartSideBAR.Avalonia", "Program.cs"));

    private static void Require(string snippet, string why)
    {
        Assert.True(ProgramSource.Contains(snippet),
            $"Program.cs 缺少 DI 注册/接线: {snippet} —— {why}");
    }

    [Fact]
    public void 关键服务注册齐全()
    {
        Require("AddSingleton<LessonStatsService>",
            "缺失 = WindowManager 无法解析, 启动即崩 (2026-11 审计 P0 原样)");
        Require("AddSingleton<WindowManager>", "窗口管理器");
        Require("AddSingleton<ConfigService>", "配置服务");
        Require("AddSingleton<IEventBus, EventBus>", "事件总线");
        Require("AddSingleton<SchedulerService>", "提醒调度");
        Require("AddSingleton<AppBarService>", "AppBar");
        Require("AddSingleton<IRecorderApi>", "录屏");
        Require("AddSingleton<IDiagnosticsService, DiagnosticsService>", "诊断");
    }

    [Fact]
    public void 启动接线齐全()
    {
        Require("LessonModeHost.Init(config)", "智能课堂时钟 (丢失 = 状态机死键)");
        Require("UpdateAutoChecker.Init(config", "每日更新检查 (丢失 = 设置开关死键)");
        Require("SingleInstance.Listen", "单实例唤醒");
        Require("GetRequiredService<WindowManager>", "侧栏创建入口 (App.axaml.cs 经此解析)");
    }

    [Fact]
    public void DI图容器级解析_WindowManager()
    {
        // 容器级真解析: 不 mock 不手工构造 —— 这正是此前 353 测试全绿但启动即崩的盲区。
        // 复刻 Program.cs 的注册清单; Program.cs 与此处的漂移由上面的源扫描测试兜住。
        var appData = Path.Combine(Path.GetTempPath(), "ssb-di-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(appData);
        try
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(sp =>
                new SmartSideBAR.Avalonia.Logging.FileLoggerProvider(Path.Combine(appData, "logs")));
            services.AddSingleton(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
            services.AddSingleton<Microsoft.Extensions.Logging.ILogger<SmartSideBAR.Avalonia.WindowManager>>(sp =>
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SmartSideBAR.Avalonia.WindowManager>.Instance);
            services.AddSingleton(SmartSideBAR.Core.Configuration.ConfigPaths.CreateDefault());
            services.AddSingleton<SmartSideBAR.Core.Configuration.ConfigService>();
            services.AddSingleton<SmartSideBAR.Core.Messaging.IEventBus, SmartSideBAR.Core.Messaging.EventBus>();
            services.AddSingleton<SmartSideBAR.Core.Policy.PolicyService>();
            services.AddSingleton<SmartSideBAR.Core.Links.LinksService>();
            services.AddSingleton(System.TimeProvider.System);
            services.AddSingleton<SmartSideBAR.Core.Scheduling.SchedulerService>();
            services.AddSingleton<SmartSideBAR.Core.Telemetry.LessonStatsService>(sp =>
                new SmartSideBAR.Core.Telemetry.LessonStatsService(
                    sp.GetRequiredService<SmartSideBAR.Core.Messaging.IEventBus>(),
                    Path.Combine(appData, "stats")));
            services.AddSingleton<SmartSideBAR.Windows.Native.IWndProcHook, SmartSideBAR.Windows.Native.WndProcHook>();
            services.AddSingleton<SmartSideBAR.Windows.AppBar.IAppBarApi, SmartSideBAR.Windows.AppBar.AppBarApi>();
            services.AddSingleton<SmartSideBAR.Windows.AppBar.AppBarService>();
            services.AddSingleton<SmartSideBAR.Windows.Input.IImeService, SmartSideBAR.Windows.Input.ImeService>();
            services.AddSingleton<SmartSideBAR.Windows.Input.HotkeyService>();
            services.AddSingleton<SmartSideBAR.Windows.Devices.IUsbWatcher, SmartSideBAR.Windows.Devices.UsbDeviceWatcher>();
            services.AddSingleton<SmartSideBAR.Windows.Devices.IPrinterMonitor, SmartSideBAR.Windows.Devices.PrinterMonitor>();
            services.AddSingleton<SmartSideBAR.Windows.Shell.IShellService, SmartSideBAR.Windows.Shell.ShellService>();
            services.AddSingleton<SmartSideBAR.Windows.Audio.ISoundService, SmartSideBAR.Windows.Audio.SoundService>();
            services.AddSingleton<SmartSideBAR.Windows.Capture.ICaptureApi, SmartSideBAR.Windows.Capture.GdiCapture>();
            services.AddSingleton<SmartSideBAR.Windows.Shell.IDiagnosticsService, SmartSideBAR.Windows.Shell.DiagnosticsService>();
            services.AddSingleton<SmartSideBAR.Windows.Capture.ILongshotService>(sp =>
                Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<SmartSideBAR.Windows.Capture.LongshotService>(sp));
            services.AddSingleton<SmartSideBAR.Windows.Recording.IRecorderApi>(sp =>
                Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<SmartSideBAR.Windows.Recording.GraphicsCaptureRecorder>(sp));
            services.AddSingleton<SmartSideBAR.Avalonia.WindowManager>();

            using var sp = services.BuildServiceProvider();
            var wm = sp.GetRequiredService<SmartSideBAR.Avalonia.WindowManager>();
            Assert.NotNull(wm);
        }
        finally
        {
            try { Directory.Delete(appData, recursive: true); } catch { }
        }
    }
}
