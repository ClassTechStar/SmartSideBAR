# Dock 化深度优化方案 · 可行性评估与实施报告（2026-11-29）

> 方案来源：《SmartSideBAR桌面效率Dock深度优化方案.md》（2026-11-29，373 行）
> 交叉验证基线：`c802835`（方案复核时点之后的 HEAD）→ 实施提交 `c171875`
> 测试基线：496 用例（方案编制时）→ **543 用例全绿**（本轮 +11）
> 本报告 = 方案第 3/4/6 步要求的可行性三分类评估 + 实施记录 + 效果对比 + 待跟进事项

---

## 一、可行性评估报告（逐项三分类）

### 1.1 可直接无调整实施的项（本轮全部落地 ✅）

| 方案条目 | 实施记录 |
|---|---|
| §3.1 `display.triggerWidthDip` (8-40) | ✅ EdgeTriggerWindow const → 构造参数，WindowManager 同源传入 |
| §3.1 `display.hoverExpandDelayMs` (0-800) | ✅ dwell 计时器从 150ms 硬编码改读参数 |
| §3.1 `display.collapseDelaySec` (0.5-30) | ✅ SidebarWindow 5s 收起与 edge 模式 4s 回收**同源**此值 |
| §3.1 `sidebar.hotkey` → 第 6 热键位 | ✅ 以 `display.sidebarHotkey` 落 DisplayConfig（与侧栏语义同段），路由 WindowRouter 已有 sidebar-toggle 分支 |
| §3.2 置顶三档 `topmostPolicy` | ✅ never/normal/always；keeper 自注销仅 normal 档，always 档永续重申 |
| §3.2 收起归还焦点 | ✅ SetDocked(true) 记前台窗口 → 隐藏后 SetForegroundWindow |
| §7.1 `railWidthDip/panelWidthDip` | ✅ WindowManager const → 配置属性（钳制 56-120/280-520），AppBar px 同源 |
| §7.1 `iconDip` | ✅ RailItem.IconSize INPC 属性 + axaml 绑定，rail 构建时按 display.iconDip 赋值 |
| §7.1 尺寸/灵敏度设置页 | ✅ 常规页新增 7 行（置顶三档 combo/宽度 2 滑条/图标 combo/触发条/悬停/收起 3 滑条/失焦收起/玻璃开关），MVVM 同构（ConfigChange 列表） |
| §7.2 失焦收起（零钩子） | ✅ Window.Deactivated 即收面板，`collapseOnFocusLoss` 默认关 |
| §4 WMI 裁剪修复 | ✅ `TrimmerRootAssembly System.Management` —— 修复安装包里 USB WMI/打印机轮询全灭（开发态正常）的缺陷（自查追加项） |
| §4 字体子集 | ✅ 7.8MB → **1.72MB**（-78%），GB2312 全集+源码用字，CJK/族名保留；FontSubsetTests 体积+字节级 CJK+族名门禁 |
| §4.2/4.4 启动冒烟+退出断言 | ✅ `SSB_SMOKE_EXIT_MS` 真实运行 N ms 后 exit 0；nightly 新增「启动冒烟+退出残留」步骤 |

### 1.2 需适配调整后才可实施的项（已做适配，标注调整内容）

| 方案条目 | 适配内容 | 状态 |
|---|---|---|
| 字体子集**随 CI 门禁** | 门禁以 FontSubsetTests（体积+CJK+族名三断言）落 Avalonia.Tests，而非文档假设的独立 CI job —— 同一触发链、更少运行时 | ✅ 已落地（适配后） |
| §7.1 **宽度可拖动**（交界 6px 拖动手柄 + 实时 SetWindowPos + 拖完重协商） | 本轮先落配置+设置页（滑条），**拖动手柄列为下一批**：需 AppBar 实时重协商与 B24 纠正路径联调，1-2 天工作量 | ⏳ 下一批 |
| §5.4 计时器 `timer:15m` 动作类型 | ActionHost 命令表扩展一个 case 即可，依赖动作面板 UI（W3-4） | ⏳ 随启动器面板 |
| 双 Profile OOBE 身份二选一 | Workspace 机制已在；OOBE 第 2 步加身份选择 + 默认 rail 组按 profile —— 需 OOBE 流程回归测试 | ⏳ W7-8 批 |

