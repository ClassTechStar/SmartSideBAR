# SmartSideBAR 渐进式迭代开发方案（参考 CyreneCompass / DiffiTouch）

> 参考项目：`CyreneCompass`（Tauri 2 + Vue 3 + GSAP，Windows 触屏快捷启动器，26.0.5）
> 参考项目：`DiffiTouch`（PySide6，类 AssistiveTouch 的 PC 平板悬浮球）
> 目标项目：`SmartSideBAR`（C# / .NET 10 + Avalonia 11，教学大屏侧边工具，主分支 `main`）
>
> **前置说明**：仓库工作区当前检出在归档分支 `archive/electron-final`，本方案针对的是 **Avalonia 主线 `main`**（含 2026-10-27 已落地的 P0/P1/P2）。后续开发请先 `git checkout main`。
> 本文结论均来自实际源码，证据以「文件路径 + 类名/成员名」标注；未确认项单列，不作臆测。

---

## 步骤一：架构梳理

### 1.1 CyreneCompass

#### 1.1.1 目录结构与分层

| 层 | 内容 | 证据 |
|---|---|---|
| 前端（Vue） | `src/App.vue`、`src/CompassOverlay.vue`、`src/main.js`、`src/theme.js`、`src/i18n.js`、`src/updater.js` | `src/` |
| 前端工具 | `src/utils/{compassMode.js, floatingBallStyle.js, physicalDrag.js, windowPosition.js}` | `src/utils/` |
| 前端组件 | `src/components/{FluentSwitch.vue, FluentNumberInput.vue, SettingNumberField.vue, FloatingImageCropper.vue}` | `src/components/` |
| 图标资源 | `src/fluent-core.json`、`src/fluent-icons.json`（离线 Iconify 包） | `src/` |
| 后端（Rust） | `src-tauri/src/{main.rs, lib.rs, compass_mode.rs}` | `src-tauri/src/` |
| 配置与权限 | `src-tauri/tauri.conf.json`、`src-tauri/capabilities/default.json`、`src-tauri/windows-app-manifest.xml`、`src-tauri/Cargo.toml` | `src-tauri/` |
| 测试/脚本 | `tests/*.test.mjs`、`scripts/rename-installer.mjs` | 仓库根 |

分层：Vue 视图 → Tauri `invoke` 命令 → Rust（Win32 / 系统能力）。Rust 侧以 `cyrene_compass_lib` 库 crate 暴露，`main.rs` 仅三行（`fn main() { cyrene_compass_lib::run() }`）。

#### 1.1.2 技术栈与构建

- 包管理器 `bun@1.3.14`；Vite 6.3.5 构建；Vue 3.5 + Pinia 3 + vue-router 4 + **GSAP 3.13**（动画）；`@iconify/vue` + `@iconify-json/fluent`；`vue-fluent-widgets`（第三方 Fluent 组件）；`@tauri-apps/api` 2.11 + `plugin-dialog` 2.7。
- Rust：`tauri 2`（features `tray-icon`）、`serde`/`serde_json`、`tauri-plugin-dialog`、`base64 0.22`、`reqwest 0.12`（rustls + stream）；Windows 专用 `windows-sys 0.59`（Shell / WindowsAndMessaging / Input / Security 等 feature）。
- 脚本：`dev`=vite、`build`=vite build、`test`=`node --test tests/*.test.mjs`、`tauri:build`=`tauri build && node scripts/rename-installer.mjs`。
- 打包：`tauri.conf.json` → NSIS，`installMode: currentUser`，`webviewInstallMode: embedBootstrapper`，双语安装界面（English / SimpChinese）。

#### 1.1.3 插件 / 扩展机制

- 无独立插件系统；能力边界由 **Tauri capabilities 权限清单**声明：`src-tauri/capabilities/default.json` 对 `main`、`compass-overlay` 两个窗口授予 `core:default`、`core:window:allow-show/hide/set-size/set-position/set-focus/start-dragging`、`dialog:default`。
- 图标扩展：离线 Iconify JSON 包（`fluent-icons.json` 按需 `import()` 懒加载，见 `App.vue:148`）。
- UI 扩展：第三方 `vue-fluent-widgets` 提供 `FluentCard`、`FluentPersonPicture`（`App.vue:131`）。

#### 1.1.4 配置与数据持久化

- 路径：`lib.rs:141-143` —— `config_dir() = installation_dir()/data`，`config_path() = data/cyrene-compass.json`。
- 读写：`#[tauri::command] load_settings() -> Option<serde_json::Value>`、`save_settings(value: serde_json::Value)`（`lib.rs:145-163`）——**以无 schema 的 JSON 值整体存取**。
- 默认值与迁移：前端 `baseItems()` 提供默认条目（`App.vue:149`）；`migrateLegacyDefaults(value)`（`App.vue:150`）按「id + 旧 label + 旧 icon + 旧 target」四元组匹配后替换为新文案/图标/目标，递归处理 `children`，属于**显式的历史默认值迁移**。
- 位置持久化：`set_ball_anchor(x,y)` 记录锚点，`restore_ball_position` 恢复（`lib.rs:164-177`）。

#### 1.1.5 UI 组件体系与渲染 / 动画

