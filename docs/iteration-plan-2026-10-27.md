# SmartSideBAR 渐进式迭代开发方案

> 参考项目：`ClassIsland`（C# / .NET / Avalonia 11，班级课表常驻工具）
> 参考项目：`Class-Widgets-2`（Python / PySide6 + QML，课表桌面挂件）
> 目标项目：`SmartSideBAR`（C# / .NET 10 + Avalonia 11，教学大屏侧边工具）
>
> 本文所有结论均基于实际源码阅读，证据以「文件路径 + 类名/成员名」标注。未能确认的实现细节单独列入「待确认项」，不作臆测。
> 约束：保持 SmartSideBAR 现有技术栈、目录结构与三层架构不变，采用模块级渐进改造。

---

## 步骤一：两个参考项目的架构梳理

### 1.1 ClassIsland

#### 1.1.1 目录结构与分层

| 项目 | 职责 | 证据 |
|---|---|---|
| `ClassIsland` | 主程序：App、窗口、服务、控件、主题 | `ClassIsland/App.axaml.cs`、`Services/`、`Controls/`、`Views/`、`Themes/` |
| `ClassIsland.Core` | 可复用控件库与核心抽象（含 `StylesBase.axaml`） | `ClassIsland.Core/Controls/`、`Abstractions/` |
| `ClassIsland.Shared` | 与 UI 无关的数据模型与接口（`IAppHost.cs`、`AttachableSettingsObject.cs`） | `ClassIsland.Shared/Models/`、`Interfaces/` |
| `ClassIsland.PluginSdk` | 插件 SDK 打包与文档生成（含 `.targets`、`generate-md5.ps1`） | `ClassIsland.PluginSdk/` |
| `ClassIsland.Packaging` | Windows 打包工程（wapproj） | `ClassIsland.Packaging/` |
| `ClassIsland.Desktop` / `.Launcher` | 桌面入口与启动器 | 同名目录 |
| `ClassIsland.Platforms.Abstractions` | 平台能力抽象 + Stubs（`PlatformServices.cs`） | `ClassIsland.Platforms.Abstractions/` |
| `ClassIsland.Shared.IPC` | 跨进程 IPC（`IpcClient.cs`、`IpcRoutedNotifyIds.cs`） | `ClassIsland.Shared.IPC/` |

依赖方向：主程序 → Core → Shared；平台能力通过 `Platforms.Abstractions` 反转，与 SmartSideBAR 的「Core 不依赖 UI、平台层单独成项目」是同一种分层思路。

#### 1.1.2 技术栈与构建

- Avalonia **11.3.17**（`AvaloniaShared.props` 的 `<AvaloniaVersion>`）。
- 调试期热重载：`HotAvalonia` 3.0.2，仅在 `Debug` 配置启用（`AvaloniaShared.props`）。
- 依赖注入：`Microsoft.Extensions.DependencyInjection`，`AddSingleton` / `AddHostedService` / `AddTransient` 集中在 `ClassIsland/App.Services.xaml.cs`（约 40+ 服务注册）。
- 服务形态：多数服务实现 `IHostedService` 或 `BackgroundService`（`ThemeService`、`TaskBarIconService`、`MemoryWatchDogService`），由宿主统一启停。

#### 1.1.3 插件 / 扩展机制

- 清单文件：`PluginService.PluginManifestFileName = "manifest.yml"`（YAML）。
- 目录：`Plugins/`（已安装）、`PluginsIndex/`、`PluginPackages/`（待安装包）、`Plugins/{Id}` 按插件 Id 分目录。
- 加载隔离：`PluginLoadContext : AssemblyLoadContext`，每个插件一个上下文，内部用 `AssemblyDependencyResolver` 解析托管/非托管依赖，并可按声明依赖委托到其它插件的上下文（`ClassIsland/PluginLoadContext.cs`）。
- 生命周期：`PluginService.ProcessPluginsInstall()` 处理待安装包 → `InitializePlugins(HostBuilderContext, IServiceCollection)` **让插件往宿主 DI 容器注册自己的服务**。
- 状态管理：`InstalledPlugins` / `UninstalledPlugins` / `PluginLoadedStatus` 三个列表，另有 `PluginMarketService` 做插件市场。
- 示例：`ClassIsland.ExamplePlugin`。

#### 1.1.4 配置与数据持久化

- 单文件配置：`Settings.json`（`SettingsService.LoadSettingsAsync` / `SaveSettings`），读写封装在 `ConfigureFileHelper.LoadConfig<T> / SaveConfig<T>`。
- 模型：`ClassIsland/Models/Settings.cs`，继承 `ObservableRecipient`（CommunityToolkit.Mvvm），字段带默认值。
- **版本化迁移**：`SettingsService.MigrateSettings(out bool requiresRestarting)` 按 `Settings.LastAppVersion` 与固定版本号比较，逐版本执行迁移，且能标记「需要重启」（`WillMigrateInitTutorial` 等开关）。
- 可管理策略：`ManagementService` 可从管理端下发默认设置（`LoadManagementSettingsAsync` 拉取远端 `Settings`）。
- 组件布局另存：`ComponentsService` 用 `ComponentLayouts/*.json` 存挂件布局（与设置本体分离）。

#### 1.1.5 UI 组件体系与渲染 / 动画