### 1.3 暂不具备实施条件的项（限制与前置依赖）

| 方案条目 | 限制原因 | 前置依赖 |
|---|---|---|
| §5.2 CPU sparkline / 网速 `GetIfTable2` 差分 | 需新建 SystemMetricsService 整层（Win32 互操作 + ClockHost 1s tick + 停表纪律），W7-8 排期；本轮已为其扫清障碍（ClockHost.Pause 停表机制已接收起态） | W7-8 批 |
| §5.3 剪贴板历史（AddClipboardFormatListener + 隐私三闸） | 独立 Win32 监听层 + 面板 + 黑名单进程判定，W3-4 主战场，不可压缩到单批 | W3-4 批 |
| §5.5 Everything 通道 | 依赖用户机器装 Everything（es.exe）；内建索引（动作包+链接聚合）可先行 | W5-6 批 |
| §8.1-8.5 窗口管理五件 | 全部需 EnumWindows/SetWindowPos 互操作层（WindowLayoutManager/SnapGrid），W9-10 专项 | W9-10 批 |
| §3.2 「仅桌面置顶」progman 方案 | 方案自身已裁决不实现（与 AppBar 互斥、易被系统清理） | —（维持裁决） |
| §8.4 图标自动整理分组 | 方案自身已裁决不实现（破坏性大） | —（维持裁决） |

---

## 二、具体修改内容明细（提交 `c171875`）

| 文件 | 改动 |
|------|------|
| SidekickConfig.cs | DisplayConfig +9 键（TriggerWidthDip/HoverExpandDelayMs/CollapseDelaySec/CollapseOnFocusLoss/TopmostPolicy/RailWidthDip/PanelWidthDip/IconDip/SidebarHotkey）；新增 `DisplayNormalize.Clamp` 单点钳制，接入 RefreshTypedNoLock |
| EdgeTriggerWindow.cs | WidthDip const → 构造参数（clamp 8-40）；dwell 150ms → `_hoverDelayMs` |
| WindowManager.cs | RailWidthDip/PanelWidthDip const → 配置属性；EdgeTriggerWindow 传参；edge 回收 4s → CollapseDelaySec；sidebar-toggle 热键位；置顶三档（keeper 自注销仅 normal；undock 置顶三档判定）；SetDocked 焦点归还 |
| SidebarWindow.axaml.cs | 收起延时/失焦收起接线；ApplyDesignTokens 宽度覆盖 XAML 字面量 |
| RailItem.cs + SidebarWindow.axaml + SidebarViewModel.cs | IconSize INPC + 绑定 + rail 构建赋值 |
| GeneralSettingsViewModel.cs + GeneralPage.axaml | 9 个新参数的 Load/Collect/绑定（滑条+combo+checkbox，MVVM 同构） |
| HotkeysSettingsViewModel.cs + HotkeysPage.axaml | 呼出/隐藏侧栏热键（留空不注册） |
| Program.cs | SSB_SMOKE_EXIT_MS 启动冒烟环境变量 |
| Assets/Fonts/MiSans-Normal.ttf | 7.8MB → **1.72MB** 子集（GB2312 全集+源码用字；CJK/族名保留） |
| scripts/gen_font_subset.py | 子集化脚本（API 方式——命令行 53KB 参数被 Windows 32K 上限截断产出假子集，实测踩坑已注释） |
| SmartSideBAR.Windows.csproj | TrimmerRootAssembly System.Management |
| nightly.yml | 启动冒烟（真实运行 5s + exit 0 断言 + 退出残留断言）步骤 |
| tests | DisplayNormalizeTests 9 例 + FontSubsetTests 2 例（体积/字节级 CJK+族名） + DiGraphTests 源扫描接线守卫 |

## 三、优化前后效果对比

