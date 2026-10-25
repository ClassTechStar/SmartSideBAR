# SmartSideBAR 架构速览

> 面向三个月后的你自己 / 新协作者。当前基线：v1.5.0（Avalonia 单栈）。
> 历史决策见 [adr-001-single-stack.md](adr-001-single-stack.md)（Electron → Avalonia 单栈收敛）。

## 三层结构

```
src/
├── SmartSideBAR.Core/       纯逻辑层, 无 UI 依赖, 全部可单测
│   ├── Configuration/       三层配置 (默认 → ProgramData 策略 → APPDATA 用户)
│   │                        SidekickConfig = JSON 模型; 各域有 Normalize.Clamp
│   ├── Design/              DesignTokens: design-tokens.json 的强类型镜像
│   │                        (随程序集嵌入; 视觉规格单一真源)
│   ├── Ink/                 墨迹识别转形 (PCA + 残差, 纯几何)
│   └── Links/ Scheduling/ FloatBall/ Devices/ Diagnostics/ Messaging/
├── SmartSideBAR.Windows/    Win32/WinRT 平台层 (net10.0-windows10.0.19041.0)
│   ├── AppBar/              SHAppBarMessage 注册/收起/几何协商 (FakeAppBarApi 可测)
│   ├── Recording/           Windows.Graphics.Capture → MediaFoundation MP4
│   ├── Capture/ Devices/ Input/ Shell/ Native/ ...
└── SmartSideBAR.Avalonia/   UI 层 (Avalonia 11)
    ├── Views/SidebarWindow  rail + 面板主机 (XAML; 几何/颜色运行时由令牌写入)
    ├── Ui/                  窗口与面板 (代码构建为主)
    │   ├── Panels/          一面板一文件 (基类 SidebarPanel: Build/Refresh 防崩溃)
    │   ├── Ink/             InkCanvas 批注引擎 (压感/平滑/形状/识别接入)
    │   ├── Theme.cs         Palette 计算 (经典快 / 液态玻璃 / 深色; accent=auto→WinRT)
    │   └── ToastService NotificationCenter EdgeTriggerWindow MiniBoardWindow ...
    ├── ViewModels/          SidebarViewModel: rail 数据化 (工作区∩policy 过滤, rail.order 排序)
    └── WindowManager.cs     窗口生命周期 + 动作路由 Dispatch("capture|annotate|...|workspace:X")
```

## 关键机制

| 机制 | 位置 | 一句话 |
|------|------|--------|
| 视觉规格单一真源 | `design-tokens.json` + `DesignTokensTests` | 改 UI 规格必须先改 JSON, 测试钉死基线 |
| 版本单一真源 | `Directory.Build.props <Version>` | 诊断/关于页/崩溃记录读程序集信息 |
| 配置三层 | `ConfigService` | 键白名单 Set (必须在默认 schema 中存在), 深合并 |
| 面板防崩溃 | `SidebarPanel.Refresh()` | 延后到 Background 优先级重建, 不在控件事件里销毁可视树 |
| AppBar 回退 | `WindowManager` | 注册失败→Topmost; Topmost Keeper 30s 重申 (休眠唤醒不掉) |
| 崩溃兜底 | `CrashLog` + `SSB_SAFE_MODE=1` | 结构化 crash-*.json; 安全模式跳过 AppBar/悬浮球 |
| 触发双模式 | `DisplayConfig.RailTrigger` | always 常驻胶囊 / edge 触发条点击 / edge-hover 悬停滑出 |
| 工作区 | `WorkspaceConfig` | presets 的 Modules ∩ policy.disabledModules 过滤 rail |

## 构建/发布链

```
dotnet test SmartSideBAR.slnx                       # 120 用例
dotnet publish src/SmartSideBAR.Avalonia -c Release -r win-x64
    --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true
    -p:TrimMode=partial -p:EnableCompressionInSingleFile=true -o artifacts/app
iscc installer/SmartSideBAR.iss                     # → dist/*-Setup.exe (Inno, 按用户安装)
```

发布产物**不进 git**（`dist/` 已 ignore；安装包只挂 GitHub Release 资产）。
CI：gitleaks → dotnet 构建/格式/测试/覆盖率(≥40%) → publish artifact；`v*` tag → Inno 安装包 → Release 草稿；nightly → 冒烟与像素基线。

## 安全边界（评审结论存档, 2026-10）

- URL 打开：协议(http/https) + 域名双白名单（`ShellService.OpenExternal`）。
- 发布默认不签名（自签根证书方案已废除——装根 CA 属于反模式; 有真实分发量后再购代码签名证书）。
- 已知接受项：按用户安装目录可写 + 原生 DLL 并存（单用户教学机威胁模型下低风险）。
- ProgramData 策略层 ACL 未验证（影响仅模块可见性, 非提权）。

## 测试地图

| 项目 | 覆盖 |
|------|------|
| Core.Tests | 配置/调度/悬浮球布局/长截图拼接/**墨迹识别(10 例)**/DesignTokens 基线 |
| Windows.Tests | AppBar (FakeAppBarApi 替身) / USB 差分扫描 |
| Avalonia.Tests | SidebarViewModel (Fake 全家桶) / **Headless 冒烟 (6 链路)** / 像素基线 |
