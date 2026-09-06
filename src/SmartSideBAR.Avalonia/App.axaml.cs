using Microsoft.Extensions.DependencyInjection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Core.Scheduling;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Audio;

namespace SmartSideBAR.Avalonia;

public partial class App : Application
{
    private readonly IServiceProvider? _sp;
    private TrayIcon? _tray;

    public App(IServiceProvider sp) => _sp = sp;

    // 设计器/XAML 预览用
    public App() { }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (_sp is not null && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var scheduler = _sp.GetRequiredService<SchedulerService>();
            var appBar = _sp.GetRequiredService<AppBarService>();
            var sounds = _sp.GetRequiredService<ISoundService>();
            var bus = _sp.GetRequiredService<IEventBus>();
            var windowManager = _sp.GetRequiredService<WindowManager>();

            desktop.MainWindow = windowManager.CreateSidebar();

            // 提醒: 铃声 (winmm) + 进程内通知 (Wave C Toast)
            bus.Subscribe<ReminderDue>(d =>
            {
                var cfg = _sp!.GetRequiredService<ConfigService>().Current.ReminderSound;
                _ = sounds.PlayReminderAsync(cfg);
            });

            scheduler.Start();
            CreateTray(desktop);

            // V8: 退出前注销 AppBar / 停服务, 系统 WorkArea 完全恢复
            desktop.ShutdownRequested += (_, _) =>
            {
                appBar.Detach();
                scheduler.Stop();
            };

            ConfigureSmokeExit(desktop);
            ConfigureAutoDock(windowManager);
            ConfigureAutoUndock(windowManager);
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>托盘 (P2-7 语义): 显示侧栏 / 打开设置 / 退出。图标运行时生成, 零资源文件。</summary>
    private void CreateTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var windowManager = _sp!.GetRequiredService<WindowManager>();
        var menu = new NativeMenu();
        var show = new NativeMenuItem("显示 / 隐藏侧栏");
        show.Click += (_, _) => windowManager.ToggleSidebar();
        var settings = new NativeMenuItem("打开设置");
        settings.Click += (_, _) => windowManager.OpenSettings();
        var exit = new NativeMenuItem("退出");
        exit.Click += (_, _) => desktop.Shutdown();
        menu.Add(show);
        menu.Add(settings);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(exit);

        _tray = new TrayIcon
        {
            Icon = MakeIcon(),
            ToolTipText = "SmartSideBAR",
            Menu = menu,
            IsVisible = true,
        };
    }

    private static WindowIcon MakeIcon()
    {
        var rtb = new RenderTargetBitmap(new PixelSize(32, 32), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        {
            ctx.FillRectangle(new SolidColorBrush(Color.Parse("#2B6EE0")), new Rect(2, 2, 28, 28), 6);
            ctx.FillRectangle(Brushes.White, new Rect(9, 14, 14, 4), 2);
            ctx.FillRectangle(Brushes.White, new Rect(9, 21, 9, 4), 2);
        }
        return new WindowIcon(rtb);
    }

    /// <summary>测试钩子: SSB_AUTO_UNDOCK_MS&gt;0 时 N 毫秒后自动展开侧栏 (undock 回归)。</summary>
    private void ConfigureAutoUndock(WindowManager wm)
    {
        if (!int.TryParse(Environment.GetEnvironmentVariable("SSB_AUTO_UNDOCK_MS"), out var ms) || ms <= 0) return;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            wm.Dispatch("dock"); // _docked=true → 取反为 undock
        };
        timer.Start();
    }

    /// <summary>测试钩子: SSB_AUTO_DOCK_MS&gt;0 时 N 毫秒后自动收起侧栏 (dock 形态验证)。</summary>
    private void ConfigureAutoDock(WindowManager wm)
    {
        if (!int.TryParse(Environment.GetEnvironmentVariable("SSB_AUTO_DOCK_MS"), out var ms) || ms <= 0) return;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            wm.Dispatch("dock");
        };
        timer.Start();
    }

    /// <summary>冒烟验证: SSB_SMOKE_EXIT_MS&gt;0 时 N 毫秒后自动退出 (自动化/CI 用)。</summary>
    private void ConfigureSmokeExit(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (!int.TryParse(Environment.GetEnvironmentVariable("SSB_SMOKE_EXIT_MS"), out var ms) || ms <= 0) return;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            desktop.Shutdown();
        };
        timer.Start();
    }
}
