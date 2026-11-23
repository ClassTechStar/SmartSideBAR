# 更新日志

本项目版本号写在 `Directory.Build.props` 的 `<Version>`。
格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/)，版本号遵循语义化版本。

## [未发布]

### M5 硬编码色清理与 UI 约定检查（2026-11-23）
- 新增 `scripts/check-ui-conventions.ps1`：页面/面板 code-behind 禁止代码构造控件
  （自绘窗口/批注/Toast/M3 工厂走允许清单），XAML 禁止 `Foreground/Background/BorderBrush/Fill/Stroke`
  写死颜色；当前扫描 0 违规，可挂 CI
- 首帧兜底资源集中到 `App.axaml`，删除 `SidebarWindow` / `SettingsWindow` 的重复资源字典
- `QuickSettingsPanel` 的 `White`/`#D9FFFFFF` 改为 `OnAccentFg`/`OnAccentSoftFg`；
  设置窗保存按钮同步改用 `OnAccentFg`（高对比主题下前景自动反色）
- `ThemeResources` 新增 `OnAccentSoftFg`（强调色上的次级文字），主题契约测试扩到 28 个键

### 面板级 MVVM 与主题隔离 / 主题重构启动（2026-11-23，M4 / M5）
- 主题切换不再重建侧栏面板：新增 `SidebarPanel.ApplyPalette` 与 `PanelViewModel.ApplyPalette`，
  就地重写面板级资源字典并刷新 VM 调色板快照（SVG 图标明暗、状态色画刷随 `Reload` 重建）
- `SidebarWindow.ApplyTheme` 删除 `ShowPanel(vm.ActivePanel)` 重建；切换主题保留面板实例与滚动位置，
  冒烟新增「面板实例不变 + `BodyFg` 切深色」断言
- M5 主题重构启动：`ThemeResources.ApplyContent` 统一 13 个内容色键（文字/卡片/强调/状态/录制），
  面板 XAML 的私有 `Panel*Fg/Bg` 键全部并入全局键；`PanelTheme` 只保留圆角与触控几何令牌
- 主题资源契约测试扩展到 27 个键（含录制态半透明底/描边）

### 侧栏面板 MVVM 化与动作宿主（2026-11-23，M3 / E2）
- M3：8 个侧栏面板全部改为 `XxxPanel.axaml` + 薄 code-behind（24–100 行）+
  `Ui/Panels/ViewModels/*` 面板 VM；`PanelCatalog` 工厂签名与 `RefreshNow()` 测试入口不变
- 新增共享样式 `PanelStyles.axaml` 与 `PanelTheme`：面板构造时把 `Palette` 写入面板级资源字典，
  XAML 统一走 `{DynamicResource}`；`PanelKit` 从控件工厂缩为 DPI 度量（`UiScale` / `MinHitDip`）
- `SidebarPanel` 新增 `RefreshCore()` 钩子：XAML 面板刷新只更新 VM，不再重建可视树；
  录制计时、通知中心订阅、MP3 选择等生命周期逻辑保留在 code-behind
- 面板行为与文案保持不变（含录屏计时/暂停、链接页签与内联新增、提醒铃声折叠区、快捷开关）
- E2：宿主扫描 `Assets/actions/*.json` 并接入 `ActionExecutor`（`IActionSink` / `WindowActionSink`），
  内置动作包随程序集发布；新增 `ActionHostTests`（6 项）验证扫描/执行/失败上报

### 设置窗 MVVM 化与插件契约 E1（2026-11-23，M2 / E1）
- M2：设置窗改为 `SettingsWindow.axaml` + `SettingsWindowViewModel`，8 个分页全部 VM 化
  （外观/常规/截图/录屏/设备/快捷键/诊断/关于）；`SettingsWindow.axaml.cs` 从 690 行降到 127 行、
  零控件构造，保存仍是一次 `ApplyMany`、语义不变
- 新增共享设置页样式 `SettingsStyles.axaml`；分页用 `x:DataType` 绑定，页面不再直接读 `ConfigService`
- 自动化页为过渡态：动态规则编辑器仍在页面代码后置，保存时并入同一批写盘（后续转 ItemsControl + VM）
- 新增 `SettingsPagesTests`：分页载入/收集映射 + 设置窗全分页 headless 加载
- E1：新增插件动作契约 `Core/Actions`（清单模型 / 白名单校验 / 分层合并），
  支持 `open-url / open-path / dispatch / workspace / copy-text / toast`，镜像外的未知命令只废单动作
