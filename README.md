<div align="center">
  <img src="build/icon.ico" width="96" height="96" alt="SmartSideBAR Logo">
  <h1>SmartSideBAR</h1>
  <p><strong>常驻教学大屏侧边的快捷操作与设备提醒中心</strong></p>
  <p>
    <a href="#功能列表"><img src="https://img.shields.io/badge/功能-12%2B模块-blue" alt="Features"></a>
    <a href="#技术栈"><img src="https://img.shields.io/badge/技术栈-Avalonia%20+%20.NET%2010-green" alt="Tech Stack"></a>
    <a href="#测试与质量门禁"><img src="https://img.shields.io/badge/测试-120%20用例-brightgreen" alt="Tests"></a>
    <a href="#license"><img src="https://img.shields.io/badge/License-Apache%202.0-blue" alt="License"></a>
  </p>
</div>

---

## 项目介绍

**SmartSideBAR** 是一款专为希沃（Seewo）交互智能平板 / Windows OPS 教学环境设计的常驻侧边快捷键工具。它常驻屏幕侧边，一触即达，将输入法切换、区域截图、屏幕批注、长截图、MP4 录屏、板中板、U 盘监控、打印机状态、定时提醒、通知中心、网页快捷入口等高频操作整合到一个低打扰的侧边抽屉中。

针对教师触屏场景优化，无需键盘快捷键，单手即可操作。内置完整的首次体验引导（OOBE），让非技术教师在 3 分钟内完成上手。当前版本还引入了**工作区**（上课/批注模式一键切换模块组合）、**墨迹识别转形**（手绘图形自动变干净）与**希沃式触发条**（防误触的收起形态）。

## 架构与发布形态