- 控件集中在 `ClassIsland/Controls/`（课表卡片 `LessonControl`、主线 `MainWindowLine`、列表详情 `ListDetailView`、加载遮罩 `LoadingMask`、OOBE 动画控件 `OobeIntroAnimationControl` 等），XAML 与代码隐藏成对。
- 主题：`Themes/{LightTheme.axaml, DarkTheme.axaml, Generic.xaml, ViewBaseStyle.xaml}` + `XamlThemes/{ClassicTheme, FluentTheme, TestInternalTheme}`；`ThemeService` 通过 `FluentAvaloniaTheme` 与 `AppBase.Current.RequestedThemeVariant`（0 默认 / 1 浅 / 2 深）切换。
- 动画：OOBE 有专门动画控件；**动画时长是否集中管理、是否读取系统「减弱动效」未能确认**（见待确认项）。

#### 1.1.6 消息事件与通知机制

- 通知主机：`NotificationHostService`，核心是 `PriorityQueue<NotificationRequest, NotificationPriority>`。
- 优先级结构：`NotificationPriority(提供者优先级序号, request.InitialQueueIndex, request.IsPriorityOverride)`；`InitialQueueIndex` 用 `Interlocked.Increment` 自增，保证同优先级内 FIFO。
- 提供者模型：模块/插件注册 `INotificationProvider`，顺序记录在 `Settings.NotificationProvidersPriority`，可在设置里调整。
- 状态机：`NotificationState.Queued` 等状态流转；另有 `NotificationWorkerService` 负责实际播放与语音播报（`SpeechService.EnqueueSpeechQueue` / `ClearSpeechQueue`）。

#### 1.1.7 日程与时间轴逻辑

- 状态枚举：`ClassIsland.Shared/Enums/TimeState.cs`（`None` / `OnClass` / 准备上课（预留） / …）。
- 核心服务：`LessonsService`，持有 `DispatcherTimer MainTimer`，对外暴露 **`PreMainTimerTicked` / `PostMainTimerTicked`** 两个钩子，让其它服务挂在主时钟前后执行。
- 事件：`OnClass` / `OnBreakingTime` / `OnAfterSchool` / `CurrentTimeStateChanged`，并提供 `DebugTriggerOnClass()` 等调试触发器（便于无依赖测试）。
- 时间量：`OnClassLeftTime`、`OnBreakingTimeLeftTime`、`CurrentTimeLayoutItem`、`NextClassTimeLayoutItem`、`NextBreakingTimeLayoutItem`。
- **多周轮换**：`Settings.MultiWeekRotationOffset` / `MultiWeekRotationMaxCycle` / `SingleWeekStartTime`；`LessonsService.cs` 依据参考日期计算 `totalElapsedWeeks` 并取 `cycleOffset`（源码注释指明与设置界面 `SetMultiWeekRotationOffset()` 对称）。
- 数据模型：`ClassIsland.Shared/Models/Profile/{ClassPlan, ClassPlanGroup, TimeLayout, TimeLayoutItem, TimeRule, Subject, OrderedSchedule, Profile}`。

#### 1.1.8 系统托盘与多屏适配

- 托盘：`TaskBarIconService`（`IHostedService`），`TrayIcon` + `NativeMenu` + `NativeMenuItemBase`。
- 多屏：`Settings.WindowDockingMonitorIndex`，取值前校验 `Screen.AllScreens` 边界（`WallpaperPickingService.cs` 中的用法）。
- 全屏与误触：`Settings.HideOnFullscreen`、`ExcludedFullscreenWindow`（默认含 `"explorer"`）、`IsMouseClickingEnabled`（点击穿透开关）。

#### 1.1.9 性能与资源优化

- `MemoryWatchDogService : BackgroundService`：`MemoryLimitBytes = 1_500_000_000`，超限则重启应用以缓解泄漏；内存取 `PrivateMemorySize64`（macOS 走 `WorkingSet64`，源码注释说明了平台差异）。
- 配置拉取走异步；诊断服务 `DiagnosticService`、`AppLogService`；另有 `HangService`（挂起检测，实现未细读）。

---

### 1.2 Class-Widgets-2

#### 1.2.1 目录结构与分层

```
src/
├── app.py                 # 入口
├── core/                   # 中枢与核心能力
│   ├── central.py          # AppCentral：服务中枢（组合所有 manager）
│   ├── config/             # 配置模型 + ConfigManager
│   ├── storage/            # 路径管理 + 数据迁移
│   ├── schedule/           # 日程（model / runtime / service / swapper / manager / editor）
│   ├── timer/              # 统一时钟 union_update.py
│   ├── notification/       # 通知（manager / provider / model / service）
│   ├── plugin/             # 插件（loader / manager / api / models / bridge / archive）
│   ├── widgets/            # 挂件（core / model）
│   ├── themes/             # 主题（loader / manager / interceptor / recovery / worker）
│   ├── automations/        # 自动化任务
│   ├── crash/              # 崩溃（handler / report / diagnosis）
│   ├── updater/ parser/ converter/ plaza/ utils/ windows/
├── qml/                    # QML 界面（MainInterface.qml + Components + widgets）
└── themes/                 # 内置主题（cw1 / material / vista / win10）
```

`AppCentral`（`src/core/central.py`）是显式服务中枢：持有 `PathManager`、`ConfigManager`、`ThemeManager`、`WidgetListModel`、`TrayIcon`、`CrashHandler`、`PlatformIntegration`，并用 `StartupState` 枚举表达启动阶段。

#### 1.2.2 技术栈与构建