- 新增 `ActionCatalogTests`（12 项）：合法解析、schema/版本、保留前缀、命令白名单、路径穿越、
  重复 id、策略层覆盖、超大文件

### MVVM 基座与主题资源化（2026-11-23，M0 / M1）
- M0：新增 `ObservableObject` / `AsyncRelayCommand` / `StateBase` 三个无依赖基座
  （属性通知、异步命令防重入与异常兜底、事件订阅随 State 释放），并固化
  `docs/plans/mvvm-baseline.md`（7 条冒烟链路 + 3 组像素基线）作为重构护栏
- M1：新增 `ThemeResources` 作为 Palette → 资源字典的唯一出口；
  侧栏面板宿主/标题/关闭钮、外观页、自动化页改走 `{DynamicResource}`，
  `ApplyTheme` 中逐控件赋笔刷收敛为一次资源写入
- 面板主题重建暂保留（面板仍为代码手绘），随 M3 面板迁移一并删除

### 安全与信任加固（2026-11-23，深度优化第一轮）
- **更新链哈希独立信道**：镜像代理不再提供安装包哈希（代理能同时替换安装包与其 SUMS），
  只有 GitHub 直连 / Gitee 作者自有源参与校验；镜像通道无可信哈希时停止自动下载与「立即安装」
- 直连 GitHub 拿到可信哈希后，安装包下载可回落 gh-proxy 镜像，校验仍用直连哈希
- 发行者署名更正：安装包发布者改为「ClassTechStar & Contributors」；
  README 明确与视源股份（希沃 / Seewo）无隶属关系，并新增安全承诺区（零遥测 / 不申请管理员 / 不装驱动不注入）
- README 录屏功能口径对齐实现（麦克风录制暂未支持）
- WndProc 子类化失败不再静默：写日志，全局热键失效时弹一次提示

### 体验与工程（2026-11-23）
- 设置保存改 `ConfigService.ApplyMany` 批量写入：20+ 键一次落盘、一次广播，消除逐键写放大
- 输入法角标改三字母 ISO 码（JPN/KOR/ENG…），修正 zh-CN / zh-TW 子语言映射反转
- 边缘触发条在触屏上禁用悬停展开（手指划过不再误触发），只保留点击
- 设置页导航不再按 `Grid.Children[0]` 摸视觉树，改持导航容器引用
- 设计规范页仅 Debug 构建可见，不再随正式产品发行
- 新增 `docs/adr/` 决策记录（ADR-001 / M4 / M6 / M8）与 Issue 模板（bug / feature / 安全私密渠道）
- 测试 +23（批量配置 / 输入法角标 / 更新链信任源），共 461 项

### 浅色也统一 M3（2026-11-22）
- `Light()`（玻璃浅色）与 `ClassicLight()`（经典快面）的内容色改走 `M3Palette(dark:false)`，
  与深色对称、与设置页同源；rail 胶囊各自的形态语言保留
- 浅色层级：页面底 surface 最亮（近白）、卡片 surfaceContainer 稍暗（与深色方向相反）
- 像素验证：surface (254,251,255) / surfaceContainer (241,236,244) / onSurface (27,27,31)
- 测试 +2（浅色同源 11 字段、卡片层级），共 446 项

### 侧栏深色与设置页统一（2026-11-22）
- `AppTheme.From(dark)` 的内容色改走 M3 语义色，与设置页 `M3Palette(dark)` 同源，
  消除"侧栏发灰、设置页清晰"的双轨深色
- rail 胶囊的渐变与边光环保留 v1.2 参数（形态语言，不随配色统一消失）；
  `appearance.liquidGlass` 开关仍控制胶囊质感
- 新增 3 项一致性测试：侧栏/设置页深色逐项相等、rail 形态保留、卡片全不透明

### 其他页面深色主题（2026-11-22）
- 设置窗 9 个页签深色统一走 M3 不透明 surface 阶梯（`AppTheme.ForSettings`：
  浅色保持既有玻璃/经典面，深色换 M3 语义色）
