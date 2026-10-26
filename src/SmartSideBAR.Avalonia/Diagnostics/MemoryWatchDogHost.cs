// 内存看门狗宿主 (P1-2)。
// 借鉴 ClassIsland 的 MemoryWatchDogService: 常驻程序内存超限时主动重启, 缓解长期泄漏。
// 判定逻辑在 Core(可单测), 这里只负责采样与执行; 默认开启但可用配置关闭。
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Diagnostics;
using SmartSideBAR.Avalonia.Ui;

namespace SmartSideBAR.Avalonia.Diagnostics;

public sealed class MemoryWatchDogHost
{
    private const string ConsumerName = "memory-watchdog";
    private const int SampleIntervalMs = 60_000;

    private readonly ConfigService _config;
    private readonly ILogger<MemoryWatchDogHost>? _log;
    private MemoryWatchDog? _dog;
    private long _limitAtCreate;

    public MemoryWatchDogHost(ConfigService config, ILogger<MemoryWatchDogHost>? log = null)
    {
        _config = config;
        _log = log;
    }

    /// <summary>启动采样(幂等)。</summary>
    public void Start()
    {
        var cfg = _config.Current.Diagnostics;
        if (!cfg.MemoryWatchDogEnabled)
        {
            _log?.LogInformation("[MemoryWatchDog] 已在配置中关闭, 不启动");
            return;
        }

        var limit = MemoryWatchDog.FromMegabytes(Math.Max(64, cfg.MemoryLimitMb));
        _limitAtCreate = limit;
        _dog = new MemoryWatchDog(limit, Math.Max(1, cfg.MemoryBreachCount));

        ClockHost.Register(ConsumerName, SampleIntervalMs, Sample);
        _log?.LogInformation("[MemoryWatchDog] 已启动, 上限 {LimitMb}MB, 连续 {Count} 次超限重启",
            cfg.MemoryLimitMb, cfg.MemoryBreachCount);
    }

    public void Stop() => ClockHost.Unregister(ConsumerName);

    private void Sample()
    {
        var dog = _dog;
        if (dog is null) return;
        try
        {
            using var p = Process.GetCurrentProcess();
            p.Refresh();
            var bytes = p.PrivateMemorySize64;
            if (!dog.Push(new MemorySample(bytes, DateTimeOffset.Now))) return;

            _log?.LogWarning("[MemoryWatchDog] 内存连续超限 ({Bytes} > {Limit}), 准备重启", bytes, _limitAtCreate);
            CrashLog.Write("MemoryWatchDog", null, fatal: false);
            Restart();
        }
        catch (Exception ex)
        {
            _log?.LogDebug(ex, "[MemoryWatchDog] 采样失败");
        }
    }

    private void Restart()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "[MemoryWatchDog] 重启失败");
            return;
        }

        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            (global::Avalonia.Application.Current?.ApplicationLifetime as
                global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
            ?.Shutdown());
    }
}
