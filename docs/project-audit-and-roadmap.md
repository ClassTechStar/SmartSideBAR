# SmartSideBAR 项目体检与开发路线建议

> 分析对象：`C:\Users\18948\Documents\GitHub\SmartSideBAR`（git HEAD `d24b693` v1.1 液态玻璃 UI 更新）
> 分析日期：2026-08-30　分析方式：全量源码通读（58 个文件 / 9232 行 TS+VUE）+ 配置与构建脚本核对
> 说明：仓库未安装 `node_modules`，**未执行** typecheck / test / build。凡涉及运行结果的结论均标注为「需实测」，其余结论均给出 `文件:行号` 依据。

---

## 一、项目现状摘要

### 1.1 技术栈与规模

| 维度 | 事实 |
|------|------|
| 运行时 | Electron 30 + Node ≥20（package.json:30,42） |
| 渲染层 | Vue 3.4 Composition API + vue-router 4（hash 路由，6 个视图） |
| 语言 | TypeScript 5.4，`strict: true`，`moduleResolution: bundler` |
| 构建 | electron-vite 2.3 三入口（main / preload / renderer）→ `out/` |
| 打包 | electron-builder 24 → NSIS 单文件 → `dist/` |
| 运行时依赖 | 5 个：`electron-log`、`electron-updater`、`sharp`、`vue`、`vue-router` |
| 开发依赖 | 13 个，含 `vitest`、`eslint`、`happy-dom`、`@vue/test-utils` |
| 代码量 | 主进程 3860 行 / 12 文件；渲染层 5080 行 / 16 文件；共享层 3 文件；合计 9232 行 |

渲染层单文件规模偏大：`AnnotateApp.vue` 708 行、`CapturePanel.vue` 602 行、`ReminderPanel.vue` 505 行、`LinksPanel.vue` 483 行，均为「模板 + 脚本 + 数百行 scoped CSS」的三合一巨型组件。

### 1.2 目录结构与构建链路

```
配置三层：DEFAULT_CONFIG(config.ts:9-37) → %ProgramData%\SeewoSidekick\config.json → %APPDATA%\config.json
         deepMerge 覆盖；ConfigService.set() 为「按点分路径定位后整键赋值 + 全量落盘」(config.ts:120-137)

构建链路：npm run build = tsc --noEmit + electron-vite build
         npm run win    = build + electron-builder --win (nsis, perMachine, requireAdministrator)
         npm run verify-build = typecheck + test + build + win   ← README 主推安装路径

安装期：build/installer.nsh customInstall 把 resources/app/defaults/config.json
        复制到 %ProgramData%\SeewoSidekick\config.json（仅首次），并写 HKLM\SOFTWARE\SeewoSidekick
```

`resources/app/defaults/config.json` 运行时**从不读取**，只作为安装期种子文件，但它被当作「策略层」参与 deepMerge —— 后果见 F8。

### 1.3 已实现模块与完成度

| # | 模块 | 主进程实现 | 渲染层 | 完成度 | 判定依据 |
|---|------|-----------|--------|--------|---------|
| 1 | 输入法切换 | `services/ime.ts:107`（PowerShell InputLanguage） | ImePanel + rail 徽标 | **85%** | 可用；但 `config.ime.slot1/slot2/fallbackSwap` 全程未使用（config.ts:12） |
| 2 | 区域截图 | `services/capture.ts` + `main.ts:125-283` | OverlayApp（含触屏抖动过滤） | **70%** | 单屏正确；多显示器坐标偏移（A4）；临时 PNG 写了但从不显示（C4） |
| 3 | 屏幕批注 | `main.ts:333-436` + 透明窗口 | AnnotateApp（6 工具 + 撤销 30 级） | **60%** | **无触屏事件**（AnnotateApp.vue:19-21 仅 mouse*）；保存只含笔迹（:441） |
| 4 | 长截图 | `services/longshot.ts:508`（窗口枚举 + PageDown + 相似度拼接） | CapturePanel 状态机 | **65%** | 硬取 `sources[0]`（:280）跨屏失效；DPI 未换算；O(n²) 元数据读取（:209-213） |
| 5 | 录屏 | `services/recorder.ts` + 隐藏窗口 MediaRecorder | RecorderApp | **60%** | **无 WebM→MP4**；fps 硬编码 15、mic 从不传（CapturePanel.vue:278）；stop 返回的 filepath 被丢弃（:289） |
| 6 | U 盘监控 | `services/usb.ts:537`（WMI 事件 + 三级降级扫描） | UsbPanel | **90%**（2026-08-31） | ~~`ignoreTypes` 未实现~~ 已实现；~~8s 轮询 spawn PowerShell（C2）~~ 已改为健康巡检；~~诊断通道错接（E5）~~ 已修复；剩余：策略层禁用联动（P2-1） |
| 7 | 打印机状态 | `services/printer.ts:95`（Win32_Printer 轮询） | PrinterPanel | **45%** | **状态映射逻辑错误**（E4）：位掩码用错属性；`PrinterState===3` 判为 ok，实为卡纸 |
| 8 | 任务管理器 | `main.ts:474-486`（spawn taskmgr） | TaskMgrPanel | **100%** | 依赖 PATH 解析 `taskmgr.exe` |
| 9 | 快捷链接 | 无（纯 config 读写） | LinksPanel（CRUD/排序/URL 规范化） | **60%** | **数据丢失缺陷**（E1）；无槽位上限（README 称 3+6） |
| 10 | 定时提醒 | `services/scheduler.ts:199`（once/interval/hourly + 贪睡 + 铃声） | ReminderPanel | **75%** | 每 3s 全量落盘（C1）；重启后过期周期提醒会立刻补触发 |
| 11 | OOBE 引导 | `main.ts:69-75,502-510` | OobeApp 6 步 | **75%**（2026-08-31） | ~~首启可见性存疑（A1）~~ 已修复；~~检测项硬编码（D8）~~ 已真实化；剩余：prefs/role 零消费（D9，待 P2-3） |
| 12 | 设置 | `config:get/set` + `window:openSettings` | SettingsApp(窗口) + SettingsPanel(侧边) | **80%**（2026-08-31） | ~~两套 UI 字段不一致~~ 已抽取共享 `SettingsForm.vue`；~~autoLaunch 初值不读真值（E3）~~ 已读真值；~~录屏参数不可配~~ 已可配；剩余：快捷键槽位（P2-2） |
| 13 | 诊断/自检 | `services/diagnostic.ts:262` + 导出包 | SettingsPanel 诊断区 | **80%**（2026-08-31） | ~~7 项检查中 4 项是空 try 块恒返回 ok~~ 已真实化（P1-12）；剩余：`features` 进一步细化（P2-4） |
| 14 | 白名单策略 | `ConfigService.isModuleDisabled()` (config.ts:140) | 无 | **10%** | 函数存在但**全库 0 调用**，UI 无过滤 |
| 15 | 快捷键槽位 | 无 | 无 | **0%** | README 列为 P1；实际仅 `capture.hotkey` 一个，无冲突检测 |
| 16 | 锁屏 / 电源 | 无 | rail 有 `lock` 图标 → 打开设置面板 | **0%** | `config.power` 三字段（launcher/sleep/powerOnAt）全库 0 引用 |
| 17 | 自动更新 | 无 | 无 | **0%** | `electron-updater` 已装但 **0 引用**；builder 无 `publish` 配置 |

