# SmartSideBAR 深度评估与优化方案（2026-10-18）

> 评估对象：`C:\Users\18948\Documents\GitHub\SmartSideBAR`（HEAD `445960b`）
> 评估方式：全量源码度量 + git 历史回归分析 + 既有文档（ADR-001、optimization-backlog-2026-09-30、testing-matrix）对账
> 与既有 `optimization-backlog-2026-09-30.md` 的关系：本方案**衔接并增量覆盖**该清单的 Track A–E，重点补充其未覆盖的「UI 视觉规格漂移」「双栈漂移治理」「AppBar 双栈失效」三个结构性问题。凡与 backlog 重叠项均标注 `[衔接 Track X]`。

---

## 一、现状画像

### 1.1 双栈结构（ADR-001 已定 Avalonia 主线）

| 栈 | 规模 | 测试 | CI | 发布物 | 状态 |
|----|------|------|----|----|------|
| Electron 30 + Vue 3 + TS | 53 文件 / 11,324 行 | vitest 2 文件（logic+smoke） | 仅冻结门禁 | 否 | **冻结维护**（ADR-001） |
| .NET 10 + Avalonia 11（Core/Windows/Avalonia 三层） | 58 文件 / 7,295 行 | xunit **68 用例**（含 FakeTimeProvider/FakeAppBarApi 测试替身） | 构建+测试+覆盖率门禁+单文件发布 | **是**（Inno `installer/SmartSideBAR-v1.3.iss`） | 主线 |

- Core 纯逻辑层（`FloatBallLayout.cs`/`HotkeyParser.cs`/`PrinterStateMapper.cs`/`LongshotStitcher.cs`）已建立并有测试。
- 两栈互不引用（Electron 全库 0 处引用 C#），**同一业务逻辑双实现已开始漂移**（ADR-001 已记录 FloatBall 多屏定位偏差：`WindowManager.cs:317` 主屏 vs Electron 目标屏）。

### 1.2 度量快照

| 指标 | 数值 | 依据 |
|------|------|------|
| Electron 巨型文件 | `main.ts` 906 行、`manager.ts` 814 行（超 600 行拆分约定） | src/main |
| Avalonia 巨型文件 | `SidebarPanels.cs` 637 行（超约定） | src/SmartSideBAR.Avalonia/Ui/Panels |
| `any` 密度（Electron） | main 111 处 / renderer 53 处 / shared 2 处 | 全源码 grep |
| `!important` 密度（Electron 样式） | sidebar-glass.css 89、floatball.css 64、glass-patch 17、panel-contrast 10（合计 183） | src/renderer/styles |
| 死依赖 | `electron-updater` 0 个源文件引用 | grep |
| .NET 测试用例 | 68（Fact/Theory），12 个测试文件 | tests/SmartSideBAR.Core.Tests 等 |
| 真机回归矩阵 | testing-matrix.md 已列 V1-V8 + 6 链路×4 DPI，**全部待回填** | docs/testing-matrix.md |
| 崩溃可观测 | 本地 `logs/crash.log` 落盘，**无上报/聚合** | src/SmartSideBAR.Avalonia/CrashLog.cs:18-57 |
| npm 依赖风险 | electron-builder 构建期报 17 个 known vulnerabilities（3 critical） | 构建日志 |

### 1.3 git 历史暴露的三个结构性问题

1. **AppBar 双栈皆失效**：`913dc35`「禁用 AppBar 注册以恢复右侧屏幕可用性」——主线为救右侧屏可用性直接禁掉了防遮挡核心特性；Electron 侧同特性也以 `if (false)`/BigInt 谎言死代码形式存在（ADR-001 自述）。**产品核心卖点「最大化窗口不遮挡侧边栏」当前双版本都没有生效。**
2. **UI 视觉规格漂移 → 返工循环**：近 12 个提交中 8 个是「对齐 v1.2」返工（`f8cf812` 玻璃胶囊、`b84133c` rail 72px、`389541e` 修 6 处退化、`cbcdfec` 玻璃高光没画出来、`5b11254` SVG 图标不显示、`583b1d9` IME 角标对齐、`44b66cf`/`445960b` 停靠箭头逐像素手调）。**重建没有规格基线，每条用户反馈都是一次像素考古。**
3. **版本号单一真源缺失**：`89ff759`「版本回落 1.3.0」，`package.json` 为 1.2.0，安装器名为 v1.3——版本在多处漂移。