- 动画用 **GSAP**（`App.vue:126`）；窗口为无边框透明（`tauri.conf.json` 中 `decorations:false, transparent:true, shadow:false, backgroundColor:#00000000`）。
- 主题：`src/theme.js` 的 `createThemeVariables(accentValue, dark, neutral)` —— **由单一强调色经 HSL 推导整套 CSS 变量**：`--accent`、`--accent-light/dark/hover`、`--accent-50/200`（带透明度的强调色）、`--bg-base/card/card-solid/hover`、`--text-primary/secondary/muted`、`--border-default/subtle/strong`，并支持 `neutral`（中性灰）与 `dark` 变体。
- 触控可达性：README 声明「无论 UI 缩放如何，每个罗盘元素的物理点击区域保底 **32 px**」；UI 缩放最低 50%。
- 悬浮球样式：`floatingBallStyle.js` 提供 `resolveBallRadius(style, radius)`（文字/图片样式按半径，其余固定 50）、`coverScale`、`clampCropOffset`、`dataUrlByteLength`（限制图片 ≤12MB）。

#### 1.1.6 消息事件与通知机制

- 前端 ⇄ 后端：`invoke`（`@tauri-apps/api/core`）+ `listen`（`@tauri-apps/api/event`）双向（`App.vue:127-129`）。
- 提示：App.vue 内 `toast` ref 做轻量提示；文件选择用 `@tauri-apps/plugin-dialog` 的 `open`。
- 事件节流：`src/utils/physicalDrag.js` 的 `createLatestMoveQueue(worker)`——**保留最新一次位移、丢弃中间样本**的合并队列；`createPointerMoveState` 把指针状态与该队列绑定（`physicalDrag.js:51-58`）。

#### 1.1.7 日程与时间轴逻辑

- 无课表/日程能力。时间相关的是若干**交互时序常量**：`compass_mode.rs:20-23` 的 `POLL_INTERVAL = 12ms`、`HIDE_DELAY = 170ms`、`BASE_DEAD_ZONE = 42.0`；长按 Tab 触发（`hold_ms` 可配）；无操作自动收起默认 10 秒（3–30 秒可调，README）。
- 方向选择：`compass_mode.rs:45` 的 `compass_direction(dx, dy, current, dead_zone_px)`——先做死区判定（`len < dead_zone` 返回 -1），再按 45° 分扇区；**已选中方向带迟滞**：`angular_distance(angle, current_center) <= 22.5` 时保持原选择，避免边界抖动。

#### 1.1.8 系统托盘与多屏适配

- 托盘：`Cargo.toml` 启用 `tauri.features = ["tray-icon"]`；托盘菜单仅「打开设置」，退出需在设置页**三次确认**（README）。
- 单实例：回环 TCP（`lib.rs:118-125` 的 `addr()`、`ping_existing()`、`start_instance_listener`），README 称可检测**不同完整性级别**启动的已有实例。
- 多屏/位置：`lib.rs:84` `restore_target(x,y,w,h,candidate,primary)` —— 先判断记录位置是否完整落在候选工作区内（`fits`），否则回退到主屏居中并置 `reset: true`；`clamp_target`（`lib.rs:108`）把目标夹取进工作区。前端 `windowPosition.js` 的 `positionForRestore` 校验整数与 i32 范围，非法值返回 `{x: I32_MAX, y: I32_MAX}`。
- DPI：`windows-app-manifest.xml`（Per-Monitor V2）；拖拽换算集中在边界做一次：`pointerDragTarget(previousTarget, previousScreen, currentScreen, scaleFactor)`（`physicalDrag.js`）；触屏拖拽另走 Rust 线程 `spawn_touch_drag_loop(generation, hwnd)` + `touch_drag_step`，用 `generation` 作代次防串扰（`lib.rs:205-262`）。
- 置顶：README 称 `TOPMOST + WS_EX_NOACTIVATE`，可显示在 UWP / PPT 放映上方且**不抢焦点**。

#### 1.1.9 性能与资源占用优化

- 指针/触摸事件合并（`createLatestMoveQueue`）、触屏拖拽下沉到 Rust 线程（避免主线程抖动）。
- 方向判定死区 + 迟滞，减少状态抖动导致的重绘。
- WebView 启动参数关闭后台节流与遮挡计算（`tauri.conf.json` 的 `additionalBrowserArgs`：`--disable-backgrounding-occluded-windows`、`--disable-renderer-backgrounding`、`--disable-background-timer-throttling`、`--disable-features=...CalculateNativeWinOcclusion...`）——不适用于 Avalonia，仅作思路参考。
- 图标包懒加载（`App.vue:148` 动态 `import` 全量图标）。
- 轮询间隔 12ms + 隐藏延迟 170ms，控制钩子开销。

---

### 1.2 DiffiTouch

#### 1.2.1 目录结构与分层

扁平模块结构（无子包）：

| 类别 | 文件 | 证据 |
|---|---|---|
| 入口 / 主窗体 | `main.py`（`class FloatingFrame(QWidget)`、`PlaceholderButton`） | `main.py:83, 56` |
| 功能页面 | `brightness_page.py`、`resolution_page.py`、`sleeeptime_page.py`、`rotate_page.py`、`sound_page.py`、`network_page.py`、`inputcontrol_page.py`、`application_page.py`、`bt_page.py` | 仓库根 |
| 能力工具 | `bt_utils.py`、`network_utils.py`、`resolution_utils.py`、`sound_utils.py`、`sleeputils.py`、`qrcode_utils.py`、`icon_utils.py`、`shell_utils.py`、`window_utils.py`、`linux_utils.py`、`linux_utils_old.py` | 仓库根 |
| 配置 | `iniConfig.py`（`class IniFile`、`IniSection`） | `iniConfig.py:12` |
| 内容注册 | `contents.py`（`BaseContent`、`@load_content`、`content_types`、`SettingsOption`） | `contents.py:34-90` |
| 触摸适配 | `touchsafe.py`（`TouchListView`） | `touchsafe.py:17` |
| 设置窗 / UI | `settings_window.py`、`uis/`（Qt Designer 编译产物） | 仓库根 |
| 资源 | `icon.ico`、`trash.png`、`trash.svg` | 仓库根 |