- 修正深色下 Fluent 控件模板仍按浅色渲染的问题：设置窗 / 崩溃提示窗 / OOBE
  均显式设置 `RequestedThemeVariant`（只换 Palette 不换变体会留白底控件）
- 独立窗口（崩溃提示 / OOBE）改用 `AppTheme.CurrentM3()`，与设置页同源深色
- 深色下自定义强调色过暗（亮度 < 0.55）时回退 M3 primary，保证可读
- 新增 `SettingsThemeTests` / `IndependentWindowThemeTests`：不透明性、surface 层级、
  无白块、文字可读（含像素级抽检验证）

### 设计规范页深色主题 + 物理弹簧动效（2026-11-22）
- 设计规范页新增明暗双色板陈列与深色组件对照块（`DarkPalette()` 全部取自 M3 深色语义角色）
- 新增 `Core/Design/SpringCurve`：二阶阻尼振子解析解（欠阻尼/临界/过阻尼三支），
  替代此前的 BackEase 近似；过冲量为解析值，动画时长取 0.1% 包络收敛时间
- `M3.Spring(spec)` 输出 (时长, IEasing)；`PageTime/SettleTime/...` 改由弹簧计算
- Filled 按钮前景改用 `onPrimary`（深色主题下白字对比不足的修正）
- 新增 `SpringCurveTests`（12 项物理正确性）+ 深色渲染测试

### Material Design 3 设计系统（2026-11）
- `design-tokens.json` 新增 `m3` 段：九档形状 / MD3 十五档排版 / 十档动效（对齐 mochi-compose）/
  六级海拔 / 明暗两套语义色角色（seed #2B6EE0）
- 新增 `Avalonia/Ui/M3.cs` 适配层：令牌 → CornerRadius/BoxShadows/TimeSpan/IEasing，
  以及 Card/Button/TextField/Chip/ListRow 组件工厂（含 44dip 触控兜底）
- 设置窗新增「设计规范」页（`DesignSpecPage`）：形状/色彩/排版/动效/组件五组可视化陈列
- 新增 `docs/material-design-3.md` 规范文档（含与 mochi 的差异说明、PanelKit 映射表）
- 校验：`M3DesignTests`（JSON↔C# 一致、刻度合法性）、`M3ComponentTests`（组件规格、硬编码扫描）、
  `DesignSpecPageTests`（真实渲染、触控达标）



### 借鉴 Flow.Launcher 的迁移（2026-11-22）
- 配置写盘加 `.bak` 备份：主文件损坏时自动回滚，不再丢用户全部配置
- SVG 图标加载加缓存（`ConcurrentDictionary`）：面板频繁重建不再反复解析 SVG
- 新增 `TimedOperation`：计时与记录分离的可单测耗时封装
- 崩溃日志展开完整 InnerException 链（最多 16 层，带循环保护）
- 更新检查支持 `HTTPS_PROXY`/`HTTP_PROXY` 环境变量（教室/校园网代理）

## [未发布]

## [1.8.0] - 2026-11

### 新增
- 阈值告警统一的边沿检测组件（`ThresholdMonitor`）：连续命中、迟滞带、只在状态跃迁时触发
- 有界时间窗口历史（`TimeWindowHistory`）：通知中心除 50 条上限外按 7 天回收
- 防抖闸门（`DebounceGate`）与集合按 Key 自愈合并（`CollectionDefaults`）
- 数据驱动变更说明：关于页展示当前版本更新内容，数据源为随程序集嵌入的 `CHANGELOG.md`
- 统一时钟支持暂停/恢复，侧栏收起态停表省 CPU
- 录屏暂停/继续（暂停段自动从成片时间线跳过，面板计时同步净时长）
- win-arm64 原生自包含构建与对应安装包（根因消除骁龙 ARM 机型 x64 仿真下的录屏硬编崩溃）
- Release 附 `SHA256SUMS.txt`，应用内更新下载后按哈希校验完整性
- 设置页「自动化」编辑器：条件 → 动作规则（课堂状态/全屏/时段/星期）可视化编辑，`Ruleset` 边沿触发消费；扩展状态区展示 `ExtensionRegistry` 加载/失败情况
- 高对比度主题（纯黑白强对比 + 全不透明描边，低视力/投影教室可用）与外观设置开关