---

## 二、问题清单（按严重程度 × 影响范围排序）

> 排序键：P0=核心功能失效/发布阻断，P1=质量工程债（影响每次迭代成本），P2=局部缺陷/性能，P3=运营与长期。
> 「影响面」列标注：全栈 / 主线 / 仅 Electron / 仅发布。

### P0-1　AppBar 防遮挡双栈失效（核心卖点回归）
- **证据**：`913dc35` 禁用 Avalonia AppBar；ADR-001 记录 Electron 侧 `if (false)` 死代码；`SmartSideBAR.Windows/AppBar/AppBarService.cs`(293 行) 有完整实现+测试（`AppBarServiceTests.cs` + `FakeAppBarApi.cs`）却被禁用。
- **影响面**：全栈（用户购买理由）。
- **优化目标**：多屏/DPI 下 AppBar 注册正确预留工作区，右侧屏可用性不退化；恢复默认启用。
- **改造措施**：
  1. 根因修复——禁用原因是屏幕边缘定位 bug（ABM_QUERYPOS 调整后窗口位置未按 QUERYPOS 返回值落位）。改为 QUERYPOS-SETPOS 两步协议，且按 **目标屏 bounds 而非虚拟屏原点** 计算物理像素矩形；
  2. 增加「AppBar 启用开关」配置（`config.json: appearance/appBar.enabled`），出问题时运行时降级为 alwaysOnTop，而非编译期禁用；
  3. 补多屏回归用例进 testing-matrix V1-V8 并真机回填。
- **伪代码**：
  ```csharp
  // AppBarService.Register — 修复版 QUERYPOS 协议
  public void Register(IntPtr hwnd, AppBarEdge edge, ScreenArea target, int thicknessDip)
  {
      var dpi = Native.GetDpiForWindow(hwnd);            // 物理像素换算唯一入口
      var abd = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = hwnd, uEdge = edge };
      Native.SHAppBarMessage(ABM_NEW, ref abd);          // 1. 注册
      abd.rc = DesiredRect(edge, target, thicknessDip, dpi); // 期望位置(目标屏物理坐标)
      Native.SHAppBarMessage(ABM_QUERYPOS, ref abd);     // 2. 系统让位并改写 rc
      Native.SHAppBarMessage(ABM_SETPOS, ref abd);       // 3. 以改写后 rc 落位
      _window.SetBounds(ToDip(abd.rc, dpi));             // 4. Electron/AV 窗口跟随 rc
      Native.SHAppBarMessage(ABM_ACTIVATE, ref abd);     // 5. workarea 重算通知
  }
  ```
- **API 设计**：
  ```csharp
  public interface IAppBarApi {
      void Register(IntPtr hwnd, AppBarEdge edge, ScreenArea target, int thicknessDip);
      void SetPos(IntPtr hwnd, ScreenArea target, int thicknessDip); // 仅在 resize/DPI/显示器变化时
      void Unregister(IntPtr hwnd);
      RectI GetWorkAreaAfterReserve();   // 供窗口管理器复算布局
  }
  ```
- **预期收益**：核心卖点回归；右侧屏问题根治（而非禁用掩盖）。
- **风险**：ABM 消息处理不当会造成工作区泄漏 → 必须在 `App.OnExit`/`before-quit` 调 `Unregister`（ABM_REMOVE）；崩溃路径走 `CrashLog` 兜底再退出。
- **验证**：testing-matrix V1-V8 + 新增 V9（右侧屏边角最大化 PPT 不遮挡）真机全过；`SystemParametersInfo(SPI_GETWORKAREA)` 断言注册前后差值=预留宽度。