#### 1.2.2 技术栈与构建

- **PySide6（Qt 6）**：`QApplication / QWidget / QSystemTrayIcon / QPropertyAnimation / QParallelAnimationGroup / QEasingCurve / QMouseEvent / QPixmap / QPainter`（`main.py:5-34`）。
- 平台能力直接用 `ctypes` 调 Win32：`window_utils.py` 加载 `user32.dll`，定义 `SW_MAXIMIZE/SW_MINIMIZE/SW_RESTORE/WM_CLOSE/WS_MAXIMIZE`，提供 `get_foreground_window()`、`get_window_title(hwnd)`。
- 环境探测：`linux_utils.detect_environment()`（`contents.py:15`），跨 Linux / Windows。
- **未找到 `requirements.txt` / `pyproject.toml` / 打包配置**（见待确认项 Q1）。

#### 1.2.3 插件 / 扩展机制

- **内容注册表模式**：`contents.py` 定义 `BaseContent`（字段 `id / name / icon / secondary / right / pre_installed / is_sub / is_open_ui / open_ui / action`），并实现 `keys() / __getitem__ / __iter__` 使实例可被 `dict()` 转换；`@load_content` 装饰器把类注册进 `content_types[content.id] = dict(content())`（`contents.py:26-31`）。
- 页面模块为「Ui 类 + QFrame」组合：`class SoundPage(ui_sound_opt.Ui_Dialog, QtWidgets.QFrame)`（`sound_page.py:6`）。
- 依赖注入：`SettingsOption` 的 `_conf`（共享 `IniFile`）、`_on_opacity/_on_content/_on_size/_on_frame_opacity` 等回调由 `main.py` 注入（`contents.py:88-92`）。

#### 1.2.4 配置与数据持久化

- 自建 INI 解析：`iniConfig.py` 的 `IniSection` 用**双向链表**维护（`_prev/_next/_childHead/_childTail`，`addChild` / `removeChild`），正则 `^\[(\w+)\]` 识别段、`^(\w+)\s*=\s*(\S+)` 识别键值（`iniConfig.py:13-15`）；`class IniFile` 供 `main.py` 与各页面共享。
- 无版本迁移机制（未见 version 字段或迁移函数）——见待确认项 Q2。

#### 1.2.5 UI 组件体系与渲染 / 动画

- 纯 Qt Widgets（无 QML）：手动 `QVBoxLayout/QHBoxLayout/QGridLayout` 布局；`QFrame` 容器；自绘 `QPixmap / QPainter / QColor / QFont`。
- 动画：`QPropertyAnimation` + `QParallelAnimationGroup` 做**页面横滑**：`finish_slide_animation(direction)` 中当前页与「影子页」两条 `pos` 动画并行，时长 **150ms**，`QEasingCurve.OutCubic`（`main.py:462-493`）；`cancel_slide_animation()` 对称回弹（`main.py:494-523`）。
- 触摸滚动：`touchsafe.py` 的 `TouchListView` 设 `ScrollPerPixel`、`setMouseTracking(True)`、拖拽阈值 **10px**，自行处理 `mousePress/Move/Release`；`sound_page.py` 对 `scrollArea.viewport()` 设 `WA_AcceptTouchEvents` 并安装事件过滤器，维护 `_touch_start_y / _scroll_orig / _is_dragging`（`sound_page.py:10-19`）。
- 触摸翻页：`FloatingFrame.touchEvent` 处理 `TouchBegin/TouchUpdate`，位移 <5px 忽略，按 `delta_x` 决定翻页方向并准备影子页（`main.py:264-300`）。

#### 1.2.6 消息事件与通知机制

- Qt 信号/槽；跨模块通过**注入回调**（`_on_opacity`、`_on_content`、`_on_size`、`_on_frame_opacity`）通信，而非全局事件总线。
- 托盘：`QSystemTrayIcon` + `QMenu` + `QAction`（`main.py:13-15`）。

#### 1.2.7 日程与时间轴逻辑

- 无日程/课表；`sleeeptime_page.py` + `sleeputils.py` 提供**休眠/待机**控制（属于系统电源动作，非日程）。

#### 1.2.8 系统托盘与多屏适配

- 托盘：`QSystemTrayIcon`（`main.py:13`）。
- 窗口控制：`window_utils.py` 对前台窗口执行最大化切换 / 最小化切换 / 关闭（ctypes + `WM_CLOSE`），并用 `WS_MAXIMIZE` 判断最大化状态。
- 显示控制：`resolution_page.py` / `resolution_utils.py`（分辨率）、`rotate_page.py`（旋转）、`brightness_page.py`（亮度）——面向平板的显示调节集合。
- 多屏：未见显式的多屏索引/工作区校验逻辑（见待确认项 Q3）。

#### 1.2.9 性能与资源占用优化

