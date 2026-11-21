// 统一时钟宿主 (P0-1)。
// 借鉴 Class-Widgets-2 的 UnionUpdateTimer: 全应用只保留一个 DispatcherTimer,
// 秒级及以上的周期任务登记到这里, 由它统一驱动。
// 亚秒级(激光笔 80ms)与可重置的闲时计时(悬浮球)保持各自定时器 —— 它们不是固定节拍,
// 强行合并会改变语义。
//
// 【启动顺序铁律】DispatcherTimer 必须在 Avalonia 平台初始化之后才能创建：
// 平台未就绪时 new DispatcherTimer() 会把 Dispatcher.UIThread 绑到空实现上，
// 之后 MainLoop 抛 PlatformNotSupportedException（应用启动即崩，测试却全绿 ——
// 测试宿主先建好了平台）。LessonModeHost.Init 在 Program.Main 里早于 AppBuilder.Start，
// 所以 Register 绝不能顺手建 timer；真正的启动点在 App.OnFrameworkInitializationCompleted。
using SmartSideBAR.Core.Scheduling;

namespace SmartSideBAR.Avalonia.Ui;

public static class ClockHost
{
    private static readonly object _gate = new();
    private static readonly ClockConsumers Consumers = new();
    private static readonly UnifiedClock Clock = new();
    private static global::Avalonia.Threading.DispatcherTimer? _timer;

    public static IReadOnlyDictionary<string, int>? ConsumerSnapshotForTests => null;

    /// <summary>
    /// 启动主时钟(幂等)。平台未就绪时只登记"待启动"意图，等 App 初始化完成后再真正建 timer。
    /// </summary>
    public static void EnsureStarted()
    {
        lock (_gate) EnsureStartedNoLock();
    }

    /// <summary>平台是否就绪（有 Application 且生命周期已挂上）。</summary>
    private static bool PlatformReady()
    {
        var app = global::Avalonia.Application.Current;
        return app is not null && app.ApplicationLifetime is not null;
    }

    private static void EnsureStartedNoLock()
    {
        if (_timer is not null) return;
        if (!PlatformReady())
        {
            // 不创建 DispatcherTimer —— 那会把 UI Dispatcher 绑到空实现
            WantsStart = true;
            return;
        }
        StartNoLock();
    }

    private static void StartNoLock()
    {
        if (_timer is not null) return;
        _timer = new global::Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000),
        };
        _timer.Tick += (_, _) =>
        {
            // 只在秒真正翻页时推进, 避免定时器抖动造成重复执行
            if (!Clock.ShouldTick(DateTimeOffset.Now)) return;
            Consumers.Pump(Environment.TickCount64);
        };
        WantsStart = false;
        // 暂停态下即便首次启动也先不跑（首次收起可能发生在 App 初始化之前）
        if (!IsPaused) _timer.Start();
    }

    /// <summary>
    /// 登记一个周期消费者。早于平台初始化调用是安全的 —— 只记录消费者，不建 timer。
    /// </summary>
    public static void Register(string name, int intervalMs, Action handler)
    {
        Consumers.Register(name, intervalMs, handler);
        EnsureStarted();
    }

    public static bool Unregister(string name) => Consumers.Unregister(name);

    /// <summary>
    /// 暂停统一时钟（借鉴 SidebarDiagnostics 的 SidebarModel.Pause/Resume）。
    /// 侧栏收起为触发条/演示态时周期任务没有意义，停表省 CPU；展开时再恢复。
    /// </summary>
    public static void Pause()
    {
        lock (_gate)
        {
            if (_timer is not null && _timer.IsEnabled) _timer.Stop();
            IsPaused = true;
        }
    }

    /// <summary>恢复统一时钟（幂等）。</summary>
    public static void Resume()
    {
        lock (_gate)
        {
            if (!IsPaused) return;
            IsPaused = false;
            Clock.Reset();
            if (_timer is not null && !_timer.IsEnabled) _timer.Start();
            else EnsureStartedNoLock();
        }
    }

    public static bool IsPaused { get; private set; }

    /// <summary>是否已登记"平台就绪后启动"的意图（测试可见）。</summary>
    public static bool WantsStart { get; private set; }

    /// <summary>仅供测试: 手动推进一拍。</summary>
    public static int PumpForTests(long nowMs) => Consumers.Pump(nowMs);
}
