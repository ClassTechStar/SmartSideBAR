# MLP Wave A 交付报告（骨架 + AppBar 一等公民化）

> 依据《优化方案_Master_MLP_v1.md》（M1.0, 2026-09-06）。分支：`avalonia`（主线开发）；
> `main` 已完成 Wave 0 治理并冻结维护（仅安全补丁）。

## 1. Wave 0 仓库治理（已交付，main @ 263db0a）

| 项 | 结果 |
|---|---|
| G1 密钥治理 | **勘误**：`cert/` 私钥从未被 git 跟踪（`git log --all -- cert/` 为空），无需历史重写。处置：`.gitignore` 改为按文件类型精确忽略（pfx/p12/password/thumbprint），公钥 `SmartSideBAR-root.cer` 与信任脚本入库可审计；新增 `uninstall-trust.cmd`（安装包默认附带，卸载/轮换时移除旧信任根）。密钥轮换步骤见 `cert/README.md`（附录 C.1），留作用户手动动作（需交互式口令） |
| G3 CI | `.github/workflows/ci.yml`：gitleaks 前置（全历史扫描）+ Electron 冻结门禁（typecheck/lint/test 全绿本地验证）+ Avalonia dotnet 门禁（构建/测试/覆盖率机制，阈值 Wave D 提升至 80）+ v2* tag release job（签名走 secrets） |
| G6 | `aede938` 无关提交已在历史，按方案默认 3a 不重写（gitleaks 防再犯） |
| 分支策略 | main 冻结 / avalonia 主开发线（ADR-M1 绞杀者迁移） |

## 2. Wave A 解决方案（本报告范围）

### 2.1 结构（方案 §4.1 落地）

```
SmartSideBAR.slnx
├── src/SmartSideBAR.Core          net10.0（平台无关，100% 单测）
│   ├── Configuration/  SidekickConfig(逐字段同构 types.ts) · ConfigService(三层合并+迁移)
│   │                   ConfigMigrator(内嵌 Load) · ConfigJsonContext(源生成) · Appearance/FloatBall 钳制
│   ├── Scheduling/     SchedulerService（TimeProvider 注入，C1 变更检测落盘）
│   ├── Links/          LinksService（E1 全量回写）
│   ├── Policy/         PolicyService + ModuleIds
│   ├── FloatBall/      FloatBallLayout（floatball-layout.ts 逐函数移植）
│   └── Messaging/      EventBus（替代 IPC 事件推送）
├── src/SmartSideBAR.Windows       net10.0-windows（互操作层，零 UI 依赖）
│   ├── AppBar/         AppBarNative(LibraryImport) · IAppBarApi/AppBarApi · AppBarService(状态机)
│   └── Native/         WndProcHook(SetWindowLongPtr 子类化) · Win32Display
├── src/SmartSideBAR.Avalonia      net10.0-windows（UI 层，Avalonia 11.3.20）
│   ├── Program.cs(DI 装配 §4.2) · App.axaml · WindowManager · Views/SidebarWindow(Demo rail)
│   └── Logging/FileLoggerProvider（Wave D 换 Serilog）
└── tests/                         Core.Tests(40) + Windows.Tests(14) = 54 用例全绿
```

### 2.2 AppBar 缺口对照（方案 §5.1 → 现状）

