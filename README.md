# SmartSideBAR

给教学大屏用的侧边工具栏。贴在屏幕边上，把上课常用的那几个操作（截图、批注、切输入法、看 U 盘）收进一个抽屉里，不用再满屏幕找。

主要跑在希沃这类交互智能平板和 Windows OPS 一体机上，按触屏操作做的，不需要键盘也能用。

## 有哪些功能

**侧栏本体**

| | |
|---|---|
| 图标轨 (rail) | 72px 图标条 + 380px 滑出面板；长按图标 500ms 可以拖着排序，松手就记住 |
| 三种触发形态 | 常驻胶囊 / 极细触发条点击展开 / 极细触发条悬停滑出。后两种收起来时只占 14px，防误触 |
| 工作区 | 「默认 / 上课 / 批注」三套预设，一键换掉 rail 上的模块组合 |
| 悬浮球 | 可拖着走，松手自动贴边；闲时自己变暗；双击开合侧栏 |
| 全屏时自动收起 | 检测到全屏应用 1.2 秒后缩成底角方块，退出后自己回来 |
| 通知中心 | 提醒、U 盘、录屏这些事件都留一份记录，面板里能翻能删 |

**截图与批注**

| | |
|---|---|
| 区域截图 | 框一块，存或者复制。多屏和高 DPI 都是对的 |
| 屏幕批注 | 全屏透明层上直接画：铅笔（按速度模拟笔锋）、荧光笔、激光笔、直线/箭头/矩形/椭圆、橡皮、撤销 |
| 墨迹转形 | 手画的圆/矩形/三角形收笔后自动换成干净图形，工具条上可关 |
| 板中板 | 一块 860×560 的浮动小白板，当演算纸用，能存成 PNG |
| 长截图 | 选窗口后自动滚屏拼接 |
| 录屏 | MP4 (H.264)，帧率和麦克风可配 |

**设备与提醒**

| | |
|---|---|
| 输入法切换 | 一键中英切换，图标上带角标；热键可改 |
| U 盘监控 | 插拔 2 秒内弹提醒，移动硬盘能认出来，忽略哪些类型可配 |
| 打印机状态 | 定时查，缺纸/卡纸/离线会弹通知 |
| 定时提醒 | 一次性的、间隔的、整点的都行，支持顺延和换铃声 |
| 快捷链接 | 自己加网页入口，按分类分页，比如 DeepSeek、GLM 这类网页版 AI |
| 任务管理器 | 一键拉起 |

**设置**

| | |
|---|---|
| 快捷设置面板 | rail 齿轮点进去是 80px 大磁贴开关，常用的几个都在这 |
| 完整设置窗 | 左侧导航，8 个分页 |
| 两种主题 | 经典纯白（默认）和液态玻璃，强调色可以跟 Windows 系统色 |
| OOBE | 首次启动 6 步引导，可以跳过 |
| 白名单 | 管理员可通过 `ProgramData` 下的策略文件关掉某些模块 |

## 装上就能用的版本