**README 功能表 13 项中，3 项完全不存在（快捷键槽位、白名单策略、锁屏/电源），1 项能力被夸大（录屏 WebM→MP4）。**

---

## 二、问题与风险清单

### A. 阻塞级（P0 — 影响首次可用或核心正确性）

| ID | 问题 | 依据 | 影响 |
|----|------|------|------|
| A1 | **OOBE 首次启动可能完全不可见**。`bootstrap()` 先 `createSidebar()`（manager.ts:118 `show:false`），再 `createOobe(mainWin)`，而 createOobe 使用 `modal: true`（manager.ts:169）挂在**尚未显示**的父窗上。首次运行 `oobe.completed=false` 分支（main.ts:69-71）不调用 `mainWin.show()` | main.ts:66-75；manager.ts:103-126,164-183 | 全新安装后用户看不到任何窗口，应用「像没启动」。*需实测确认 Windows 模态子窗行为* |
| A2 | **高分辨率非缩放屏展开时窗口高度溢出**。createSidebar 用 `height = workArea.height`（manager.ts:101，未乘 uiScale），resizeMain 却用 `height: (height \|\| workArea.height) * uiScale`（manager.ts:429） | manager.ts:97-101 vs 413-430 | 4K@100%（workArea 3840×2160，uiScale=2）点击任意图标 → 高度变为 4320 DIP，窗口飞出屏幕 |
| A3 | **README 唯一推荐的安装路径必然失败**。仓库 **0 个测试文件**，`vitest` 默认 `passWithNoTests:false` 会以非零码退出；且无 ESLint 配置文件，`npm run lint` 同样失败 | package.json:14,16,18；全库 `find` 无 `*.test.ts`/`.eslintrc*` | `npm run verify-build` 在 `npm run test` 处中断，无法产出安装包 |
| A4 | **区域截图多显示器坐标整体偏移**。物理坐标换算为 `(region.x + shotWorkArea.x) * scaleFactor`（main.ts:215），但 desktopCapturer 源图像以该显示器 `bounds` 原点为 0；当目标屏 `bounds.x≠0`（副屏）时，偏移整整一个屏宽，且 `saveImage` 会把 x 钳到 0（capture.ts:101） | main.ts:213-219；capture.ts:101-104 | 副屏截图裁到错误区域。正确式应为 `region.x + (workArea.x - bounds.x)` |
| A5 | **长截图跨屏 / 非 100% 缩放失效**。`_captureWindow` 硬取 `sources[0]`（主屏），却用 `GetWindowRect` 返回的**虚拟桌面物理坐标**裁剪；scaleFactor≠1 时再叠加一层缩放误差 | longshot.ts:271-307（:280 `sources[0]`，:290-293 直接用 win.x/win.y） | 目标窗口在副屏或 DPI≠100% 时，拼出的长图是主屏错位内容 |

### B. 安全

