# 命名空间与分层职责

> 本文档是分层边界的**单一事实来源**。新增代码必须归属到下表中的某一层；跨层引用先来这里核对。
> 借鉴自 VeriRandom 的 `docs/namespaces.md`（把"哪个命名空间能放什么"写死，避免慢慢烂掉）。

## SmartSideBAR.Core

**纯逻辑，不依赖 UI 与 Win32。**

| 子目录 | 职责 | 边界 |
|---|---|---|
| `Configuration/` | 配置模型（`SidekickConfig`）、读写（`ConfigService`）、键级订阅（`ConfigSubscription`）、导入导出（`ConfigTransfer`）、默认值迁移（`DefaultItemMigration`） | 只认 JSON，不认识窗口 |
| `Design/` | 设计令牌（`DesignTokens`）与 `design-tokens.json` 的强类型镜像 | 颜色/尺寸/圆角的**唯一**来源 |
| `Scheduling/` | 提醒调度（`SchedulerService`）、时间状态机（`TimeStateMachine`）、统一时钟（`UnifiedClock`）、重复规则（`Recurrence`） | 时间一律经 `TimeProvider`，便于单测 |
| `Messaging/` | 事件总线（`EventBus`）与通知队列（`NotificationQueue`） | 不弹窗，只排队 |
| `FloatBall/` | 悬浮球布局与交互纯逻辑（`FloatBallLayout`、`PointerDrag`、`DirectionSelector`、`FanArcSelector`、`FloatBallPositionPolicy`） | 输出几何/选中结果，不画控件 |
| `Panels/` | 面板注册表（`PanelRegistry`、`PanelDefinition`） | 元数据与排序，不含构造方式 |
| `Extensions/` | 扩展点注册（`ExtensionRegistry`） | 源码级注册，不做动态程序集加载 |
| `Rules/` | 规则求值（`Ruleset`） | 纯条件求值，不含动作执行 |
| `Diagnostics/` | 日志轮转、内存看门狗、崩溃上下文、版本比较 | 只观测，不改状态 |
| `Devices/` `Capture/` `Ink/` `Input/` `Links/` | 各业务域模型与纯逻辑 | 不直接调 Win32；平台细节交给 Windows 层 |

**硬性约束**：`SmartSideBAR.Core` 不得引用 `SmartSideBAR.Windows`、`SmartSideBAR.Avalonia`，也不得出现 `System.Windows.*`、`Avalonia.*`、P/Invoke。

## SmartSideBAR.Windows

**Windows 平台能力边界。** Win32 / COM / WMI 只能出现在这里。

| 子目录 | 职责 |
|---|---|
| `AppBar/` | 应用栏停靠（`SHAppBarMessage`）、工作区计算、全屏探测 |
| `Native/` | P/Invoke 封装（`Win32Display`、`Win32WindowStyle` 等窄工具类） |
| `Devices/` | USB / 打印机 / 亮度（WMI）设备能力 |
| `Audio/` | 系统音量（Core Audio COM） |
| `Capture/` `Recording/` | 截图（GDI+）与录屏（Windows.Graphics.Capture） |
| `Input/` `Shell/` | 全局热键、Shell 启动、诊断打包 |

**硬性约束**：Windows 层可被 Avalonia 层消费，但不得被 Core 反向依赖。跨平台能力先在 Core 定义窄接口，Windows 层实现。

## SmartSideBAR.Avalonia

**UI 与应用装配。** 窗口、面板、视图模型、生命周期都在这里。

| 子目录 | 职责 |
|---|---|
| `Ui/` | 窗口与面板控件（`FloatBallWindow`、`SidebarWindow`、`Panels/`） |
| `Ui/Panels/` | 各业务面板实现 + `PanelCatalog`（注册表 + 工厂） |
| `ViewModels/` | `SidebarViewModel`、`SidebarServices` 等 |
| `Views/` | XAML 窗口（`SidebarWindow.axaml`） |
| `Updater/` | 应用内更新（检查/下载/校验） |
| `Diagnostics/` | 内存看门狗宿主等运行期守护 |

**硬性约束**：
- 面板必须经 `PanelCatalog` 注册，禁止在窗口里写 `switch (PanelKind)`。
- 颜色/尺寸一律取 `DesignTokens`，禁止硬编码十六进制色值（`SvgIcons` 等白名单处除外）。
- 不在页面/面板里 `new` 可复用服务，统一走 `SidebarServices`。

## tests/

`SmartSideBAR.Core.Tests`（纯逻辑单测）/ `SmartSideBAR.Windows.Tests`（Win32 行为）/ `SmartSideBAR.Avalonia.Tests`（headless UI 冒烟）。

**约定**：需要文件系统的测试自建临时目录并在 `Dispose` 清理，不得污染真实配置目录。