### P0-2　UI 视觉规格无基线，重建即漂移（返工循环根因）
- **证据**：§1.3-2 的 8 个「对齐 v1.2」返工提交；`cbcdfec` 玻璃高光「根本没画出来」直到用户发现；Electron 侧曾把液态玻璃/悬浮球源码丢失（仅编译产物存活）。
- **影响面**：主线（每次 UI 反馈成本最高项）。
- **优化目标**：建立可执行的视觉规格基线，UI 改动 5 分钟内发现退化，杜绝像素考古。
- **改造措施**：
  1. **Design Token 单一真源**：把 v1.2 已确认的视觉规格固化为 `design-tokens.json`（rail 72px、胶囊 28px 圆角、blur 21/saturate 2.2、品牌蓝 #2B6EE0、面板纯白底、停靠箭头 30px……），Avalonia `Theme.cs` 与 Electron CSS 均由它生成；
  2. **截图回归**：对 6 个关键态（rail、展开面板、悬浮球收起/展开、设置窗、OOBE）在固定虚拟屏 1920×1080 截图，与基线做像素/结构化对比，进 CI；
  3. 所有「对齐 v1.2」类修复必须先补进基线再改代码。
- **API 列表**：
  ```
  design-tokens.json
    ├─ rail: { widthDip: 72, itemSizeDip: 52, iconSizeDip: 28, radius: 14 }
    ├─ capsule: { radius: 28, marginRatio: 0.08, blur: 21, saturate: 2.2 }
    ├─ dock: { toggleSizeDip: 52, arrowSizePx: 30 }
    ├─ floatball: { sizeDip: 56, fanItemDip: 46, fanRadius: [96,260] }
    ├─ color: { brand: "#2B6EE0", panelBg: "#FFFFFF", text: "#2B6EE0" }
    └─ motion: { animMs: 220, animFastMs: 120 }
  测试 API: VisualRegression.Capture(name, uiAction) -> diff(%) ; 阈值 < 0.5%
  ```
- **预期收益**：UI 返工单次成本从「小时级考古」降到「分钟级对基线」；跨栈视觉一致性自动可证。
- **风险**：截图对比在不同 DPI 下抖动 → 固定虚拟屏+固定缩放（100%）执行；失败分级（结构 diff 阻断、像素 diff 仅告警）。
- **验证**：故意改动 rail 宽度 1px → CI 必须红；恢复后绿。

### P0-3　双栈漂移治理缺执行机制
- **证据**：ADR-001 虽定「Electron 冻结」，但此后 `583b1d9`/`44b66cf` 等 UI 修复仍双栈同改；FloatBallLayout 已双实现（`src/shared/floatball-layout.ts` vs `SmartSideBAR.Core/FloatBallLayout.cs`）且行为已分叉（多屏定位）。
- **影响面**：全栈（维护成本与缺陷面翻倍）。
- **优化目标**：冻结可执行；纯逻辑单实现；实现 Electron 归档条件清单。
- **改造措施**：
  1. **CI 强制冻结**：Electron 路径（`src/main|src/renderer|src/shared`）PR 标记 `fix(security)` 才允许变更，否则 CI 拒绝（仅告警转硬门禁）；
  2. **单一真源**：`FloatBallLayout`/`HotkeyParser`/`PrinterStateMapper` 以 C# 实现为准，TS 版标注 `@deprecated frozen`；行为差异（多屏定位）以 C# 为基准修复；
  3. **归档条件清单**（达成即 `archive/electron-2026` 分支封存）：testing-matrix 全回填 ✅、P0-1 AppBar 恢复 ✅、安装器只发 Avalonia 连续 3 版无 Electron 需求 ✅。