| ID | 问题 | 依据 | 风险 |
|----|------|------|------|
| B1 | **IPC「白名单」是假门**。main.ts:627-632 监听通用 `ipcMain.on('message')` 后 `preventDefault()`，**根本拦不到 `invoke`/`send`**；真正的攻击面是 43 个 `ipcMain.handle`，全部无来源校验、无参数校验。README「安全技术特性」声称「未注册通道自动拦截」 | main.ts:626-632 | 安全声明与实现不符，存在错误的安全预期 |
| B2 | `shell:openExternal` 接受任意 URL 无白名单；`shell:openPath` 接受任意路径 | main.ts:489-499 | 渲染层一旦被注入即可拉起任意程序/协议 |
| B3 | `ConfigService.set` 支持任意点分路径写入（`key.split('.')` 后逐级赋值），键名无白名单 | config.ts:120-127 | 可写入非预期配置字段乃至原型链路径 |
| B4 | index.html 无 CSP meta；dev 下 `webSecurity: !isDev` 关闭同源策略；所有窗口 `sandbox: false` | index.html:1-18；manager.ts:124 | 纵深防御缺失 |
| B5 | `requestedExecutionLevel: requireAdministrator` + `perMachine: true` ✅ **2026-08-31 修复**（P1-10）：`perMachine: false` + `requestedExecutionLevel: asInvoker`；`installer.nsh` 按账户类型分支——管理员写 HKLM + ProgramData 策略层，普通用户写 HKCU 并跳过 ProgramData（运行时由 DEFAULT_CONFIG + %APPDATA% 兜底） | electron-builder.yml:16,32 | 常驻课堂工具每次启动都提权；开机自启撞 UAC；与未来自动更新（需静默）冲突 |
| B6 | `Object.freeze(sidekickApi)` 仅**浅冻结**，嵌套对象仍可篡改 | preload.ts:193 | README 声称「冻结 API 防止篡改」，实际可绕过 |

### C. 性能 / 资源

| ID | 问题 | 依据 | 影响 |
|----|------|------|------|
| C1 | Scheduler 每 3 秒**无条件**执行 `ConfigService.set('reminders', …)` → 整份 JSON 落盘 | scheduler.ts:35,84 | 每天约 2.9 万次全量写盘；与 README「低资源占用」目标相悖 |
| C2 | USB 后备轮询每 8 秒 spawn 一次 PowerShell 执行 `Get-CimInstance Win32_DiskDrive` 关联查询（最重的一条路径） ✅ **2026-08-31 修复**：监听器健康时仅 5 分钟巡检一次（空闲 10 分钟约 2 次扫描）；仅当 WMI 监听器异常退出时才回落 8s 降级轮询，并配合指数退避重启 | usb.ts:454,192-215 | 常驻进程 churn，4–8GB 教学一体机上 CPU 抖动明显 |
| C3 | 长截图循环内每帧对**全部已采集帧**重跑 `sharp(f).metadata()`，复杂度 O(n²) | longshot.ts:209-213 | 20 帧 1080p 即需数百次 PNG 头解析，实测可能达分钟级 |
| C4 | 区域截图先把整屏 PNG 写入临时目录（`saveTempImage`），而 OverlayApp 的 `onInit` 回调体为空，从不使用该图 | main.ts:157；capture.ts:140-147；OverlayApp.vue:85-87 | 每次截图多一次全屏 PNG 编码 + 磁盘写入，纯浪费 I/O |
| C5 | 每次输入法 `getState`/`toggle` 各 spawn 一个 PowerShell（5s 超时） | ime.ts:75,87 | 课堂高频操作下延迟明显（数百 ms 级） |
| C6 | 安装包体积风险：Electron 30 + sharp 预编译二进制 + 已提交的 `out/`，README 预算 ≤110MB ✅ **2026-08-31 实测解除**：`electron-builder --win` 产出 **81.6MB** | README:223；package.json:23；dist 实测 | 原「未构建验证，超标概率高」已排除 |

### D. 未完成 / 占位实现 / 功能夸大

| ID | 问题 | 依据 |
|----|------|------|
| D1 | **批注完全无触屏支持**：AnnotateApp 只绑定 `mousedown/mousemove/mouseup`，而 OverlayApp 有完整 touch 处理 | AnnotateApp.vue:19-21 vs OverlayApp.vue:4 |
| D2 | 批注保存仅导出画布（透明背景，只含笔迹），不含屏幕内容 | AnnotateApp.vue:441-443（注释自述「只含批注内容」） |
| D3 | `undoStack` 是普通数组，模板 `:disabled="undoStack.length === 0"` **不响应式**，撤销按钮禁用态永不刷新 | AnnotateApp.vue:91,140 |
| D4 | **无 WebM→MP4**（README 功能表明确宣称）。仅按 MediaRecorder 实际 mimeType 定扩展名 | recorder.ts:212；RecoderApp.vue:97-108 |
| D5 | 录屏参数硬编码：`{fps:15}`，mic 从不传；`config.recorder.fps/bitrate/mic` 为死配置 | CapturePanel.vue:278；config.ts:14 |
| D6 | 录屏停止返回的 filepath 被丢弃，且 `getStatus()` 不含 filepath → 「最近文件」里永远不出现录屏 | CapturePanel.vue:289-293,143-152；recorder.ts:31-37 |
| D7 | 长截图倒计时在侧边栏**已隐藏后**才播放：main.ts 先 `hideMain()+sleep(500)` 再进入 `LongshotService.start()` 的倒计时 | main.ts:315-317；longshot.ts:151-155 |
| D8 | OOBE 环境检测：打印机/USB 硬编码文本「就绪」；**触控能力完全不检测**（README 称检测「屏幕/触控/打印机/输入法」） ✅ **2026-08-31 修复**（P1-12）：新增触控能力判定（`maxTouchPoints`/`pointer:coarse`），打印机/USB 改真实探测 | OobeApp.vue:200-201；README:82 |
| D9 | OOBE 收集的 `prefs`/`role` 写入配置后**无任何消费方**（README 称「按角色裁剪」） | OobeApp.vue:146-158；config.ts:33 |
| D10 | 诊断服务 4 项是空 try 块恒返回 ok（录屏/USB/打印机/输入法）；`features.clipboard/desktopCapturer` 硬编码 `true` ✅ **2026-08-31 修复**（P1-12）：7 项服务检查全部真实探测；`features.clipboard/desktopCapturer/autoLaunch` 真实探测 | diagnostic.ts:171-216,226-228 |
| D11 | preload `display.windowOrigin` 返回固定 `{x:0,y:0}` | preload.ts:84 |
| D12 | `notification:dismiss` 主进程为空实现 | main.ts:563-565 |
| D13 | README 的「快捷键槽位 3 个 + 冲突检测」「白名单策略」「锁屏/电源」——代码均无实现 | 见 1.3 表 #14/#15/#16 |
| D14 | `electron-updater` 0 引用 + builder 无 publish → 「自动更新」不存在 | 全库 grep；electron-builder.yml 无 publish 段 |
| D15 | `config.usb.ignoreTypes = ['phone','carplay']` 从不参与过滤 ✅ **2026-08-31 修复**：`UsbService.start()` 读取配置，`filterIgnored()` 应用于初始扫描 / 事件扫描 / 手动刷新全路径 | config.ts:15；usb.ts 无引用 |

