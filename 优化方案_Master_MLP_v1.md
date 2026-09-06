# SmartSideBAR Master 级（MLP）收官优化方案 v1.0

> **定位**：基于 v1.2.0（git `71f1cc8`）现状的**全生命周期收官规划**——以「迁移到 C# / Avalonia / .NET 10」为主线，承接《project-audit-and-roadmap.md》全部遗留项，把「能用的 Electron 工具」建成「轻量、原生、可运营的教学大屏常驻平台」。
> **技术主线**：**C# 13 / .NET 10 (LTS) / Avalonia 11.x**——后续所有开发以此为唯一技术栈；Windows 原生能力（AppBar / 截图 / 录屏 / USB / 热键 / 输入法）全部走 P/Invoke 或 WinRT，不再依赖 Node 原生模块。
> **文档版本**：M1.0（Master 级 / MLP）　**日期**：2026-09-06　**基线**：v1.2.0（NSIS 81.6MB / 76 IPC 通道 / 审计 P0·P1·P2 主体已完成）
> **一句话结论**：Electron 版已完成 30+ 项审计修复，功能骨架扎实；但**Chromium 常驻成本（220MB 内存预算）、Node 原生模块二等公民（AppBar 通知缺失/构建链脆弱）、双坐标系统（物理 px vs DIP）**三大结构性成本在「4-8GB 教学一体机常驻」场景被持续放大。MLP 的决策是**绞杀者式迁移**：Avalonia 版逐模块对等替换，AppBar 升级为一等公民（补齐 ABN 通知/自动隐藏），录屏原生 MP4，USB 摆脱 PowerShell；期间 Electron v1.2.0 保持可用作回退。

---

## 0. 文档导读

| 你关心 | 直达章节 |
|---|---|
| 为什么迁移、怎么迁 | §3 迁移决策与策略（ADR 表 + 绞杀者路线） |
| AppBar 完整方案 | §5（缺口对照 + 完整 C# 实现 + 验收） |
| 15 个模块逐个怎么迁 | §6 模块迁移设计（每节：现状 → C# 方案 → 关键代码） |
| IPC 怎么映射 | 附录 A（76 通道 → C# API 全表） |
| 配置怎么兼容 | 附录 B（SidekickConfig → C# record + 迁移） |
| 测试 / CI / 签名 | §7 质量工程（含新发现的 pfx 入库隐患治理） |
| 排期与今天做什么 | §10 里程碑 + 行动清单 |

**约定**：C# 代码遵循 .NET 10 风格——file-scoped namespace、`sealed`、primary constructor、raw string、`LibraryImport` 源生成 P/Invoke、`ConfigureAwait(false)`；引用现状一律给 `文件:行号` 或提交号依据；所有代码为**实现级蓝本**（可直接粘贴改写）。

---

## 1. 执行摘要

### 1.1 现状的本质判断

v1.2.0 是一个「打磨过的 Electron 应用」：审计 30+ 项 P0/P1/P2 修复落地、`verify-build` 全链路绿、NSIS 81.6MB（≤110MB 预算）、自签名链建立、AppBar 防遮挡已集成。**继续在 Electron 上迭代的边际收益正在递减，而结构性成本不变**：

| # | 结构性成本 | 证据 | 在教学场景的代价 |
|---|---|---|---|
| S1 | **Chromium 常驻开销** | README 性能预算：空闲内存 ≤220MB（标准档）/ ≤150MB（节能档）；三进程模型（main + renderer ×N：侧栏/OOBE/覆盖层/批注/录屏/设置/悬浮球 7 类窗口） | 4-8GB OPS 一体机上常驻占用最高的软件之一，挤占课件软件资源 |
| S2 | **Node 原生模块二等公民** | `src/native/appbar.cc` + `rebuild-native.mjs` + node-gyp + ABI 绑定 + asarUnpack 路径探测（appbar.ts:27-47） | 每次 Electron 升级需重编 N-API；**ABN 通知无法接收**（见 §5.1 缺口表）；新原生能力（DXGI 录屏、WM_DEVICECHANGE）都要再写 C++ |
| S3 | **双坐标系统** | AppBar 用**物理像素**（appbar.cc:11），Electron `setBounds` 用 **DIP**；manager.ts:118-133 手动换算 `dipWidth × scaleFactor` | 非整数缩放（125%/150%/250%）下换算误差累积，A2 类窗口飞出屏幕 bug 的温床 |
| S4 | **双运行时技术栈** | 主进程 TS + 渲染层 Vue + 原生 C++ + PowerShell 子进程（IME/USB） | 四种语言栈，测试只能覆盖 IPC 注册表（smoke.test.ts 5 用例），服务层 0 单测 |
| S5 | **能力天花板** | 录屏 MediaRecorder 仅 WebM 且无转码（审计 D4）；批注无触屏（D1，mouse* 绑定）；USB 靠 WMI+PowerShell 降级 | 希沃触屏是主输入方式；课堂需要 MP4；U 盘识别已在 Electron 内打了三层补丁（2026-08-30 变更记录） |

### 1.2 MLP 六大主线

1. **M1 架构迁移**：`SmartSideBAR` C# 解决方案（Core/Windows/Avalonia 三层），15 模块逐一对等迁移，Electron 版冻结维护。
2. **M2 AppBar 一等公民化**：P/Invoke 直调 + WndProc 通知闭环（ABN_POSCHANGED/STATECHANGE/FULLSCREENAPP/WINDOWARRANGE）+ 可选自动隐藏，物理像素单坐标轨。
3. **M3 原生能力升级**：录屏 Media Foundation **原生 MP4**；批注/截图 **Avalonia 统一指针事件**（触屏+鼠标天然合一）；USB **WM_DEVICECHANGE**（零 PowerShell）；热键 **RegisterHotKey** 原生冲突检测。
4. **M4 质量工程**：服务层真单测（接口注入假 Win32）、覆盖率门禁、CI/CD、**签名私钥出库**（新发现隐患，见 §7.4）。
5. **M5 性能收官**：空闲内存 220MB → **≤80MB**；冷启动 ≤3s → **≤1.2s**；安装包 81.6MB → **≤25MB**（自包含单文件）。
6. **M6 运营与演进**：MSIX/NSIS 双通道、诊断包、白名单策略深化、课堂场景联动（录屏自动归档到 U 盘等）。

### 1.3 目标架构一图

```
┌─────────────────────────────────────────────────────────────────────┐
│  SmartSideBAR.Avalonia (net10.0-windows, UI 层)                      │
│   MainWindow(侧栏轨+面板) · AnnotateWindow(透明置顶) · OverlayWindow │
│   OobeWindow(6步) · SettingsWindow · FloatBallWindow(透明点击穿透)   │
│   ★全部窗口经 WindowManager 集中管理；统一 Avalonia Pointer 触控     │
├─────────────────────────────────────────────────────────────────────┤
│  SmartSideBAR.Windows (net10.0-windows, 平台互操作层)                │
│   ★AppBarService(P/Invoke SHAppBarMessage + WndProc 通知闭环)        │
│   CaptureService(GDI BitBlt) · RecorderService(WinRT GraphicsCapture │
│     + Media Foundation MP4) · UsbService(WM_DEVICECHANGE)            │
│   ImeService(TSF/InputLanguage) · HotkeyService(RegisterHotKey)      │
│   PrinterService(System.Management WMI) · TrayService · ShellService │
├─────────────────────────────────────────────────────────────────────┤
│  SmartSideBAR.Core (net10.0, 平台无关层 —— 100% 可单测)              │
│   ConfigService(三层合并+源生成JSON) · SchedulerService(TimeProvider) │
│   PolicyService(白名单) · DiagnosticsService · AppState/EventBus     │
│   LinksService · ReminderService · Migrator(v1→v2 config)            │
├─────────────────────────────────────────────────────────────────────┤
│  SmartSideBAR.Tests (xunit)：Core 全覆盖 + Windows 层接口注入假Win32 │
└─────────────────────────────────────────────────────────────────────┘
     桥接：UI ↔ 服务 经 IMessenger(EventBus) / 直接接口调用（无 IPC 序列化边界）
```

> 对比 Electron 版：7 类 BrowserWindow → 进程内 7 类 Avalonia Window；76 个 IPC 通道 → 类型安全的服务接口调用 + EventBus 事件（映射全表见附录 A）。

---

## 2. 现状审计（2026-09-06 快照）

### 2.1 v1.2.0 完成度矩阵（承接审计文档 §1.3，标注迁移处置）

| # | 模块 | 完成度 | Electron 版遗留问题 | MLP 处置 |
|---|---|---|---|---|
| 1 | 输入法切换 | 85% | 常驻 PowerShell 守护（C5 修复后 <50ms） | §6.1 TSF/InputLanguage 原生化 |
| 2 | 区域截图 | 70% | 多显示器坐标偏移（A4 修复于 94bf2a1） | §6.2 GDI + 单坐标轨 |
| 3 | 屏幕批注 | 60% | **无触屏**（D1：仅 mouse* 绑定）；保存仅笔迹（D2→P1-8） | §6.3 Avalonia Pointer 天然触屏 |
| 4 | 长截图 | 65% | 跨屏/DPI 失效（A5/P0-7）；O(n²)（C3/P1-1） | §6.4 GDI+SkiaSharp 线性拼接 |
| 5 | 录屏 | 60% | **WebM 无转码**（D4）；fps/mic 曾硬编码（P1-7 修复） | §6.5 Media Foundation 原生 MP4 |
| 6 | U 盘监控 | 90% | WMI 事件 + PS 降级三层补丁 | §6.6 WM_DEVICECHANGE 零 PS |
| 7 | 打印机状态 | 45% | **状态映射错误**（E4/P1-4，未见完成标记） | §6.7 重写时用 DetectedErrorState |
| 8 | 任务管理器 | 100% | PATH 解析 taskmgr | §6.8 直接 Process.Start |
| 9 | 快捷链接 | 60% | E1 数据丢失（P1-5 修复） | §6.9 Core LinksService + 单测 |
| 10 | 定时提醒 | 75% | 3s 全量落盘（C1/P1-2） | §6.10 TimeProvider 可测调度 |
| 11 | OOBE | 90% | 已真实化（P1-12） | §6.11 Avalonia 向导页 |
| 12 | 设置 | 90% | 已统一（P1-6 SettingsForm） | §6.11 同构迁移 |
| 13 | 诊断 | 80% | 7 项真实探测（P1-12） | §6.12 结构保留 |
| 14 | 白名单策略 | 90% | 已落地（P2-1/P2-3） | §6.12 PolicyService |
| 15 | 快捷键槽位 | 90% | 3 槽位+冲突建议（P2-2） | §6.13 RegisterHotKey |
| 16 | 悬浮球 | 回归 | v1.2.0 重建（液态玻璃） | §6.14 透明窗口+点击穿透 |
| 17 | 托盘/退出 | ✅ | P2-7 完成 | §6.14 原生托盘 |
| 18 | **AppBar 防遮挡** | **60%** | **已集成但有结构性缺口（§5.1）** | **§5 完整重写（MLP 核心）** |