| 缺口 | 状态 | 落点 |
|---|---|---|
| K1 ABN 通知无人接收 | ✅ 闭环 | `WndProcHook` 子类化 + `AppBarService.OnWndProc` 状态机（POSCHANGED/STATECHANGE/FULLSCREENAPP/WINDOWARRANGE 全处理） |
| K2 ACTIVATE/WINDOWPOSCHANGED 未上报 | ✅ | WM_ACTIVATE → ABM_ACTIVATE；每次重排后 ABM_WINDOWPOSCHANGED |
| K3 ABN_FULLSCREENAPP | ✅ | 全屏收起 rail / 退出按用户偏好恢复（V5），`FullscreenAppChanged` 事件 |
| K4 坐标双轨 | ✅ 单轨 | 物理像素全链路：`PhysicalDpiScope` 强制 PMv2 线程上下文 + `MoveWindow` 直达，零 DIP 换算 |
| K5 构建链脆弱 | ✅ 消灭 | node-gyp/N-API 全退役，P/Invoke 编译期绑定 |
| K6 自动隐藏 | ⏳ P2 | 常量已备（ABM_SETAUTOHIDEBAR），按方案列 P2 |
| K7 多显示器/热插拔 | ✅ | WM_DISPLAYCHANGE / WM_DPICHANGED → Reposition |
| V6 孤儿注册自愈 | ✅ | Attach 前补发 ABM_REMOVE（对未注册 hwnd 无副作用） |
| V8 退出恢复 WorkArea | ✅ 实测 | Detach → ABM_REMOVE；进程死亡后系统回收亦实测确认 |

### 2.3 对方案的两处实现勘误（重要）

1. **§5.3 `Reposition` 锚点：`rcWork` → `rcMonitor`**。方案蓝本以工作区为锚，但注册后 rcWork
   已扣除自身 AppBar 占位——注册期 ABN_POSCHANGED 风暴中占位带每次向屏幕内侧漂移一个带宽
   （实测 150% 缩放下工作区被蚕食至 43px）。已改为全屏矩形锚定，沿边裁剪交给 ABM_QUERYPOS
   （与 v1.2.0 appbar.cc 语义一致），并加了漂移回归测试 `Reposition_IsStable_UnderRepeatedPosChanged`。
2. **§5.3 `WinProcHook`：Avalonia `AddWndProcCallback` 非公开 API**，改用 `SetWindowLongPtr(GWLP_WNDPROC)`
   经典子类化——等效且零 UI 框架耦合，Wave B 的全局热键（WM_HOTKEY）与 USB（WM_DEVICECHANGE）复用同一钩子。

另：宿主（Avalonia）线程的 DPI 感知上下文不受我们控制，实测坐标被虚拟化（÷1.5）。
`AppBarApi` 所有调用经 `SetThreadDpiAwarenessContext(PMv2)` 作用域包裹——物理像素单轨由此硬保证。

### 2.4 开发期修复的 bug 清单

| # | Bug | 修复 |
|---|---|---|
| 1 | EventBus 退订实现错误 | Unsubscription 携带事件类型 |
| 2 | 调度器启动追赶循环遍历时修改集合 | 快照遍历 |
| 3 | `IReadOnlyList<Reminder>` 缺 JSON 源生成元数据 | Context 补注册 |
| 4 | `NormalizeAccent` 3 位色号未校验 hex 字符 | 对齐 appearance.ts 正则语义 |
| 5 | `NextFullHour` 跨日对齐错误 | 按本地时间整点重构 |
| 6 | rcWork 锚点反蚀漂移（见勘误 1） | rcMonitor 锚定 + 回归测试 |
| 7 | 线程 DPI 上下文虚拟化（见勘误 3） | PhysicalDpiScope 强制 PMv2 |

### 2.5 验证证据

- **单测 54/54 全绿**（Core 40 + Windows 14，零警告零错误构建）。AppBarService 状态机经
  FakeAppBarApi 断言操作序列：Attach→NEW/QUERYPOS/SETPOS/WINDOWPOSCHANGED/MOVE；
  POSCHANGED→重协商；FULLSCREENAPP→rail/恢复；ACTIVATE 上报；DISPLAYCHANGE→重排；Detach→REMOVE。