- `PySide6~=6.10.3` + `RinUI`（QML 组件库）+ `loguru`（日志）+ `pydantic~=2.12`（配置模型）+ `pyyaml` + `icalendar` / `recurring-ical-events`（递归日程）+ `platformdirs`（路径）+ `packaging`（版本比较）。
- 打包：`hatchling` + `ClassWidgets-2.spec`（PyInstaller 规格）。
- 界面是 QML（`src/qml/MainInterface.qml`），逻辑在 Python，通过 `Property/Slot/Signal` 暴露给 QML。

#### 1.2.3 插件 / 扩展机制

- `src/core/plugin/loader.py`：`importlib` 动态导入；用 `packaging.specifiers.SpecifierSet` + `Version` 与 `__API_VERSION__`（`api.py`）校验宿主/插件 API 兼容。
- 优雅失败：`models.py` 定义 `PluginLoadFailure`（含 `plugin_id`、`name`、`display_name()`），加载失败可逐个上报而不是整体崩溃。
- 内置插件：`src/plugins/cw_widgets`；路径不可读时 `_safe_is_dir` / `_safe_exists` 捕获 `PermissionError`（WinError 5）并记 warning。

#### 1.2.4 配置与数据持久化

- 模型：`src/core/config/model.py` 的 `ConfigBaseModel(BaseModel)`（pydantic），带三个私有属性：`_on_change`（变更回调）、`_config_path`、`_locked_keys`（锁定键集合）；`_bind_context()` 递归把回调/路径/锁定键下发给嵌套模型。
- 管理器：`config/manager.py` 的 `ConfigManager(path=..., filename="configs.json")`。
- 迁移：`storage/migration.py` 把旧目录 `plugins/themes/configs` 搬到新工作区且**不覆盖已存在文件**；用 `QLockFile` 做单实例；失败抛 `DataMigrationError`（"do not start with incomplete data"）。

#### 1.2.5 UI 组件体系与渲染 / 动画

- QML 组件：`src/qml/ClassWidgets/Components/`（含 dialogs：`ClassSwapRestoreDialog`、`ThemeLoadErrorDialog`、`CheckSingleInstanceDialog`）。
- 窗口标志：`MainInterface.qml` 组合 `Qt.FramelessWindowHint | Qt.Window | Qt.WindowStaysOnTopHint`，并**按条件开关 `Qt.WindowTransparentForInput` 实现点击穿透**。
- 动画细节（是否在 QML 用 `PropertyAnimation`/`Behavior`、时长是否集中）未细读，见待确认项。

#### 1.2.6 消息事件与通知机制

- `NotificationProvider`：`一个 Provider = 一个通知来源（模块 / 插件）`，构造参数 `id / name / icon / use_system_notify`。
- 级别：`NotificationLevel(IntEnum)`（`notification/model.py`）。
- `NotificationManager`：持有 `providers: dict`、`_pending_notifications: list`、`_lock: Lock`（线程安全）、`_qml_ready` —— **界面未就绪时先暂存，就绪后再投递**；`notified = Signal(dict)`。

#### 1.2.7 日程与时间轴逻辑

- 模型：`schedule/model.py` 的 `ScheduleData / MetaInfo / Timeline / Entry / EntryType / Subject / Timetable / WeekType`。
- 运行时：`ScheduleRuntime`（QObject），信号 `updated`（文件更新）与 `currentsChanged(EntryType)`；持有 `current_week`、`current_week_of_cycle`、`time_offset`；刷新用 `QTimer(setSingleShot=True, interval=50)` 做**去抖**后统一 flush。
- **换课**：`schedule/swapper.py` 的 `ClassSwapManager` 通过 schedule 的 **`overrides`** 实现临时换课，持久化到 `configs.json`，跨天自动清理，并提供 `swapCommitted` 信号与恢复对话框（QML 侧 `ClassSwapRestoreDialog`）。
- 递归日程：依赖 `icalendar` + `recurring-ical-events`（`requirements.txt`）。

#### 1.2.8 系统托盘与多屏适配

- 托盘：`AppCentral` 持有 `TrayIcon`（`src/core/windows/` 或 core 内，`central.py:143` 引用 `TrayIcon`）。
- 穿透：`Qt.WindowTransparentForInput`（`MainInterface.qml:51-52`）。
- 多屏：未读到大范围多屏定位逻辑，见待确认项。

#### 1.2.9 性能与资源优化

- **统一时钟**：`timer/union_update.py` 的 `UnionUpdateTimer` —— 单个 `QTimer(50ms)` 轮询，仅当「秒发生变化」时发一次 `tick` 信号，全应用共用一个时钟源。
- 日程刷新去抖（50ms 单次定时器）；配置变更回调（`_on_change`）避免全量重绘。

#### 1.2.10 崩溃与可观测性

- `crash/handler.py` 的 `CrashHandler`：把未处理异常转成 `CrashReport`（`report.py` 的 `build_crash_report`），发 `crashReported` 信号，交给「问题报告窗口」，**不再静默退出**；并配套 `diagnosis.py`。
- 安全模式：`central.py` 的 `safe_mode_requested()` + `_safe_mode`，与 SmartSideBAR 的 `SSB_SAFE_MODE=1` 是同一思路。

---

## 步骤二：可迁移点筛选（对照 SmartSideBAR 现状）