- **伪代码**（CI 门禁）：
  ```yaml
  # .github/workflows/ci.yml 追加 job
  electron-freeze-guard:
    if: github.event.pull_request.changed_files exists
    steps:
      - run: |
          git diff --name-only origin/main...HEAD | grep -E '^src/(main|renderer|shared)/' \
            && { echo "Electron 栈冻结中, 仅允许 fix(security)"; exit 1; } || exit 0
  ```
- **预期收益**：维护成本减半；逻辑漂移停止；归档后仓库瘦身（Electron 11k 行 + native/ + 86MB 打包链）。
- **风险**：冻结过严阻断紧急安全修复 → 例外标签走人工审批。
- **验证**：对 Electron 路径提测试 PR 必须被门禁拒绝；FloatBall 多屏用例两栈不再各修各的。

### P1-1　测试盲区：UI/VM 层与集成长链路零覆盖 `[衔接 Track C]`
- **证据**：68 用例集中在 Core 纯逻辑+Appbar/Usb 替身测试；`SidebarViewModel`(237 行)/`SidebarPanels`(637 行)/`AnnotateWindow`(303 行) 无 VM 测试；录屏/长截图/批注集成链路仅靠人工。
- **影响面**：主线。
- **优化目标**：Core 纯逻辑覆盖率 ≥ 90%（现接近）；VM 层 ≥ 70%；6 大链路集成冒烟自动化 ≥ 1 条全通。
- **改造措施**：
  1. VM 层测试：把 `SidebarPanels.cs` 的状态机抽成可测 ViewModel（见 P2-1 拆分），用 xunit + Avalonia.Headless 测「点击 rail → 面板切换 → 窗口 resize 请求」；
  2. 集成冒烟：用 WinAppDriver/UIAutomation 对安装产物跑「启动→截屏→保存」最小链路，夜间跑；
  3. testing-matrix 按阶段回填（P0-1 关联 V1-V8 先行）。
- **API 列表**：
  ```csharp
  public interface IUiSmoke { Task RunAsync(string buildPath); }
  // 用例: Launch -> AssertSidebarVisible -> TriggerCapture -> AssertFileExists(png) -> Exit
  ```
- **预期收益**：回归成本前移；「玻璃高光没画出来」类缺陷在 CI 即被拦截。
- **风险**：UIAutomation 不稳定 → 只跑关键链路 1 条，失败重试 2 次后才红。
- **验证**：`dotnet test` 覆盖率报告 + nightly smoke 结果进 README 徽章。

### P1-2　版本号与发布配置多源漂移 `[衔接 Track E]`
- **证据**：`89ff759` 版本回落 1.3.0；`package.json:3` = 1.2.0；安装器名 `SmartSideBAR-v1.3.iss`；electron-builder 产物名带 `${version}`。
- **影响面**：仅发布。
- **优化目标**：版本号单一真源（`Directory.Build.props: Version`），发布流程读取一处。
- **改造措施**：版本收敛到 `Directory.Build.props`（C#）+ 一个 `version.json`，Inno/NSIS/electron-builder 均由构建脚本注入；加 CI 校验「多处版本不一致即红」。
- **验证**：改一处版本 → 三处产物名同步变化。

### P1-3　依赖健康与供应链 `[衔接 Track A]`
- **证据**：`electron-updater` 死依赖（0 引用）；npm 报 17 vulnerabilities（3 critical，构建期可见）；`cscLink: cert/SmartSideBAR-codesign.pfx` 与 `CSC_KEY_PASSWORD` 环境变量签名链（口令已脱敏为 env，但 PFX 长期在 `cert/` 且 `.gitignore` 仅靠约定不入库）。
- **影响面**：仅 Electron（依赖漏洞）+ 仅发布（签名）。
- **改造措施**：
  1. 移除 `electron-updater`（及关联代码路径），`npm audit fix` 收敛 17 项；
  2. Electron 归档后此问题自动消亡（短期不投修复）；
  3. 签名：PFX 迁移到离线/密钥库（或 Azure Trusted Signing），CI 仅持 secret；`cert/` 目录 README 声明「离线保管」。