### 2.2 新发现的问题（本轮审计增量，编号续接 G 系列）

| ID | 问题 | 依据 | 严重度 |
|---|---|---|---|
| G1 | **代码签名私钥与密码提交进仓库**：`cert/SmartSideBAR-codesign.pfx`（3.5KB 私钥）与 `cert/pfx-password.txt`（526B 明文密码）被 git 跟踪 | `git ls-files cert/`；cert 目录列表 | **严重**——任何获得仓库访问权的人可签发可信代码；自签名根证书 `install-trust.cmd` 会在教师机导入信任根 |
| G2 | **服务层零单测**：测试仅 smoke.test.ts 5 用例（IPC 注册表完整性），3860 行主进程逻辑无测试 | tests/ 目录唯一文件 | 高 |
| G3 | **无 CI**：F2 修复明确「CI 除外」，无任何 workflow | 无 .github/ 目录 | 高 |
| G4 | **AppBar 通知链断裂**：`appbar.cc:33` 注册 `uCallbackMessage=WM_USER+1`，但 N-API 模块与 Electron 主进程均无 WndProc 接收该消息 | appbar.cc 全文；manager.ts 集成点无消息钩子 | 高（详见 §5.1） |
| G5 | **巨型文件持续膨胀**：main.ts 37.8KB / manager.ts 32.2KB（v1.1 审计时 12 文件 3860 行，现已更大） | src/main 实测 | 中 |
| G6 | **`aede938` 无关提交**："Update print statement from 'Hello' to 'Goodbye'" 混入主线 | git log | 低（卫生） |

### 2.3 性能/体积基线

| 指标 | Electron v1.2.0 | Avalonia 目标 | 依据 |
|---|---|---|---|
| 空闲内存 | 预算 ≤220MB（未实测） | **≤80MB** | 无 Chromium；单进程 |
| 冷启动至可交互 | 预算 ≤3s（未实测） | **≤1.2s** | 无 Chromium/V8/asar 初始化 |
| 安装包 | 81.6MB 实测 | **≤25MB** | 自包含+裁剪单文件 |
| 常驻进程数 | ≥3（main+gpu+renderer）+1 PS 守护 | **1** | |
| 原生模块构建 | node-gyp + electron-rebuild + asarUnpack | **零**（P/Invoke 编译期绑定） | |

---

## 3. 迁移决策与策略

### 3.1 为什么是 C# + Avalonia + .NET 10（论证）

| 维度 | Electron 30（现状） | Avalonia 11 + .NET 10（目标） | 对本项目的意义 |
|---|---|---|---|
| 常驻内存 | 3 进程 + Chromium ~150-220MB | 单进程 + Avalonia 渲染线程 ~50-80MB | 教学一体机资源释放 |
| Win32 互操作 | N-API C++ 桥（appbar.cc）或 koffi FFI | **P/Invoke 源生成（LibraryImport），编译期类型检查** | AppBar/WndProc/热键/DXGI 全部一等公民 |
| 窗口消息 | 主进程无消息循环钩子（G4 根因） | `Win32Window.AddWndProcCallback` 标准 API | **ABN 通知闭环成为可能** |
| 触屏输入 | 渲染层 mouse*/touch* 两套事件易漏（D1 实证） | **统一 Pointer 事件**（Avalonia 指针抽象合并鼠标/触摸/笔） | 批注触屏零成本修复 |
| 录屏 | MediaRecorder WebM（无 MP4） | **WinRT GraphicsCapture + Media Foundation（H.264 MP4）** | 课堂生态兼容（希沃白板/微信可直读） |
| 截图 | desktopCapturer 全屏流转 | GDI BitBlt / DXGI 按需 | 毫秒级、无编解码往返 |
| 坐标系统 | DIP + 物理像素双轨手动换算 | Avalonia 提供 `PixelPoint`/`Scaling` 显式 API，**可全链路物理像素单轨** | 消灭 A2/A4/A5 一类 DPI bug |
| 分发 | NSIS 81.6MB | 自包含单文件 ≤25MB / MSIX | U 盘/运维分发更轻 |
| 单测 | 需拉起 Electron | Core 层纯 .NET + Windows 层接口注入 | 服务层覆盖率可达 80%+ |

### 3.2 ADR 决策记录

| ID | 决策 | 理由 | 放弃的替代方案 |
|---|---|---|---|
| ADR-M1 | **绞杀者迁移**：新建 C# 解决方案与 Electron 版并存，逐模块对等后切换；Electron v1.2.0 冻结维护（仅安全补丁） | 教师已在用 v1.2.0，不能停摆；迁移可随时回退 | 一次性重写（风险大）；Electron 继续演进（S1-S5 成本永续） |
| ADR-M2 | UI 层选 **Avalonia 11**（非 WPF/WinUI3/MAUI） | 单项目即可覆盖侧栏/透明窗/置顶窗/托盘；XAML 与 Vue 组件心智接近；无 WinUI3 的 Win10 1809+ 限制 | WPF（仅 Windows 但生态老）；WinUI3（窗口形态受限：AppBar/点击穿透支持弱） |
| ADR-M3 | **Core 层 net10.0 纯净**（无 Win32 引用），平台细节全部收敛在 Windows 层接口后 | 服务逻辑 100% 可单测 | 单项目全塞（重蹈 manager.ts 覆辙） |
| ADR-M4 | 录屏用 **WinRT GraphicsCapture + Media Foundation**（非 FFmpeg 打包） | 零第三方原生依赖；系统级编码器；MP4 直出 | FFmpeg（+60MB 包体或外挂 exe） |
| ADR-M5 | USB 用 **WM_DEVICECHANGE + RegisterDeviceNotification**（非 WMI/PowerShell） | 原生消息驱动 ≤1s 延迟；零子进程 | WMI 事件（Electron 版已用，仍需 fallback） |
| ADR-M6 | IME 首版沿用 **InputLanguage/WM_INPUTLANGCHANGEREQUEST** 方案，TSF COM 完整方案列为 P2 | TSF COM 互操作量大；InputLanguage 已满足切换需求 | TSF ITfInputProcessorProfiles（P2 演进） |
| ADR-M7 | 分发 **NSIS 为主 + MSIX 可选**；签名沿用自签名链但**密钥全部出库**（§7.4） | 学校环境无商店；NSIS 免 PowerShell；MSIX 供愿意者选用 | Squirrel（已随 P2-6 移除自动更新承诺） |
| ADR-M8 | 配置三层结构与 SidekickConfig Schema **逐字段兼容**（附录 B），`migrateConfig()` 沿用 | 装机用户零感迁移 | 新 schema 断代 |

### 3.3 绞杀者迁移路线（总览）

```
阶段0（1d）   仓库治理：pfx 出库 + CI 骨架 + 分支策略（Electron: main 冻结 / avalonia: 主开发线）
阶段1 Wave A  C# 骨架 + AppBar 完整实现（独立 Demo 窗口可验证）+ 配置/日志/EventBus
阶段2 Wave B  常驻服务族：IME/USB/打印机/热键/调度/托盘 + 侧栏 UI 框架（rail/面板骨架）
阶段3 Wave C  捕获族：截图/批注/长截图/录屏 + OOBE/设置/悬浮球/诊断 → 功能对等点 FE-PARITY
阶段4 Wave E  双轨并行内测（Avalonia 版打 beta 标签）→ 达标切换为默认 → Electron 归档
```

**功能对等点（FE-PARITY）定义**：§2.1 表中 1-17 号模块在 Avalonia 版全部达到「Electron 版修复后」的行为，且附录 A 的 76 通道语义全有着落。

---

## 4. 目标架构

### 4.1 解决方案结构

```
SmartSideBAR/
├── src/
│   ├── SmartSideBAR.Core/               # net10.0（平台无关）
│   │   ├── Configuration/
│   │   │   ├── SidekickConfig.cs        # record 全量映射（附录 B）
│   │   │   ├── ConfigService.cs         # 三层合并：默认 → ProgramData → AppData
│   │   │   ├── ConfigMigrator.cs        # version 1→2 沿用 migrateConfig 语义
│   │   │   └── ConfigJsonContext.cs     # JSON 源生成
│   │   ├── Scheduling/
│   │   │   └── SchedulerService.cs      # TimeProvider 注入
│   │   ├── Policy/
│   │   │   └── PolicyService.cs         # disabledModules 白名单
│   │   ├── Links/
│   │   ├── Reminders/
│   │   ├── Diagnostics/
│   │   └── Messaging/
│   │       └── EventBus.cs              # 替代 IPC 事件推送
│   ├── SmartSideBAR.Windows/            # net10.0-windows（互操作层）
│   │   ├── AppBar/                      # ★ §5 完整实现
│   │   │   ├── AppBarInterop.cs         # LibraryImport P/Invoke
│   │   │   ├── IAppBarApi.cs            # 可注入接口（单测用）
│   │   │   └── AppBarService.cs         # 生命周期 + 通知状态机
│   │   ├── Capture/  (GdiCapture.cs)
│   │   ├── Recording/ (GraphicsCaptureRecorder.cs  → MP4)
│   │   ├── Devices/  (UsbDeviceWatcher.cs, PrinterMonitor.cs)
│   │   ├── Input/    (ImeService.cs, HotkeyService.cs)
│   │   ├── Shell/    (ShellService.cs, TrayService.cs, AutoLaunchService.cs)
│   │   └── Native/   (Win32WindowExtensions.cs, WinProcHook.cs)
│   └── SmartSideBAR.Avalonia/           # net10.0-windows（UI 层）
│       ├── App.axaml / WindowManager.cs
│       ├── Views/ (SidebarView, OobeView, SettingsView, AnnotateView, OverlayView, FloatBallView, RecorderHudView)
│       ├── ViewModels/ (每模块一个，调用服务接口)
│       └── Controls/ (GlassPanel 等液态玻璃控件)
├── tests/
│   ├── SmartSideBAR.Core.Tests/         # 纯逻辑全覆盖
│   └── SmartSideBAR.Windows.Tests/      # 假 IAppBarApi / 假时钟 / 注册表仿真
├── installer/ (SmartSideBar.iss NSIS)
└── .github/workflows/ci.yml             # §7.3
```