- 触摸路径自管：阈值过滤（5px 翻页 / 10px 滚动）+ 影子页复用，避免整页重建。
- 动画时长控制在 150ms，双动画并行组一次性启动。
- 未见内存/定时器专项优化（见待确认项 Q4）。

---

## 步骤二：可迁移点筛选（对照 SmartSideBAR 现状）

> 现状基线（`main`）：三层结构 `SmartSideBAR.Core`（Capture/Configuration/Design/Devices/Diagnostics/Extensions/FloatBall/Ink/Input/Links/Messaging/Policy/Rules/Scheduling）、`SmartSideBAR.Windows`（AppBar/Audio/Capture/Devices/Input/Native/Recording/Shell）、`SmartSideBAR.Avalonia`（Ui/Views/ViewModels/WindowManager/CrashLog/Diagnostics）；已具备 `design-tokens.json` + `DesignTokens.cs`、`ConfigService` 三层配置、`Theme.cs`/`ThemeRegistry.cs`、`ClockHost.cs`（统一时钟）、`NotificationQueue`（优先级通知）、`MemoryWatchDogHost`、`ConfigSubscription`、`UnifiedClock`、`Recurrence`、`Ruleset`、`ExtensionRegistry`。测试 180 项。

| # | 借鉴点（来源） | 适用模块与场景 | 预期收益 | 与现有实现的差异 | 改造成本 | 优先级 |
|---|---|---|---|---|---|---|
| T1 | **窗口位置恢复的完整校验 + 工作区夹取 + reset 标志**（Cyrene `lib.rs:84 restore_target` / `:108 clamp_target` / `windowPosition.js positionForRestore`） | `FloatBallWindow.cs`、`Core/FloatBall/FloatBallLayout.cs`、`WindowManager.cs` | 拔掉副屏 / 改分辨率 / 休眠唤醒后，悬浮球与侧栏不会落在不可见区域或压住任务栏；越界回到主屏中央并修正记录 | 现状持久化「相对工作区偏移」并 `ResolveBallPosition` 读回，**未见完整性校验与任务栏避让** | 低（1 人天） | **P0** |
| T2 | **触屏目标最小 32px 保底**（Cyrene README；`floatingBallStyle.resolveBallRadius`） | `DesignTokens` / 面板与 rail 控件尺寸计算 | 大屏触屏下 40–96 DIP 范围内缩放仍保证可点中，减少误触 | 现状尺寸全部来自令牌，无「物理最小点击区」下限约束 | 低（1 人天） | **P0** |
| T3 | **最新位移合并队列 + 边界一次性 DPI 换算**（Cyrene `physicalDrag.js: createLatestMoveQueue / pointerDragTarget / exceedsDragThreshold`） | `FloatBallWindow.cs` 拖拽、`Ink/InkCanvas.cs` 笔迹采样 | 触屏高频采样下拖动不再抖动/掉帧，且消除「相对位移累加」类问题（历史上 B22 同类） | 现状 `FloatBallWindow` 用 `PointToScreen − 抓取偏移`；无采样合并 | 中（1.5 人天） | **P1** |
| T4 | **方向选择的死区 + 迟滞**（Cyrene `compass_mode.rs:45 compass_direction` + `angular_distance`） | `FloatBallWindow.cs` 扇形菜单方向选中 | 手指在扇区边界微动时不会来回跳选 | 现状扇形菜单按角度选扇区，**未见死区与迟滞** | 低（0.5 人天） | **P1** |
| T5 | **置顶但不抢焦点（WS_EX_NOACTIVATE）**（Cyrene README） | `OverlayWindow.cs`、`AnnotateWindow.cs`、`FloatBallWindow.cs` | 侧栏/悬浮球弹出时不打断教师当前输入焦点（PPT 放映、白板书写） | 现状用 `Topmost`，**未见 NOACTIVATE 设置** | 中（1 人天，需 P/Invoke 扩展样式） | **P1** |
| T6 | **快捷设置补充课堂常用滑杆**（DiffiTouch `brightness_page/sound_page` + `*_utils`） | `Ui/Panels/QuickSettingsPanel.cs`、`Ui/SettingsWindow.cs` | 亮度/音量直接在侧栏调节，减少课堂中断 | 现状快捷设置为 6 个开关（液态玻璃/减弱动效/全屏收起/系统色/USB/录屏），**无亮度/音量** | 中（2 人天） | **P1** |
| T7 | **内容注册表驱动面板**（DiffiTouch `contents.py` 的 `BaseContent` + `@load_content`；Cyrene `baseItems()` 的 id/children 树） | `Ui/Panels/*`、`Core/Extensions` | 面板由「id/图标/动作/子项」数据描述，新增面板少写代码；与已落地的 `ExtensionRegistry` 天然衔接 | 现状面板为代码构建类（一面板一文件），无声明式描述 | 高（4 人天） | **P2** |
| T8 | **触摸滚动适配**（DiffiTouch `touchsafe.TouchListView`、滚动视口事件过滤器 + 阈值） | `ReminderPanel.cs`、`LinksPanel.cs`、`SettingsWindow.cs` 的滚动区 | 触屏拖动列表顺滑（ScrollPerPixel + 阈值），避免误触发点击 | 现状依赖 Avalonia `ScrollViewer`，触屏平移参数未调优 | 中（1.5 人天） | **P2** |
| T9 | **分页滑动动画（影子页并行动画）**（DiffiTouch `main.py:462 finish_slide_animation`，150ms OutCubic） | 面板多页场景（如链接分类页签、9 宫格分页） | 翻页有连续过渡而非硬切 | 现状动效令牌已定义 180ms 面板动效，但**无分页转场** | 中（1.5 人天） | **P2** |
| T10 | **应用内更新检查与下载安装**（Cyrene `updater.js` + `lib.rs:310 check_update` / `:402 download_and_launch_update`，reqwest + 安装包名正则 `isInstaller`） | 新增 `Avalonia/Updater`；`SettingsWindow` 关于页 | 教室机无需手动下安装包；与现有 winget 渠道互补 | 现状无应用内更新（README 仅提 winget） | 中高（3 人天） | **P2** |
| T11 | **显式历史默认值迁移**（Cyrene `App.vue:150 migrateLegacyDefaults`） | `Core/Configuration/ConfigService.cs` | 旧版本默认条目/文案能被精确识别并替换，避免「老用户配置看起来不对」 | 现状有 `Version` 字段与 `Normalize/Clamp`，**无针对默认条目的迁移** | 低（1 人天） | **P2** |
| T12 | **单实例可识别不同完整性级别**（Cyrene `lib.rs:118-125` 回环 TCP + `ping_existing`） | `Avalonia/SingleInstance.cs` | 管理员权限启动的实例也能被正确唤醒而不是新开进程 | 现状用命名互斥量 + 命名事件 | 低（0.5 人天） | **P2** |
| T13 | **i18n 字典（zh/en）**（Cyrene `src/i18n.js`） | 全局 | 支持英文界面 | 现状纯中文硬编码 | 高 | **不建议**（当前场景为国内教室） |
| T14 | **INI 链表配置解析**（DiffiTouch `iniConfig.py`） | — | — | 与现有 JSON 三层配置体系冲突 | — | **不迁移** |