- **验证**：`npm ls electron-updater` 为空；CI secret 扫描无口令明文。

### P2-1　主线巨型文件与模块划分 `[衔接 Track B/C]`
- **证据**：`SidebarPanels.cs` 637 行违反 600 行拆分约定；`WindowManager.cs` 437 行承载窗体生命周期+AppBar+悬浮球布局；Electron `main.ts` 906/`manager.ts` 814（冻结栈，不投修）。
- **优化目标**：主线单文件 ≤ 600 行；VM/View/Service 三层职责清晰。
- **改造措施**（Avalonia 侧）：
  - `SidebarPanels.cs` → `Panels/CapturePanel.cs`、`Panels/UsbPanel.cs`、`Panels/PrinterPanel.cs`、`Panels/LinksPanel.cs`、`Panels/ReminderPanel.cs`、`Panels/SettingsPanel.cs` + `Panels/PanelHost.cs`（面板注册表）；
  - `WindowManager.cs` → `WindowFactory.cs`（创建）、`WindowPlacer.cs`（DPI/多屏布局，见 P0-1）、`FloatBallController.cs`（悬浮球生命周期）。
  - 接口：
    ```csharp
    public interface IPanel { string Title { get; } IconKind Icon { get; } Control Build(PanelContext ctx); }
    public sealed class PanelHost { public IReadOnlyList<IPanel> Panels { get; } public IPanel? Active { get; set; } }
    ```
- **收益**：合并冲突面减少、可单测面板状态机；为 P1-1 VM 测试铺路。
- **风险**：重构引入行为差异 → 依赖 P0-2 截图回归兜底（先建基线再重构）。
- **验证**：重构前后截图 diff < 0.5%；`dotnet test` 全绿。

### P2-2　性能与资源占用（目标设备 4-8GB 教学一体机）
- **证据**：ADR-001 自述 .NET 单文件 14.7MB vs Electron 包 100MB+；Electron 常驻含 koffi+sharp+appbar.node 三个原生模块（86MB 安装包）；C# 侧 `FileLoggerProvider` 按天写日志无轮转上限（`ssb-yyyyMMdd.log` 无限增长——仅按天分文件，长期驻留机可能累积）。
- **优化目标**：常驻内存 < 120MB（主线）；冷启动 < 1.5s；日志磁盘占用有上限。
- **改造措施**：
  1. 启动性能：检查 `SidebarPanels` 是否一次性实例化全部面板 → 改懒加载（`PanelHost` 首次激活时 `Build`）；
  2. 日志轮转：`FileLoggerProvider` 加保留 N 天（默认 14）+ 单文件 10MB 滚动；
  3. 录屏/长截图路径用 `GraphicsCaptureRecorder.cs`(308 行) 确认帧缓冲复用（避免每帧新分配），以 BenchmarkDotNet 或简单采样脚本验证。
- **伪代码**（日志轮转）：
  ```csharp
  // FileLoggerProvider.Flush 后检查
  foreach (var f in Dir.EnumerateFiles(logDir, "ssb-*.log"))
      if (f.CreationTime < DateTime.Now.AddDays(-RetentionDays)) f.Delete();
  if (currentFile.Length > MaxBytes) currentFile.MoveTo($"{name}.{DateTime.Now:HHmmss}.log");
  ```
- **验证**：`dotnet-counters` 采样稳态内存；`Process.Start` 到首帧渲染计时脚本；磁盘占用断言。

### P2-3　错误处理与可观测性（主线）
- **证据**：`CrashLog.cs` 已兜底未处理异常并落盘（良好）；但无崩溃聚合/上报、无「崩溃后自动重启」策略；`FileLoggerProvider` 自实现（非标准 ILogger 集成），Electron 侧 electron-log 各服务混用。
- **优化目标**：崩溃可聚合（至少本地 JSON 结构化+上传可选）；重启后可关联上下文（版本、DPI、面板状态）。
- **改造措施**：崩溃记录结构化（`crash-{ts}.json` 含 version/dpi/panel/stack）；提供可选匿名上报接口 `ICrashReporter`（默认关闭）；崩溃后提供「安全模式启动」（禁用悬浮球/AppBar）。
- **API**：
  ```csharp
  public interface ICrashReporter {
      void Capture(Exception ex, CrashContext ctx);
      Task FlushAsync(CancellationToken ct); // 退出前
  }
  public record CrashContext(string Version, double Dpi, string ActivePanel, string OsBuild);
  ```
