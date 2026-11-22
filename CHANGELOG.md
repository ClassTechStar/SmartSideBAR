# 更新日志

本项目版本号写在 `Directory.Build.props` 的 `<Version>`。
格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/)，版本号遵循语义化版本。

## [未发布]

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