- **真机冒烟（2560×1600 @150%）**：
  - `ABM_NEW → True`，侧栏窗口物理贴右缘（虚拟坐标 1664-1707 = 物理 2496-2560，64px 宽）
  - 工作区稳定扣除恰好一个带宽（0,0)-(1664,1019)，多次 ABN_POSCHANGED 无漂移
  - 退出：ABM_REMOVE → 工作区完全恢复 (0,0)-(1707,1019)
  - 冒烟自动化：`SSB_SMOKE_EXIT_MS` 环境变量 N 毫秒自动退出（CI 可用）
- **配置互认**：`%APPDATA%\SmartSideBAR\config.json` camelCase 与 Electron 版逐字段互认；
  遗留目录（`%APPDATA%\smartsidebar`、`%ProgramData%\SeewoSidekick`）首启自动迁移（.migrated.bak）。

### 2.6 已落点的 IPC 通道（附录 A 进度）

| 区块 | 状态 |
|---|---|
| A.7 config:get/set/updated、power（部分） | ✅ ConfigService（B3 键白名单根修） |
| A.8 reminder:add/remove/list/due | ✅ SchedulerService |
| A.9 window:resize（rail/panel 切换） | ✅ Demo（SetExpanded→AppBar 重排） |
| A.10 diag:update（配置变更广播） | ✅ ConfigChanged |
| 其余（IME/USB/捕获族/悬浮球/OOBE…） | Wave B/C（接口桩已在 DI 预留） |

### 2.7 Wave B 入口提示

- `IWndProcHook` 支持同窗口多回调叠加——HotkeyService/UsbDeviceWatcher 直接 `Add`。
- `ModuleIds` + `PolicyService` 就绪，服务启动跳过逻辑按 P2-1 语义接入。
- 铃声播放 (NAudio)、IME (InputLanguage)、USB (WM_DEVICECHANGE) 按方案 §6 对应节实现。

---

# 追加：Wave B / C / D 交付报告（2026-09-06 晚，分支 avalonia）

## Wave B 常驻服务族（b0ae19e）

| 模块 | 落点 | 真机状态 |
|---|---|---|
| 输入法 (§6.1) | ImeService：键盘布局枚举 + WM_INPUTLANGCHANGEREQUEST，零 PS 子进程 | rail 按钮可切 |
| U 盘 (§6.6) | UsbDeviceWatcher：WM_DEVICECHANGE 顶层窗口广播（勘误见下） | 监听中 |
| 打印机 (§6.7) | PrinterMonitor：WMI 轮询 + DetectedErrorState 映射（E4 根修，单测锁定） | 2 台实检 |
| 热键 (§6.13) | HotkeyService：RegisterHotKey 系统级冲突事实 + 替代建议 | 5 槽位注册零冲突 |
| Shell (§6.8) | taskmgr / 外链白名单 (B2) / HKCU 自启 | — |
| 铃声 | winmm PlaySound 别名 + MCI 播 MP3（PS 守护退役） | — |
| 托盘 (§6.14) | 运行时图标 + 显示侧栏/设置/退出 | — |

## Wave C 捕获族 + UX（8daa938 / 3e88891）

- **区域截图**（§6.2）：全屏透明覆盖层拖选 → BitBlt 物理像素裁剪 → PNG/JPG 落盘 → Toast。
- **屏幕批注**（§6.3/D1/D3/P1-8）：InkCanvas 统一 Pointer（触屏/笔/鼠标）；导出 仅笔迹/含背景 双模式。
- **长截图**（§6.4/A5/C3）：EnumWindows（剔 cloaked）+ DWM 扩展框 + 行哈希线性拼接
  （`LongshotStitcher` 纯函数单测）+ 倒计时/进度事件。
- **录屏**（§6.5/ADR-M4）：GraphicsCaptureItem →（**IGraphicsCaptureItemInterop COM 路径**，
  19041 投影无 CreateForMonitorAsync）→ Direct3D11CaptureFramePool → MediaStreamSource →
  MediaTranscoder 系统 H.264 → **MP4 直出**；停止以 null 样本收尾保证 moov 完整。
