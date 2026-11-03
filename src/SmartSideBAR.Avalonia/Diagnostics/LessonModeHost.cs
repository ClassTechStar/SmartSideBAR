// 智能课堂宿主 —— TimeStateMachine 的首个消费方 (此前状态机零调用方)。
// 统一时钟每 15s 判定课堂状态 (上课/预备/课间/放学), 状态变化时广播:
//   - WindowManager: 上课 → 自动切换「上课」工作区 (可配), 放学 → 提示
//   - ToastService:  上课中静默 info 级提醒 (USB 插拔等不打断讲课), 通知中心仍留档
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Scheduling;

namespace SmartSideBAR.Avalonia.Diagnostics;

public static class LessonModeHost
{
    private static readonly object _gate = new();
    private static ConfigService? _config;
    private static TimeStateMachine? _machine;
    private static string _machineKey = "";
    private static LessonStateSnapshot? _last;
    private static bool _registered;

    /// <summary>上课中抑制 info 级 Toast (可在 automation.quietOnClass 关闭)。</summary>
    public static bool QuietOnClass { get; private set; } = true;

    public static LessonStateSnapshot? Current { get; private set; }

    public static bool IsInClass => Current?.State == LessonState.OnClass;

    /// <summary>课堂状态变化 (仅变化时触发; Idle→Prepare→OnClass→课间→AfterSchool)。</summary>
    public static event Action<LessonStateSnapshot>? StateChanged;

    public static void Init(ConfigService config)
    {
        lock (_gate)
        {
            _config = config;
            RefreshQuiet();
            if (_registered) return;
            _registered = true;
            ClockHost.Register("lesson-mode", 15_000, Tick);
        }
        Tick();
    }

    private static void RefreshQuiet()
        => QuietOnClass = _config?.Current.Automation.QuietOnClass != false;

    private static void Tick()
    {
        LessonConfig lesson;
        bool mode;
        lock (_gate)
        {
            var cfg = _config?.Current;
            if (cfg is null) return;
            lesson = cfg.Lesson;
            mode = cfg.Automation.LessonMode;
            RefreshQuiet();
        }

        if (!mode)
        {
            Current = null;
            return;
        }

        // 课表键变化 (设置里改了课表) → 重建状态机
        var key = string.Join('|', lesson.Periods.Select(p => $"{p.StartMinute}-{p.EndMinute}"));
        lock (_gate)
        {
            if (_machine is null || key != _machineKey)
            {
                _machine = new TimeStateMachine(lesson.Periods, lesson.PrepareSeconds);
                _machineKey = key;
                _last = null;
            }
        }

        if (lesson.Periods.Count == 0)
        {
            Current = null;
            return;
        }

        var now = DateTime.Now;
        var nowSeconds = now.Hour * 3600 + now.Minute * 60 + now.Second;
        var snapshot = _machine!.Evaluate(nowSeconds);

        lock (_gate)
        {
            if (!TimeStateMachine.Changed(_last, snapshot))
            {
                Current = snapshot;
                return;
            }
            _last = snapshot;
            Current = snapshot;
        }
        StateChanged?.Invoke(snapshot);
    }
}