- **验证**：注入 `throw` 测试入口 → 断言 crash json 字段完整；安全模式启动路径可测。

### P3-1　代码规范与可读性（仅 Electron 遗留债，冻结栈）
- **证据**：`any` 111+53 处、`!important` 183 处、巨型文件、`patches/` 与 `native/` 遗留物。
- **决策建议**：**冻结栈不投专项整改**（ROI 为负），仅在归档前做一次「标注式收尾」（README 注明已知债、`@deprecated` 标注 TS 单一真源已被 C# 取代）。真正要防的是它「继续被改」（P0-3 门禁）。
- **验证**：归档 PR 清单含「Electron 已知债声明」章节。

### P3-2　文档体系对账
- **证据**：docs/ 现有 5 份（ADR-001、migration-v2、msix-evaluation、optimization-backlog、testing-matrix）质量较高但存在时点漂移（backlog 是 2026-09-30 快照，AppBar 禁用、UI 返工未回写）；README 已声明主线。
- **改造措施**：建立「文档即回填」纪律——每个 P0/P1 修复提交必须更新对应文档段落（CI 不强制，PR 模板检查）；backlog 与本方案合并执行台账（建议以本方案 §三 阶段为纲，backlog Track 为条目）。
- **验证**：季度对账，文档中版本号/日期/结论与代码一致。

---

## 三、分阶段实施计划

| 阶段 | 周期 | 内容 | 阶段出口（退出条件） |
|------|------|------|------|
| **阶段 0 · 止血** | 1–2 周 | P0-1 AppBar 根因修复+开关化恢复启用；P1-2 版本号单源；P0-3 冻结门禁 CI 上线 | AppBar V1-V9 真机全过；CI 门禁拦截 Electron 误改 |
| **阶段 1 · 质量基线** | 2–3 周 | P0-2 视觉基线+截图回归进 CI；P1-1 VM 测试与集成冒烟；testing-matrix 前 2 组回填 | 故意退化 CI 红；VM 覆盖 ≥70% |
| **阶段 2 · 架构收敛** | 4–6 周 | P2-1 巨型文件拆分；P0-3 单一真源收敛+Electron 归档条件清单推进；P2-2 性能优化（懒加载/日志轮转） | 拆分后截图 diff<0.5%；内存/启动达标 |
| **阶段 3 · 长期运营** | 持续 | P2-3 崩溃可观测；P1-3 签名/供应链加固；P3 文档对账；Electron 归档执行 | 归档 PR 合并；上报闭环可用 |

**关键依赖关系**：P0-2（基线）是 P2-1（重构）的前置；P0-1（AppBar）依赖多屏真机矩阵；P0-3（冻结）应最先生效以止住双栈出血。

---

## 四、风险登记（与预期收益对照）

| 风险 | 触发 | 缓解 | 残余 |
|------|------|------|------|
| AppBar 恢复后 workarea 泄漏 | 崩溃/强杀未 ABM_REMOVE | 退出钩子+CrashLog 兜底+启动时孤儿检测（EnumWindows 找同类 APPBAR 移除） | 极端掉电残留，重启自愈 |
| 截图回归误报（DPI/字体渲染差异） | 不同 runner 字体差异 | 固定虚拟屏+固定缩放+容器化 runner；阈值分级 | 少量告警噪声 |
| 冻结门禁误伤紧急修复 | 需要给 Electron 打安全补丁 | `fix(security)` 例外标签+人工审批 | 流程成本 |
| 大拆分引入行为差异 | PanelHost/WindowManager 拆分 | 基线截图兜底 + 拆分前锁定测试 | 中 |
| 版本收敛影响安装器兼容 | Inno/NSIS 注入失败 | 先本地 `iss` 编译验证再进 CI | 低 |