> SmartSideBAR 现状基线：三层结构 `SmartSideBAR.Core`（Capture/Configuration/Design/Devices/Diagnostics/FloatBall/Ink/Input/Links/Messaging/Policy/Scheduling）、`SmartSideBAR.Windows`（AppBar/Audio/Capture/Devices/Input/Native/Recording/Shell）、`SmartSideBAR.Avalonia`（Ui/Views/ViewModels/WindowManager/CrashLog）；配置三层覆盖（默认 → ProgramData 策略 → APPDATA 用户，`ConfigService` + `ConfigJsonContext` 源生成）；`design-tokens.json` + `DesignTokens.cs`；`EventBus`、`ToastService`、`NotificationCenter`、`SchedulerService`（`TimeProvider` 可测）；测试 130 项（Core 94 / Windows 17 / Avalonia 19）。

### 2.1 高价值迁移项

| # | 借鉴点（来源） | 适用模块与场景 | 预期收益 | 与现有实现差异 | 改造成本 | 优先级 |
|---|---|---|---|---|---|---|
| T1 | **统一时钟中枢**：单定时器轮询 + 秒变化才广播（CW2 `timer/union_update.py`） | Avalonia 层：`App.axaml.cs`（3 个 DispatcherTimer）、`FloatBallWindow`（闲时变暗）、`InkCanvas`（80ms 激光笔）、`EdgeTriggerWindow`（150ms 悬停）、`CapturePanel`（计时器）、`WindowManager`（30s Topmost keeper / 1.2s 全屏防抖） | 定时器数量从 8+ 降到 1~2；教学一体机（4–8GB）唤醒更省电；时间基准统一后「闲时变暗 / 录屏计时 / 提醒到点」不会各算各的 | 现状是各窗口各自 `DispatcherTimer`，彼此独立且部分重复唤醒 | 中（1.5 人天） | **P0** |
| T2 | **通知优先级队列 + 来源/级别 + 就绪前暂存**（CI `NotificationHostService.PriorityQueue` + `NotificationProvidersPriority`；CW2 `NotificationManager` 的 `providers` / `_pending_notifications` / `_qml_ready` / `Lock`） | `Ui/ToastService.cs`、`Ui/NotificationCenter.cs` | 提醒/USB/录屏/错误可分级；上课模式下错误优先、USB 插拔降级；冷启动期通知不丢 | 现状 `ToastService` 只有「3 秒内去重 + 最多 3 条 + 排队」，无优先级、无来源归属、无就绪前暂存 | 中（2 人天） | **P0** |
| T3 | **全屏判定排除名单 + 穿透开关**（CI `Settings.ExcludedFullscreenWindow`（默认含 `explorer`）、`HideOnFullscreen`、`IsMouseClickingEnabled`） | `WindowManager.cs` 全屏收缩逻辑、`Ui/DockWindow.cs` | 消除「资源管理器最大化 / 无边框工具窗口」导致的侧栏误收缩；可配置化后现场可自救 | 现状：已做前台窗口几何复核与 1.2s 防抖，但**没有可配置的排除名单** | 低（0.5 人天） | **P0** |
| T4 | **崩溃可见化 + 安全模式引导**（CW2 `crash/handler.py` → `CrashReport` → 报告窗口；CI `Views/CrashWindow.axaml`） | `CrashLog.cs`、`Core/Diagnostics/CrashContext.cs`、`Program.cs` | 常驻程序静默退出时用户/教师能立刻看到原因并一键进安全模式 | 现状：三路异常捕获后只落 `crash-*.json`，`SSB_SAFE_MODE` 需手动设环境变量，用户侧不可见 | 中（1.5 人天） | **P0** |
| T5 | **时间状态机 + 预备提醒 + 调试触发器**（CI `TimeState` + `LessonsService` 的 `OnClass/OnBreakingTime/OnAfterSchool/CurrentTimeStateChanged` + `DebugTriggerOnClass()` + `classPrepareNotifySeconds`） | `Core/Scheduling/SchedulerService.cs`、`Ui/Panels/ReminderPanel.cs` | 提醒从「到点触发」升级为「上课/课间/放学」状态机，支持提前 N 秒预备提醒；调试触发器让状态机可单测（无需真实时间） | 现状 `SchedulerService` 基于 `TimeProvider` 做一次性/间隔/整点提醒，有 7 个单测但无状态机概念 | 中高（3 人天） | **P1** |
| T6 | **内存看门狗**（CI `MemoryWatchDogService`，阈值 1.5GB，超限重启） | 新增 `SmartSideBAR.Core/Diagnostics/MemoryWatchDog.cs` + Avalonia 侧托管 | 常驻数周后内存泄漏不再需要人工重启，契合「长期常驻教学机」场景 | 现状无任何内存守护 | 低（0.5 人天代码 + 0.5 验证） | **P1** |
| T7 | **主题外置 + 加载失败自动回退**（CI `Themes/*.axaml` + `XamlThemes/{Classic,Fluent}` + `ThemeService`；CW2 `themes/loader.py` PEP440 校验 + `recovery.py` 回退 `DEFAULT_THEME_ID` + `ThemeLoadErrorDialog`） | `Ui/Theme.cs`、`Views/SidebarWindow.axaml` | 新增主题不必改代码；主题异常时自动回经典白面而不是白屏 | 现状：`Theme.cs` 用代码算调色板，两套主题（经典/液态玻璃）写死在代码里 | 中（2 人天） | **P1** |
| T8 | **配置变更订阅 + 锁定键**（CW2 `ConfigBaseModel._on_change` / `_locked_keys` / `_bind_context()`） | `Core/Configuration/ConfigService.cs`、`SidekickConfig.cs` | 设置项可按键订阅变更，避免「改一项重建整个 rail」；策略层下发的键标记为锁定，UI 置灰 | 现状：`ConfigService.Set<T>` 有键白名单 + 深合并 + `ConfigChanged` 广播，但**订阅粒度是整个配置**，无「键级锁定」语义 | 中（2 人天） | **P1** |
| T9 | **多屏显式指定 + 边界校验**（CI `Settings.WindowDockingMonitorIndex` + `Screen.AllScreens` 越界回退） | `WindowManager.cs`、`Core/Configuration`（新增 `display.monitorIndex`） | 多屏教室可固定侧栏在某块屏，热插拔后按索引回落 | 现状：悬浮球/触发条/板中板跟随「侧栏所在屏」，但没有用户可指定的目标屏索引 | 低（1 人天） | **P1** |