---

## 步骤三：迭代方案

> 通用约束：每次改动后必须 `dotnet build` 0 错误、`dotnet test` 全绿（≥180 项，只增不减）、`dotnet format --verify-no-changes` 通过；像素基线 3 态断言通过；界面规格仍以 `design-tokens.json` 为唯一来源。

### P0 —— 位置与可达性兜底（约 2 人天）

#### P0-1 悬浮球位置恢复的完整校验与夹取（对应 T1）

- **目标**：恢复位置时校验是否完整落在工作区内，越界/压任务栏则回主屏中央并修正持久化记录。
- **涉及模块 / 关键文件**：
  - 新增 `src/SmartSideBAR.Core/FloatBall/FloatBallPositionPolicy.cs`（纯逻辑：`FitsWorkArea` / `Clamp` / `Restore`，返回是否 reset）
  - 修改 `src/SmartSideBAR.Avalonia/Ui/FloatBallWindow.cs`（`ResolveBallPosition` 调用处）
  - 复用 `src/SmartSideBAR.Windows/Native/Win32Display.cs` 的 `WorkAreaByIndex` / `EnumerateWorkAreas`（已落地）
- **改动范围**：新增 1 文件；修改 1 处调用（不改变持久化字段语义）
- **技术难点与风险**：多屏热插拔时工作区数量变化 → 用已落地的 `EnumerateWorkAreas()` 实时枚举，索引越界回落主屏；任务栏占据需以 `rcWork` 而非 `rcMonitor` 判定（现有 `WorkAreaByIndex` 已返回工作区）。
- **验证**：新增单测（越界回中 + reset、边界内原样、宽高大于工作区时的夹取）；双屏真机拔出副屏后重启验证。
- **回滚**：新增逻辑置于独立方法，旧路径保留为 `LegacyResolve`，由配置开关 `debug.legacyPosition` 切换。

#### P0-2 触屏目标最小点击区（对应 T2）

- **目标**：任意 UI 缩放下，可交互元素的物理点击区不小于 32px。
- **涉及模块 / 关键文件**：`design-tokens.json`（新增 `touch.minHitPx`）、`src/SmartSideBAR.Core/Design/DesignTokens.cs`（强类型镜像）、`src/SmartSideBAR.Avalonia/Ui/Panels/PanelKit.cs`（磁贴尺寸计算）、`Views/SidebarWindow.axaml.cs`（rail 条目尺寸）
- **改动范围**：令牌新增 1 项；尺寸计算处统一走 `Math.Max(计算值, minHitPx / 缩放)`
- **技术难点与风险**：放大最小点击区可能挤压既有布局 → 先在 rail 与快捷设置磁贴两处生效，观察后再推广；最小尺寸只在元素小于阈值时生效，不影响既有等大卡片布局。
- **验证**：像素基线断言保持通过；150% 缩放下真机点选 rail 与磁贴；新增单测覆盖「阈值生效 / 未触发时尺寸不变」。
- **回滚**：令牌默认值设为 0 即等同现状。

### P1 —— 触屏体验与课堂常用能力（约 6 人天）