- **悬浮球/设置/OOBE/诊断**：扇形菜单 8 动作；设置六区即时生效；6 步向导含真实环境探测；
  诊断 7 项聚合 + 诊断包（report.json + logs.zip）。
- **动作总路由**：rail 按钮 / 悬浮球 / 热键 / 托盘 四入口共用 `WindowManager.Dispatch`。

## Wave D 质量与性能（部分完成）

| 指标（§8） | v1.2.0 基线 | 实测（Release 单文件，2560×1600@150%） | 目标 | 结论 |
|---|---|---|---|---|
| 空闲内存 | 预算 ≤220MB 未实测 | **私有 87.5MB / 工作集 134.6MB** | ≤80MB | 大幅优于 Electron 预算；未达 80MB，列 Wave D 优化（Avalonia 渲染线程 + WinRT 持有） |
| 冷启动 | ≤3s 未实测 | **~3.5s**（bootstrap→AppBar 就绪，含单文件解压） | ≤1.2s | 未达；单文件解压为首帧大头，可改多文件分发或 ReadyToRun 缓解 |
| 安装包 | 81.6MB | **14.7MB 单文件 / 10MB zip** | ≤25MB | ✅ |
| 常驻子进程 | 1（PS 守护） | **0** | 0 | ✅ |
| 覆盖率门禁 | 无 | 机制就绪，阈值 0（74 用例全绿） | 80% | Wave D 收官项 |

## Wave B/C 新增勘误与根修（对方案）

1. **§6.6/ADR-M5 勘误**：`DBT_DEVTYP_VOLUME` 不支持 `RegisterDeviceNotification` 过滤注册
   （该 API 仅接受 DEVICEINTERFACE/HANDLE，VOLUME 一律 err=13，真机复现）。卷事件本就
   广播至所有顶层窗口——直接 WndProcHook 接收即正解（与 v1.2.0 一致）。
2. **§6.5 补充**：19041 SDK 投影的 `GraphicsCaptureItem` 仅暴露 `CreateFromVisual`，
   监视器捕获必须走 `IGraphicsCaptureItemInterop.CreateForMonitor` COM 互操作。
3. **file-scoped namespace 遮蔽**：项目名含 `.Windows`/`.Avalonia` 时，命名空间内引用
   `Windows.*`/`Avalonia.*` 会被解析到自有前缀——一律 `global::` 别名（多次踩坑后固化为规范）。

## 安装包（本机已产出）

| 产物 | 路径 | 说明 |
|---|---|---|
| 单文件 | `artifacts/app/SmartSideBAR.Avalonia.exe` (14.7MB) | 自包含+部分裁剪+压缩，冒烟全绿 |
| 绿色包 | `dist/SmartSideBAR-2.0.0-waveC-win-x64.zip` (10MB) | install.cmd/uninstall.cmd 免管理员安装 |
| NSIS 脚本 | `installer/SmartSideBAR.nsi` | CI（choco nsis）产出标准安装器；per-user；签名 CI 注入 |

发布冒烟（Trimmed 产物）：AppBar 注册 ✓ / 5 热键 ✓ / 调度器 ✓ / 打印机 2 台 ✓ / 单进程 ✓。

## 遗留（Wave D/E 收官清单）

1. 真机人工回归：V1-V7 全场景矩阵（希沃触屏 + 多显示器）、长截图滚动页（PDF/网页）、
   录屏端到端产物校验（可播性/时长）、悬浮球触屏拖拽。
2. 录屏麦克风混流（ADR-M4 增强）；自动隐藏 AppBar（K6/P2）；悬浮球 WS_EX_TRANSPARENT 闲时穿透。
3. 覆盖率门禁 0→80%；内存优化（87.5→80MB 以下）；冷启动（ReadyToRun / 多文件分发）。
4. NSIS 实机打包 + 签名（CI secrets）+ v2.0-beta Release（Wave E）。