### 2.2 中低价值 / 探索性迁移项

| # | 借鉴点（来源） | 适用模块与场景 | 预期收益 | 差异 | 成本 | 优先级 |
|---|---|---|---|---|---|---|
| T10 | **轻量扩展点 / 插件机制**（CI `PluginLoadContext` + `manifest.yml` + `InitializePlugins` 注册服务；CW2 `plugin/loader.py` 的 `SpecifierSet` API 版本校验 + `PluginLoadFailure` 优雅失败） | 新增 `SmartSideBAR.Core/Extensions/` | 第三方可加面板/挂件；学校定制不必改主线 | 现状完全无扩展机制，面板写死在 `Ui/Panels/` | 高（5+ 人天） | **P2** |
| T11 | **规则引擎（条件 → 动作）**（CI `RulesetService` + `ActionService` + `AutomationService`；CW2 `core/automations/`） | 新增 `Core/Rules/` | 「上课时自动静音 USB 提醒」这类自动化 | 现状只有 `EventBus` + `Dispatch()` 动作路由，无条件组合能力 | 高（5+ 人天） | **P2** |
| T12 | **重复日程规则 + 临时调整 overrides**（CW2 `icalendar`/`recurring-ical-events` + `schedule/swapper.py` 的 overrides / 跨天清理 / 恢复对话框） | `Core/Scheduling/SchedulerService.cs`、`ReminderPanel` | 支持「每周一第 3 节」「本周临时暂停」 | 现状提醒只支持一次性/间隔/整点 | 中高（3 人天） | **P2** |
| T13 | **多周轮换（单双周）**（CI `MultiWeekRotationOffset` / `MaxCycle` / `SingleWeekStartTime`） | 仅当引入课表才有意义 | 贴合真实校历 | 现状无课表概念 | 中（依附 T12） | **P2** |
| T14 | **DI 容器改造**（CI `App.Services.xaml.cs` 的 `AddSingleton/AddHostedService`） | `Program.cs`、`ViewModels/SidebarServices.cs` | 服务生命周期统一、便于测试替换 | 现状是显式手工装配（可读性尚可，但服务多了会臃肿） | 高（4 人天，且触及启动路径） | **P2（可选）** |
| T15 | **数据导入导出**（CI `Views/ExcelImportWindow` / `ExcelExportWindow`） | 设置窗「关于/诊断」分页 | 配置可备份迁移、批量部署 | 现状无导出 | 低（1 人天） | **P2** |
| T16 | **i18n**（CW2 Weblate 多语言；CI 未见明确 i18n 实现，待确认） | 全局 | 出海/少数民族地区 | 现状纯中文硬编码 | 高 | **不建议** |

### 2.3 明确不建议迁移

- **QML/Python 栈的一切**：SmartSideBAR 是 C#/Avalonia，仅借鉴其模式，不借鉴实现。
- **CI 的 `ManagementService` 远端策略下发**：SmartSideBAR 已有 `ProgramData` 本地策略层，引入远端管理面超出单机工具范畴。
- **ClassIsland 的完整插件市场**：体量与收益不匹配。

---

## 步骤三：迭代开发方案

### 3.1 P0 —— 稳定性与体验补齐（预计 5 人天）

#### P0-1 统一时钟中枢（对应 T1）

- **目标**：把散落的 `DispatcherTimer` 收敛为单一时间源，各消费方订阅 `tick` 后自行节流。
- **涉及模块 / 关键文件**：
  - 新增 `src/SmartSideBAR.Core/Scheduling/UnifiedClock.cs`（纯逻辑：`TimeProvider` 驱动的秒级 tick 判定，便于单测）
  - 新增 `src/SmartSideBAR.Avalonia/Ui/ClockHost.cs`（桥接到 `DispatcherTimer`）
  - 修改 `App.axaml.cs`（3 处定时器）、`Ui/FloatBallWindow.cs`（`_idleTimer`）、`Ui/Ink/InkCanvas.cs`（`_laserTimer`）、`Ui/EdgeTriggerWindow.cs`（dwell）、`Ui/Panels/CapturePanel.cs`（`_ticker`）、`WindowManager.cs`（Topmost keeper）
- **改动范围**：新增 2 文件；修改 6 文件（仅替换定时器创建/订阅部分，不动业务逻辑）
- **技术难点与风险**：
  - 激光笔消隐（80ms）与悬停展开（150ms）是亚秒级节拍，不能挂在秒级 tick 上 → 应对：保留这两个为独立定时器，只收敛「秒级及以上」的消费者（闲时变暗 4s、录屏计时 1s、Topmost 30s、全屏防抖 1.2s）。
  - `DispatcherTimer` 必须在 UI 线程 → 应对：时钟中枢在 UI 线程广播，消费方只做轻量计算。