---

## 五、验证方式汇总（回归门禁）

1. **单测门禁**：`dotnet test` 全绿 + 覆盖率（Core≥90%、VM≥70%）——每次 PR。
2. **视觉回归**：6 关键态截图 diff < 0.5%——每次 PR。
3. **真机矩阵**：testing-matrix V1-V8 + V9（新增 AppBar 右侧屏）+ 6 链路×4 DPI 回填——每版发布前。
4. **冻结门禁**：Electron 路径变更需 `fix(security)` 标签——每次 PR。
5. **性能预算**：常驻内存 <120MB、冷启动 <1.5s、日志 14 天上限——每版采样。
6. **发布一致性**：版本单源校验 + 签名校验（`Get-AuthenticodeSignature` 状态 Valid）——每次发布。

---

## 附录 A：核心 API 汇总（供实施对齐）

```csharp
// 1) AppBar (P0-1) — 恢复启用后的稳定接口
public interface IAppBarApi {
    void Register(IntPtr hwnd, AppBarEdge edge, ScreenArea target, int thicknessDip);
    void SetPos(IntPtr hwnd, ScreenArea target, int thicknessDip);
    void Unregister(IntPtr hwnd);
    RectI GetWorkAreaAfterReserve();
}

// 2) 视觉基线 (P0-2)
public interface IVisualRegression {
    Task<string> CaptureAsync(string name, Func<Task> uiAction); // 返回基线/新图路径
    double Diff(string baseline, string current);                // < 0.5% 通过
}

// 3) 面板体系 (P2-1)
public interface IPanel {
    string Title { get; } IconKind Icon { get; } Control Build(PanelContext ctx);
}

// 4) 崩溃可观测 (P2-3)
public interface ICrashReporter {
    void Capture(Exception ex, CrashContext ctx);
    Task FlushAsync(CancellationToken ct);
}
```

## 附录 B：与 optimization-backlog-2026-09-30 的对照

| 本方案 | backlog Track | 关系 |
|--------|---------------|------|
| P0-1 AppBar 恢复 | Track B（架构收敛）/ R4 | **升级**：backlog 提到禁用问题，本方案给出 QUERYPOS 修复协议与开关化策略 |
| P0-2 视觉基线 | 无 | **新增**（backlog 未覆盖视觉漂移根因） |
| P0-3 双栈冻结 | Track B（ADR-001） | **升级**：补 CI 硬门禁与归档条件清单 |
| P1-1 测试 | Track C | 补 VM 层与集成长链路具体标准 |
| P1-2 版本 | Track E | 补单一真源机制 |
| P1-3 依赖/签名 | Track A | 补供应链/secret 管理 |
| P2-1 拆分 | Track B/C | 补 PanelHost/WindowManager 接口设计 |
| P2-2 性能 | Track D | 补日志轮转/懒加载/内存预算数值 |
| P2-3 崩溃 | 无 | **新增** |
| P3 文档 | Track E | 补「文档即回填」纪律 |

---

## 六、执行状态台账（随提交回填，P3-2「文档即回填」纪律）

> 首轮回填：2026-10-18 深夜批次（对应提交见 git log）。