### E. 逻辑缺陷 / 数据一致性

| ID | 问题 | 依据 |
|----|------|------|
| E1 | **链接数据丢失**：`loadLinks` 只加载 `enabled && url非空` 的链接，`saveAll` 又把该子集整体回写 → 默认配置里的 `l4`/`l5` 在任意一次编辑后被永久删除 | LinksPanel.vue:83,246-255 |
| E2 | `oobe.setState` 传**部分对象**，而 `ConfigService.set` 是整键覆盖 → `skip()` 会抹掉 `prefs`/`env`/`completedAt` 等字段 | OobeApp.vue:141；config.ts:127 |
| E3 | 开机自启状态三处矛盾：SettingsApp 初值恒 `false` 且不读真值（preload 未暴露 `power:getAutoLaunch`，尽管 main.ts:517 有 handler）；SettingsPanel 初值恒 `true` ✅ **2026-08-31 修复**（P1-6）：`power:getAutoLaunch` 已暴露；autoLaunch 读取抽到共享 `SettingsForm.vue` 的 `onMounted`，设置窗口与侧边栏两处均读系统登录项真实状态 | SettingsApp.vue:97,107-121；SettingsPanel.vue:144；preload.ts:106-108 |
| E4 | **打印机状态映射错误**：位掩码 `0x80/0x40/0x200000` 属于 `DetectedErrorState`，却被用在 `PrinterState \|\| PrinterStatus` 上；`state===3` 判为 `ok`，而 Win32_Printer.PrinterState **3 = Paper Jam**。查询里 `Select DetectedErrorState` 取了却不用 | printer.ts:17,20-29 |
| E5 | `usb.getDiagnostics` 错接到 `help:runDiagnostics` → UsbPanel 每次挂载触发一次**全量诊断** | preload.ts:156；UsbPanel.vue:130-134 |
| E6 | `PrinterService.broadcast` 无 `isDestroyed()` 保护（UsbService 有） | printer.ts:91-93 vs usb.ts:489 |
| E7 | 显示器变更监听**重复注册**：DisplayService.init 注册三个事件（无防抖），main.ts 又注册一遍（250ms 防抖） | display.ts:28-42；main.ts:82-92 |
| E8 | 崩溃恢复不区分窗口：`render-process-gone` 对任意窗口（overlay/annotator/recorder）崩溃都重建侧边栏 | main.ts:657-662 |
| E9 | **`overlay:ready` 竞态未统一**：recorder.ts 明确修了「先注册监听再加载页面」（:84-98），但区域截图（main.ts:189）与批注（main.ts:363）仍是「先创建窗口、后注册监听」 | recorder.ts:84-98 vs main.ts:164-189 / 342-363 |
| E10 | 交互与文案不符：OOBE 完成页称「鼠标移至屏幕边缘即可展开」，但 `onMouseEnter` 只清定时器、不展开 | OobeApp.vue:75；SidebarApp.vue:129-133 |
| E11 | 录屏窗口与 overlay/annotate **共用 `overlay:ready` 通道**，并发时会互相误触发 | recorder.ts:98 vs main.ts:189/363 |
| E12 | **收起/展开箭头视觉过细**（2026-08-30 用户实测反馈）：`.dock-arrow` 用字体字符 `↑`/`↓`（22px / font-weight 600），而 rail 图标是 22×22 的 SVG —— 字体笔画天然比 SVG 线条细，且粗细随系统字体浮动，无法与图标系统对齐 | SidebarApp.vue:21,385-389 vs :334-338 |

### F. 工程与仓库卫生