- **验证**：新增单测覆盖 `UnifiedClock`（跨秒判定、跳跃、休眠恢复）；手动验证悬浮球闲时变暗、录屏计时、全屏收缩行为与改造前一致；headless 冒烟 7 链路全绿。
- **回滚**：各窗口保留旧定时器字段与开关（配置项 `debug.legacyTimers`），异常时可一键回退。

#### P0-2 通知优先级队列（对应 T2）

- **目标**：通知分来源、分级别、可排队、界面未就绪时暂存。
- **涉及模块 / 关键文件**：修改 `Ui/ToastService.cs`、`Ui/NotificationCenter.cs`；新增 `src/SmartSideBAR.Core/Messaging/NotificationQueue.cs`（纯逻辑优先级队列，可单测）；`Core/Configuration` 增加来源优先级配置
- **改动范围**：新增 1~2 文件；重构 `ToastService` 内部队列（对外 `Show/Toast` 接口保持不变，调用方零改动）
- **技术难点与风险**：`NotificationCenter` 内存上限 50 条与优先级淘汰策略冲突 → 应对：淘汰时优先丢弃低级别且已读的条目；并发投递用 `lock` 保护（对齐 CW2 `_lock`）。
- **验证**：新增单测（同优先级 FIFO、高级别插队、满 50 条淘汰顺序、未就绪暂存后补投）；冒烟链路「Toast → 通知中心」保持通过。
- **回滚**：`ToastService` 保留旧队列实现作为 `LegacyQueue`，由配置开关切换。

#### P0-3 全屏排除名单与穿透（对应 T3）

- **目标**：`ExcludedFullscreenWindow` 可配置；可选点击穿透。
- **涉及模块 / 关键文件**：`Core/Configuration/`（新增 `display.excludedFullscreenWindows` 默认含 `explorer`）、`WindowManager.cs` 全屏复核逻辑、`Core/Configuration/SidekickConfig.cs`
- **改动范围**：配置模型新增 1 个键；`WindowManager` 增加名单比对（约 20 行）
- **技术难点与风险**：类名匹配大小写与进程名获取 → 应对：统一小写比对；取不到前台窗口信息时按「不在名单」处理（保守不收缩）。
- **验证**：新增单测（名单命中不收缩、未命中收缩）；真机验证资源管理器最大化时侧栏不误收起。
- **回滚**：名单为空即等同现状。

#### P0-4 崩溃可见化与安全模式（对应 T4）

- **目标**：崩溃时弹出报告窗口，可一键以安全模式重启；报告内容沿用现有 `crash-*.json` 结构。
- **涉及模块 / 关键文件**：`CrashLog.cs`、新增 `src/SmartSideBAR.Avalonia/Ui/CrashReportWindow.cs`、`Program.cs`（`SSB_SAFE_MODE` 引导）
- **改动范围**：新增 1 窗口；`CrashLog` 增加「写文件 + 触发 UI 上报」双路
- **技术难点与风险**：崩溃时 UI 线程可能已不可用 → 应对：仅在能拿到 UI 线程时弹窗，否则只落盘并在**下次启动**时提示上次异常（更安全）。
- **验证**：人为抛异常验证落盘 + 启动提示；验证安全模式可跳过 AppBar/悬浮球启动。
- **回滚**：窗口为新增路径，失败不影响原有落盘。

### 3.2 P1 —— 能力增强（预计 9 人天）

| 任务 | 对应 | 目标 | 关键文件 | 改动范围 | 难点与应对 | 人天 | 验证 / 回滚 |
|---|---|---|---|---|---|---|---|
| P1-1 提醒时间状态机 | T5 | 上课/课间/放学状态机 + 提前 N 秒预备 + 调试触发器 | `Core/Scheduling/SchedulerService.cs`、新增 `Core/Scheduling/TimeState.cs`、`Ui/Panels/ReminderPanel.cs` | 新增枚举与状态计算；`SchedulerService` 增 `AdvanceTo(DateTime)` | 状态判定需覆盖跨天/节假日 → 用 `FakeTimeProvider` 单测；首次只做「上课/课间/放学」三态 | 3 | 新增 8~10 单测（含跨天）；冒烟「提醒面板渲染」保持；状态机为新增模块，可独立禁用 |
| P1-2 内存看门狗 | T6 | 超阈值（建议 800MB，低于 CI 的 1.5GB，因目标机 4–8GB）自动重启并记日志 | 新增 `Core/Diagnostics/MemoryWatchDog.cs`、`Program.cs` 托管 | 新增 1 文件 + 启动注册 | 误杀风险 → 连续 3 次采样超限才触发；重启前落 crash 记录 | 1 | 单测阈值判定；真机长跑观察；开关 `diagnostics.memoryWatchDog` 可关 |
| P1-3 主题外置与回退 | T7 | 主题移到 XAML 资源字典，加载失败回经典白面 | 新增 `src/SmartSideBAR.Avalonia/Themes/{Classic.axaml, LiquidGlass.axaml}`、修改 `Ui/Theme.cs` | 新增 2 资源字典；`Theme.cs` 改为「加载 + 回退」，保留现有调色板计算作为兜底 | 令牌与资源字典双源可能漂移 → 令牌仍是唯一来源，XAML 只引用令牌派生值 | 2 | 像素基线断言（胶囊/面板底色/对比度）保持通过；人为破坏 XAML 验证回退；旧代码路径保留 |
| P1-4 配置键级订阅与锁定 | T8 | 设置项按键订阅变更；策略层键标记为锁定并在 UI 置灰 | `Core/Configuration/ConfigService.cs`、新增 `Core/Configuration/ConfigSubscription.cs`、`Ui/SettingsWindow.cs` | 新增订阅模块；`ConfigService` 增 `Subscribe(key, handler)` 与 `IsLocked(key)` | 锁定键语义与现有三层合并冲突 → 锁定的键只允许策略层写，用户层写入直接拒绝并记录 | 2 | 单测（订阅触发次数、锁定键拒绝写）；冒烟「主题切换即时生效」保持 |
| P1-5 多屏显式指定 | T9 | `display.monitorIndex` 指定目标屏，越界回落主屏 | `Core/Configuration/SidekickConfig.cs`、`WindowManager.cs`、`Windows/Native/Win32Display.cs` | 配置新增 1 键；定位逻辑加索引解析 | 索引与「侧栏所在屏」语义冲突 → 显式索引优先，未配置（null）时维持现状行为 | 1 | 单测索引越界回退；双屏真机验证 |

