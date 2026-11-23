# MVVM 重构方案（草案，未实施）

> 目标：把"界面在代码里手搓、状态靠控件回读"的结构，换成"状态对象 → 绑定 → 视图"的单向数据流。
> 原则：**行为不变，只换骨架**；每一步都可单独验收、可单独回滚，不做大爆炸重写。

## 一、现状基线（2026-11-23）

| 指标 | 现状 |
|---|---|
| axaml 文件 | 5 个（App / SidebarWindow / 外观页 / 自动化页 / 字体样式） |
| C# 视图代码 | 152 个源文件中大部分 UI 仍为代码构造 |
| `SettingsWindow.cs` | 690 行，8 个内置页 + 1 个 Debug 页，控件字段 30+ |
| `SidebarWindow.axaml.cs` | 462 行；`ApplyTheme()` 直接给 30+ 控件属性赋笔刷，并**重建当前面板** |
| 面板 | `Ui/Panels` 8 个（24–359 行），`PanelKit` 231 行承载布局语法 |
| 现有 MVVM 雏形 | `SidebarViewModel`（281 行）+ 手写 `INotifyPropertyChanged` + `RelayCommand`（14 行），无 CommunityToolkit 依赖 |
| 状态来源 | 控件直接 `_config.Current` 读取；保存时逐控件回写（现已收敛为 `ApplyMany`） |

已有资产（不用重做）：`SidebarViewModel`、`RailItem`、`LinkRow`、`PanelRegistry`/`PanelCatalog`、`ApplyMany` 批量保存、headless 像素基线。

## 二、目标结构

```
ConfigService (唯一持久化源)
    │ ConfigChanged / 键级订阅
    ▼
State 层 (Core 或 Avalonia/ViewModels, 纯 C# 可单测)
    ├── SidebarState      rail 项 / 当前面板 / IME 角标 / dock 态
    ├── SettingsState     9 个页面的字段快照 + IsDirty + 校验错误
    └── ThemeState        Palette → ResourceDictionary (见 M1)
    │ INotifyPropertyChanged
    ▼
View (axaml, x:DataType 编译期绑定)  ← DynamicResource ← Theme 资源字典
```

约束：

1. **View 不读 ConfigService**，只读 State；写操作走命令。
2. 新增一个视图文件 = `.axaml` + 极薄 code-behind（只做控件生命周期，不做业务）。
3. 业务规则留在 Core（可单测），VM 只做编排。
4. 不引入新 MVVM 框架依赖：补一个 30 行的 `ObservableObject` 基类与 `AsyncRelayCommand`，与现有 `RelayCommand` 同源。

## 三、分阶段实施

### M0 · 基座与护栏（工作量 S，无依赖）

产出：

- `ViewModels/Infrastructure/ObservableObject.cs`：`SetProperty<T>` + `Raise`。
- `ViewModels/Infrastructure/AsyncRelayCommand.cs`：`Func<Task>` + `IsRunning`（防重入）。
- `ViewModels/Infrastructure/StateBase.cs`：统一订阅 `ConfigChanged` 的入口（含解绑）。
- 新增 `docs/plans/mvvm-baseline.md`：记录当前 3 组 headless 像素基线 + 7 条冒烟链路，作为重构期间的"行为不变"证据。

验收：现有测试全绿；不产生行为改动。

### M1 · 主题资源化（工作量 M，无依赖，先做收益最大）

改什么：

- 新增 `Ui/ThemeResources.cs`：`Palette → ResourceDictionary`（`RailHover/RailPressed/RailActive/BadgeBg/BadgeFg/IndicatorBar/PanelBg/PanelHeaderBorder/...`），一次性产出，不碰控件。
- `App.axaml` 增加 `DynamicResource` 引用的最小资源占位；`SidebarWindow.axaml` / `AppearancePage.axaml` / `AutomationPage.axaml` 的硬编码颜色改 `{DynamicResource ...}`。
- `SidebarWindow.ApplyTheme()` 缩为：`ThemeResources.Apply(Resources, p)` + 图标深浅切换 + sheen 参数。
- **面板重建（已在 M3 收尾删除）**：M1 时面板仍是 C# 手绘、构造时固化配色，保留重建避免"切主题后面板还是旧配色"；
  M3 面板全部改用 `DynamicResource` 后，改为 `SidebarPanel.ApplyPalette` 原地更新（见"面板级 MVVM 与主题隔离"）。
- `Palette` 保留，继续服务 SVG 染色、GlassSheen 等非 XAML 消费者。

验收：

- 切换主题时无面板重建、无闪白；像素基线与重构前一致（允许颜色值不变）。
- `ApplyTheme` 中控件属性赋值从 30+ 降到 ≤8（只保留无法资源化的项）。

回滚：资源字典与旧 `ApplyTheme` 并存一版，出问题切回旧方法。

### M2 · 设置窗全量 axaml 化（工作量 L，依赖 M0）