### 优化
- 设置保存改为 `SetIfChanged`，未变化的键不落盘、不广播
- 内存看门狗复用通用阈值/历史组件，消除重复判定逻辑
- `.gitattributes` 统一换行规范化
- 应用内更新：单例 HttpClient、多通道探测（GitHub → 镜像代理 → Gitee）、下载进度显示与显式失败原因
- 墨迹转文字改走 WinRT InkAnalyzer 手写识别（位图 OCR 兜底）
- 录屏 x64 真机默认硬编并自动回退软编，帧池缓冲 2 → 3
- 长截图新增黑块检测（GPU 加速窗口）与拼接校验（重叠为 0 重试后显式失败，不再输出重影长图）
- EventBus 处理器异常接入日志钩子；WindowManager 拆出 `WindowRouter` 动作路由与 `DeviceEventBridge` 设备事件桥
- UI 迁 XAML（改哪迁哪）：外观设置页迁 axaml 作样板，新面板统一走 axaml
- 可访问性：设置导航/保存按钮/rail 磁贴/面板关闭钮补 AutomationProperties 命名，导航支持键盘逐站停靠
- 覆盖率门禁 40% → 55%（实测 58.8%）：补自动化规则引擎/扇形布局/热键解析/策略服务纯逻辑测试
- 设置保存改为 `ApplyMany` 批量写入：20+ 键一次落盘、一次广播，消除逐键写放大
- 输入法角标改三字母 ISO 码（JPN/KOR/ENG…），并修正 zh-CN / zh-TW 子语言映射反转
- 边缘触发条在触屏上禁用悬停展开（手指划过不再误触发），只保留点击
- 设置页导航不再按 `Grid.Children[0]` 摸视觉树，改持导航容器引用
- 设计规范页仅 Debug 构建可见，不再随正式产品发行
- WndProc 子类化失败不再静默：写日志并对全局热键失效弹一次提示
- 新增 `docs/adr/` 决策记录目录（ADR-001 / M4 / M6 / M8）与 Issue 模板（bug / feature / 安全私密渠道）

### 修复
- 配置键写入 null（配置导入脱敏路径）时 `ExpandPath` 空引用崩溃
- 版本号单一事实源失守：安装脚本版本改由 CI 从 `Directory.Build.props` 注入，产物名带架构记号
- 应用内更新永远找不到安装包：安装包名匹配规则与实际产物名对齐
- 麦克风"假开关"：设置页明确标注暂未支持并禁用
- winget 清单停在 1.5.0 且从未合入：发版后由 `winget.yml` 自动向 winget-pkgs 提交

## [1.6.0] - 2026-10

竞赛版。

### 新增
- 离线墨迹转文字、智能课堂、课堂统计、演示模式
- 面板注册表（`PanelCatalog`）与面板内容注册表（`PanelRegistry`）
- 扇形菜单角度判定（死区 + 迟滞）、拖拽采样合并、触屏滚动、分页转场
- 亮度/音量快捷调节（能力探测，设备不支持时自动隐藏）
- 应用内更新检查（GitHub Release，安装包名与大小校验）
- 配置导入导出（导出脱敏）、历史默认值迁移、跨权限单实例识别
- 时间状态机、内存看门狗、崩溃可见化、通知优先级队列
- 配置键级订阅与锁定键（策略段 `policy.` 不可被用户写）

### 修复
- CI 覆盖率门禁空转：测试项目补 `coverlet.collector`，门禁增加显式校验
- 高频移动事件导致的拖拽抖动（采样合并 + 边界一次性 DPI 换算）
- 全屏误收缩：支持 `display.excludedFullscreenWindows` 排除名单

## [1.5.0] - 2026-09

- 侧栏 + 悬浮球 + 扇形菜单主形态定稿
- 三层配置（默认 → ProgramData 策略 → APPDATA 用户）
- 设计令牌单一来源（`design-tokens.json`）
- headless 冒烟测试与像素基线断言
- Inno 安装包与 winget 清单

## [1.0.0 - 1.2.0] - 2026 早期

Electron 栈初版与过渡版本，源码存于 `archive/electron-final` 分支；
当时的界面快照目录已从主干移除（仓库瘦身），需要对照可到该分支查看。
