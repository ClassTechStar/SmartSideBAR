// 附录 B.1: 与 v1.2.0 src/shared/types.ts 逐字段同构
// JSON 命名 camelCase, 与 Electron 版 config.json 互认 (ADR-M8)。
using System.Text.Json.Serialization;
using SmartSideBAR.Core.Scheduling;

namespace SmartSideBAR.Core.Configuration;

public sealed record SidekickConfig
{
    public const int CurrentVersion = 2;

    public int Version { get; init; } = CurrentVersion;
    public ImeConfig Ime { get; init; } = new();
    public CaptureConfig Capture { get; init; } = new();
    public RecorderConfig Recorder { get; init; } = new();
    public UsbConfig Usb { get; init; } = new();
    public PrinterConfig Printer { get; init; } = new();
    public DisplayConfig Display { get; init; } = new();
    public AppearanceConfig Appearance { get; init; } = new();
    public FloatBallConfig FloatBall { get; init; } = new();
    public WorkspaceConfig Workspace { get; init; } = new();
    public RailConfig Rail { get; init; } = new();
    public LessonConfig Lesson { get; init; } = new();
    public AutomationConfig Automation { get; init; } = new();
    public UpdatesConfig Updates { get; init; } = new();
    public List<LinkItem> Links { get; init; } = [];
    public List<Reminder> Reminders { get; init; } = [];
    public ReminderSoundConfig ReminderSound { get; init; } = new();
    public OobeState Oobe { get; init; } = new();
    public PolicyConfig Policy { get; init; } = new();
    public DiagnosticsConfig Diagnostics { get; init; } = new();
    /// <summary>
    /// 临时日程调整（借鉴 CW2 的 schedule overrides）: 暂停某天或改到某时刻, 跨天自动清理。
    /// </summary>
    public List<ScheduleOverride> Overrides { get; init; } = [];
}

public sealed record ImeConfig
{
    public string Slot1 { get; init; } = "Microsoft Pinyin";
    public string Slot2 { get; init; } = "US";
    public bool FallbackSwap { get; init; } = true;
    /// <summary>输入法切换热键 (原来硬编码 Ctrl+Shift+I 且设置界面不落盘)。</summary>
    public string Hotkey { get; init; } = "Ctrl+Shift+I";
}

public sealed record CaptureConfig
{
    public string Hotkey { get; init; } = "Ctrl+Shift+A";
    public string AnnotateHotkey { get; init; } = "Ctrl+Shift+D";
    public string LongshotHotkey { get; init; } = "Ctrl+Shift+L";
    public string Format { get; init; } = "PNG"; // png|jpg
    public string Dir { get; init; } = "{Pictures}/Sidekick";
    /// <summary>墨迹识别转形: 收笔后把手绘闭合图形/直线替换为干净形状（工具条可临时关闭）。</summary>
    public bool InkToShape { get; init; } = true;
}

public sealed record RecorderConfig
{
    /// <summary>
    /// 录屏总开关。部分机型（如骁龙 ARM 上跑 x64 仿真）的 MediaFoundation 管道会
    /// 在转码准备阶段把进程直接带崩（无托管异常），这类机器上关掉即可自保。
    /// </summary>
    public bool Enabled { get; init; } = true;
    public int Fps { get; init; } = 15;
    public string Bitrate { get; init; } = "2M";
    public bool Mic { get; init; }
    public string Dir { get; init; } = "{Videos}/Sidekick";
}

public sealed record UsbConfig
{
    public bool Enabled { get; init; } = true;
    public List<string> IgnoreTypes { get; init; } = ["phone", "carplay"];
}

public sealed record PrinterConfig
{
    public int PollIntervalSec { get; init; } = 10;
}

