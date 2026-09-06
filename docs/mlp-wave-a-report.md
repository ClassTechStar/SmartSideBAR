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