| ID | 问题 | 依据 |
|----|------|------|
| F1 | 构建产物 `out/`（6 个文件）**已被 git 跟踪**，但 `.gitignore:6` 又忽略 `out/` → 长期处于「已跟踪又被忽略」的脏状态 ✅ **2026-08-31 修复**（`git rm -r --cached out/` 取消跟踪）；另外 `patches/v1.1-liquid-glass.css` 依赖 Vue scoped 哈希 `[data-v-92633577]`，`SidebarApp.vue` 样式一改补丁即静默失效（此半项未解决，hash 来源穷举见 2026-08-30 的 `usb-repro/hash*.js`，未命中） | `git ls-files out/`；.gitignore:6；patches 第 13-26 行 |
| F2 | 无测试、无 ESLint 配置、无 CI，但 package.json 暴露了 `test` / `lint` / `verify-build` ✅ **2026-08-31 修复（CI 除外）**：`tests/smoke.test.ts`（5 用例）+ `.eslintrc.cjs`（vue-eslint-parser + TS parser 链）+ `vitest.config.ts` 已建立；`typecheck / test / lint / build / electron-builder --win` 全链路实测通过（安装包 81.6MB ≤110MB 预算） | package.json:14-18 |
| F3 | **许可证冲突**：LICENSE 文件是 **Apache-2.0**，package.json 声明 `"license": "MIT"` ✅ **已修复**（package.json 现为 Apache-2.0） | LICENSE:1-3；package.json:7 |
| F4 | 版本号四处不一致：package.json `1.1.0` / main.ts 日志 `v2.1.0` / SettingsApp 显示 `v2.0.0` / SettingsPanel 与 diagnostic 兜底 `2.0.0`；`DEFAULT_CONFIG.version=2` 但**无任何迁移逻辑** ✅ **版本已统一为 1.1.0**（迁移逻辑仍缺，配置结构变更时需补） | package.json:3；main.ts:48；SettingsApp.vue:82；SettingsPanel.vue:136 |
| F5 | README 项目结构写了 `scripts/ 辅助脚本` 目录，仓库中不存在 ✅ **2026-08-31 修复**：创建 `scripts/.gitkeep` 补齐目录，并从 README 项目结构中移除该描述 | README:151 |
| F6 | 死代码：IPC 通道 `ime:locale`、`capture:longshot`、`display:metricsChanged` 全库 **0 引用**；`window:minimize`、`power:getAutoLaunch` 有 handler 无调用方；未使用 import（manager.ts:3 的 `screen`/`shell`、recorder.ts:6 的 `screen`、longshot.ts:3 的 `Notification`） ✅ **2026-08-31 修复**：三个 0 引用通道与 `window:minimize`（含 handler 与 `WindowManager.minimize()`）已删除；`power:getAutoLaunch` 已有调用方（P1-6）；capture.ts 的 `nativeImage`/`dirname`、manager.ts 的 `screen`/`shell`、config.ts 的 `OobeState`、LinksPanel 的 `isValidUrl`、SidebarApp 的 `DOCK_HEIGHT` 一并清理（lint 驱动） | 逐通道 grep 统计 |
| F7 | `build/smartsidebar.nsi` 是独立 NSIS 脚本，electron-builder 只 `include: build/installer.nsh` → 前者为死文件 ✅ **2026-08-31 删除** | electron-builder.yml:24 |
| F8 | `resources/app/defaults/config.json` 运行时从不读取；它被 NSIS 复制到 ProgramData 后充当「策略层」参与 deepMerge → **后续版本修改 `DEFAULT_CONFIG` 对已装机用户不生效** | installer.nsh:12-15；config.ts:82-90 |
| F9 | 注释与事实不符：ime.ts / usb.ts 多处写「PowerShell 7.6.5 兼容」，实际 `spawn('powershell')` 调用的是 Windows PowerShell 5.1；PowerShell 亦无 7.6.5 版本 ✅ **已于 2026-08-30 修复**（ime.ts 与 usb.ts 注释均已改正） | ime.ts:13,25；usb.ts:1,27,49 |
| F10 | 无托盘图标、无退出入口；`window-all-closed` 空实现保持后台 → 用户除任务管理器外无法退出 | main.ts:652-654 |
| F11 | 未调用 `app.setAppUserModelId` → Windows 通知来源与开机自启项命名异常 | main.ts 全文缺失 |

---

## 三、开发路线建议

> 工作量按「1 名熟悉 Electron+Vue 的开发者 / 人天」估算，含自测。前置依赖列「无」表示可立即开工。

### P0 — 阻塞首发质量（合计 ≈ 6.5 人天）

| # | 目标 | 涉及模块 | 工作量 | 验收标准 | 前置依赖 |
|---|------|---------|-------|---------|---------|
| P0-1 | 修复 OOBE 首次启动不可见 | `main.ts:66-75`、`windows/manager.ts:151-197` | 0.5d | 清空 userData 后启动，OOBE 窗口稳定可见；「跳过」与「开始使用」两条路径均能正确显示侧边栏 | 无 |
| P0-2 | 修复 resizeMain 高度 uiScale 双乘 | `windows/manager.ts:410-432` | 0.5d | 在 1080p@100%、1080p@150%、4K@150%、4K@100% 四种组合下，展开/收起/停靠后窗口高度恒等于 `workArea.height`，无溢出 | 无 |
| P0-3 | 修复区域截图多显示器坐标偏移 | `main.ts:213-219`、`services/capture.ts:54-90` | 1d | 目标屏为副屏（左/右/上/下四种排布）时，裁剪结果与选框像素级对齐（误差 ≤2px） | 无 |
| P0-4 | 建立最小可验证构建：补 1 个 smoke 测试、补 ESLint 配置、让 `verify-build` 真正可跑通 ✅ **2026-08-31 完成**（修复 `.eslintrc.cjs` 的 vue 解析链后 68 错误清零；`verify-build` 全链路实测绿，产出 `dist/SmartSideBAR-安装包-1.1.0.exe` 81.6MB） | 新增 `tests/`、`.eslintrc.cjs`、`package.json` | 0.5d | 全新 clone 后 `npm i && npm run verify-build` 全绿并产出 `dist/*.exe` | 无 |
| P0-5 | 批注支持触屏 | `renderer/annotate/AnnotateApp.vue` | 1.5d | 希沃触屏单指可书写、画箭头/矩形、撤销；复用 OverlayApp 的 4px/80ms 抖动过滤；鼠标与触摸互不干扰 | 无 |
| P0-6 | 加固 IPC 面：删除假白名单，改为信道级参数校验；`shell:openExternal/openPath` 加白名单；index.html 加 CSP | `main.ts:626-632`、`preload.ts`、`renderer/index.html` | 1.5d | 未注册通道与非法 URL 被拦截并记日志；渲染层 43 个 handle 全部有入参校验 | P0-4 |
| P0-7 | 修复长截图跨屏与 DPI 失效 | `services/longshot.ts:271-307` | 1d | 目标窗口位于副屏、系统缩放 125%/150%/200% 时，首帧裁剪区域与该窗口实际位置一致 | P0-3 |