public sealed record DisplayConfig
{
    public string SidebarMonitor { get; init; } = "primary";
    public SidebarSide SidebarSide { get; init; } = SidebarSide.Right;
    public bool FitWindowsToWorkArea { get; init; } = true;
    /// <summary>检测到真正全屏的应用时自动把侧栏收成底角方块（教室演示场景默认开启）。</summary>
    public bool AutoCollapseOnFullscreen { get; init; } = true;
    /// <summary>
    /// 触发模式（希沃方案建议 #1）:
    /// always = 常驻胶囊（现状, 触屏教学推荐）;
    /// edge = 极细触发条, 点击展开, 防误触（写代码/大屏推荐）;
    /// edge-hover = 触发条悬停滑出（程序员习惯）。
    /// </summary>
    public string RailTrigger { get; init; } = "always";
    /// <summary>
    /// 全屏判定排除名单（进程名小写, 借鉴 ClassIsland 的 ExcludedFullscreenWindow）。
    /// 命中名单的窗口即使铺满屏幕也不触发自动收起, 避免资源管理器/无边框工具窗口导致误收缩。
    /// </summary>
    public List<string> ExcludedFullscreenWindows { get; init; } = ["explorer"];
    /// <summary>触发条宽度 (DIP)。8-40: 太窄点不中, 太宽失去"防误触"意义。</summary>
    public double TriggerWidthDip { get; init; } = 14;
    /// <summary>触发条悬停展开延时 (ms)。0=即触即开; Seelen delay_to_show 同值 150。</summary>
    public int HoverExpandDelayMs { get; init; } = 150;
    /// <summary>鼠标移出自动收起延时 (秒)。SidebarWindow 5s 收起与 edge 模式 4s 回收同源此值。</summary>
    public double CollapseDelaySec { get; init; } = 5;
    /// <summary>本进程窗口失活后收起面板 (需 300ms 判定, 零钩子实现)。默认关。</summary>
    public bool CollapseOnFocusLoss { get; init; }
    /// <summary>置顶三档: never=正常 Z 序 (AppBar 已避让) / normal=Topmost 回退+keeper / always=强制置顶。</summary>
    public string TopmostPolicy { get; init; } = "normal";
    /// <summary>图标轨宽度 (DIP)。56-120。</summary>
    public double RailWidthDip { get; init; } = 72;
    /// <summary>面板宽度 (DIP)。280-520。</summary>
    public double PanelWidthDip { get; init; } = 380;
    /// <summary>rail 图标尺寸 (DIP)。20-40, 默认 28。</summary>
    public double IconDip { get; init; } = 28;
    /// <summary>呼出/隐藏侧栏热键 (空=不注册)。路由到 sidebar-toggle。</summary>
    public string SidebarHotkey { get; init; } = "";
    /// <summary>
    /// 目标显示器索引（借鉴 ClassIsland 的 WindowDockingMonitorIndex）。
    /// null = 沿用现状（跟随侧栏所在屏）; 指定后按索引定位, 越界自动回落主屏。
    /// </summary>
    public int? MonitorIndex { get; init; }
}

/// <summary>Display 数值键钳制 —— 新参数化键的合法域在此单点维护。</summary>
public static class DisplayNormalize
{
    public static DisplayConfig Clamp(DisplayConfig input)
    {
        var policy = new[] { "never", "normal", "always" };
        return input with
        {
            TriggerWidthDip = ClampNum(input.TriggerWidthDip, 8, 40, 14),
            HoverExpandDelayMs = (int)ClampNum(input.HoverExpandDelayMs, 0, 800, 150),
            CollapseDelaySec = ClampNum(input.CollapseDelaySec, 0.5, 30, 5),
            TopmostPolicy = policy.Contains(input.TopmostPolicy, StringComparer.OrdinalIgnoreCase)
                ? input.TopmostPolicy.ToLowerInvariant() : "normal",
            RailWidthDip = ClampNum(input.RailWidthDip, 56, 120, 72),
            PanelWidthDip = ClampNum(input.PanelWidthDip, 280, 520, 380),
            IconDip = ClampNum(input.IconDip, 20, 40, 28),
        };
    }