| 任务 | 对应 | 目标 | 关键文件 | 改动范围 | 难点与应对 | 人天 | 验证 / 回滚 |
|---|---|---|---|---|---|---|---|
| P1-1 拖拽采样合并 + 边界 DPI 换算 | T3 | 指针/触摸位移在边界换算一次，高频样本合并为「最新一次」 | 新增 `Core/FloatBall/PointerDrag.cs`（纯逻辑：`DragTarget(prevTarget, prevScreen, curScreen, scale)`、`ExceedsThreshold`、`LatestMoveQueue`）；修改 `Ui/FloatBallWindow.cs` 拖拽处理 | 新增 1 文件 + 改拖拽处理 | 合并队列异步顺序 → 单线程串行消费、只保留最新；换算仅在边界做一次 | 1.5 | 单测（换算、阈值、队列只保留最新）；真机触屏拖动无抖动；旧逻辑由开关保留 |
| P1-2 扇形菜单死区与迟滞 | T4 | 方向选择加入死区（建议 24–42 DIP，可配）与角度迟滞（22.5° 内保持） | 新增 `Core/FloatBall/DirectionSelector.cs`（纯逻辑）；修改 `Ui/FloatBallWindow.cs` 扇形选中 | 新增 1 文件 + 改选中逻辑 | 死区过大导致难以选中 → 默认值取令牌并可调，先按 32px 试 | 0.5 | 单测（死区返回 -1、边界迟滞保持、跨越切换）；真机手指沿边界移动不跳选 |
| P1-3 置顶不抢焦点 | T5 | 侧栏/悬浮球/覆盖层置顶但不激活 | 新增 `Windows/Native/Win32WindowStyle.cs`（`SetNoActivate` 扩展样式）；修改 `Ui/FloatBallWindow.cs`、`Ui/OverlayWindow.cs`、`WindowManager.cs` | 新增 1 文件 + 3 处窗口创建后设置 | 设置 NOACTIVATE 后输入框可能无法接收键盘 → 仅对悬浮球/覆盖层生效，设置窗与批注输入窗不启用 | 1 | 真机在 PPT 放映/白板书写时弹出悬浮球，验证焦点未转移；像素基线不变 |
| P1-4 快捷设置加亮度/音量 | T6 | 侧栏快捷设置内直接调屏幕亮度与系统音量 | 新增 `Windows/Devices/BrightnessService.cs`、`Core/Devices/VolumeModel.cs`；修改 `Ui/Panels/QuickSettingsPanel.cs` | 新增 2 文件 + 面板新增滑杆 | 亮度在无背光控制接口的台式机上不可用 → 能力探测失败则隐藏滑杆；音量需 Windows Core Audio（可用 `Core/Devices` 现有封装扩展） | 2 | 单测（值钳制 0–100）；教学一体机验证亮度/音量联动；开关 `quickSettings.brightness/volume` 可关 |

### P2 —— 结构演进与增值能力（约 11 人天，按需启动）

| 任务 | 对应 | 目标 | 关键文件 | 难点与应对 | 人天 | 验证方式 |
|---|---|---|---|---|---|---|
| P2-1 面板内容注册表 | T7 | 面板由 `id/名称/图标/动作/子项` 数据描述，注册进统一表 | 新增 `Core/Panels/{PanelDefinition.cs, PanelRegistry.cs}`；改造 `Ui/Panels/*` 逐个登记 | 与已落地 `ExtensionRegistry` 的 API 版本校验衔接；先新增注册层，面板改造逐个进行，不一次性重写 | 4 | 全量测试不回归；冒烟「面板打开」链路保持；新旧渲染路径并存 |
| P2-2 触摸滚动适配 | T8 | 列表按像素滚动 + 10px 阈值，触屏拖动不误触发点击 | 修改 `Ui/Panels/ReminderPanel.cs`、`LinksPanel.cs`、`Ui/SettingsWindow.cs` 滚动区；新增 `Ui/TouchScroll.cs` 行为 | Avalonia 触屏平移参数需实测；先只装在提醒/链接两个列表 | 1.5 | 真机触屏滚动与点选；像素基线不变 |
| P2-3 分页转场动画 | T9 | 多页面板切换走滑动转场（150–180ms，OutCubic） | 新增 `Ui/PageTransition.cs`；改造 `LinksPanel` 分类页签 | 与既有 `reduceMotion` 联动，关闭动效时直接切换 | 1.5 | 动效开启/关闭两态；headless 冒烟不报错 |
| P2-4 应用内更新检查 | T10 | 关于页可检查更新、下载并启动安装包 | 新增 `Avalonia/Updater/{UpdateService.cs, UpdateModels.cs}`、`Core/Diagnostics/VersionComparer.cs`；修改 `Ui/SettingsWindow.cs` 关于页 | 仅信任 HTTPS + GitHub Release 资产名白名单；下载校验大小；失败可重试 | 3 | 单测版本号比较与资产名匹配；离线/404 场景降级提示；开关 `updater.enabled` 默认关 |
| P2-5 历史默认值迁移 | T11 | 对旧版本写入的默认条目做精确识别并替换 | 修改 `Core/Configuration/ConfigService.cs`；新增 `Core/Configuration/DefaultItemMigration.cs` | 按「id + 旧值组合」匹配，避免误伤用户自定义 | 1 | 单测（命中替换 / 已自定义则不动） |
| P2-6 单实例完整性级别识别 | T12 | 管理员与普通权限实例互能唤醒 | 修改 `Avalonia/SingleInstance.cs`（在互斥量之上加回环探测） | 端口冲突则沿用现有互斥量路径 | 0.5 | 分别以普通/管理员启动验证只存在一个实例 |

---

## 阶段里程碑