### 4.2 服务装配（Program.cs）

```csharp
// 文件: src/SmartSideBAR.Avalonia/Program.cs
using Microsoft.Extensions.DependencyInjection;

[STAThread]
public static void Main()
{
    var services = new ServiceCollection();
    services.AddSingleton<ConfigService>();
    services.AddSingleton<IEventBus, EventBus>();
    services.AddSingleton<SchedulerService>();
    services.AddSingleton<PolicyService>();
    // Windows 互操作层（接口 + 实现成对注册，测试可替换）
    services.AddSingleton<IAppBarApi, AppBarApi>();
    services.AddSingleton<AppBarService>();
    services.AddSingleton<ICaptureApi, GdiCapture>();
    services.AddSingleton<IRecorderApi, GraphicsCaptureRecorder>();
    services.AddSingleton<IUsbWatcher, UsbDeviceWatcher>();
    services.AddSingleton<IPrinterMonitor, PrinterMonitor>();
    services.AddSingleton<IImeService, ImeService>();
    services.AddSingleton<IHotkeyService, HotkeyService>();
    // …其余服务
    var sp = services.BuildServiceProvider();
    AppBootstrap.Run(sp);     // 初始化日志/配置迁移 → 启动 Avalonia
}
```

> 与 Electron 版差异：v1.2.0 的 `main.ts` bootstrap 顺序（单例锁→托盘→服务→窗口）在 `AppBootstrap` 同构保留，但**无 IPC 注册**——UI 与服务在同一进程直接调用类型安全接口。

---

## 5. AppBar 一等公民化（MLP 核心章节）