    private static double ClampNum(double v, double min, double max, double fallback) =>
        double.IsFinite(v) ? Math.Min(max, Math.Max(min, v)) : fallback;
}

public sealed record DiagnosticsConfig
{
    /// <summary>内存看门狗开关（借鉴 ClassIsland 的 MemoryWatchDogService）。</summary>
    public bool MemoryWatchDogEnabled { get; init; } = true;
    /// <summary>内存上限 (MB), 连续多次采样超限才重启。目标机 4-8GB, 阈值取 800MB。</summary>
    public int MemoryLimitMb { get; init; } = 800;
    /// <summary>连续超限次数阈值, 避免一次性抖动误杀。</summary>
    public int MemoryBreachCount { get; init; } = 3;
}

/// <summary>KoBar 工作区（建议: 教师场景一键切换模块组合）。空 Modules = 不限制（跟随 policy）。</summary>
public sealed record WorkspaceConfig
{
    public string Current { get; init; } = "默认";
    public List<WorkspacePreset> Presets { get; init; } =
    [
        new WorkspacePreset { Name = "默认", Modules = [] },
        new WorkspacePreset
        {
            Name = "上课",
            Modules = ["ime", "capture", "annotate", "longshot", "record", "usb", "bell"],
        },
        new WorkspacePreset { Name = "批注", Modules = ["ime", "annotate"] },
    ];
}

public sealed record WorkspacePreset
{
    public string Name { get; init; } = "";
    public List<string> Modules { get; init; } = [];
}

/// <summary>rail 图标顺序（长按重排序的持久化目标）。缺省项按内置顺序补齐。</summary>
public sealed record RailConfig
{
    public List<string> Order { get; init; } = [];
}

/// <summary>课表（分钟自当日 0 点起）—— TimeStateMachine 的配置来源。</summary>
public sealed record LessonConfig
{
    public List<Scheduling.LessonPeriod> Periods { get; init; } = [];
    public int PrepareSeconds { get; init; } = 60;
}

/// <summary>智能课堂自动化 —— TimeStateMachine 的消费策略（竞赛核心叙事之一）。</summary>
/// <summary>
/// 应用内更新 (更新源 GitHub Release, 多通道兜底)。
/// 原则: 自动部分止步于「检查/下载」, 安装必须由用户点击 —— 绝不静默替换运行中的程序。
/// </summary>
public sealed record UpdatesConfig
{
    /// <summary>总开关; 关闭后自动检查完全不联网 (设置里的手动检查仍可用)。</summary>
    public bool Enabled { get; init; } = true;
    /// <summary>每天自动检查一次, 发现新版弹提醒 (上课中照常静默进通知中心)。</summary>
    public bool AutoCheck { get; init; } = true;
    /// <summary>发现新版后后台下载缓存, 安装仍需用户点击 (默认关)。</summary>
    public bool AutoDownload { get; init; }
}

public sealed record AutomationConfig
{
    /// <summary>状态机驱动自动切换: 上课 → 自动切到 ClassWorkspace 工作区。</summary>
    public bool LessonMode { get; init; } = true;
    /// <summary>进入上课状态时自动切换到的工作区（对应 workspace.presets 里的名字）。</summary>
    public string ClassWorkspace { get; init; } = "上课";
    /// <summary>上课中抑制 info 级 Toast（USB 插拔等不打断讲课, 通知中心仍留档）。</summary>
    public bool QuietOnClass { get; init; } = true;
    /// <summary>
    /// 条件 → 动作自动化规则（P2-6: 设置页「自动化」编辑, Ruleset 消费）。
    /// 示例: 上课时自动切「上课」工作区 / 全屏时收起侧栏。
    /// </summary>
    public List<AutomationRuleConfig> Rules { get; init; } = [];
}