**落地状态（2026-11-23）**：已完成主体。`SettingsWindow.axaml` + `SettingsWindowViewModel` + 8 个分页 VM/axaml；
`SettingsWindow.axaml.cs` 127 行、零控件构造；分页 headless 全部可打开。
自动化页为过渡态：动态规则编辑器仍在页面代码后置，保存时由窗口并入同一批 `ApplyMany`，后续转 ItemsControl + VM。

拆分：

```
Ui/Settings/
├── SettingsWindow.axaml(.cs)        导航 + 页宿主 + 保存条（目标 <200 行）
├── SettingsViewModel.cs             字段快照 / IsDirty / SaveCommand / 校验
├── Pages/AppearancePage.axaml       已存在，补绑定
├── Pages/GeneralPage.axaml
├── Pages/AutomationPage.axaml       已存在
├── Pages/CapturePage.axaml
├── Pages/RecorderPage.axaml
├── Pages/DevicesPage.axaml
├── Pages/HotkeysPage.axaml
├── Pages/DiagnosticsPage.axaml
└── Pages/AboutPage.axaml
```

要点：

- 每个页面 `x:DataType="vm:XxxPageViewModel"`，编译期绑定；控件不再被字段持有。
- `SettingsState` 从 `config.Current` 一次性映射；`SaveCommand` 收集 VM 字段 → `ConfigService.ApplyMany`（保持现有"保存后关闭"语义，不改交互）。
- 诊断页的异步动作改为 `AsyncRelayCommand`，结果绑定到 `DiagnosticsText`。
- 版本/更新状态绑定到 VM，不再在页面里 `new UpdateService()`。

验收：

- `SettingsWindow` 无 `new Button/TextBox/ComboBox`；`.cs` ≤200 行。
- 保存行为与改造前逐键一致（现有 `ConfigApplyManyTests` + 新增"页面字段 → 配置键"映射测试）。
- headless：9 页逐页打开无异常；像素基线重录一次。

### M3 · 面板迁移（工作量 L，依赖 M1/M0）

**落地状态（2026-11-23）**：已完成。8 个面板全部改为 `XxxPanel.axaml` + 薄 code-behind +
`Ui/Panels/ViewModels/*`；面板 code-behind 24–100 行（平均 <40），`PanelCatalog` 与工厂签名不变。
共享样式收敛到 `PanelStyles.axaml`，`PanelTheme` 在面板构造时把 `Palette` 写入面板级资源字典，
XAML 一律 `{DynamicResource}` 消费（主题色不散落在 XAML 字面量里）。
`SidebarPanel` 新增 `RefreshCore()` 钩子：XAML 面板刷新只 `Vm.Reload()`，不替换可视树；
`PanelKit` 从控件工厂缩为 DPI 度量（`UiScale` / `MinHitDip`）。

顺序（按风险从低到高）：

1. `TaskMgrPanel`（24 行）、`PrinterPanel`（71）、`UsbPanel`（84）、`SettingsPanel`（56）——纯展示/单动作，先立模板。
2. `CapturePanel`（173）、`LinksPanel`（227）——多动作/列表。
3. `QuickSettingsPanel`（278）、`ReminderPanel`（359）——状态最多，最后做。

模板：

- 每个面板 = `XxxPanel.axaml` + `XxxPanel.axaml.cs`（继承 `SidebarPanel` 壳）+ `XxxPanelViewModel`。
- `PanelKit` 从"控件工厂"降级为 `Styles/PanelStyles.axaml`（卡片/磁贴/Option 行的样式类）；保留少量静态工厂给无法绑定的场景，逐步删。
- `PanelCatalog` 不变（工厂返回 `SidebarPanel`），迁移期间新旧面板共存。

验收：

- 8 个面板全部 axaml；`Ui/Panels/*.cs` 平均 <150 行。
- 现有 7 条 headless 冒烟链路（rail 点击/批注/工作区/提醒/Toast/链接/录屏态）全绿。

#### M3 收尾 · 面板级 MVVM 与主题隔离（2026-11-23，已完成）

> 产品口径上的"面板级 MVVM 与主题隔离"里程碑；计划中的 M4（WindowManager 收口）仍待办。

- `SidebarPanel.ApplyPalette(Palette)`：主题切换时就地重写面板级资源字典，并调用
  `PanelViewModel.ApplyPalette` 刷新 VM 调色板快照（SVG 图标明暗、状态色画刷随 `Reload` 重建）。
- `SidebarWindow.ApplyTheme` 删除 `ShowPanel(vm2.ActivePanel)` 重建，改为 `_panel?.ApplyPalette(p)`；
  切换主题不再替换面板可视树、不再重置滚动位置。
- 冒烟新增 `Theme_switch_updates_open_panel_in_place`：断言面板实例不变、`BodyFg` 已切到深色。
- `ThemeResources.ApplyContent` 成为面板与设置页共用的内容色出口（M5 主题重构第一步）；
  面板 XAML 的 `Panel*Fg/Bg` 私有键全部并回全局键（`BodyFg/MutedFg/HoverBg/AccentFg/...`）。