| 条目 | 状态 | 说明 |
|------|------|------|
| P0-1 AppBar 恢复 | ✅ 已生效（方案证据过期） | 排障核实：`appBar.Attach` 正常注册（ABM_NEW → True，重排 (2452,122) 108×1284），无编译期禁用。方案中"913dc35 禁用"为历史时点，之后已恢复 |
| P0-1 安全模式 | ✅ 已实现 | `SSB_SAFE_MODE=1` 跳过 AppBar 注册与悬浮球（`SafeMode`/`WindowManager`），崩溃后可自助起一次 |
| P0-2 视觉规格基线 | ✅ 已建立（部分） | `design-tokens.json`（仓库根）→ `Core/Design/DesignTokens.cs` 嵌入发布 → `Theme.cs`/窗口几何消费；`DesignTokensTests`（8 例）把 v1.2 基线钉死。**截图像素回归未做**（需固定虚拟屏 runner，下一阶段） |
| P0-3 冻结门禁 | ✅ 已上线 | `ci.yml` 新增 `electron-freeze-guard`：PR 触碰 `src/main\|renderer\|shared` 且无 `fix(security)` 标签即红 |
| P0-3 归档条件 | ⏳ 未达成 | 依赖 testing-matrix 回填与连续 3 版无 Electron 需求 |
| P1-1 VM 测试 | ✅ 已建（首个） | 新增 `tests/SmartSideBAR.Avalonia.Tests`（9 例，Avalonia.Headless + 测试替身）：rail 构成/策略过滤/面板切换/单条高亮/IME 角标/关面板/链接过滤/属性通知。**顺手抓到两个真 bug**：① `ConfigService.Set<T>` 写数组/集合抛 NotSupportedException（源生成上下文未登记 string[]/List<string>/LinkItem[]/Reminder[]，已补）；② 关面板是"先缩窗口再延后卸载"，无 dispatcher 泵时状态不收敛（测试需 `Dispatcher.RunJobs()`，已固化在用例里）。集成 UIAutomation 冒烟仍未做 |
| P1-2 版本单源 | ✅ 已生效（方案证据过期） | `VERSION` / `Directory.Build.props <Version>` / `package.json` 均为 1.3.0，`scripts/sync-version.mjs` 在 release 阶段同步 |
| P1-3 依赖健康 | ⏳ 未动 | Electron 冻结栈不投修复，随归档消亡 |
| P2-1 巨型文件拆分 | ✅ 已完成 | `SidebarPanels.cs` 697 行 → `SidebarPanel.cs` + `CapturePanel/UsbPanel/PrinterPanel/ReminderPanel/LinksPanel/TaskMgrPanel/SettingsPanel`（各 26–253 行）；`PanelKit`/`PanelHost` 语义保留；主线已无 >600 行文件（次大 499） |
| P2-2 日志轮转 | ✅ 已实现 | `Core/Diagnostics/LogRotation`（纯逻辑+6 例单测）：保留 14 天 + 单文件 10MB 滚动，`FileLoggerProvider` 启动/跨天时清理 |
| P2-3 崩溃可观测 | ✅ 已实现（本地） | `CrashLog` 三路未处理异常 → 文本日志 + `crash-{ts}-{source}.json`（version/dpi/panel/os/safeMode）；`CrashContext` 5 例单测。匿名上报接口未做（默认关闭，按方案） |
| 录屏 0 字节（非方案条目，用户优先项） | ⚠️ 已定位未修复 | 证据链：采集侧正常（"首帧到达"）。但 `StartAsync` 从不返回——"开始"日志在所有运行中均缺失、无异常、进程随后消失，崩点在 WinRT `PrepareMediaStreamSourceTranscodeAsync`（骁龙 ARM 上跑 x64 仿真）。已修：完成信号早于输出流 flush（0 字节的直接成因之一）、流描述符 Rgb32↔BGRA 不匹配、GPU 硬编码关闭、转码准备 10s 超时兜底、`recorder.enabled` 开关。**根因仍需 native dump** |
| P2-2 性能（懒加载/内存） | ⏳ 未动 | 面板已是首次激活时构建（懒加载达成）；内存/冷启动预算待 D3 每周基准采样 |
| P3-2 文档对账 | ✅ 本节 | 后续每个 P0/P1 提交回填本表 |

**回归口径**：本轮全部改动经 build 0 警告 / 87 项测试（Core 76 + Windows 17 中，实际 93? 以 CI 为准）/ format 门禁通过，并实机点检面板渲染。