去 [Releases](https://github.com/ClassTechStar/SmartSideBAR/releases) 下 `SmartSideBAR-v{version}-Setup.exe`，双击装。装在当前用户目录下，不需要管理员权限。

安装包是源码构建的（`dotnet publish` + Inno Setup），**目前没有做代码签名**（之前试过自签根证书，往用户机器里装根 CA 这事风险太大，已经去掉了。等真有分发量再买正规证书）。SmartScreen 要弹「仍要运行」就点一下。

学校电教批量部署可以用 winget：清单在 `packaging/winget/`，等合进 winget-pkgs 之后就是 `winget install ClassTechStar.SmartSideBAR`。

## 自己构建

要 .NET 10 SDK。

```bash
git clone https://github.com/ClassTechStar/SmartSideBAR.git
cd SmartSideBAR

# 构建 + 跑测试
dotnet build SmartSideBAR.slnx -c Release
dotnet test SmartSideBAR.slnx

# 发布单文件
dotnet publish src/SmartSideBAR.Avalonia -c Release -r win-x64 \
  --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true \
  -p:TrimMode=partial -p:EnableCompressionInSingleFile=true \
  -o artifacts/app

# 打安装包（要装 Inno Setup；发版时改 iss 里的 AppVersion，一共三处）
iscc installer/SmartSideBAR.iss
```

平时开发直接跑就行：

```bash
dotnet run --project src/SmartSideBAR.Avalonia
```

> 早期 Electron 版本的源码在 `archive/electron-final` 分支。仓库根下的 `SmartSideBAR-1.0.0/1.1/1.2.0` 三个目录是当时留的版本快照，只是方便对着看界面，不参与构建。

## 怎么用

**侧栏**：点屏幕边缘的图标轨展开；点轨底的箭头或者双击空白处收起。点图标出面板，`Esc` 或者鼠标离开 5 秒后自己收。长按图标 500ms 进入拖拽排序。工作区在快捷设置面板顶上切。

**默认热键**（设置里能改）：

| 按键 | 功能 |
|--------|------|
| `Ctrl+Shift+A` | 区域截图 |
| `Ctrl+Shift+D` | 屏幕批注 |
| `Ctrl+Shift+L` | 长截图 |
| `Alt+Q` | 悬浮球开关 |
| `Ctrl+Shift+I` | 输入法切换 |

**批注工具条**：铅笔、荧光笔、激光笔、直线、箭头、矩形、椭圆、橡皮，6 种颜色，细中粗三档，转形开关，撤销/清空/存笔迹/存截图。收笔后手绘的闭合图形会自动转成规整形状。

**首次启动**：会有 6 步引导（欢迎 → 选角色 → 环境检测 → 偏好 → 上手练习 → 完成），两三分钟的事，也可以直接跳过。

## 配置文件

配置分三层，后一层盖前一层：程序内置默认 → `%ProgramData%\SeewoSidekick\config.json`（管理员策略）→ `%APPDATA%\SmartSideBAR\config.json`（用户自己的）。

常用的几个键：

| 键 | 干什么的 | 默认 |
|----|------|------|
| `appearance.liquidGlass` | 液态玻璃质感，false 是经典纯白 | `false` |
| `appearance.accent` | 强调色，`"auto"` 跟随 Windows 系统 | `"auto"` |
| `appearance.reduceMotion` | 关掉过渡动画 | `false` |
| `display.railTrigger` | 触发形态 `always`/`edge`/`edge-hover` | `"always"` |
| `display.sidebarSide` | 停靠在 `left` 还是 `right` | `"right"` |
| `workspace.presets` | 三套工作区预设（名字 + 模块列表） | 默认/上课/批注 |
| `rail.order` | rail 图标顺序（长按排序后写入） | `[]` |
| `capture.inkToShape` | 墨迹转形 | `true` |
| `floatBall.idleOpacity/idleDelayMs` | 悬浮球闲时变暗 | `0.55` / `4000ms` |

界面尺寸、颜色、动效这些规格集中在仓库根的 `design-tokens.json` 里，编译时嵌进程序。想改 UI 规格就先改这个文件，`DesignTokensTests` 会盯着别改坏。

## 技术栈

| 层 | 用什么 | |
|------|------|------|
| UI | [Avalonia](https://avaloniaui.net/) 11.x + Svg.Skia | 界面 |
| 运行时 | .NET 10，自包含单文件约 15MB | 开了 ReadyToRun 和裁剪 |
| 核心逻辑 | `SmartSideBAR.Core` | 配置、调度、墨迹识别这些，不依赖 UI，能单测 |
| 平台层 | `SmartSideBAR.Windows` | AppBar、热键、录屏、USB 监控，走 P/Invoke + WinRT |
| 测试 | xUnit | 130 个用例：Core 94 + Windows 17 + Avalonia 19（含 headless 冒烟和像素基线） |

## 开发命令

```bash
dotnet build SmartSideBAR.slnx -c Debug     # 构建
dotnet test SmartSideBAR.slnx               # 全部测试（130 个）
dotnet run --project src/SmartSideBAR.Avalonia   # 跑起来
dotnet format SmartSideBAR.slnx --verify-no-changes  # 格式检查，CI 会卡这个
dotnet publish src/SmartSideBAR.Avalonia -c Release -r win-x64 ...  # 发布
```

CI 做的事：扫密钥 → 构建/测试/覆盖率(≥40%)/格式检查 → 上传产物；打 `v*` 标签时用 Inno 打安装包、建 Release 草稿；另外有个 nightly 每晚跑三遍全量测试抓偶发问题。

## 目录结构

```
SmartSideBAR/
├── design-tokens.json            # 界面规格（几何/颜色/动效/阴影）
├── Directory.Build.props         # 版本号写在这（<Version>）
├── src/
│   ├── SmartSideBAR.Avalonia/    # 主线 UI
│   │   ├── Ui/                   # 主题、窗口、面板、Toast、悬浮球、批注
│   │   ├── Views/                # SidebarWindow（rail + 面板容器）
│   │   ├── ViewModels/           # rail 与面板的视图模型
│   │   └── WindowManager.cs      # 窗口生命周期、动作分发
│   ├── SmartSideBAR.Core/        # 纯逻辑，可单测
│   │   ├── Configuration/        # 三层配置
│   │   ├── Design/               # design-tokens 的 C# 镜像
│   │   ├── Ink/                  # 墨迹识别
│   │   └── Links/ Scheduling/ FloatBall/ Devices/ Diagnostics/ ...
│   └── SmartSideBAR.Windows/     # Win32/WinRT 平台层
│       ├── AppBar/               # 系统工作区预留（最大化窗口不压侧栏）
│       └── Recording/ Capture/ Devices/ Input/ Shell/ ...
├── tests/                        # Core.Tests / Windows.Tests / Avalonia.Tests
├── installer/                    # Inno Setup 脚本
├── packaging/winget/             # winget 清单
├── scripts/                      # bench-p28.mjs 性能采样
└── docs/                         # ARCHITECTURE.md 结构速览
```

## 几件实现上的事

**动效**：全局统一了几档时长（微反馈 120ms、面板 180ms、窗口 250ms、菜单交错 32ms），都从令牌取。`reduceMotion` 打开后全部跳过。触屏按下去是填充加深加轻微缩放，选中是填充加贴边色条。

**稳定性**：单实例（二次启动会把已有窗口唤醒）；AppBar 注册失败就退回置顶模式，置顶模式下每 30 秒重申一次（一体机睡眠唤醒后置顶经常掉）；未处理异常写成 `crash-*.json`；日志按天滚动，留 14 天或 10MB 就清；截图和批注都有超时保护。

**多屏与 DPI**：业务坐标统一用 DIP，缩放 100%–250% 都试过（含 1.75/2.25/2.5 这种非整数）。侧栏、悬浮球、板中板都跟着侧栏所在屏走。显示器热插拔会重新定位。全屏检测带 1.2 秒防抖——不加的话，窗口切换动画期间会把侧栏莫名其妙收起来。

**体积**：自包含单文件约 15MB。同样的功能用 Electron 做出来是 100MB 以上。

## 系统要求

- Windows 10 1803+ 或 Windows 11，64 位
- 4GB 内存以上
- 1366×768 到 3840×2160，系统缩放 100%–250%
- 开发要 .NET 10 SDK；只有 `scripts/bench-p28.mjs` 需要 Node.js 20+

## 参与开发

有想法开 Issue，或者直接发 PR。主线改动交之前跑一下 `dotnet test`；动了 `design-tokens.json` 记得同步 `DesignTokensTests`。改架构先改 `docs/ARCHITECTURE.md`。

## 许可证

[Apache-2.0](LICENSE) © 2026 ClassTechStar