### 3.3 P2 —— 扩展性探索（预计 8+ 人天，按需启动）

| 任务 | 对应 | 目标 | 关键文件（新增为主） | 难点与应对 | 人天 | 验证方式 |
|---|---|---|---|---|---|---|
| P2-1 轻量扩展点 | T10 | 面板/挂件可由外部包注册；带 API 版本校验与优雅失败 | `Core/Extensions/`（`ExtensionManifest.cs` / `ExtensionLoader.cs` / `ExtensionApi.cs`） | 程序集隔离在 .NET 中可用 `AssemblyLoadContext`（对齐 CI `PluginLoadContext`），但需评估 `PublishTrimmed` 下的兼容性 → 先做「同进程源码级注册点」，隔离加载留到二期 | 5 | 内置一个示例扩展走通注册/卸载；全量测试不回归 |
| P2-2 规则引擎 | T11 | 条件（时间/全屏/设备）→ 动作（静音/收起/提醒） | `Core/Rules/`（`Rule.cs` / `RulesetService.cs` / `ActionRegistry.cs`） | 条件求值需可测 → 全部纯逻辑 + 单测；UI 只做编辑壳 | 5 | 规则求值单测；设置页手动验证 |
| P2-3 重复日程与临时调整 | T12/T13 | 支持每周重复规则与「本周暂停」，跨天自动清理 | `Core/Scheduling/Recurrence.cs`、`Core/Scheduling/ScheduleOverride.cs` | 不引入 icalendar（跨栈），自行实现轻量 RRULE 子集；调课 overrides 借鉴 CW2 的「跨天清理 + 可恢复」语义 | 3 | 递归展开单测；跨天清理单测 |
| P2-4 DI 容器改造（可选） | T14 | 服务注册统一到 `IServiceCollection` | `Program.cs`、`ViewModels/SidebarServices.cs` | **风险最高**（触及启动路径）→ 分阶段：先新增 DI 容器但保留手工装配并存，验证无差异后再切换 | 4 | 启动耗时对比；全量测试与冒烟 |
| P2-5 配置导入导出 | T15 | 设置窗支持备份/恢复配置 | `Ui/SettingsWindow.cs` 新增导出按钮、`Core/Configuration/ConfigTransfer.cs` | 导出需脱敏（路径/账号类信息） | 1 | 导出后导入往返一致性单测 |

---

## 阶段里程碑

| 里程碑 | 包含任务 | 周期 | 结束后 SmartSideBAR 具备的能力 |
|---|---|---|---|
| **M1 体验与稳定基线** | P0-1 ~ P0-4 | 约 1~2 周（5 人天） | 定时器归一、省电；通知有优先级与来源归属；全屏误收缩可配排除名单；崩溃不再静默且能一键进安全模式。对外功能与界面零变化。 |
| **M2 教学场景增强** | P1-1 ~ P1-5 | 约 3~5 周（9 人天） | 提醒具备上课/课间状态机与预备提醒；常驻数周有内存自愈；主题可外置且失败自动回退；设置项按键订阅、策略键锁定置灰；多屏教室可指定侧栏屏幕。 |
| **M3 可扩展形态** | P2-1 ~ P2-5（按需） | 6 周以上（8+ 人天） | 具备扩展点（第三方面板）、规则自动化、重复日程与临时调课；可选完成 DI 容器统一与配置备份。 |

**贯穿约束**：每个里程碑结束都必须满足 —— `dotnet build` 0 错误、`dotnet test` 全绿（130 项，新增项只增不减）、`dotnet format --verify-no-changes` 通过、像素基线 3 态断言通过；界面视觉规格仍以 `design-tokens.json` 为唯一来源，改 UI 先改令牌。

---

## 待确认项（未在本次阅读中确认，需实施前复核）

| # | 项目 | 待确认内容 |
|---|---|---|
| Q1 | ClassIsland | 动画时长是否集中管理、是否读取系统「减弱动效」设置（`Themes/` 与 `Controls/` 未见明确令牌文件） |
| Q2 | ClassIsland | 插件包扩展名与 `manifest.yml` 完整字段集（仅确认了文件名与 `Id/Name/Version` 三个字段的使用） |
| Q3 | ClassIsland | `HangService`、`ProfileAnalyzeService` 的具体职责 |
| Q4 | ClassIsland | 是否有 i18n 机制（影响 T16 结论） |
| Q5 | Class-Widgets-2 | QML 侧动画实现方式与时长管理（仅确认窗口标志与穿透，未读动画） |
| Q6 | Class-Widgets-2 | 多屏定位逻辑是否存在（仅见 `setScreen` 相关检索无命中） |
| Q7 | Class-Widgets-2 | `TrayIcon` 具体实现位置（`central.py` 仅见引用） |
| Q8 | SmartSideBAR | `ToastService` 现有队列的完整行为（仅从注释确认 3s 去重 / 最多 3 条，需读全文确认后再动 P0-2） |
| Q9 | SmartSideBAR | `PublishTrimmed=true` 下 `AssemblyLoadContext` 动态加载是否可用（决定 P2-1 隔离方案可行性） |