> 需求原点（用户原话）：「使用 AppBar API 以防止其它应用最大化时遮住侧栏；AppBar API 可以使侧栏拥有和 Win 任务栏类似的地位，使其成为桌面布局的一部分，将窗口缩放的工作交给 Windows 完成。」参考：[应用程序桌面工具栏（Microsoft Learn）](https://learn.microsoft.com/zh-cn/windows/win32/shell/application-desktop-toolbars)

### 5.1 现有实现剖析与缺口对照

v1.2.0 已完成：`ABM_NEW` 注册（manager.ts:198-212）、`ABM_QUERYPOS+ABM_SETPOS` 预留（appbar.cc:57-99）、`ABM_REMOVE` 注销（manager.ts:903-907）、`ABM_GETTASKBARPOS` 查询、`alwaysOnTop` fallback（manager.ts:171）。

| # | 缺口 | 现状证据 | 后果 | C# 方案 |
|---|---|---|---|---|
| K1 | **ABN_POSCHANGED 通知无人接收** | appbar.cc:33 注册 `uCallbackMessage`，但 N-API 无窗口过程，Electron 主进程未挂 WndProc | 任务栏换边/分辨率变化/其他 AppBar 注册注销时，**系统 WorkArea 已变而侧栏不自适应**，仅靠 Electron `display-metrics-changed`（250ms 防抖）间接触发 | §5.3 WndProc 挂接 + 通知状态机 |
| K2 | **ABM_ACTIVATE / ABM_WINDOWPOSCHANGED 未调用** | appbar.cc 仅 4 个导出函数 | 应用栏激活状态不上报系统，多 AppBar 排布（与任务栏同边）时 z 序错乱 | §5.3 生命周期钩子补齐 |
| K3 | **ABN_FULLSCREENAPP 未处理** | 同 K1 | 全屏应用（授课视频/电子教材）启动时侧栏仍占位或闪烁 | §5.3 全屏降级策略 |
| K4 | **坐标双轨**：AppBar 物理像素，Electron DIP | appbar.cc:11 注释明示；manager.ts:118-133 手动 `× scaleFactor` | 125%/150%/250% 非整数缩放下取整误差 → 窗口与预留区错位 | §5.4 物理像素单轨（PixelPoint） |
| K5 | **构建链脆弱** | rebuild-native.mjs + node-gyp + asarUnpack + 三处路径探测（appbar.ts:27-47） | Electron 升级 = ABI 失效 = AppBar 整体退化为 alwaysOnTop（防遮挡能力静默丢失） | P/Invoke 编译期绑定，零构建步骤 |
| K6 | **自动隐藏未实现** | 无 ABM_SETAUTOHIDEBAR 调用 | 侧栏常驻占用 64-88px 屏宽，小屏（1366×768）教师希望可让出 | §5.5 可选自动隐藏（P2 增强） |
| K7 | **多显示器 AppBar 语义** | manager.ts 仅主侧栏目标屏注册 | 副屏不预留（属合理默认），但显示器热插拔时注册不迁移 | §5.3 处理 WM_DISPLAYCHANGE 重注册 |

### 5.2 P/Invoke 层（AppBarInterop）

```csharp
// 文件: src/SmartSideBAR.Windows/AppBar/AppBarInterop.cs
namespace SmartSideBAR.Windows.AppBar;

/// <summary>Windows AppBar（应用栏）P/Invoke 常量与结构。参考:
/// https://learn.microsoft.com/zh-cn/windows/win32/shell/application-desktop-toolbars </summary>
internal static partial class AppBarInterop
{
    // ---- ABM_* 消息（SHAppBarMessage 第一个参数）----
    public const uint ABM_NEW = 0x0000;
    public const uint ABM_REMOVE = 0x0001;
    public const uint ABM_QUERYPOS = 0x0002;
    public const uint ABM_SETPOS = 0x0003;
    public const uint ABM_GETTASKBARPOS = 0x0005;
    public const uint ABM_ACTIVATE = 0x0006;              // K2：收到 WM_ACTIVATE 时上报
    public const uint ABM_GETAUTOHIDEBAR = 0x0007;
    public const uint ABM_SETAUTOHIDEBAR = 0x0008;        // K6：自动隐藏注册
    public const uint ABM_WINDOWPOSCHANGED = 0x0009;      // K2：收到 WM_WINDOWPOSCHANGED 时上报

    // ---- ABN_* 通知码（回调消息的 wParam）----
    public const uint ABN_STATECHANGE = 0x0000;           // 任务栏 总是置顶/自动隐藏 状态变化
    public const uint ABN_POSCHANGED = 0x0001;            // K1：任务栏/其他 AppBar 位置变化
    public const uint ABN_FULLSCREENAPP = 0x0002;         // K3：全屏应用打开/关闭
    public const uint ABN_WINDOWARRANGE = 0x0003;         // 任务栏「层叠/平铺」命令

    // ---- 边缘 ----
    public const uint ABE_LEFT = 0, ABE_TOP = 1, ABE_RIGHT = 2, ABE_BOTTOM = 3;

    /// <summary>应用栏回调消息 ID：WM_APP + 0xBAB（应用自定义区，避免与现有 WM_USER+1 冲突）</summary>
    public const uint AppBarCallbackMsg = 0x8000 + 0x0BAB;

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;                 // ABE_*
        public RECT rc;                    // 物理像素（K4：本方案全链路唯一坐标轨）
        public int lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [LibraryImport("shell32.dll")]
    internal static partial uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [LibraryImport("user32.dll")]
    internal static partial bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, [MarshalAs(UnmanagedType.Bool)] bool repaint);

    [LibraryImport("user32.dll")]
    internal static partial bool PostMessage(IntPtr hWnd, uint msg, nuint wParam, nint lParam);

    // 屏幕物理尺寸（物理像素，不经 DPI 换算 —— K4 单轨的度量基准）
    [LibraryImport("user32.dll")]
    internal static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [LibraryImport("user32.dll")]
    internal static partial bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public uint cbSize; public RECT rcMonitor; public RECT rcWork;
        public uint dwFlags;
    }
}
```

### 5.3 服务层（AppBarService：生命周期 + 通知状态机）

```csharp
// 文件: src/SmartSideBAR.Windows/AppBar/IAppBarApi.cs
namespace SmartSideBAR.Windows.AppBar;

/// <summary>Win32 AppBar API 抽象 —— 测试注入假实现（mock SHAppBarMessage 序列断言）。</summary>
public interface IAppBarApi
{
    bool Register(nint hwnd, uint callbackMsg);                       // ABM_NEW
    void Remove(nint hwnd);                                           // ABM_REMOVE
    AppBarRect QueryAndSetPos(nint hwnd, uint edge, AppBarRect want); // QUERYPOS→规整→SETPOS
    void Activate(nint hwnd);                                         // ABM_ACTIVATE
    void WindowPosChanged(nint hwnd);                                 // ABM_WINDOWPOSCHANGED
    TaskbarInfo? GetTaskbarPos();
}

public readonly record struct AppBarRect(int X, int Y, int W, int H);
public readonly record struct TaskbarInfo(uint Edge, AppBarRect Rect);
```

```csharp
// 文件: src/SmartSideBAR.Windows/AppBar/AppBarService.cs
namespace SmartSideBAR.Windows.AppBar;

/// <summary>
/// 侧栏应用栏编排器。修复 v1.2.0 缺口 K1/K2/K3/K7：
/// 通知经 Win32 消息钩子进入本类状态机，任务栏/分辨率/其他应用栏变化时自动重排。
/// 生命周期：Attach(注册+首次定位) ⇄ OnAppBarNotification(自适应) → Detach(注销)。
/// </summary>
public sealed class AppBarService(IAppBarApi api, IEventBus bus, ILogger<AppBarService> log)
{
    private nint _hwnd;
    private uint _edge = AppBarInterop.ABE_RIGHT;
    private int _widthPx = 64;            // rail 收起宽度（物理像素）
    private int _expandedPx = 420;        // 面板展开宽度（物理像素）
    private bool _registered;
    private bool _fullscreenActive;       // K3：全屏应用在场标志

    public bool IsRegistered => _registered;

    /// <summary>挂接：必须在窗口句柄可用后调用（Avalonia: window.TryGetPlatformHandle()）。</summary>
    public void Attach(Avalonia.Controls.Window window, uint edge, int railWidthPx, int expandedWidthPx)
    {
        _hwnd = window.TryGetPlatformHandle()!.Handle;
        _edge = edge; _widthPx = railWidthPx; _expandedPx = expandedWidthPx;

        if (!api.Register(_hwnd, AppBarInterop.AppBarCallbackMsg))
        { log.LogWarning("AppBar 注册失败（可能与他栏冲突），回退 alwaysOnTop 模式"); return; }
        _registered = true;

        // K2：挂接 WndProc（Avalonia Win32 标准扩展点），接管 ABN_* 通知与 WM_ACTIVATE
        WinProcHook.Add(window, OnWndProc);
        Reposition(_widthPx);
        log.LogInformation("AppBar 已注册 edge={Edge} rail={Rail}px", edge, railWidthPx);
    }

    /// <summary>重排：注册后 / 展开收起 / ABN_POSCHANGED / 分辨率与任务栏变化（K1/K4）。</summary>
    public void Reposition(int widthPx)
    {
        if (!_registered) return;
        var monitor = AppBarInterop.MonitorFromWindow(_hwnd, /*MONITOR_DEFAULTTONEAR*/ 2);
        var mi = new AppBarInterop.MONITORINFO { cbSize = (uint)Marshal.SizeOf<AppBarInterop.MONITORINFO>() };
        AppBarInterop.GetMonitorInfo(monitor, ref mi);
        // 期望矩形：贴目标边缘、全高（物理像素 —— 与系统协商的唯一语言）
        var want = _edge switch
        {
            AppBarInterop.ABE_LEFT   => new AppBarRect(mi.rcWork.Left, mi.rcWork.Top, widthPx, mi.rcWork.Bottom - mi.rcWork.Top),
            AppBarInterop.ABE_RIGHT  => new AppBarRect(mi.rcWork.Right - widthPx, mi.rcWork.Top, widthPx, mi.rcWork.Bottom - mi.rcWork.Top),
            AppBarInterop.ABE_TOP    => new AppBarRect(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right - mi.rcWork.Left, widthPx),
            _                         => new AppBarRect(mi.rcWork.Left, mi.rcWork.Bottom - widthPx, mi.rcWork.Right - mi.rcWork.Left, widthPx),
        };
        var granted = api.QueryAndSetPos(_hwnd, _edge, want);   // 系统审批（避让任务栏/他栏）
        api.WindowPosChanged(_hwnd);                            // K2：位置变更上报
        // 用系统授予的矩形驱动窗口 —— 与 Electron 版不同，这里直接 MoveWindow 物理像素，无 DIP 换算（K4）
        AppBarInterop.MoveWindow(_hwnd, granted.X, granted.Y, granted.W, granted.H, true);
        bus.Publish(new AppBarGeometryChanged(granted));
    }

    /// <summary>K1/K2/K3 核心：WndProc 状态机 —— v1.2.0 完全缺失的通知闭环。</summary>
    private nint OnWndProc(nint hwnd, uint msg, nuint wParam, nint lParam, ref bool handled)
    {
        if (msg == AppBarInterop.AppBarCallbackMsg)
        {
            switch ((uint)wParam)
            {
                case AppBarInterop.ABN_POSCHANGED:              // 任务栏换边/他栏变化/WorkArea 变
                    Reposition(CurrentWidthPx());
                    break;
                case AppBarInterop.ABN_FULLSCREENAPP:           // 授课视频等全屏应用开/关
                    _fullscreenActive = lParam != 0;
                    bus.Publish(new FullscreenAppChanged(_fullscreenActive));
                    // 策略：全屏在场时保持注册（教师仍可唤出）但收起到 rail 宽度
                    if (_fullscreenActive) Reposition(_widthPx);
                    break;
                case AppBarInterop.ABN_STATECHANGE:             // 任务栏「自动隐藏/总在顶部」切换
                    Reposition(CurrentWidthPx());
                    break;
                case AppBarInterop.ABN_WINDOWARRANGE:           // 层叠/平铺命令：TRUE=即将排列(隐藏) FALSE=完成(显示)
                    bus.Publish(new WindowArrangePending(wParam != 0));
                    break;
            }
            handled = true;
            return 0;
        }
        if (msg == 0x0006 /*WM_ACTIVATE*/ && _registered)
        {
            api.Activate(_hwnd);                                // K2：激活状态上报（多 AppBar z 序维护）
        }
        return 0;   // 不拦截其余消息
    }

    /// <summary>退出/窗口销毁前必须调用 —— 否则系统 WorkArea 永久泄漏（v1.2.0 已正确处理，保留）。</summary>
    public void Detach()
    {
        if (!_registered) return;
        WinProcHook.Remove(_hwnd);
        api.Remove(_hwnd);
        _registered = false;
        log.LogInformation("AppBar 已注销，系统 WorkArea 恢复");
    }

    private int CurrentWidthPx() => _expanded ? _expandedPx : _widthPx;
    private bool _expanded;
    public void SetExpanded(bool expanded) { _expanded = expanded; Reposition(CurrentWidthPx()); }
}

// EventBus 事件（UI 层订阅以同步面板布局）
public sealed record AppBarGeometryChanged(AppBarRect Rect);
public sealed record FullscreenAppChanged(bool Active);
public sealed record WindowArrangePending(bool Hiding);
```

```csharp
// 文件: src/SmartSideBAR.Windows/Native/WinProcHook.cs
// Avalonia.Win32 提供 IWindowImpl.AddWndProcCallback —— 官方消息钩子入口
namespace SmartSideBAR.Windows.Native;

public static class WinProcHook
{
    private static readonly ConditionalWeakTable<Window, List<WndProcCallback>> Hooks = new();

    public static void Add(Window window, WndProcCallback callback)
    {
        var impl = window.TryGetPlatformHandle()?.PlatformImpl as Win32.IWindowImpl
            ?? throw new InvalidOperationException("仅支持 Win32 平台");
        impl.AddWndProcCallback((hwnd, msg, wParam, lParam, handled) =>
            callback(hwnd, msg, wParam, lParam, ref handled), IntPtr.Zero);
    }
    // Remove：窗口销毁时随 impl 释放（Detach 中显式调用以保确定时）
    public static void Remove(nint hwnd) { /* 从注册表清除该 hwnd 的回调 */ }
}

public delegate nint WndProcCallback(nint hwnd, uint msg, nuint wParam, nint lParam, ref bool handled);
```

### 5.4 坐标单轨策略（K4 修复）

**规则：与系统协商一律物理像素；Avalonia 窗口定位一律 `PixelPoint`/`PixelRect`。**

```csharp
// WindowManager 中侧栏窗口的创建与定位（节选）
sidebar.WindowStartupLocation = WindowStartupLocation.Manual;
sidebar.SystemDecorations = SystemDecorations.None;
sidebar.ShowInTaskbar = false;
sidebar.Topmost = false;                       // AppBar 模式下不需要置顶（系统保证不被最大化遮盖）
// v1.2.0 的 alwaysOnTop 仅在 Attach 注册失败时启用（fallback 保持一致）
if (!appBar.IsRegistered) sidebar.Topmost = true;

// 展开动画期间的高频定位也走物理像素（PixelPoint 消除 DIP 取整误差）
void OnExpanded(bool open)
{
    appBar.SetExpanded(open);                  // MoveWindow 由 AppBarService 统一执行
    // 面板内容宽度 = granted.W - railWidth（从 AppBarGeometryChanged 事件取，不自算）
}
```

DPI 变化时**不需要手动换算**：`ABN_POSCHANGED` 会随 `WM_DISPLAYCHANGE` 到达 → `Reposition` 以新 MonitorInfo 重新协商 → `MoveWindow` 物理像素落位。Avalonia 内部按 `Scaling` 渲染，业务层零换算。

### 5.5 自动隐藏（K6，P2 可选增强）

```
注册：ABM_SETAUTOHIDEBAR（每边仅允许 1 个自动隐藏应用栏；返回失败则降级常驻模式）
行为：鼠标离开 → 隐藏为 2px 触发条（自己管理，不走系统动画）；
      鼠标贴边 → ABM_ACTIVATE + 展开到 rail 宽；
      注意：自动隐藏栏占用不进 WorkArea（教师课件可获得全屏面积）
验收：1366×768 小屏实测——隐藏后最大化窗口满屏；贴边 200ms 内唤出。
```

### 5.6 验收清单（Wave A 出口条件）

| # | 场景 | 预期 |
|---|---|---|
| V1 | 任一应用最大化 | 工作区自动避让侧栏，**侧栏完整可见**（核心需求） |
| V2 | 任务栏移到左缘/上缘 | 侧栏自动让位（ABN_POSCHANGED → Reposition），无重叠无缝隙 |
| V3 | 100%/125%/150%/200%/250% 缩放 | 侧栏贴边像素级对齐（±0px），无 DIP 取整错位 |
| V4 | 双显示器 + 热插拔目标屏 | 监听 WM_DISPLAYCHANGE 重注册（K7） |
| V5 | 全屏授课视频播放 | ABN_FULLSCREENAPP 到达；侧栏收起为 rail；退出全屏自动恢复 |
| V6 | 进程被任务管理器强杀 | ⚠️ 已知边界：强杀不保证 ABM_REMOVE → 手动提供「修复侧栏」（下次启动时检测孤儿注册并补注销） |
| V7 | 与第三方 AppBar（如 PowerToys Run 常驻/TranslucentTB）同边共存 | 系统自动分栏，无死锁 |
| V8 | 退出应用 | WorkArea 完全恢复（对比退出前后 `SystemInformation.WorkingArea`） |

> V6 说明：Windows 对强杀进程的 AppBar 注册会在 Explorer 重建/注销时清理，但可能滞后。`AppBarService.Attach` 启动时先对同 callbackMsg 的陈旧注册补发 `ABM_REMOVE`（以自愈）。

---

## 6. 模块迁移设计（15 模块逐一对等）

> 每模块格式：**现状（依据）→ C# 方案 → 关键代码/接口 → 顺带修复的审计项**。完整 IPC 映射见附录 A。

### 6.1 输入法切换（ImeService）

**现状**：常驻 PowerShell 守护（stdin 命令→stdout JSON，C5 修复后 <50ms）；`config.ime.slot1/slot2` 仍未消费。
**C# 方案（ADR-M6）**：`System.Windows.Forms.InputLanguage` 枚举 + 前台窗 `WM_INPUTLANGCHANGEREQUEST` 广播切换。

```csharp
public sealed class ImeService : IImeService
{
    public ImeState GetState()
    {
        var lang = InputLanguage.CurrentInputLanguage;     // 线程键盘布局
        bool zh = lang.Culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        return new ImeState(lang.Culture.Name, zh, zh ? ImeMode.Cn : ImeMode.En);
    }

    public void Toggle()
    {
        var target = FindNext();                            // slot1→slot2 轮换（终于消费 config.ime.slot1/2）
        PostMessage(GetForegroundWindow(),
            0x0050 /*WM_INPUTLANGCHANGEREQUEST*/, 0,
            (nint)LoadKeyboardLayout(target.KbdLayout, 0));
    }
    // 事件：钩子 WH_SHELL/HSHELL_LANGUAGE → bus.Publish(new ImeChanged(state))（替代 ime:changed 通道）
}
```
**顺带修复**：slot1/slot2/fallbackSwap 配置终于生效（审计 1.1 #1 遗留）；延迟从 <50ms → <5ms；零子进程。

### 6.2 区域截图（GdiCapture + OverlayView）

**现状**：desktopCapturer 全屏流转 + 临时内存图；A4 多屏偏移已修（94bf2a1）。
**C# 方案**：OverlayView（透明全屏窗）选框 → GDI 按屏 BitBlt → 裁剪即所得。

```csharp
public sealed class GdiCapture : ICaptureApi
{
    /// <summary>物理像素直接裁剪 —— 与选框同一坐标系，A4 类偏移在结构上不可能复现。</summary>
    public Bitmap CaptureRegion(PixelRect regionPx)
    {
        var bmp = new Bitmap(regionPx.Width, regionPx.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        using var dc = g.GetHdc();
        BitBlt(dc, 0, 0, regionPx.Width, regionPx.Height,
               GetDC(IntPtr.Zero), regionPx.X, regionPx.Y, /*SRCCOPY|CAPTUREBLT*/ 0x00CC0020 | 0x40000000);
        g.ReleaseHdc(dc);
        return bmp;
    }
}
```
选框 View 复用 v1.2.0 的 4px/80ms 触屏抖动过滤参数（OverlayApp.vue 已验证）。

### 6.3 屏幕批注（AnnotateView —— 顺带修复 D1/D2/D3）

**现状**：AnnotateApp 曾仅 mouse* 绑定（D1，P0-5 修复中）；保存仅笔迹（D2→P1-8）；undoStack 非响应式（D3）。
**C# 方案**：Avalonia **PointerPressed/Moved/Released 统一指针**——鼠标/触摸/笔天然同一事件流，D1 从根上消失；撤销栈为 `ObservableCollection`（D3 消失）；导出双模式（仅笔迹/含背景合成）。

```csharp
// AnnotateCanvas（自定义控件节选）
protected override void OnPointerPressed(PointerPressedEventArgs e)
{
    var p = e.GetCurrentPoint(this);
    if (!p.Pointer.Type.IsTouchOrPenOrMouse()) return;    // 全类型接受 —— D1 根修
    _strokes.StartNew(p.Position, MapPressure(e));        // 触摸压力→笔宽（进阶：e.Pointer.Type == Pen）
}
// 导出（P1-8 语义）：mode = StrokesOnly | WithBackground
public void Export(string path, ExportMode mode, Bitmap? background)
```

### 6.4 长截图（LongshotService —— 顺带修复 A5/C3 根因）

**现状**：窗口枚举 + PageDown + 相似度拼接；曾硬取 `sources[0]`（A5）与 O(n²) metadata（C3）。
**C# 方案**：`EnumWindows` 定位目标窗口（跨屏/DPI 用 `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)` 物理像素矩形）→ 定时 BitBlt 滚动帧 → SkiaSharp 拼接（**线性增量**：只比对新帧底部与拼接尾部哈希，O(n)）。

```csharp
public sealed class LongshotService(IEventBus bus) : ILongshotService
{
    public async Task<LongshotResult> CaptureAsync(TargetWindow win, CancellationToken ct)
    {
        var tail = new SKBitmap(); int committedH = 0;
        foreach (var frame in ScrollFrames(win, ct))       // PageDown + 稳定检测（沿 v1.2.0 节奏参数）
        {
            var overlap = FindOverlap(tail, frame);        // 增量比对：仅尾行哈希窗口
            committedH += frame.Height - overlap;
            tail = Stitch(tail, frame, overlap);           // Skia 画布追加 —— O(n)
            bus.Publish(new LongshotProgress(committedH)); // longshot:progress 对应事件
        }
        return Save(tail, _cfg.Capture.Dir);               // PNG/JPEG 按 config.capture.format
    }
}
```

### 6.5 录屏（GraphicsCaptureRecorder —— 顺带修复 D4/D5/D6 根因）

**现状**：隐藏窗口 MediaRecorder WebM；无 MP4 转码（D4）；曾 fps/mic 硬编码（P1-7 修复）。
**C# 方案（ADR-M4）**：**WinRT `GraphicsCapturePicker/Item` + Media Foundation H.264 编码器 → MP4 直出**；`config.recorder.fps/bitrate/mic/dir` 全消费；系统麦克风可选混入。

```csharp
public sealed class GraphicsCaptureRecorder : IRecorderApi, IDisposable
{
    // Media Foundation 管线（节选）：捕获帧 → H.264 编码 → MP4 容器
    public async Task StartAsync(RecorderOptions opt, CancellationToken ct)
    {
        _sink = await MediaSink.CreateAsync(opt.FilePath /* .mp4 */,
                     videoEncoding: H264Profiles.From(bitrateKbps: opt.Bitrate, fps: opt.Fps),
                     audio: opt.Mic ? await MicrophoneSource.CreateAsync() : null);
        _item = await GraphicsCaptureItem.CreateForMonitorAsync(_cfg.TargetMonitor);
        _item.FrameArrived += (s, e) => _sink.SubmitFrame(e.Frame);   // 帧直送，零中间文件
        _item.StartCapture();
    }
    public Task StopAsync() { _item.Stop(); return _sink.FinalizeAsync(); }  // MP4 收尾
    // 状态事件 → recorder:statusChanged 对应 EventBus 广播（含 filepath —— D6 修复）
}
```
**收益**：MP4 直出（希沃白板/微信可直读）；无隐藏 BrowserWindow；`Windows.Graphics.Capture` 有系统黄框提示（Win11 可关，教学场景可配置）。

### 6.6 U 盘监控（UsbDeviceWatcher —— 顺带修复 C2 根因）

**现状**：WMI 事件订阅 + 健康巡检 + PS 降级三层（usb.ts 537 行）。
**C# 方案（ADR-M5）**：主窗口接收 **WM_DEVICECHANGE**（`RegisterDeviceNotification` 订阅 DBT_DEVTYP_VOLUME）→ 立即扫描盘符（`DriveInfo` + `Get-Disk.BusType` 等价 C#：`DeviceIoControl(IOCTL_STORAGE_QUERY_PROPERTY)` 取 BusType）。

```csharp
public sealed class UsbDeviceWatcher : IUsbWatcher
{
    public void Start(Window host)
    {
        var hwnd = host.TryGetPlatformHandle()!.Handle;
        RegisterDeviceNotification(hwnd, DBT_DEVTYP_VOLUME, DEVICE_NOTIFY_WINDOW_HANDLE);
        WinProcHook.Add(host, OnDeviceChange);     // 复用 §5.3 同一钩子基础设施
    }
    private nint OnDeviceChange(nint hwnd, uint msg, nuint wParam, nint lParam, ref bool handled)
    {
        if (msg != 0x0219 /*WM_DEVICECHANGE*/) return 0;
        if (wParam == 0x8000 /*DBT_DEVICEARRIVAL*/ || wParam == 0x8004 /*DBT_DEVICEREMOVECOMPLETE*/)
        {
            var drive = ParseVolumeLetter(lParam);
            var info = QueryDrive(drive);          // BusType/容量/卷标 —— 沿 v1.2.0 移动硬盘识别逻辑
            bus.Publish(wParam == 0x8000 ? new UsbArrived(info) : new UsbRemoved(info));
        }
        handled = true; return 0;
    }
}
```
**收益**：≤1s 延迟（原 ≤2s 承诺）；**零 PowerShell/零 WMI**（C2 的三层补丁整体退役）；`ignoreTypes` 过滤逻辑保留。

### 6.7 打印机状态（PrinterMonitor —— 顺带修复 E4 根因）

**现状**：Win32_Printer 轮询，但 E4 状态映射错误（`PrinterState===3` 判 ok，实为卡纸；位掩码误用于 PrinterStatus）且未见 P1-4 完成标记。
**C# 方案**：System.Management 同源 WMI，**映射表重写**：

```csharp
public static PrinterState MapState(uint detectedErrorState, ushort printerStatus)
{
    // Win32_Printer.DetectedErrorState 位掩码（正确字段！E4 根修）
    if ((detectedErrorState & 0x80) != 0) return PrinterState.Offline;      // 响应错误/离线
    if ((detectedErrorState & 0x200000) != 0) return PrinterState.LowInk;   // 墨量低
    if ((detectedErrorState & 0x40) != 0) return PrinterState.OutOfPaper;   // 缺纸
    if ((detectedErrorState & 0x02) != 0 || (detectedErrorState & 0x1000) != 0)
        return PrinterState.Jammed;                                         // 卡纸/装纸问题
    return printerStatus == 3 ? PrinterState.Idle : PrinterState.Ok;        // 3=Idle（不再误判卡纸）
}
```
轮询间隔 `config.printer.pollIntervalSec` 消费；状态变化 → `bus.Publish(new PrinterChanged(...))`。

### 6.8 任务管理器 / Shell / 自启（ShellService）

```csharp
public sealed class ShellService : IShellService
{
    public void OpenTaskManager() => Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
    public void OpenExternal(Uri url)
    {
        if (!IsAllowed(url)) throw new SecurityException($"URL 不在白名单: {url}");  // B2 根修
        Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });
    }
    public void SetAutoLaunch(bool enable)
    {
        // HKCU\...\Run（asInvoker 权限足够 —— P1-10 的普通用户路径延续）
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enable) key.SetValue("SmartSideBAR", $"\"{Environment.ProcessPath}\" /silent");
        else key.DeleteValue("SmartSideBAR", throwOnMissingValue: false);
    }
    // IsAllowed：https/http + 域名白名单（config.links 与内置运维页），杜绝任意协议拉起
}
```

### 6.9 快捷链接 / 6.10 定时提醒（Core 层，100% 单测）

- **LinksService**（Core）：CRUD + 排序 + `enabled && url非空` 过滤仅在**读取视图**，**保存全量回写**（E1/P1-5 语义固化并加单测）。
- **SchedulerService**（Core）：`TimeProvider` 注入；`once/interval/hourly` 三类 + 贪睡 + 顺延（v1.2.0 行为）；**变更检测落盘**（C1/P1-2：仅 reminders 哈希变化时写盘）；过期周期提醒启动时**不补触发**（审计 §1.3 #10 遗留）。
- 铃声：`NAudio` 播放 MP3/预设（替代 Web Audio），音量/重复次数消费 `reminderSound` 全字段。

### 6.11 OOBE / 设置（Avalonia ViewModels）

- OOBE 6 步流程、角色裁剪（prefs→disabledModules 映射，P2-3 语义）、环境检测（屏幕/触控/打印机/输入法真实探测，P1-12 语义）逐项对等迁移；`OobeState` 结构不变（附录 B）。
- Settings：`SettingsForm` 唯一表单语义保留（P1-6）；3 热键槽位 + 冲突替代建议（P2-2）。

### 6.12 诊断 / 白名单策略（Core）

- `DiagnosticsService`：7 项真实探测对等迁移（各服务 `GetStatus()` 聚合）；导出诊断包（report.json + 日志 zip）。
- `PolicyService`：`disabledModules` 驱动服务启动跳过 + rail 图标过滤（P2-1/P2-3 语义）。

### 6.13 全局热键（HotkeyService）

```csharp
public sealed class HotkeyService : IHotkeyService
{
    private const uint WM_HOTKEY = 0x0312;
    public RegisterResult Register(int slot, HotkeyCombination combo)
    {
        // RegisterHotKey 原生返回 false = 冲突 —— P2-2 的「冲突检测」从启发式变为系统事实
        if (!RegisterHotKey(_hwnd, slot, ModifiersOf(combo), VkOf(combo)))
            return RegisterResult.Conflicted(SuggestAlternative(combo));   // 邻近组合建议算法沿用 v1.2.0
        _slots[slot] = combo; return RegisterResult.Ok();
    }
    // WM_HOTKEY 经 §5.3 WinProcHook 路由到 bus.Publish(new HotkeyPressed(slot))
}
```

### 6.14 悬浮球 / 托盘

- 悬浮球：Avalonia 透明无边框 + `WS_EX_TRANSPARENT`（点击穿透窗口区域切换，对应 `floatball:setClickThrough`）+ 拖拽吸附（`floatball-layout.ts` 布局算法迁移为 Core 的 `FloatBallLayout`）。
- 托盘：Avalonia `TrayIcon`（显示侧边栏/打开设置/退出 —— P2-7 语义）。

---

## 7. 质量工程

### 7.1 测试金字塔

| 层 | 工具 | 覆盖对象 | 目标 |
|---|---|---|---|
| 单元（Core） | xunit | 配置合并/迁移、调度器（FakeTimeProvider）、链接 CRUD、策略过滤、布局算法 | **line ≥85%** |
| 单元（Windows 层假件） | xunit + 假 IAppBarApi/ICaptureApi | AppBarService 状态机（注册→POSCHANGED→重排→注销序列断言）、热键映射、打印机映射表 | **line ≥80%** |
| 契约 | xunit | 附录 A 全表：每通道语义在 C# 服务 API 有断言（防迁移遗漏） | 76/76 |
| E2E 冒烟 | 手动清单（docs/testing.md 沿用）+ 可选 FlaUI | §5.6 V1-V8 + 6 条主链路 × DPI 矩阵 | 全绿记录入库 |

```csharp
// AppBarService 状态机单测示例（Electron 版做不到的事）
[Fact]
public void Taskbar_moved_to_left_edge_repositions_to_right()
{
    var api = new FakeAppBarApi();            // 记录 SHAppBarMessage 调用序列
    var svc = new AppBarService(api, new FakeBus(), NullLogger<AppBarService>.Instance);
    svc.AttachFake(hwnd: 0x1234, edge: ABE_RIGHT, rail: 64, expanded: 420);
    api.SimulateNotification(ABN_POSCHANGED); // 模拟任务栏换边
    Assert.Equal(ABE_RIGHT, api.LastQueryEdge);            // 重协商发生
    Assert.Equal(2, api.QueryCalls);                        // QUERYPOS 被再次调用
    Assert.Contains(api.MoveWindowCalls, m => m.W == 64);   // 以 rail 宽落位
}
```

### 7.2 静态分析

`dotnet format` + analyzers（`.editorconfig` 强制 sealed/CA 规则集）+ `gitleaks` 扫描（§7.4）。

### 7.3 CI/CD（GitHub Actions，修复 G3）

```yaml
name: CI
on:
  push: { branches: [main, avalonia] }
  pull_request:
  workflow_dispatch:

jobs:
  build-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - run: dotnet format --verify-no-changes --no-restore   # 格式门禁
      - run: dotnet test -c Release --collect:"XPlat Code Coverage"
      - run: |                                                # 覆盖率门禁 ≥80%
          dotnet tool install -g dotnet-reportgenerator-globaltool
          reportgenerator -reports:**/coverage.cobertura.xml -targetdir:cover -reporttypes:TextSummary
          $line=[regex]::Match((Get-Content cover/Summary.txt -Raw),'Line coverage:\s+([\d.]+)%').Groups[1].Value
          if ([double]$line -lt 80) { throw "coverage $line% < 80%" }
      - run: dotnet publish src/SmartSideBAR.Avalonia -c Release -r win-x64
               --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true
               -o artifacts/app
      - uses: actions/upload-artifact@v4
        with: { name: app-win-x64, path: artifacts/app }

  release:                                                    # tag v2* 触发：NSIS 打包 + 签名 + Release
    needs: build-test
    if: startsWith(github.ref, 'refs/tags/v2')
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/download-artifact@v4
        with: { name: app-win-x64, path: artifacts/app }
      - name: Sign (signtool, 证书从 secrets 注入 —— 绝不再入仓库)
        env:
          PFX_B64: ${{ secrets.CODESIGN_PFX_B64 }}
          PFX_PASS: ${{ secrets.CODESIGN_PFX_PASS }}
        run: |
          [IO.File]::WriteAllBytes("cert.pfx",[Convert]::FromBase64String($env:PFX_B64))
          signtool sign /f cert.pfx /p $env:PFX_PASS /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
            artifacts/app/SmartSideBAR.exe
          Remove-Item cert.pfx
      - name: NSIS installer
        run: makensis installer/SmartSideBar.iss
      - uses: softprops/action-gh-release@v2
        with: { draft: true, files: installer/Output/*.exe }
```

### 7.4 签名与密钥治理（修复 G1 —— 最高优先安全项）

| # | 动作 | 说明 |
|---|---|---|
| 1 | **吊销并重签**：生成全新代码签名密钥对；旧 `SmartSideBAR-codesign.pfx` 视为已泄露，其对应根证书从所有装机移除指引（下个安装包提供 `uninstall-trust.cmd`） | 私钥已入 git 历史，仅删文件无效 |
| 2 | **历史清除评估**：`git filter-repo` 重写历史移除 cert/（需协调所有 clone；若仓库仅内部小范围使用可选只吊销不重写） | 二选一，默认吊销+重签 |
| 3 | 新私钥进 CI secrets（base64）；本地签名用证书存储区（`signtool /sha1`） | 零私钥文件落盘 |
| 4 | `gitleaks` 进 CI 前置 job；`.gitignore` 增加 `*.pfx`/`*.cer`/`*password*` | 防再犯 |
| 5 | `install-trust.cmd`（导入自签根）改为安装包内显式选项 + 卸载清理 | 最小信任面 |

---

## 8. 性能预算与验收（M5）

| 指标 | v1.2.0 基线 | 目标 | 测法 |
|---|---|---|---|
| 空闲内存（工作集） | ≤220MB 预算/未实测 | **≤80MB** | 任务管理器 10 分钟采样（沿用 docs/testing.md 清单） |
| 冷启动至 rail 可见 | ≤3s 预算/未实测 | **≤1.2s** | Stopwatch 埋点 + ETL（可选 PerfView） |
| 触控响应 | ≤120ms 中位数 | **≤60ms** | 输入延迟脚本（docs/testing.md P2-8 脚本迁移） |
| AppBar 自适应（任务栏换边→侧栏就位） | 不支持（K1） | **≤200ms** | §5.6 V2 计时 |
| U 盘事件延迟 | ≤2s | **≤1s** | WM_DEVICECHANGE 直达 |
| 安装包 | 81.6MB | **≤25MB** | publish 产物实测 |
| 常驻子进程 | 1（PS 守护） | **0** | process explorer 快照 |

---

## 9. 风险登记册

| 风险 | 等级 | 缓解 | 止损 |
|---|---|---|---|
| 迁移周期内 Electron 版出现严重 bug | 中 | v1.2.0 冻结但保留 hotfix 分支；FE-PARITY 前不卸任 | Avalonia 版提前切换核心三模块（IME/USB/截图）以独立进程形态救急 |
| Avalonia 透明窗/点击穿透在旧 GPU 驱动上的渲染异常 | 中 | 目标机型清单（希沃 OPS 常见型号）先行 spike 验证 | 降级：悬浮球改用 WS_EX_LAYERED 原生窗口承载图标 |
| GraphicsCapture 在 Win10 1809 以下不可用 | 低 | 系统要求本就 Win10+；运行时 `ApiInformation.IsMethodPresent` 探测降级 GDI 录制（AVI）+ 明确提示 | 仅提示不支持，不阻塞其他模块 |
| AppBar 与第三方软件（TranslucentTB 等）冲突 | 低 | §5.6 V7 共存测试；注册失败回退 alwaysOnTop（v1.2.0 同策略） | 回退置顶模式并在诊断包上报 |
| Trimmed 发布反射裁剪破坏 JSON/插件 | 中 | 源生成 JSON 已规避；CI 冒烟跑 Trimmed 产物 | 关闭 PublishTrimmed（包体 +~10MB 可接受） |
| 双轨期配置漂移（Electron/Avalonia 同时写 ProgramData） | 高 | 切换脚本：安装 Avalonia 版前检测 Electron 版进程并提示退出；共享同一 config 路径与 schema（附录 B） | 检测到双写冲突时以 mtime 新者为准并落诊断日志 |
| 单人开发战线过长 | **高** | Wave 划分可独立交付；每 Wave 结束都有「可安装可演示」产物 | 优先级重排：捕获族（Wave C）可无限期后置，常驻服务族（Wave B）先行 |

---

## 10. 里程碑与行动清单

### 10.1 Wave 划分（单人估算，含自测）

| Wave | 内容 | 工期 | 出口条件 |
|---|---|---|---|
| **Wave 0 仓库治理** | G1 密钥吊销重签、CI 骨架（先跑 Electron 版 typecheck/lint/test）、分支策略（main 冻结 / avalonia 主线）、G6 清理 | 1-2d | gitleaks 零命中；CI 绿；新签名链就绪 |
| **Wave A 骨架+AppBar** | 三项目解决方案 + DI/日志/EventBus/配置迁移 + **AppBarService 完整实现（§5）** + Demo 窗口 | 4-5d | §5.6 V1-V8 全绿；config.json 读写与 v1.2.0 互认 |
| **Wave B 常驻服务族** | IME/USB/打印机/热键/调度/链接/策略/托盘 + 侧栏 rail/面板 UI 骨架 | 8-10d | 6 个常驻服务真机 72h 稳定（零 PS 子进程）；单测 ≥60 用例 |
| **Wave C 捕获族+UX** | 截图/批注/长截图/录屏(MP4)/OOBE/设置/悬浮球/诊断 | 10-12d | FE-PARITY：附录 A 76 通道全有着落；docs/testing.md 6 链路 × DPI 矩阵全绿 |
| **Wave D 质量收官** | 覆盖率 80% 门禁、性能预算实测（§8）、Trimmed 产物验证、诊断包完善 | 4-5d | CI 全门禁绿；性能表全部实测回填 |
| **Wave E 发布切换** | NSIS/签名/Release v2.0-beta → 稳定；Electron 归档公告；迁移文档 | 3-4d | 双轨内测 1 周 → Avalonia 版设为默认下载 |

### 10.2 今天可做的 7 件事

1. **G1 应急**：生成新代码签名密钥；把 `cert/` 从工作区移除并加入 `.gitignore`（吊销动作同步决策历史重写与否）。
2. 建 `avalonia` 分支 + 三项目解决方案骨架（§4.1 目录树）。
3. 把 `src/native/appbar.cc` 的缺口清单（§5.1）落为 issue（K1-K7），标 Wave A。
4. 写 `IAppBarApi` 假件 + `AppBarService` 状态机测试骨架（§7.1 示例）—— 先测后码。
5. Electron 版 CI 最小化：`.github/workflows/ci.yml` 跑 typecheck+lint+test（即使迁移中，旧版也要有门禁）。
6. `docs/testing.md` 追加 §5.6 V1-V8 AppBar 验收清单（对 Electron 版先跑一遍，记录 K1 缺口的实际表现作对照基线）。
7. 决策录（ADR-M1~M8）团队评审确认——特别是录屏 MP4（ADR-M4）与绞杀者节奏（ADR-M1）。

---

*附录 A（76 IPC 通道 → C# API 全表）、附录 B（配置 Schema 映射与迁移）、附录 C（签名治理操作手册）如下。*

---

## 附录 A：IPC 通道 → C# 服务 API 映射全表

> 迁移语义：Electron 的 `ipcRenderer.invoke(ch, args)`（请求-响应）→ C# **服务接口方法直接调用**（同进程，无序列化）；`ipcRenderer.on(ch, cb)`（主进程推送事件）→ **EventBus 事件订阅**。FE-PARITY 验收 = 下表 76 行全部有落点且行为对等。

### A.1 输入法（IImeService）

| 通道 | 方向 | C# 落点 |
|---|---|---|
| `ime:getState` | invoke | `IImeService.GetState() : ImeState` |
| `ime:toggle` | invoke | `IImeService.Toggle()` |
| `ime:changed` | event | `bus.Subscribe<ImeChanged>()`（WH_SHELL 语言钩子触发） |

### A.2 截图/批注（ICaptureApi + WindowManager）

| 通道 | 方向 | C# 落点 |
|---|---|---|
| `capture:region` | invoke | `ICaptureApi.CaptureRegionAsync()`（唤起 OverlayView 选框） |
| `capture:annotate` | invoke | `WindowManager.ShowAnnotateAsync()` |
| `overlay:init` | event | `OverlayView.OnInit(params)`（构造参数传递，替代事件注入） |
| `overlay:region` | invoke | OverlayView 选框完成回调（View→ViewModel→Service） |
| `overlay:saveAnnotate` | invoke | `IAnnotateService.ExportAsync(path, mode)` |
| `overlay:cancel` | invoke | `WindowManager.CloseOverlay()` |
| `overlay:ready` | event | **结构性消除**：同进程无「窗口加载完成」竞态（原 E9/E11 竞态根因消失，P1-9 在新架构免费达成） |
| `overlay:screenshot` | invoke | `ICaptureApi.CaptureFullScreenAsync()` |

### A.3 长截图（ILongshotService）

| 通道 | 方向 | C# 落点 |
|---|---|---|
| `longshot:start` | invoke | `ILongshotService.StartAsync(TargetWindow)` |
| `longshot:stop` | invoke | `ILongshotService.StopAsync()` |
| `longshot:progress` | event | `bus.Subscribe<LongshotProgress>()` |
| `longshot:selectWindow` | invoke | `WindowManager.ShowWindowPicker()`（EnumWindows 列表窗） |
| `longshot:countdown` | event | `bus.Subscribe<LongshotCountdown>()` |

### A.4 录屏（IRecorderApi）

| 通道 | 方向 | C# 落点 |
|---|---|---|
| `recorder:start/stop` | invoke | `IRecorderApi.StartAsync(opt)/StopAsync()` |
| `recorder:status` | invoke | `IRecorderApi.GetStatus() : RecorderStatus`（**含 filepath**，D6 修复） |
| `recorder:statusChanged` | event | `bus.Subscribe<RecorderStatusChanged>()` |
| `recorder:pageStart/pageStop` | invoke | HUD 窗口显隐（ViewModel 命令） |
| `recorder:data/complete/started/ready` | event | `RecorderStatusChanged` / `RecordingCompleted(filepath)` 载荷合并；`ready` 随 A.2 同理结构性消除 |

### A.5 USB（IUsbWatcher）

| 通道 | 方向 | C# 落点 |
|---|---|---|
| `usb:arrived/removed` | event | `bus.Subscribe<UsbArrived/UsbRemoved>()`（WM_DEVICECHANGE 触发） |
| `usb:list/scan` | invoke | `IUsbWatcher.ListAsync()/RescanAsync()` |
| `usb:getDiagnostics` | invoke | `IUsbWatcher.GetDiagnostics()`（E5 语义修正：仅 USB 自检，不再误触全量诊断） |

### A.6 打印机（IPrinterMonitor）

| 通道 | 方向 | C# 落点 |
|---|---|---|
| `printer:status` | invoke | `IPrinterMonitor.GetStatus()` |
| `printer:changed` | event | `bus.Subscribe<PrinterChanged>()` |

### A.7 配置/显示/OOBE/电源（Core + WindowManager）

| 通道 | 方向 | C# 落点 |
|---|---|---|
| `config:get/set` | invoke | `ConfigService.Get<T>(key)/Set(key, value)`（键白名单校验 —— B3 根修） |
| `config:updated` | event | `bus.Subscribe<ConfigChanged>()`（去抖落盘后广播） |
| `display:list` | invoke | `IDisplayService.ListMonitors()` |
| `display:sidebarTarget` | invoke | `IDisplayService.SetSidebarTarget(monitorId)` |
| `oobe:getState/setState` | invoke | `IOobeService.GetState()/SaveState(OobeState)`（**字段级合并**——E2 根修：record with 'with' 表达式） |
| `oobe:closeAndOpenMain` | invoke | `WindowManager.CompleteOobe()` |
| `power:setAutoLaunch/getAutoLaunch` | invoke | `IShellService.SetAutoLaunch(bool)/GetAutoLaunch()` |

### A.8 热键/Shell/提醒（IHotkeyService/IShellService/SchedulerService）

| 通道 | 方向 | C# 落点 |
|---|---|---|
| `hotkey:getState` | invoke | `IHotkeyService.GetSlots() : HotkeySlotInfo[]`（含冲突与建议） |
| `shell:openExternal/openPath/showItemInFolder` | invoke | `IShellService.OpenExternal(Uri)/OpenPath(safePath)/RevealInExplorer(path)`（白名单见 §6.8） |
| `app:openTaskMgr` | invoke | `IShellService.OpenTaskManager()` |
| `reminder:add/remove/list` | invoke | `SchedulerService.AddReminder/RemoveReminder/ListReminders` |
| `reminder:due` | event | `bus.Subscribe<ReminderDue>()` |
| `reminder:selectSound/playTest` | invoke | `ISoundService.PickFileAsync()/PlayPreviewAsync()` |

### A.9 窗口/外观/悬浮球（WindowManager + AppearanceService）

| 通道 | 方向 | C# 落点 |
|---|---|---|
| `window:show/hide/resize/dock/undock/openSettings` | invoke | `WindowManager.ShowSidebar()/HideSidebar()/ResizeRail/Panel()/Dock()/Undock()/OpenSettings()`（resize/dock 内部转 `AppBarService.Reposition`） |
| `appearance:get/set/changed` | invoke/event | `IAppearanceService.GetSnapshot()/Apply(AppearanceConfig)/bus.Subscribe<AppearanceChanged>()` |
| `floatball:show/hide/toggle/expand/collapse` | invoke | `IFloatBallService.Show()/Hide()/Toggle()/Expand()/Collapse()` |
| `floatball:dragStart/dragEnd` | invoke | FloatBallView 拖拽回调（吸附算法 `FloatBallLayout` Core 层可单测） |
| `floatball:action` | invoke | `IFloatBallService.InvokeAction(id)` |
| `floatball:layout` | invoke | `FloatBallLayout.Apply(config)` |
| `floatball:setClickThrough` | invoke | `IFloatBallService.SetClickThrough(bool)`（WS_EX_TRANSPARENT 切换） |

### A.10 通知/帮助/诊断（INotifyService/IDiagnosticsService）

| 通道 | 方向 | C# 落点 |
|---|---|---|
| `notification:show` | invoke | `INotifyService.Show(NotificationItem)`（Windows Toast / 应用内 Toast 双通道） |
| `notification:dismiss` | invoke | `INotifyService.Dismiss(id)`（**实现补齐**——v1.2.0 空实现，D12） |
| `help:runDiagnostics` | invoke | `IDiagnosticsService.RunFullCheckAsync()` |
| `help:exportDiagPack` | invoke | `IDiagnosticsService.ExportPackAsync(path)` |
| `diag:getStatus/update` | invoke/event | `IDiagnosticsService.GetStatus()/bus.Subscribe<DiagChanged>()` |

> **统计**：76 通道中 4 个（`overlay:ready`、`recorder:ready/started` 类）因同进程架构**结构性消除**（竞态根因消失），其余 72 个均落点到类型安全接口或 EventBus 事件；FE-PARITY 验收时按本表逐行勾验。

---

## 附录 B：配置 Schema 映射与迁移（ADR-M8 逐字段兼容）

### B.1 SidekickConfig → C# record（源生成 JSON）

```csharp
// 文件: src/SmartSideBAR.Core/Configuration/SidekickConfig.cs
namespace SmartSideBAR.Core.Configuration;

public sealed record SidekickConfig(
    int Version,
    ImeConfig Ime,
    CaptureConfig Capture,
    RecorderConfig Recorder,
    UsbConfig Usb,
    PrinterConfig Printer,
    DisplayConfig Display,
    AppearanceConfig Appearance,
    FloatBallConfig FloatBall,
    List<LinkItem> Links,
    List<Reminder> Reminders,
    ReminderSoundConfig ReminderSound,
    OobeState Oobe,
    PolicyConfig Policy)
{
    public const int CurrentVersion = 2;
}

public sealed record ImeConfig(string Slot1, string Slot2, bool FallbackSwap);
public sealed record CaptureConfig(
    string Hotkey, string AnnotateHotkey, string LongshotHotkey,
    string Format /*png|jpg*/, string Dir);
public sealed record RecorderConfig(int Fps, string Bitrate, bool Mic, string Dir);
public sealed record UsbConfig(bool Enabled, List<string> IgnoreTypes);
public sealed record PrinterConfig(int PollIntervalSec);
public sealed record DisplayConfig(
    string SidebarMonitor, SidebarSide SidebarSide, bool FitWindowsToWorkArea);
public enum SidebarSide { Left, Right }
public sealed record PolicyConfig(List<string> DisabledModules);

// 与 types.ts 同构的值类型（ImeState/PrinterStatus/LinkItem/Reminder/... 一一对应，略）
// JSON 源生成（AOT/Trim 友好）：
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SidekickConfig))]
public sealed partial class ConfigJsonContext : JsonSerializerContext;
```

### B.2 三层合并与路径（语义与 v1.2.0 完全一致）

```
层1 默认值     : DEFAULT_CONFIG（代码内 —— 不再依赖 resources/app/defaults/config.json，
                 修复 F8「ProgramData 种子文件冻结旧默认值」：安装包不再复制种子，ProgramData
                 层仅在存在时参与合并）
层2 策略层     : %ProgramData%\SmartSideBAR\config.json   （管理员可下发 disabledModules 等）
层3 用户层     : %APPDATA%\SmartSideBAR\config.json        （设置界面写入）
合并规则       : 深合并（record with 链）；启动时 ConfigMigrator 按 Version 迁移
                 （沿用 migrateConfig()：未知字段保留、缺字段补默认、version 提升）
```

> v1.2.0 的 ProgramData 目录名为 `SeewoSidekick`（installer.nsh）——迁移安装器做一次目录探测兼容（存在旧目录则优先读取并提示收敛到新目录），保证希沃装机平滑。

### B.3 数据文件与目录迁移

| 数据 | 旧路径（v1.2.0） | 新路径 | 迁移 |
|---|---|---|---|
| 用户配置 | `%APPDATA%\config.json`（app.getPath('userData') 根） | `%APPDATA%\SmartSideBAR\config.json` | 首启检测旧文件→迁移→旧文件改名 `.migrated.bak` |
| 策略配置 | `%ProgramData%\SeewoSidekick\config.json` | `%ProgramData%\SmartSideBAR\config.json` | 同上（保 SeewoSidekick 兼容读取一版） |
| 截图/录屏输出 | `config.capture.dir / recorder.dir`（用户自选） | 同配置键，不迁移路径只保留值 | 无需动作 |
| 日志 | electron-log 默认目录 | `%APPDATA%\SmartSideBAR\logs\`（Serilog 滚动文件，7d 保留） | 不迁移 |

---

## 附录 C：签名密钥治理操作手册（G1 修复步骤）

```
C.1 吊销与重签（当天完成）
  1) 生成新代码签名密钥对（PowerShell）:
     $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=SmartSideBAR Code Signing"
     Export-Certificate  -Cert $cert -FilePath SmartSideBAR-codesign-new.cer   （公钥可入库）
     Export-PfxCertificate -Cert $cert -FilePath $env:TEMP\cs.pfx -Password (Read-Host -AsSecureString)
     # 私钥仅存本机证书存储区（PrivateKey 可导出=false）或密码管理器，不落项目目录
  2) CI secrets 配置：CODESIGN_PFX_B64（base64(pfx)）、CODESIGN_PFX_PASS
  3) 旧密钥处置：仓库中 SmartSideBAR-codesign.pfx 对应证书加入「吊销清单」——
     由于是自签名链，实际动作 = 下一个安装包 uninstall-trust.cmd 移除旧根证书 + 公告