/// <summary>自动化规则的持久化形态（对应 Rules.RuleGroup）。</summary>
public sealed record AutomationRuleConfig
{
    public bool Enabled { get; init; } = true;
    /// <summary>动作 id（Dispatch 词表, 如 "workspace:上课" / "ime" / "dock"）。</summary>
    public string ActionId { get; init; } = "";
    /// <summary>true = 全部条件满足才触发; false = 任一满足即触发。</summary>
    public bool AllMustMatch { get; init; } = true;
    public List<AutomationConditionConfig> Conditions { get; init; } = [];
}

/// <summary>自动化规则条件的持久化形态（对应 Rules.RuleCondition）。</summary>
public sealed record AutomationConditionConfig
{
    public Rules.RuleConditionKind Kind { get; init; } = Rules.RuleConditionKind.LessonState;
    public Rules.RuleMatchMode Mode { get; init; } = Rules.RuleMatchMode.Equals;
    public string Value { get; init; } = "";
}

[JsonConverter(typeof(JsonStringEnumConverter<SidebarSide>))]
public enum SidebarSide { Left, Right }

public sealed record PolicyConfig
{
    public List<string> DisabledModules { get; init; } = [];
}

// ---- 与 shared/types.ts 同构的值类型 ----

public sealed record LinkItem
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    public string? Icon { get; init; }
    public bool Enabled { get; init; } = true;
    /// <summary>标签页分类（建议 #3: 应用/网站/工具/AI，自定义值也会自动成页）。</summary>
    public string Category { get; init; } = "网站";
}

[JsonConverter(typeof(JsonStringEnumConverter<ReminderKind>))]
public enum ReminderKind { Once, Interval, Hourly }

public sealed record Reminder
{
    public string Id { get; init; } = "";
    public ReminderKind Kind { get; init; } = ReminderKind.Once;
    /// <summary>epoch 毫秒, 与 v1.2.0 Reminder.at 语义一致</summary>
    public long At { get; init; }
    public string? Note { get; init; }
    public int? RepeatMin { get; init; }
    public long? SnoozedUntil { get; init; }
    /// <summary>
    /// 重复规则编码串: "mode|interval|weekday|minute"（借鉴 CW2 的重复日程）。
    /// mode: none|daily|weekly|biweekly; weekday 0=周日; minute 为当日起的分钟数。
    /// 空或解析失败 = 不重复（等同现状的一次性/间隔/整点语义）。
    /// </summary>
    public string? Recurrence { get; init; }
}

public sealed record ReminderSoundConfig
{
    public string Preset { get; init; } = "default"; // default|bell|chime|alarm
    public string? Mp3Path { get; init; }
    public double Volume { get; init; } = 0.8;       // 0~1
    public int Repeat { get; init; } = 3;            // 1~5
    public int RepeatInterval { get; init; } = 800;  // ms
}

public sealed record OobePrefs
{
    public bool Ime { get; init; } = true;
    public bool Usb { get; init; } = true;
    public bool Shot { get; init; } = true;
    public bool Recorder { get; init; }
    public bool Printer { get; init; } = true;
}

public sealed record OobeEnvScreen
{
    public int W { get; init; } = 1920;
    public int H { get; init; } = 1080;
    public double Scale { get; init; } = 1;
    public bool Touch { get; init; } = true;
}

public sealed record OobeEnv
{
    public OobeEnvScreen Screen { get; init; } = new();
    public int PrinterCount { get; init; }
    public bool ImeOk { get; init; } = true;
    public string Os { get; init; } = "win32";
}

public sealed record OobeState
{
    public bool Completed { get; init; }
    public string? CompletedAt { get; init; } // ISO 字符串, 与 v1.2.0 一致
    public bool Skipped { get; init; }
    public string? Role { get; init; }        // teacher|admin|null
    public int LastStepIndex { get; init; }
    public OobePrefs Prefs { get; init; } = new();
    public OobeEnv Env { get; init; } = new();
}