- 面板 VM 在 `Reload` 中补齐 `EmptyIcon` 通知；`CapturePanelViewModel.Actions` 改为随主题重建，
  避免图标停留在旧明暗模式。

#### M5 主题重构（已启动，2026-11-23）

- 已完成第一步：`ThemeResources` 新增 13 个内容色键（`TextFg/CardTextFg/HoverBg/AccentFg/AccentSoftBg/
  OnAccentFg/DividerFg/OkFg/WarnFg/ErrFg/RecFg/RecSoftBg/RecBorderFg`），`ApplyContent` 统一写入；
  `PanelTheme` 只保留几何令牌，不再各面板各写一份 Palette → 画刷映射。
- 面板 XAML 不再使用 `Panel*Fg/Bg` 私有键；后续清理：设置页/外观页剩余硬编码色、约定检查脚本。

### M4 · WindowManager 收口（工作量 M，依赖 M0）

- 拆 `WindowLifecycle`（创建/显示/复用/关闭）与 `WindowGeometry`（DPI 换算、工作区、AppBar 几何协商；把散落 6 处的 `* scale` 收进来）。
- `WindowRouter` 保留；自动化求值独立为 `AutomationHost`。
- `ToastService.Instance` 改为 `IToastService` 注入（`WindowManager`/`SettingsWindow`/VM 全走构造参数），保留静态壳给 XAML 设计器。
- 保留 `WindowManager` 作为门面（对外 API 不变），先拆内部再改调用点。

验收：

- `WindowManager.cs` ≤250 行，几何换算只有一处。
- 现有窗口相关测试（`AppBarServiceTests`、`SmokeTests`）全绿。

### M5 · 收口与约束（工作量 S）

**落地状态（2026-11-23）**：主题重构与约定检查已完成；README 贡献指引待补。

- 已新增 `scripts/check-ui-conventions.ps1`（CI 步骤）：`Ui/`、`Views/` 下出现
  `new Button/TextBlock/Border/...` 即失败（自绘层 `Ui/*Window.cs`、`Ink/`、`M3`、`Toast` 等走允许清单）；
  同时检查 XAML 的 `Foreground/Background/BorderBrush/Fill/Stroke` 是否写死颜色。
  当前结果：0 违规。
- 硬编码色清理：首帧兜底资源集中到 `App.axaml`（删除 `SidebarWindow` / `SettingsWindow` 的重复副本）；
  `QuickSettingsPanel` 的 `White` / `#D9FFFFFF` 改为 `OnAccentFg` / `OnAccentSoftFg`；
  设置窗保存按钮前景改用 `OnAccentFg`。C# 自绘层（批注笔色、玻璃高光渐变、令牌兜底）按设计保留。
- `PanelKit` 只保留 DPI 度量；旧页面构造函数已删除。
- README/ARCHITECTURE 更新"新增页面 = axaml + VM"的贡献指引。

## 四、测试策略

| 层 | 手段 | 覆盖 |
|---|---|---|
| State/VM | xUnit 纯单测（不启动 Avalonia） | 字段映射、脏标记、命令可执行条件、保存键序列 |
| View 结构 | headless + 视觉树断言 | 页面加载、绑定生效（文本/可见性）、无异常 |
| 视觉回归 | headless 像素基线（3 态） | 主题切换、面板打开、无闪白 |
| 行为不变 | 现有冒烟 7 链路 | 迁移前后逐条对比 |

每个阶段合并前必须：`dotnet build` 0 警告、全量测试绿、`dotnet format` 通过、像素基线无差异（或差异经确认）。

## 五、风险与对策

| 风险 | 对策 |
|---|---|
| 绑定错误运行时才暴露 | 全部 `x:DataType` + 编译期绑定；CI 开 `AvaloniaUseCompiledBindingsByDefault` |
| 迁移中两套 UI 并存导致行为漂移 | 每个页面/面板单独 PR，旧实现保留到验收通过再删 |
| 主题资源化影响自绘层（sheen/SVG 染色） | `Palette` 不删，资源字典只是新增出口 |
| `SettingsWindow` 拆分过大 | 按页拆，一次一页；先做无状态页（关于/截图）再做课表/自动化 |
| Toast 单例改 DI 影响面广 | 先引入接口 + 静态壳，调用点逐步替换，最后删静态 |
| 设计规格漂移 | 令牌仍是唯一来源；像素基线作为门禁 |

## 六、非目标（本轮明确不做）

- 不改 `InkCanvas` 的笔迹算法与渲染（全项目最扎实的资产）。
- 不把 `SidebarWindow` 的 AppBar/Win32 窗口形态改成纯 XAML 窗口（平台层保留）。
- 不在 MVVM 重构中顺手改"保存 vs 即时生效"的交互语义——那需要单独的产品决策与 ADR。
- 不引入 ReactiveUI / Prism / CommunityToolkit 等框架。

## 七、依赖顺序

```
M0 ──► M1 ──► M3
 └────► M2
 └────► M4 ──► M5
```

M1 与 M2 可并行（不同文件域）；M3 依赖 M1 的 `DynamicResource` 才能做到"切主题不重建"。
