# SmartSideBAR 架构速览

给后来接手的人（包括过几个月忘了细节的自己）看的。当前版本 v1.5.0，只跑 Avalonia 单栈。
技术选型的历史决策在 [adr-001-single-stack.md](adr-001-single-stack.md)（当时从 Electron 收敛到 Avalonia）。

## 三层结构

```
src/
├── SmartSideBAR.Core/       纯逻辑，不碰 UI，全部可单测
│   ├── Configuration/       三层配置（默认 → ProgramData 策略 → APPDATA 用户）
│   │                        SidekickConfig 是 JSON 模型；各域有 Normalize.Clamp
│   ├── Design/              DesignTokens：design-tokens.json 的强类型镜像
│   │                        （编译时嵌进程序集）
│   ├── Ink/                 墨迹识别转形（PCA + 残差，纯几何计算）
│   └── Links/ Scheduling/ FloatBall/ Devices/ Diagnostics/ Messaging/
├── SmartSideBAR.Windows/    Win32/WinRT 平台层 (net10.0-windows10.0.19041.0)
│   ├── AppBar/              SHAppBarMessage 注册/收起/几何协商（测试用 FakeAppBarApi）
│   ├── Recording/           Windows.Graphics.Capture → MediaFoundation MP4
│   ├── Capture/ Devices/ Input/ Shell/ Native/ ...
└── SmartSideBAR.Avalonia/   UI 层 (Avalonia 11)
    ├── Views/SidebarWindow  rail + 面板容器（XAML；几何和颜色运行时从令牌写入）
    ├── Ui/                  各窗口与面板（大多是代码直接构建）
    │   ├── Panels/          一个面板一个文件（基类 SidebarPanel：Build/Refresh 带防崩溃处理）
    │   ├── Ink/             InkCanvas 批注引擎（压感/平滑/形状/识别）
    │   ├── Theme.cs         算调色板（经典快 / 液态玻璃 / 深色；accent=auto 时问 WinRT）
    │   └── ToastService NotificationCenter EdgeTriggerWindow MiniBoardWindow ...
    ├── ViewModels/          SidebarViewModel：rail 的数据模型（工作区∩policy 过滤, rail.order 排序）
    └── WindowManager.cs     窗口生命周期 + 动作分发 Dispatch("capture|annotate|...|workspace:X")
```

## 几个关键机制

| 机制 | 在哪 | 说明 |
|------|------|------|
| UI 规格统一放一处 | `design-tokens.json` + `DesignTokensTests` | 改 UI 规格先改 JSON，测试里固定了基线值 |
| 版本号统一放一处 | `Directory.Build.props <Version>` | 诊断页、关于页、崩溃记录都从程序集信息读 |
| 配置三层 | `ConfigService` | Set 只认默认 schema 里已有的键，写入走深合并 |
| 面板防崩溃 | `SidebarPanel.Refresh()` | 重建推迟到 Background 优先级，不在控件事件里销毁可视树 |
| AppBar 回退 | `WindowManager` | 注册失败就用置顶；置顶每 30 秒重申一次（防睡眠唤醒后掉） |
| 崩溃处理 | `CrashLog` + `SSB_SAFE_MODE=1` | 结构化 crash-*.json；安全模式跳过 AppBar/悬浮球 |
| 三种触发形态 | `DisplayConfig.RailTrigger` | always 常驻胶囊 / edge 触发条点击 / edge-hover 悬停滑出 |
| 工作区 | `WorkspaceConfig` | presets 的 Modules 再过一遍 policy.disabledModules，得到 rail 实际项 |

## 构建与发布

```
dotnet test SmartSideBAR.slnx                       # 130 用例
dotnet publish src/SmartSideBAR.Avalonia -c Release -r win-x64
    --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true
    -p:TrimMode=partial -p:EnableCompressionInSingleFile=true -o artifacts/app
iscc installer/SmartSideBAR.iss                     # → dist/*-Setup.exe（Inno，按用户安装）
```

安装包不进 git（`dist/` 已 ignore；Release 上挂的才是分发物）。
CI：gitleaks 扫密钥 → dotnet 构建/格式/测试/覆盖率(≥40%) → 上传产物；打 `v*` 标签时出安装包、建 Release 草稿；nightly 跑冒烟和像素基线。

## 安全边界（2026-10 评审时定的）

- 打开 URL：协议（http/https）和域名双白名单（`ShellService.OpenExternal`）。
- 发布不签名。之前试过自签根证书，往用户机器装根 CA 风险太大，已经去掉；等有真实分发量再买代码签名证书。
- 已知且接受：按用户安装目录可写、原生 DLL 与主程序同目录（单用户教学机场景风险低）。
- ProgramData 策略层的 ACL 没验证过（影响只是模块可见性，不涉及提权）。

## 测试都覆盖了什么

| 项目 | 覆盖 |
|------|------|
| Core.Tests（94） | 配置/调度/悬浮球布局/长截图拼接/墨迹识别/DesignTokens 基线 |
| Windows.Tests（17） | AppBar（用 FakeAppBarApi）/ USB 差分扫描 |
| Avalonia.Tests（19） | SidebarViewModel（全套 Fake）/ Headless 冒烟 7 链路 / 像素基线 3 态 |