### P1 — 稳定性与体验（合计 ≈ 11 人天）

| # | 目标 | 涉及模块 | 工作量 | 验收标准 | 前置依赖 |
|---|------|---------|-------|---------|---------|
| P1-1 | 长截图性能优化：增量维护累计高度、相似度比较缓存 raw buffer、去除 O(n²) | `longshot.ts:182-217,310-349` | 1d | 20 帧 1080p 长截图总耗时 ≤ 60s（原路径实测对照） | P0-7 |
| P1-2 | Scheduler 落盘去抖：仅在提醒集合实际变化时写入 | `scheduler.ts:48-85` | 0.5d | 空闲 10 分钟内 `config.json` 写入次数为 0 | 无 |
| P1-3 | USB 轮询降载：拉长间隔 / 仅在 WMI 不可用时启用 / 复用常驻进程 ◐ **大部分完成**（2026-08-30 三次 PowerShell 调用合并为单次 `PS_SCAN_SCRIPT`；2026-08-31 轮询策略改为事件驱动 + 健康巡检：监听器健康时仅 5 分钟巡检 1 次，异常退出时回落 8s 降级轮询 + 指数退避重启 5s→60s 封顶。剩余「复用常驻进程做扫描」随事件驱动达成后收益已很小，暂不实施） | `services/usb.ts:382-457` | 1.5d | 空闲 10 分钟内 PowerShell 进程创建次数 ≤ 3 | 无 |
| P1-4 | 修正打印机状态映射：改用 `DetectedErrorState` 位掩码 + `PrinterStatus` 综合判定 | `printer.ts:16-29,63-79` | 1d | 缺纸/卡纸/离线/墨量低四类可人工构造并正确识别；`PrinterState=3` 判为卡纸而非正常 | 无 |
| P1-5 | 修复链接数据丢失：saveAll 回写全量 links | `LinksPanel.vue:80-85,246-255` | 0.5d | 编辑任一链接后，`l4`/`l5` 等未启用链接仍在 config 中 | 无 |
| P1-6 | 统一设置 UI：合并 SettingsPanel / SettingsApp，autoLaunch 读取真值 ✅ **2026-08-31 完成**（抽取 `SettingsForm.vue` 共享表单，SettingsApp 降为布局壳；autoLaunch 在共享表单 `onMounted` 读 `power:getAutoLaunch` 真值） | 新增 `components/SettingsForm.vue`、`SettingsApp.vue` | 1.5d | 两处设置项一致；开关初值与系统登录项真实状态一致 | 无 |
| P1-7 | 录屏参数与结果闭环：fps/mic 取自 config；stop 回传 filepath 写入最近文件；README 更正「转 MP4」表述 | `CapturePanel.vue:274-293`、`recorder.ts:31-37,179-246`、README | 1d | 改 config.recorder.fps=30/mic=true 后生效；录屏结束后最近文件出现该条 | P0-4 |
| P1-8 | 批注保存语义明确化：提供「仅笔迹 / 含屏幕背景」两种导出 | `AnnotateApp.vue:437-444`、`capture.ts:150-176` | 0.5d | 两种模式各产出一张 PNG，内容符合预期 | P0-5 |
| P1-9 | 统一 `overlay:ready` 为「先注册监听、再加载页面」，并拆分录屏专用通道 | `main.ts:164-189,342-363`、`recorder.ts:84-98`、`shared/ipc-channels.ts` | 0.5d | 连续 20 次触发截图/批注/录屏，无一次进入 60s/300s 超时分支 | 无 |
| P1-10 | 降级 `requireAdministrator`，评估最小提权范围 ✅ **2026-08-31 完成**（`perMachine: false` + `requestedExecutionLevel: asInvoker`；`installer.nsh` 账户类型分支写 HKLM/HKCU 与 ProgramData 策略层，普通用户跳过 ProgramData） | `electron-builder.yml:16,32`、`installer.nsh` | 1d | 普通用户可安装并开机自启，无 UAC 弹窗；ProgramData 策略目录权限另行处理 | 无 |
| P1-11 | 仓库卫生：移除已跟踪的 `out/`、删死文件、统一许可证与版本号、清理死 IPC 通道与未用 import ✅ **2026-08-31 完成**（`out/` 取消跟踪、`smartsidebar.nsi` 删除、4 个死通道 + `WindowManager.minimize()` 删除、lint 驱动清理 8 处死引用；许可证与版本号统一为 Apache-2.0 / 1.1.0；已本地提交 `94bf2a1`） | 全仓 | 0.5d | `git status` 干净；`package.json.license` 与 LICENSE 一致；全仓版本号唯一 | P0-4 |
| P1-12 | 修正 OOBE 与诊断的「假检测」 ✅ **2026-08-31 完成**：OOBE 环境检测项改为真实探测（新增触控能力判定，打印机走 `printer.getStatus()`、USB 走 `usb.scan()`，不再硬编码「就绪」）；诊断服务 7 项全部真实探测（录屏/USB/打印机/输入法/长截图改用各服务真实状态，`features.clipboard/desktopCapturer/autoLaunch` 不再硬编码） | `OobeApp.vue:177-203`、`diagnostic.ts:149-231` | 1.5d | 诊断报告 7 项均基于真实探测；OOBE 检测项含触控能力真实判定 | 无 |