| 里程碑 | 包含任务 | 周期 | 结束后 SmartSideBAR 具备的能力 |
|---|---|---|---|
| **M1 位置与可达性兜底** | P0-1、P0-2 | 约 1 周（2 人天） | 拔屏/改分辨率/休眠唤醒后悬浮球与侧栏必然可见且不压任务栏，越界自动回中并修正记录；触屏任意缩放下点击区不小于 32px。对外功能与视觉零变化。 |
| **M2 触屏体验与课堂能力** | P1-1 ~ P1-4 | 约 2–3 周（6 人天） | 触屏拖拽平滑不抖动、扇形菜单边界不跳选、悬浮球弹出不抢教师输入焦点；侧栏可直接调亮度与音量，课堂中断减少。 |
| **M3 结构演进与增值** | P2-1 ~ P2-6（按需） | 6 周以上（11 人天） | 面板数据化描述（便于扩展与第三方登记）、列表触屏滚动顺滑、分页转场、可选的应用内更新、默认值迁移与跨权限单实例识别。 |

**贯穿约束**：每个里程碑结束都需满足 —— `dotnet build` 0 错误、`dotnet test` 全绿（≥180 项）、`dotnet format --verify-no-changes` 通过、像素基线 3 态断言通过、界面规格仍以 `design-tokens.json` 为唯一来源。

---

## 待确认项

| # | 项目 | 待确认内容 |
|---|---|---|
| Q1 | DiffiTouch | 依赖与打包方式：未找到 `requirements.txt` / `pyproject.toml` / 打包配置，实际依赖与分发方式未知 |
| Q2 | DiffiTouch | `iniConfig.py` 是否存在版本迁移（未见 version 字段或迁移函数） |
| Q3 | DiffiTouch | 是否存在多屏索引/工作区校验（仅在 `window_utils.py` 见到前台窗口操作） |
| Q4 | DiffiTouch | 是否有定时器/内存专项优化（未见） |
| Q5 | CyreneCompass | `WS_EX_NOACTIVATE` 的具体设置位置（仅 README 提及，未在 `lib.rs` 前 450 行检索到） |
| Q6 | CyreneCompass | 触屏拖拽 Rust 线程中 `generation` 的完整并发语义（仅确认其作为代次参数传递） |
| Q7 | CyreneCompass | `save_settings` 是否做写入原子性保护（未见临时文件+替换逻辑） |
| Q8 | SmartSideBAR | `FloatBallWindow.ResolveBallPosition` 现有实现是否已含工作区校验（P0-1 实施前需读全文确认） |
| Q9 | SmartSideBAR | 目标机是否支持亮度调节接口（决定 P1-4 的亮度滑杆是否可用，需做能力探测） |

---

## 实施状态（2026-10-29）

按 P0 → P1 → P2 顺序落地，不确定项按约定跳过。开发前已从归档分支 `archive/electron-final` 切回 `main`。

### 已完成

| 任务 | 落地内容 | 关键文件 |
|---|---|---|
| P0-1 位置恢复校验 | `FloatBallPositionPolicy`（Fits / Restore / Clamp + reset 标志）；悬浮球启动时校验记录位置，越界回主屏中央并**回写修正后的配置** | 新增 `Core/FloatBall/FloatBallPositionPolicy.cs`；改 `Avalonia/Ui/FloatBallWindow.cs` |
| P0-2 触屏最小点击区 | 令牌 `touch.minHitPx = 32`；`DesignTokens.TouchTokens`；`PanelKit.MinHitDip` 折算成 DIP，磁贴 `MinHeight` 取 max(60, MinHitDip) | `design-tokens.json`、`Core/Design/DesignTokens.cs`、`Avalonia/Ui/Panels/PanelKit.cs` |
| P1-1 拖拽换算 | `PointerDrag.Target`（上一目标 + 两次屏幕采样 × 缩放，换算只做一次）、`Exceeds` 阈值、`LatestMoveQueue` 最新样本合并队列 | 新增 `Core/FloatBall/PointerDrag.cs`；改 `FloatBallWindow.OnMove` |
| P1-2 方向选择（核心） | `DirectionSelector.Select` 死区 + 22.5° 迟滞 + `AngularDistance` | 新增 `Core/FloatBall/DirectionSelector.cs` |
| P1-3 置顶不抢焦点 | `Win32WindowStyle.SetNoActivate / ClearNoActivate`（WS_EX_NOACTIVATE）；悬浮球 Opened 后应用 | 新增 `Windows/Native/Win32WindowStyle.cs`；改 `FloatBallWindow` |
| P1-4 亮度 / 音量 | `BrightnessService`（WMI，IsSupported 探测）、`VolumeService`（Core Audio COM，IsSupported 探测）；快捷设置新增「课堂常用」滑杆，**设备不支持时整段不渲染** | 新增 `Windows/Devices/BrightnessService.cs`、`Windows/Audio/VolumeService.cs`；改 `Ui/Panels/QuickSettingsPanel.cs` |
| P2-1 面板注册表（核心） | `PanelDefinition` / `PanelRegistry`（注册、Order 排序、禁用名单过滤） | 新增 `Core/Panels/PanelRegistry.cs` |
| P2-2 触屏滚动 | `TouchScroll.Attach(ScrollViewer, threshold)`：按住拖动即滚动，10 DIP 阈值 | 新增 `Avalonia/Ui/TouchScroll.cs` |
| P2-3 分页转场 | `PageTransition.SlideAsync`：当前页滑出 + 影子页滑入并行，时长取 `motion.panelMs`，关闭动效时直接切换 | 新增 `Avalonia/Ui/PageTransition.cs` |
| P2-4 应用内更新 | `VersionComparer`（版本比较 + 安装包名匹配）、`UpdateService`（检查 / 下载 / 大小校验）；设置窗关于页「检查更新」入口 | 新增 `Core/Diagnostics/VersionComparer.cs`、`Avalonia/Updater/UpdateService.cs`；改 `Ui/SettingsWindow.cs` |
| P2-5 默认值迁移 | `DefaultItemMigration.ApplyLinks`：按「id + 旧名 + 旧链接」精确匹配才替换，用户改过的不动 | 新增 `Core/Configuration/DefaultItemMigration.cs` |
| P2-6 跨权限单实例 | 在命名互斥量之上加**回环端口持有式探测**（端口 51871），抢不到端口即认为已有实例；进程退出释放 | 改 `Avalonia/SingleInstance.cs`、`Program.cs` |

