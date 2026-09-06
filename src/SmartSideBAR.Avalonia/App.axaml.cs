using Microsoft.Extensions.DependencyInjection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SmartSideBAR.Core.Scheduling;
using SmartSideBAR.Windows.AppBar;

namespace SmartSideBAR.Avalonia;

public partial class App : Application
{
    private readonly IServiceProvider? _sp;

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
            var windowManager = _sp.GetRequiredService<WindowManager>();

            desktop.MainWindow = windowManager.CreateSidebar();

            // V8: 退出前注销 AppBar / 停调度, 系统 WorkArea 完全恢复
            desktop.ShutdownRequested += (_, _) =>
            {
                appBar.Detach();
                scheduler.Stop();
            };

            ConfigureSmokeExit(desktop);
        }
        base.OnFrameworkInitializationCompleted();
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