### P2 — 补齐定位差距（合计 ≈ 12.5 人天）

| # | 目标 | 涉及模块 | 工作量 | 验收标准 | 前置依赖 |
|---|------|---------|-------|---------|---------|
| P2-1 | 白名单策略落地：把 `isModuleDisabled` 注入 railItems 与各面板 | `config.ts:140`、`SidebarApp.vue:89-108` | 1d | ProgramData 策略禁用某模块后，该 rail 图标消失且对应服务不启动 | P1-11 |
| P2-2 | 快捷键槽位（3 个）+ 冲突检测与替代建议 | 新增 `services/hotkey.ts`、`SettingsApp.vue`、`ipc-channels.ts` | 2d | 注册失败时给出可用替代组合；改键即时生效 | P1-6 |
| P2-3 | OOBE prefs / role 真正生效，模块可见性按角色裁剪 | `OobeApp.vue:145-165`、`SidebarApp.vue` | 1.5d | 选「教师」与「管理员」后 rail 项按预设差异显示 | P2-1 |
| P2-4 | 诊断服务真实化 + 导出包可定位问题 | `diagnostic.ts` | 1.5d | 断网/拔打印机/禁用通知等场景下报告状态正确变化 | P1-12 |
| P2-5 | 锁屏/电源功能落地，或移除 rail 上的 `lock` 图标 | `ipc-channels.ts`、`SidebarApp.vue:106`、`config.ts:11` | 0.5d | 二选一：功能可用，或图标消失且 config.power 删除 | 无 |
| P2-6 | 自动更新：接入 `electron-updater` + builder `publish`，或移除依赖与 README 声明 | 新增更新模块、`electron-builder.yml`、README | 1d | 检测到新版本并提示；或依赖与文档均已删除 | P1-10 |
| P2-7 | 托盘图标 + 退出入口 + `app.setAppUserModelId` | `main.ts`、新增 tray 模块 | 1d | 托盘含「打开设置/退出」；通知来源名称正确 | 无 |
| P2-8 | 性能预算实测与裁剪（空闲内存 ≤220MB、冷启动 ≤3s、包体 ≤110MB） | 全仓 + 构建配置 | 2d | 输出实测数据表；超标项有明确裁剪方案（如按需加载 sharp） | P1-2、P1-3 |
| P2-9 | 建立核心链路回归清单与手工 E2E 用例（截图/批注/长截图/录屏/USB/提醒） | 新增 `docs/testing.md` | 1d | 覆盖 6 条主链路 × 3 种 DPI × 2 种显示器组合 | P0-4 |
| P2-10 | 重构巨型组件：AnnotateApp / CapturePanel 拆分（逻辑 composables + 子组件） | `renderer/annotate`、`renderer/components` | 1.5d | 单文件 ≤300 行；逻辑可单测 | P0-4 |
| P2-11 | **收起/展开箭头与 rail 图标视觉对齐**：把字符 `↑`/`↓` 替换为与 rail 图标同规格的内联 SVG（22×22、stroke-width 对齐现有图标的线条粗细），docked 水平条形态同步适配；不采用「继续加粗字体」方案（粗细仍受系统字体控制） | `SidebarApp.vue:20-22,356-394`；若保留液态玻璃效果需同步 `patches/v1.1-liquid-glass.css` | 0.5d | 箭头笔画粗细与 rail 图标视觉一致（并排对比无明显的粗细差）；100%/150%/200% 缩放下不变形；docked 水平态箭头方向正确（↑ 展开 / ↓ 收起逻辑不变） | 无 |

### 建议执行顺序

```
第一批（打磨首发，≈6.5d）：P0-1 → P0-2 → P0-3 → P0-5 → P0-4 → P0-7 → P0-6
第二批（稳住体验，≈11d）：P1-5 → P1-2 → P1-4 → P1-1 → P1-3 → P1-9 → P1-6 → P1-7 → P1-8 → P1-10 → P1-11 → P1-12
第三批（补齐定位，≈12.5d）：P2-7 → P2-1 → P2-3 → P2-2 → P2-5 → P2-4 → P2-6 → P2-11 → P2-8 → P2-9 → P2-10
```

**一句话结论**：项目的骨架（三进程结构、配置三层、窗口/DPI 管理、IPC 契约）设计得相当扎实，长截图与录屏的完成度超出预期；但**工程质量保障为零**（0 测试 / 0 lint / 0 CI），且 README 对外承诺了 3 个不存在的模块和 1 项不存在的能力。建议先把 P0 的 7 项做完再对外发布，否则「首次启动看不见界面」与「高分辨率屏窗口飞出屏幕」会直接劝退目标用户（教师）。

---

## 四、变更记录