### 部分完成 / 需后续接入

| 任务 | 状态 | 说明 |
|---|---|---|
| ~~P1-2 接入扇形菜单~~ | **已接线**（2026-10-29 后续） | 新增 `FanArcSelector`（按本项目**弧段**布局判定，非 8 方向罗盘）：死区 24 DIP + 迟滞 4°，指针朝某方向移动即选中并高亮白边，回到死区取消；精确点中按钮仍走 `Click`（用 `IsPointerOver` 去重，不会双触发） |
| ~~P2-1 面板改用注册表驱动~~ | **已完成**（2026-10-29 后续） | 新增 `PanelCatalog`：元数据登记进 `Core/Panels/PanelRegistry`（id/名称/图标/Order），构造方式登记为工厂；`SidebarWindow.ShowPanel` 的 switch 换成 `PanelCatalog.Create(kind, context)`。新增面板只需注册一条定义 + 一个工厂 |
| ~~P2-2 / P2-3 接入具体面板~~ | **已接入**（2026-10-29 补完） | `TouchScroll.Attach(PanelScroll)` 接在 `SidebarWindow` 的面板滚动宿主上，提醒/链接/设置等**所有面板**一并生效；`PageTransition.SlideInAsync` 接入链接面板分类切换与提醒面板「提醒/通知」页签切换，关闭动效时直接显示 |

### 跳过（按"不确定的先别碰"约定）

| 项 | 原因 |
|---|---|
| 亮度/音量在无接口设备上的兜底行为 | 已用 `IsSupported` 探测并隐藏控件，未做更深入的厂商适配（待确认项 Q9） |
| Cyrene 中 `WS_EX_NOACTIVATE` 的具体设置位置 | 属对方实现细节（待确认项 Q5），我方按标准 SetWindowLongPtr(GWL_EXSTYLE) 实现，不影响落地 |

### 验证结果

- `dotnet build SmartSideBAR.slnx -c Release`：0 错误
- `dotnet test SmartSideBAR.slnx -c Release`：**220 项全过**（Core 184 / Windows 17 / Avalonia 19，较本轮前 180 项增加 40 项）
- `dotnet format --verify-no-changes`：通过
- 新增单测：位置策略、拖拽换算与合并队列、方向选择迟滞、面板注册表、默认值迁移、版本比较与安装包匹配

### 补完记录（2026-10-29 后续）

- **P2-2 触屏滚动接入**：面板内容统一由 `SidebarWindow.axaml` 的 `PanelScroll` 承载，
  在 `SidebarWindow` 构造里 `TouchScroll.Attach(PanelScroll)` 一次即可覆盖全部面板，
  无需逐面板改造（提醒、链接、设置、快捷设置等均生效）。
- **P2-3 分页转场接入**：新增 `PageTransition.SlideInAsync`（入场滑动，方向可正可负）。
  链接面板按分类字符串比较决定滑入方向，提醒面板按「提醒/通知」页签序号决定方向；
  首次构建不播放动画，`appearance.reduceMotion` 打开时直接显示。
- 验证补充：构建 0 错误；220 项测试全过（含 headless 冒烟 7 链路，动画在 Bounds 为 0 时自动跳过）；
  `dotnet format --verify-no-changes` 通过。

### 补完记录（2026-10-29 后续 · 第二轮）

- **P1-2 扇形菜单角度判定**：实际布局是一段圆弧（`FanStartDeg=4°`，`FanSweepDeg=92°`），
  不是绕一圈的 8 方向罗盘，因此新增 `Core/FloatBall/FanArcSelector`：
  先判死区（`FloatBallLayout.FanDeadZoneDip = 24`），再按弧段分索引，并带 4° 迟滞；
  悬浮球展开后 `OnMove` 走角度判定并高亮（白边 2.5px），`OnRelease` 按结果派发；
  指针正落在某个按钮上时交给按钮 `Click`，避免重复触发。
- **P2-1 面板注册表驱动**：新增 `Avalonia/Ui/Panels/PanelCatalog`（注册表 + 工厂 + `PanelContext`），
  `ShowPanel` 由 switch 改为 `PanelCatalog.Create(...)`；面板 id 与 `PanelKind` 的映射集中在目录里，
  定义被禁用时直接返回 null。headless 冒烟 7 链路仍全绿，说明渲染路径与原实现一致。
- 验证补充：构建 0 错误 0 警告；测试 **227 项全过**（Core 191 / Windows 17 / Avalonia 19）；
  `dotnet format --verify-no-changes` 通过。