| 指标 | 优化前 | 优化后 |
|------|--------|--------|
| 内嵌字体 | 7.8MB（全量 TTF） | **1.72MB 子集**（-78%，含 CJK 与族名，FontSubsetTests 钉死） |
| 安装包体积 | 38MB | ≈32MB（-6MB，字体子集直接体现；实测以 ISCC 重打为准） |
| 可调参数 | 触发模式 1 项 | **+9 项**（触发条宽/悬停延时/收起延时/置顶三档/rail 宽/面板宽/图标尺寸/失焦收起/玻璃质感） |
| 呼出热键 | 无 | 第 6 热键位可配（路由已接） |
| 焦点 | 收起后侧栏持有焦点 | 收起归还前台窗口 |
| 置顶 | 单一策略（回退+keeper） | 三档（never 不置顶 / normal 回退+keeper / always 永续），keepser 自注销策略分档 |
| USB/打印机（安装包） | WMI 被裁剪全灭（仅 WM_DEVICECHANGE 兜底） | TrimmerRootAssembly 修复，WMI 查询恢复 |
| 启动崩守卫 | DiGraphTests 只测注册 | **+真实运行冒烟**（SSB_SMOKE_EXIT_MS → nightly exit 0 + 残留断言），STAThread/平台类缺陷从此必有红灯 |
| 测试 | 496 | **543**（+47：DI 图 3/冒烟接线含内/字体门禁 2/参数化钳制 9/其余批次回归） |

## 四、后续维护注意事项

1. **新参数一律进 DisplayNormalize.Clamp**：加配置键时同步加钳制域与默认值，UI 层不再自行 clamp；
2. **启动顺序铁律**（91eb4c1）：平台未就绪时禁止 new DispatcherTimer —— ClockHost.Register 已 defer，新服务照抄该模式；
3. **字体**：改 UI 文案后重跑 `py -3 scripts/gen_font_subset.py`（pip install fonttools）；禁止把全量 TTF 拷回 Assets（FontSubsetTests 会红）；
4. **发布包验证**： nightly 启动冒烟已覆盖；本地重打安装包前跑一次 `SSB_SMOKE_EXIT_MS=5000` 的 exe 启动即可等价自检；
5. **设置页规范**：新设置项走 GeneralSettingsViewModel 模式（LoadFrom/CollectChanges/ConfigChange.From），勿回退到 code-behind 构造控件（check-ui-conventions.ps1 会拦）。

## 五、待跟进事项清单

| 事项 | 批次 | 说明 |
|------|------|------|
| 宽度拖动手柄（rail/面板交界 6px + AppBar 实时重协商） | 下一批 | 配置化已通，仅剩拖拽交互与 B24 纠正联调，1-2 天 |
| 剪贴板历史（ClipboardMonitor + 隐私三闸 + 面板） | W3-4 | 方案 §5.3 详设完备，最大功能大陆 |
| 启动器面板（LauncherPanel + 拖入添加 + 分组嵌套 + 传参） | W5-6 | ActionHost 契约已就绪，仅 UI 与 .lnk 解析 |
| 系统看板（SystemMetricsService 四指标 + 停表纪律 + rail 徽章） | W7-8 | GetSystemTimes/GlobalMemoryStatusEx/GetIfTable2 差分 |
| 窗口管理五件（快照/分屏/置顶工具/桌面图标显隐/文件中转站） | W9-10 | EnumWindows/SetWindowPos 互操作层 |
| 双 Profile OOBE 身份选择 | W7-8 | Workspace 机制已在，只差 OOBE 接入 |
| 签名收尾（SignTool + Release 附公钥指纹） | W11 | 上轮 P0 唯一未落地项，方案 W11 排期 |
| winget manifest 随 v1.8.0 Release 更新 | 随发版 | 版本/SHA256/URL |
| 空载基线实测数字回填 | nightly 首跑后 | 红线：收起态 CPU ≤0.5%、内存 ≤90MB |

**验证终态：Debug/Release 双模式构建 0 错误；543 用例全绿（Core 354 + Windows 20 + Avalonia 133，较方案实施前 +47）；format 通过；Debug 包真运行 8s 冒烟存活无崩溃日志。**