> **决策 (ADR-001)**：Avalonia (C# / .NET 10) 为主线栈。Electron 栈已冻结，仅保安全修复（CI 有 `electron-freeze-guard` 门禁）。

| 栈 | 技术 | 状态 | 发布物 |
|----|------|------|--------|
| **Avalonia（主线）** | C# .NET 10 + Avalonia 11 | 活跃开发 | **是**（Inno Setup 安装包） |
| Electron（冻结） | Electron 30 + Vue 3 + TS | 仅安全修复 | 否 |

**实际安装产物**是 Avalonia 版 `SmartSideBAR.exe`（自包含单文件约 15MB），通过 Inno Setup 打包为 `SmartSideBAR-v{version}-Setup.exe`。版本号单一事实源：`Directory.Build.props` 的 `<Version>`。

## 适用场景

- **希沃交互智能平板** / Windows OPS 教学一体机
- 4GB–8GB 内存的教育版 Windows 10/11 设备
- 触屏优先、低资源占用、低打扰的课堂辅助工具

## 功能列表

**侧栏与交互**
| 功能 | 说明 |
|------|------|
| 边缘图标轨 (rail) | 72px 图标轨 + 380px 滑出面板，选中项带品牌色指示条；**长按 500ms 拖拽排序**（持久化） |
| 触发模式 | `常驻胶囊`（触屏教学）/ `极细触发条·点击展开`（防误触，对标希沃）/ `极细触发条·悬停滑出` |
| 工作区 | 「默认 / 上课 / 批注」一键切换模块组合，按场景裁剪 rail |
| 悬浮球 | 拖拽吸附 + 位置持久化 + 扇形玻璃菜单；闲时自动变暗；双击开合侧栏 |
| 全屏自动收起 | 检测到全屏应用 1.2s 后收起为底角方块/触发条，退出自动恢复 |
| 通知中心 | 提醒/USB/录屏等事件统一留档（≤50 条），「提醒与通知」面板回看、单条删除、全部清除 |

**截图与批注**
| 功能 | 说明 |
|------|------|
| 区域截图 | 全屏覆盖框选 → 保存/复制，多屏与高 DPI 正确 |
| 屏幕批注 | 全屏透明覆盖层：压感笔（速度模拟笔锋）、荧光笔、激光笔（自动消隐）、直线/箭头/矩形/椭圆、笔迹级橡皮、快照撤销 |
| 墨迹识别转形 | 收笔后手绘圆/椭圆/矩形/三角形/直线自动替换为干净形状（纯几何识别，工具条可关） |
| 板中板 | 独立浮动小黑板/小白板，临时演算纸，拖到任意位置，保存为 PNG |
| 长截图 | 选窗口 → 自动滚动拼接 |
| 录屏 | MP4（H.264 转码），帧率/麦克风可配 |

**设备与提醒**
| 功能 | 说明 |
|------|------|
| 输入法切换 | rail 一键中英切换 + 角标；热键可配置 |
| U 盘监控 | 插拔 ≤2s 提醒，移动硬盘识别，忽略类型可配 |
| 打印机状态 | 轮询 + 异常状态 Toast 提醒 |
| 定时提醒 | 一次性/间隔/整点，顺延/取消，铃声可换 MP3 |
| 快捷链接 | 分类页签 + 方形卡片网格，内联新增网页入口（DeepSeek/GLM 等网页版 AI 一键直达） |
| 任务管理器 | 一键拉起 |

**效率**
| 功能 | 说明 |
|------|------|
| 快捷设置面板 | rail 齿轮直达：80px 大磁贴开关（玻璃质感/动效/系统强调色/USB/录屏等） |
| 完整设置窗 | 左导航 + 分组卡片，8 个分页（外观/常规/截图/录屏/设备/快捷键/诊断/关于） |
| 主题 | 经典纯白（1.0 体感基线，出厂默认）⇄ 液态玻璃（v1.2 观感）双主题，强调色可跟随 Windows 系统色 |
| OOBE | 6 步首次引导，按角色裁剪，可跳过 |
| 白名单策略 | ProgramData 层 `policy.disabledModules` 下发，管控模块可见性 |

## 安装方法

### 方法一：下载安装包（推荐）

从 [GitHub Releases](https://github.com/ClassTechStar/SmartSideBAR/releases) 下载 `SmartSideBAR-v{version}-Setup.exe`，双击安装（按用户安装，无 UAC）。

> 安装包由源码构建（`dotnet publish` + Inno Setup）。自签名形态下，目标电脑需先运行 `cert/install-trust.cmd` 导入证书信任。

### 方法二：从源码构建

```bash
# 1. 克隆仓库（需要 .NET 10 SDK）
git clone https://github.com/ClassTechStar/SmartSideBAR.git
cd SmartSideBAR

# 2. 构建 + 测试
dotnet build SmartSideBAR.slnx -c Release
dotnet test SmartSideBAR.slnx

# 3. 发布自包含单文件
dotnet publish src/SmartSideBAR.Avalonia -c Release -r win-x64 \
  --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true \
  -p:TrimMode=partial -p:EnableCompressionInSingleFile=true \
  -o artifacts/app

# 4. 编译安装器 (需要 Inno Setup)
iscc installer/SmartSideBAR-v1.3.iss
```

### 方法三：开发运行（热重载调试）

```bash
dotnet run --project src/SmartSideBAR.Avalonia
```

> Electron 冻结栈如需调试（仅安全修复场景）：`npm install && npm run dev`。

## 使用说明

### 侧边栏

- **展开/收起**：点击屏幕边缘图标轨；轨底箭头或双击空白区收起为底角方块（或触发条，取决于触发模式）
- **面板**：点击 rail 图标展开对应功能面板，`Esc` 或鼠标离开 5s 自动收起
- **重排序**：长按任意图标 500ms 进入拖拽排序，松手即保存
- **工作区**：快捷设置面板顶部切换「默认 / 上课 / 批注」，rail 立即按场景裁剪

### 全局热键（可在设置修改）

| 默认键 | 动作 |
|--------|------|
| `Ctrl+Shift+A` | 区域截图 |
| `Ctrl+Shift+D` | 屏幕批注 |
| `Ctrl+Shift+L` | 长截图 |
| `Alt+Q` | 悬浮球开关 |
| `Ctrl+Shift+I` | 输入法切换 |

### 批注工具条

笔 / 荧光笔 / 激光笔 / 线 / 箭 / 框 / 圆 / 擦 + 6 色 + 细中粗 + 转形开关 + 撤销/清空/存笔迹/存截图。手绘闭合图形收笔后自动转为干净形状。

### 首次使用（OOBE）

安装后首次启动自动弹出 6 步引导：欢迎 → 角色 → 环境检测 → 偏好设置 → 上手练习 → 完成，全程 ≤ 3 分钟。

## 配置

配置三层覆盖：内置默认 → `%ProgramData%\SeewoSidekick\config.json`（策略层）→ `%APPDATA%\SmartSideBAR\config.json`（用户层）。常用键：

| 键 | 说明 | 默认 |
|----|------|------|
| `appearance.liquidGlass` | 液态玻璃质感（false = 经典纯白快面） | `false` |
| `appearance.accent` | 强调色，`"auto"` 跟随 Windows 系统 | `"auto"` |
| `appearance.reduceMotion` | 跳过过渡动画 | `false` |
| `display.railTrigger` | 触发模式 `always`/`edge`/`edge-hover` | `"always"` |
| `display.sidebarSide` | 停靠边 `left`/`right` | `"right"` |
| `workspace.presets` | 工作区预设（name + modules） | 默认/上课/批注 |
| `rail.order` | rail 图标顺序（长按排序写入） | `[]` |
| `capture.inkToShape` | 墨迹识别转形 | `true` |
| `floatBall.idleOpacity/idleDelayMs` | 悬浮球闲时变暗 | `0.55` / `4000ms` |

视觉规格单一真源：仓库根 `design-tokens.json`（几何/颜色/动效/阴影/指示条），随程序集嵌入，`DesignTokensTests` 钉死基线——改 UI 规格必须先改 JSON 再改测试。

## 技术栈

### Avalonia 主线（当前发布）

| 层级 | 技术 | 说明 |
|------|------|------|
| UI 框架 | [Avalonia](https://avaloniaui.net/) 11.x + Svg.Skia | 触屏优先 UI |
| 运行时 | .NET 10（self-contained 单文件，≈15MB） | ReadyToRun + 裁剪 |
| 核心逻辑 | `SmartSideBAR.Core`（纯逻辑层） | 配置/调度/悬浮球布局/墨迹识别/链接服务 |
| 平台层 | `SmartSideBAR.Windows`（P/Invoke + WinRT） | AppBar/热键/WGC 录屏/USB/长截图 |
| 测试 | xUnit（Core/Windows/Avalonia 三层） | 120 用例 + Fake 替身（AppBar/时钟） |

### Electron 冻结栈（历史维护）

Electron 30 + Vue 3 + TypeScript，`src/main|renderer|shared`；仅安全修复，归档条件见 ADR-001。

## 开发命令

```bash
dotnet build SmartSideBAR.slnx -c Debug     # 构建
dotnet test SmartSideBAR.slnx               # 全量测试 (120 用例)
dotnet run --project src/SmartSideBAR.Avalonia   # 运行主线
dotnet format SmartSideBAR.slnx --verify-no-changes  # 格式检查 (CI 同款)
dotnet publish src/SmartSideBAR.Avalonia -c Release -r win-x64 ...  # 发布
```

CI（`.github/workflows/ci.yml`）：gitleaks 密钥扫描 → dotnet 构建/测试/覆盖率/格式门禁 → 发布单文件 artifact；`electron-freeze-guard` 拒绝未带 `fix(security)` 标签的 Electron 路径变更。

## 项目结构

```
SmartSideBAR/
├── design-tokens.json            # 视觉规格单一真源 (几何/颜色/动效/阴影)
├── Directory.Build.props         # 版本号单一事实源 (<Version>)
├── src/
│   ├── SmartSideBAR.Avalonia/    # 主线 UI (WinExe)
│   │   ├── Ui/                   # Theme/窗口/面板/Toast/通知中心/悬浮球/批注/板中板
│   │   ├── Views/                # SidebarWindow (rail+面板主机)
│   │   ├── ViewModels/           # rail/面板视图模型
│   │   └── WindowManager.cs      # 窗口生命周期与动作路由
│   ├── SmartSideBAR.Core/        # 纯逻辑层 (可单测)
│   │   ├── Configuration/        # 三层配置 (SidekickConfig + 各域 Config)
│   │   ├── Design/               # DesignTokens (令牌镜像)
│   │   ├── Ink/                  # 墨迹识别转形 (纯几何)
│   │   ├── Links/ Scheduling/ FloatBall/ Devices/ Diagnostics/ ...
│   ├── SmartSideBAR.Windows/     # Win32/WinRT 平台层
│   │   ├── AppBar/               # SHAppBarMessage 注册与自动收起
│   │   ├── Recording/ Capture/ Devices/ Input/ Shell/ ...
│   ├── SmartSideBAR.Avalonia/    # (见上)
│   ├── main|renderer|shared/     # Electron 冻结栈 (仅安全修复)
│   └── native/                   # Electron 版 appbar N-API 原生模块 (冻结)
├── tests/                        # Core.Tests / Windows.Tests / Avalonia.Tests
├── installer/                    # Inno Setup 安装脚本
├── cert/                         # 自签名证书与信任脚本
└── scripts/                      # 辅助脚本 (native 重建/冒烟/性能基准)
```

## 核心技术特性

### 触屏优先的交互与动效

- 全应用统一动效词汇（微反馈 120ms / 面板 180ms / 窗口 250ms / 交错 32ms），令牌驱动，`reduceMotion` 全局尊重
- 触控按压 = 填充加深 + 缩放反馈；选中态 = 填充 + 贴屏侧品牌色指示条
- 悬浮球闲时变暗、扇形菜单交错弹出；经典/玻璃双主题一键切换

### 稳定性保障

- 单实例守卫（二次启动唤醒既有实例）
- AppBar 注册失败自动回退 Topmost，Topmost Keeper 每 30s 重申置顶（休眠唤醒/显示切换不掉置顶）
- 未处理异常三路捕获 → `crash-*.json` 结构化落盘；`SSB_SAFE_MODE=1` 安全模式可跳过 AppBar/悬浮球自救启动
- 日志按天滚动 + 14 天/10MB 上限清理
- 操作超时保护（截图 60s，批注 5min）

### DPI 与多显示器

- DIP 为唯一业务坐标域，100%–250% 缩放（含 1.75/2.25/2.5 非整数倍）
- AppBar 几何以目标显示器为准；悬浮球/触发条/板中板均定位到侧栏所在屏
- 显示器热插拔自动重定位，全屏应用检测 1.2s 防抖

### 性能

| 指标 | 实际 |
|------|------|
| 安装包体积 | 自包含单文件 ≈ 15MB（Electron 版为 100MB+） |
| 常驻渲染 | 经典主题零持久模糊，低配一体机合成开销最低 |
| 启动 | ReadyToRun + 裁剪 + 压缩单文件 |

## 系统要求

- **操作系统**：Windows 10 1803+ / Windows 11（64 位）
- **内存**：≥ 4GB RAM
- **屏幕**：1366×768 ~ 3840×2160，系统缩放 100%–250%
- **开发**：.NET 10 SDK（Windows）；Electron 冻结栈需 Node.js ≥ 20

## 贡献指南

欢迎提交 Issue 和 Pull Request！

1. Fork 本仓库 → 创建功能分支 `feat/xxx` → 提交 → PR
2. 主线改动请先跑 `dotnet test`；触碰 `design-tokens.json` 必须同步更新 `DesignTokensTests`
3. Electron 路径（`src/main|renderer|shared`）仅接受带 `fix(security)` 标签的变更（CI 强制）

## 许可证

[Apache-2.0](LICENSE) © 2026 ClassTechStar

---

<p align="center">Made with ❤️ for teachers and students</p>