---

## 实施状态（2026-10-27）

本轮按 P0 → P1 → P2 顺序落地，不确定项按约定跳过。

### 已完成

| 任务 | 落地内容 | 关键文件 |
|---|---|---|
| P0-1 统一时钟 | `UnifiedClock` + `ClockConsumers`（纯逻辑）；`ClockHost` 单一 DispatcherTimer；Topmost keeper 从独立 30s 定时器迁到统一时钟 | `Core/Scheduling/UnifiedClock.cs`、`Avalonia/Ui/ClockHost.cs`、`WindowManager.cs` |
| P0-2 通知优先级队列 | `NotificationLevel` / `NotificationRequest` / `NotificationPriorityComparer` / `NotificationQueue`；`ToastService` 换成优先级队列（来源+级别+强制置顶），对外 `Show(message, tone)` 不变 | `Core/Messaging/NotificationQueue.cs`、`Avalonia/Ui/ToastService.cs` |
| P0-3 全屏排除名单 | 配置 `display.excludedFullscreenWindows`（默认 `["explorer"]`）；`AppBarService.ForegroundProcessName()`；命中名单不自动收缩 | `Core/Configuration/SidekickConfig.cs`、`Windows/AppBar/AppBarService.cs`、`WindowManager.cs` |
| P0-4 崩溃可见化 | 崩溃写 `crash-pending.flag`；下次启动 `CrashReportWindow` 提示，可一键安全模式重启 | `CrashLog.cs`、`Avalonia/Ui/CrashReportWindow.cs`、`App.axaml.cs` |
| P1-1 时间状态机 | `LessonState` / `LessonPeriod` / `TimeStateMachine`（上课/预备/课间/放学 + 剩余秒数 + 变化检测） | `Core/Scheduling/TimeStateMachine.cs` |
| P1-2 内存看门狗 | `MemoryWatchDog`（连续 N 次超限才触发）+ `MemoryWatchDogHost`（60s 采样，重启前落日志） | `Core/Diagnostics/MemoryWatchDog.cs`、`Avalonia/Diagnostics/MemoryWatchDogHost.cs` |
| P1-3 主题回退 | `ThemeRegistry`（按 id 注册 + 失败回退）；`AppTheme.From` 生成异常时回经典白面 | `Avalonia/Ui/ThemeRegistry.cs`、`Theme.cs` |
| P1-4 配置键级订阅 | `ConfigSubscription`（精确键/前缀订阅、锁定键）；`ConfigService.Subscriptions` 默认锁定 `policy.` 段，`Set` 成功后按键广播 | `Core/Configuration/ConfigSubscription.cs`、`ConfigService.cs` |
| P1-5 多屏指定 | 配置 `display.monitorIndex`；`Win32Display.WorkAreaByIndex()`（`EnumDisplayMonitors`，越界回退主屏） | `SidekickConfig.cs`、`Windows/Native/Win32Display.cs`、`WindowManager.cs` |
| P2-1 扩展点（源码级） | `ExtensionManifest` / `ExtensionLoadFailure` / `ExtensionRegistry` / `ExtensionEntryPoint`，带 API 版本校验 | `Core/Extensions/ExtensionRegistry.cs` |
| P2-2 规则引擎 | `RuleCondition` / `RuleGroup` / `RuleContext` / `Ruleset.Evaluate`（全量/任一匹配） | `Core/Rules/Ruleset.cs` |
| P2-3 重复日程 | `RecurrenceRule`（日/周/双周）+ `ScheduleOverride`（暂停/改时刻）+ 跨天清理；配置新增 `overrides`，提醒新增 `recurrence` 编码串 | `Core/Scheduling/Recurrence.cs`、`SidekickConfig.cs` |
| P2-5 配置导入导出 | `ConfigTransfer`（导出脱敏、导入白名单过滤、叶子摊平）；设置窗「诊断 → 配置备份」两个入口 | `Core/Configuration/ConfigTransfer.cs`、`Avalonia/Ui/SettingsWindow.cs` |

### 跳过（按"不确定的先别碰"约定）

| 任务 | 原因 |
|---|---|
| P2-1 的 `AssemblyLoadContext` 动态隔离 | 待确认项 Q9：`PublishTrimmed=true` 下动态加载是否可用未验证；先只做源码级注册点 |
| P2-4 DI 容器改造 | 方案本身标注"可选"，且触及启动路径，风险最高；保留现有显式装配 |
| P1-1 接入提醒面板 UI | 需要课表时间段配置，属于新功能面；核心状态机与单测已就绪，接入留待产品确认 |

### 验证结果

- `dotnet build SmartSideBAR.slnx -c Release`：0 错误
- `dotnet test SmartSideBAR.slnx -c Release`：**180 项全过**（Core 144 / Windows 17 / Avalonia 19，较改造前 130 项增加 50 项）
- `dotnet format --verify-no-changes`：通过
- 新增单测覆盖：统一时钟、通知队列、时间状态机、内存看门狗、配置订阅、重复规则、规则引擎、扩展注册、配置导入导出