C.2 仓库清理（当天完成）
  1) git rm --cached cert/SmartSideBAR-codesign.pfx cert/pfx-password.txt（保留 .cer 与 install 脚本模板）
  2) .gitignore 增加：*.pfx / *.p12 / *password*.txt / cert/private/
  3) 历史处置决策（二选一，默认 3a）：
     3a) 不重写历史：接受旧密钥留于历史（已吊销，无签名价值），gitleaks CI 防再犯
     3b) 重写历史：git filter-repo --path cert/SmartSideBAR-codesign.pfx --invert-paths
        （需协调所有协作 clone 重新拉取；GitHub 侧联系支持清缓存）

C.3 防再犯
  1) CI 增加 gitleaks job（§7.3 前置）
  2) 提交钩子（可选 pre-commit + detect-secrets）
  3) install-trust.cmd 改造：默认不导入；安装器提供勾选项；卸载脚本同步移除
```

---

## 附录 D：MLP 与既有审计文档的承接对照

| 审计条目（project-audit-and-roadmap.md） | v1.2.0 状态 | MLP 承接 |
|---|---|---|
| P0-1~P0-7（阻塞首发） | 已修（94bf2a1） | 迁移时以「行为基准」固化进契约测试（附录 A） |
| P1-1（长截图 O(n²)）| 未标完成 | §6.4 结构性修复（线性拼接） |
| P1-2（调度落盘）| 未标完成 | §6.10 变更检测落盘 |
| P1-4（打印机映射）| 未标完成 | §6.7 映射表重写 + 单测 |
| P1-5（链接丢失）| 未标完成 | §6.9 保存全量回写 + 单测 |
| P1-7/8（录屏闭环/批注导出）| 未标完成 | §6.5（MP4+filepath）/§6.3（双模式导出） |
| P1-9（overlay:ready 竞态）| 未标完成 | 同进程架构结构性消除（A.2） |
| P2-8（性能实测）| 清单已建、数据空 | §8 全表实测回填（Avalonia 版） |
| F8（ProgramData 冻结默认值）| 半解决 | 附录 B.2 安装器不再撒种子 |
| D12（通知空实现）| 未修 | A.10 补齐 |
| 新发现 G1-G6 | 本轮新增 | §7.4（G1）/§7.3（G3）/§5（G4）/§3.3（G5/G6） |

---

*本方案（M1.0）为 SmartSideBAR 的收官级规划：以 C# / Avalonia / .NET 10 重立地基，以 AppBar 一等公民化兑现「侧栏成为桌面布局一部分」的产品承诺。执行原则：密钥先治（Wave 0）、AppBar 先行（Wave A）、对等再切换（FE-PARITY）、每 Wave 可交付可回退。*