| 日期 | 变更 | 关联条目 |
|------|------|---------|
| 2026-08-30 | 初版体检报告 | 全文 |
| 2026-08-30 | **修复 USB 移动硬盘识别不到**（用户实测反馈）。根因有三：① `Get-CimAssociatedInstance -ResultClassName Win32_DiskDriveToDiskPartition` 在 Win11 实测返回 0 条关联，改用经典 WQL `ASSOCIATORS OF`；② USB 桥接盘 `InterfaceType` 伪装为 `SCSI`、`PNPDeviceID` 为 `SCSI\DISK...`，原过滤条件全部不命中（移动硬盘 `DriveType=3`，第 2 层降级必然落空；第 3 层 wmic 已被系统移除）；③ `ConvertTo-Json` 管道写法在空数组时输出空字符串（静默降级）+ 未设 UTF8 输出编码（中文卷标乱码）。现合并为单次调用的 `PS_SCAN_SCRIPT`（DriveType 2/5 → `Get-Disk.BusType` → WQL 兜底）。已实测识别两块不同桥接芯片的移动硬盘（JMicron 128GB / Phi H1 465.7GB，含繁体中文卷标「磁碟區」正确解码、无盘符分区正确跳过）。改动：`usb.ts`（+UsbDrive 类型、删 wmic 降级）、`UsbPanel.vue`（新增「移动硬盘」标签与型号显示）、`ime.ts`（仅注释） | 新增事实已并入 E12 上下文；P1-3 标注部分完成；F9 标注已修复 |
| 2026-08-30 | **新增 E12 / P2-11**：用户反馈收起/展开箭头（字符 `↑`/`↓`）比 rail 图标细。根因是箭头用字体字符而图标是 SVG，字体笔画无法与图标线条对齐；修复方案定为替换为同规格内联 SVG，估 0.5d | E12、P2-11 |
| 2026-08-31 | **P0-4 收尾完成**：`.eslintrc.cjs` 的 `.vue` 覆盖块缺少 `vue-eslint-parser` + `parserOptions.parser` 链，导致全部 13 个 SFC 解析报错（68 errors）。修复后 lint 0 error / 0 warning；顺带补齐 `no-require-imports`/`no-useless-escape`/d.ts 空对象类型的豁免说明。`verify-build` 全链路实测通过：typecheck ✓ / vitest 5 用例 ✓ / eslint ✓ / electron-vite build ✓ / electron-builder NSIS ✓，产出 `dist/SmartSideBAR-安装包-1.1.0.exe` **81.6MB**（≤110MB 预算，C6 风险解除） | P0-4、F2、C6 |
| 2026-08-31 | **P1-3 收尾（USB 轮询降载）**：原实现在监听器健康时仍无条件 30s 轮询（空闲 10 分钟 ≈20 次 PowerShell，远超验收 ≤3）。改为双定时器策略——监听器健康仅 5 分钟巡检 1 次；监听器异常退出后回落 8s 降级轮询，并实现此前「声明未用」的指数退避重启（5s→10s→20s→40s→60s 封顶；稳定运行 >60s 后重置计数）。诊断接口同步输出 `currentPollMs`/`degradedPolling`/`watcherFailures` | P1-3、C2 |
| 2026-08-31 | **D15 修复（`config.usb.ignoreTypes` 落地）**：`UsbService.start()` 读取配置，新增 `filterIgnored()` 应用于初始扫描 / 事件驱动扫描 / 手动刷新三条路径 | D15 |
| 2026-08-31 | **P1-11 仓库卫生**：`git rm -r --cached out/` 结束「已跟踪又被忽略」脏状态（F1 前半）；删除死文件 `build/smartsidebar.nsi`（F7）；删除 0 引用通道 `ime:locale`/`capture:longshot`/`display:metricsChanged` 与 `window:minimize`（含 main.ts handler + `WindowManager.minimize()`）（F6）；lint 驱动清理未用引用：capture.ts `nativeImage`/`dirname`、manager.ts `screen`/`shell`、config.ts `OobeState`、LinksPanel `isValidUrl()`、SidebarApp `DOCK_HEIGHT` | F1、F6、F7、P1-11 |
| 2026-08-31 | **F5 修复**：README 项目结构列出 `scripts/ 辅助脚本` 目录但仓库中不存在。创建 `scripts/.gitkeep` 补齐目录，并从 README 中移除该描述 | F5、P1-11 |
| 2026-08-31 | **P1-6 统一设置 UI**：原 SettingsApp(窗口) 与 SettingsPanel(侧边) 两套字段漂移（E3 autoLaunch 三处矛盾、录屏参数一处配一处不配）。抽取 `components/SettingsForm.vue` 为唯一表单实现（含 loadConfig/save/autoLaunch 读真值），SettingsApp 降为布局壳（`ref` + `defineExpose.save`），两处共享同一字段集与保存逻辑 | P1-6、E3 |
| 2026-08-31 | **P1-10 降级提权**：`electron-builder.yml` 改为 `perMachine: false` + `requestedExecutionLevel: asInvoker`（普通用户免 UAC 安装/自启）；`installer.nsh` 用 `UserInfo::GetAccountType` 分支——管理员写 HKLM 检测键 + ProgramData 策略层，普通用户写 HKCU 并跳过 ProgramData（运行时由 DEFAULT_CONFIG + %APPDATA% 兜底）。打包实测通过（NSIS 脚本编译 OK，`perMachine=false` 生效） | P1-10、B5 |
| 2026-08-31 | **P1-12 修正假检测**：OOBE 环境检测新增「触控能力」真实判定（`maxTouchPoints`/`pointer:coarse`），打印机走 `printer.getStatus()`、USB 走 `usb.scan()`，去掉硬编码「就绪」。诊断服务 `_checkServices` 的录屏/USB/打印机/输入法/长截图 5 项空 try 块改为真实探测（`RecorderService.getStatus()`/`UsbService.getDiagnostics()`/`PrinterService.refresh()+getStatus()`/`ImeService.getState()`/`LongshotService.isRunning()`）；`_checkFeatures` 的 `clipboard/desktopCapturer` 改真实探测、`autoLaunch` 读 `app.getLoginItemSettings()` | P1-12、D8、D10 |
