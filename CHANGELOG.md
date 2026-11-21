# 更新日志

本项目版本号写在 `Directory.Build.props` 的 `<Version>`。
格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/)，版本号遵循语义化版本。

## [未发布]

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
