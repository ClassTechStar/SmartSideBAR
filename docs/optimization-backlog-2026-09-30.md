# SmartSideBAR 深度优化任务清单（2026-09-30）

> 基线：`main @ 913dc35`（fix(appbar): 禁用 AppBar 注册以恢复右侧屏幕可用性，已推送 origin/main），
> package.json `1.3.0`，工作区干净。
> 方法：全量源码扫描（TS/Vue/C#/native/CI/安装器）+ 与 `docs/remaining-work.md`（2026-09-06）、
> `docs/project-audit-and-roadmap.md` 交叉核对。
> 本清单 = remaining-work.md 的现状对齐 + 新增的工程/安全/发布项。销账时请回写本文件勾选框。

---

## 一、现状摘要（清单的出发点）

| 维度 | 事实 |
|------|------|
| 双栈并存 | Electron 30 + Vue 3 + TS（`src/main`、`src/renderer`）与 C# .NET 10 + Avalonia 11（`src/SmartSideBAR.Avalonia`、`.Windows`、`.Core`）**两套完整实现并存于 main**，互不引用（Electron 全库 0 处引用 Avalonia/dotnet） |
| 实际发布物 | Inno 安装器 `installer/SmartSideBAR-v1.3.iss:41` 打包的是 **Avalonia exe**（来自本地未跟踪目录 `artifacts/v1.3-fix/`）；NSIS `installer/SmartSideBAR.nsi` 亦打包 Avalonia exe |
| 最新决策 | `913dc35`：Electron 侧禁用 AppBar 注册（`manager.ts:203` `if (false && ...)`），`getHwnd` 恢复 BigInt 透传写法（`appbar.ts:19`），侧边栏仅靠 alwaysOnTop 置顶 |
| CI 现状 | 4 个 job 中 **dotnet 与 release 两个从不运行**（死 job，原因见 R2）；Electron job 仅 typecheck+lint+test，无构建无产物 |
| 测试现状 | C# 侧约 40+ 用例（Core/Windows 两层，含 AppBar/调度/配置/悬浮球布局）；TS 侧仅 `tests/smoke.test.ts` 41 行（4 个 IPC 白名单用例），主进程 0 覆盖 |
| 版本方案 | 三套并存：Electron `1.3.0`、Inno `AppVersion 1.3.1`、`Directory.Build.props` `2.0.0-waveA`；硬编码版本字符串 8+ 处，其中 2 处仍停留 `1.1.0` |
| 既有遗留 | `docs/remaining-work.md`（P0×5 / P1×11 / P2×6 / P3×5）自 2026-09-06 起**一项未销账** |

---

## 二、潜在风险登记册（按危险度排序）

| # | 风险 | 证据 | 后果 |
|---|------|------|------|
| R1 | **代码签名口令明文入库**：`cscKeyPassword: SmartSideBAR-2026` 直接写在 electron-builder.yml 中随 git 分发；pfx 本体虽未提交，但口令+历史中若曾出现 pfx 即可伪造签名 | electron-builder.yml:40 | 任何获得仓库读权限者可尝试伪造发布物；且与 CI 注释「证书从 secrets 注入，绝不入仓库」(ci.yml:6,92) 的安全承诺自相矛盾 |
| R2 | **CI 门禁半瘫痪**：dotnet job 条件 `hashFiles('SmartSideBAR.sln')` 与实际文件 `SmartSideBAR.slnx` 不匹配 → 永不运行；release job `needs:[dotnet]` 连带永不运行；即使运行，NSIS 步骤引用的 `installer/SmartSideBar.iss` 与实际 `installer/SmartSideBAR-v1.3.iss` 大小写/名称不符 | ci.yml:45,104,107 | .NET 侧 40+ 测试、format 门禁、覆盖率门禁、发布流水线**全部空转**；C# 回归只能靠本地自觉 |
| R3 | **安装包构建不可复现**：Inno/NSIS 脚本从 `artifacts/v1.3-fix/`（gitignore、仅存在于某台机器）取文件；安装器脚本三套并存（iss/nsi/installer.nsh），版本与路径各自漂移 | SmartSideBAR-v1.3.iss:41；SmartSideBAR.nsi:10,31；build/installer.nsh:17,24 | 换一台电脑无法出包；出包内容不对应任何一次 git 提交；审计不可追溯 |
| R4 | **双栈逻辑漂移**：ConfigService / SchedulerService / FloatBallLayout / HotkeyParser / PrinterStateMapper / AppearanceConfig 在 TS 与 C# 各一份实现，仅 C# 有测试；近期 v1.3 修复（外观对齐、2.5K 适配、AppBar）在两条栈上重复劳动 | src/main/services/* 与 src/SmartSideBAR.Core/* 对应文件 | 同一功能两处维护，行为随时间发散；修一处漏一处（FloatBall 多显示器定位已现偏差：WindowManager.cs:317 用主屏 vs Electron 用目标屏） |
| R5 | **AppBar 禁用的技术债**：`if (false && ...)` 死代码 + `updateAppBarPos` 三个调用点全部注释（函数成死代码）+ `getHwnd` 用 `as unknown as number` 把 BigInt 伪装成 Number（类型系统谎言，正是 1.2 版注册静默失败的根因） | manager.ts:203,806,904,918；appbar.ts:19 | 未来任何人恢复 AppBar 都会踩回同一个坑；lint/评审噪声；而 C# 侧 `AppBarService.cs` 已有完整可用实现（含测试） |
| R6 | **质量保障失衡**：TS 侧主进程 1011 行 main.ts + 935 行 manager.ts + 629 行 usb.ts 0 测试；ESLint 全局关闭 `no-explicit-any` 等 5 类规则 | tests/ 仅 smoke.test.ts；.eslintrc.cjs | Electron 冻结期回归无网；若继续维护 TS 栈则风险持续 |
| R7 | **版本漂移**：8+ 处硬编码；`SettingsPanel.vue:189` 默认值仍 `1.1.0`；`build/installer.nsh:17,24` 注册表写 `1.1.0`；Inno `AppVersion 1.3.1` vs `VersionInfoVersion 1.3.0.0` 自相矛盾；props 2.0.0-waveA vs csproj 1.3.0 双方案 | 各 file:line | 诊断报告/关于页/卸载列表显示错乱；安装检测键与升级逻辑判断失据 |
| R8 | **真机验收缺口**：remaining-work P0 五项（AppBar V1-V8 回归矩阵、录屏端到端 MP4 校验、长截图真机、NSIS 实测、Explorer 重启清理）全部未销账 | remaining-work.md:10-19 | 面向教师真机发布的最大不确定性来源 |
| R9 | **性能未达标**：冷启动 ~3.5s（目标 ≤1.2s，单文件解压为大头）、空闲内存 87.5MB（目标 ≤80MB） | remaining-work.md:42-43 | 目标设备为 4-8GB 教学一体机，体感直接可见 |
| R10 | **文档失真**：README 只描述 Electron 架构与 `npm run verify-build` 安装路径，对 Avalonia 主线/真实安装方式零着墨；`docs/project-audit-and-roadmap.md` 首行起即为**未解决的合并冲突标记**（`<<<<<<< HEAD` … `>>>>>>> e2a74b87`）且已提交 | README 全文；project-audit-and-roadmap.md:1,241-243 | 新成员/协作方按 README 走会得到错误的架构认知与构建路径 |
| R11 | **零散缺陷**：recorder.ts 录屏就绪监听器注册在 `recorder:ready` 却从 `overlay:ready` 移除 → 每次录屏泄漏一个 ipcMain 监听器；ToastService 每个 toast 新建顶级窗口无队列去重；SidebarViewModel.OpenLink 吞异常无反馈 | recorder.ts:101,133；ToastService.cs；SidebarViewModel.cs:125-135 | 长时间驻留内存缓慢增长；多提醒并发行为不可预期 |

---

## 三、任务清单

> 优先级：**P0** = 本迭代必须（安全/发布/决策止血）；**P1** = 下一迭代（功能与质量）；
> **P2** = 排期优化；**P3** = 运营演进。
> 进度标记：`未开始` / `已诊断未修` / `进行中` / `已达成`。

### Track A · 安全与发布止血（P0）

#### A1 签名材料出库与口令处理 `P0` `已达成`
- **目标**：消除 R1。electron-builder.yml 删除 `cscKeyPassword` 行；口令只存在于 CI secret（复用 release job 已设计的 `CODESIGN_PFX_B64/PASS`，ci.yml:92-96）与本地构建机的环境变量。
- **待完成**：① 移除 yml 中口令并改为读 env；② 评估是否轮换证书（口令已随仓库历史分发，若仓库曾公开/多人可见则建议轮换）；③ gitleaks 自定义规则确认 yml 类口令可被拦截；④ 各构建机改用环境变量注入。
- **依赖**：无，可立即执行。**风险**：本地 `npm run win` 需要开发者自行设置环境变量，需在 README 写明。
- **验收**：仓库全历史 grep 不再出现口令明文；本地与 CI 出包均可签名。

#### A2 修复 CI 双死 job `P0` `已达成`
- **目标**：让 dotnet 与 release job 真正运行。
- **待完成**：① `ci.yml:45,52,54,56,58` 的 `SmartSideBAR.sln` 全部改为 `SmartSideBAR.slnx`（dotnet 10 支持 slnx restore/build）；② release job `ci.yml:104,107` 的 `installer/SmartSideBar.iss` 改为真实文件名（见 A4 统一后的名字）；③ 覆盖率阈值 `$min = 0`（ci.yml:68）按 C1 节奏提升。
- **依赖**：无前置。是 A4、C1、C2、D3、E1 的**总前置**。
- **验收**：push 到 main 后 4 个 job 全绿；覆盖率报告产出。

#### A3 版本号单一事实源 `P0` `已达成`
- **目标**：消除 R7。版本只定义一次，其余位置生成或动态读取。
- **待完成**：① Electron 侧：`SettingsForm.vue:107`、`SettingsPanel.vue:136,189`、`diagnostic.ts:88`、`main.ts:55` 全部改为构建期注入或 `app.getVersion()` 透传；② 安装器侧：Inno/NSIS 的版本定义统一由发布脚本从 package.json 或 csproj 读取后写入；③ 处理 `Directory.Build.props:9`（2.0.0-waveA）与 `SmartSideBAR.Avalonia.csproj`（1.3.0）的双方案冲突——随 B1 决策定一个；④ 修正 `build/installer.nsh:17,24` 注册表版本。
- **依赖**：无前置；A4 依赖它。**验收**：改一处版本号，全链路（诊断页/关于页/安装器/注册表/日志）一致。

#### A4 安装包流水线可复现化 `P0` `已达成`
- **目标**：消除 R3。CI 从源码直接产出可安装、已签名的安装包。
- **待完成**：① 安装器三选一（建议保留 Inno `SmartSideBAR-v1.3.iss`，归档 nsi 与 electron-builder 的 installer.nsh——后者仅服务 Electron 栈）；② iss 的 `Source: ..\artifacts\v1.3-fix\*` 改为引用 CI publish 输出目录（A2 已产出 `app-win-x64` artifact）；③ 版本号经 A3 注入；④ release job 补齐：签名安装包本体（当前 ci.yml:100 只签 exe 不签安装器）+ 产出 GitHub Release 草稿；⑤ `AppVersion 1.3.1` 与 `VersionInfoVersion 1.3.0.0` 的矛盾随 A3 消除。
- **依赖**：A2（CI 可用）、A3（版本注入）、A1（secrets 签名）。
- **验收**：打 tag 后无需任何本地机器参与，Release 草稿出现已签名安装包；干净机器安装通过。

#### A5 真机回归矩阵销账 `P0` `进行中`（文档已就绪, 待真机执行）
- **目标**：R8 五项逐一销账：AppBar V1-V8 场景表、6 主链路 × 100%/125%/150%/250% 缩放、录屏 ≥1 分钟 MP4 产物校验（希沃白板/微信可读）、长截图（PDF/网页/课件）拼接与取消路径、NSIS/Inno 实机安装、Explorer 重启后 dock 贴齐验证。
- **待完成**：按 docs/testing.md 矩阵在希沃真机执行并回填结果；发现问题回流入 Track B/C。
- **依赖**：可与 A1-A4 并行；录屏/长截图用例依赖当前版本安装包。
- **风险**：这是发布前唯一无法在代码层面代替的验收，需尽早预约真机。

### Track B · 架构收敛与功能补全（P0 决策 → P1 执行）

#### B1 单栈路线决策 `P0（决策）` `已达成`
- **目标**：消除 R4 的根源。书面 ADR 明确主线栈，另一栈进入冻结/归档。
- **事实输入**：实际发布物是 Avalonia exe（A4 证据）；C# 侧 AppBar/测试/覆盖率基建更完整；CI 注释自认 Electron 为「v1.2.0 冻结维护」；但近期 v1.3 用户反馈修复（外观对齐、2.5K、SVG 图标、913dc35）两条栈都在改——双栈维护正在真实发生。
- **建议**：**Avalonia 为主线**，Electron 停止功能开发仅保安全修复，条件满足后归档（E3）。
- **待完成**：决策会 → 写 ADR（含保留 Electron 的成本与理由，若选择保留）→ 按决策调整下述任务取舍。
- **依赖**：无前置；**B2-C3-C6-D(TS 侧任务)-E3 均以它为开关**。
- **风险**：不决策则 R4 持续发酵，每个用户反馈都要修两遍。

#### B2 Electron 侧 AppBar 债务清理 `P2（随 B1 降级或升级）` `已达成`
- **目标**：消除 R5。无论最终是否启用 AppBar，`if (false && ...)` 不应存在。
- **待完成**：① 若 Electron 归档（B1 建议）：删除 `manager.ts:203` 死代码与 `updateAppBarPos` 死函数（manager.ts:118-135 及 806/904/918 三处注释），`appbar.ts:19` 恢复 BigInt→Number 显式转换（正确写法 1.3 初版已有：`Number(buf.readBigUInt64LE(0))`），原生模块与 postinstall 链一并评估去留；② 若决定恢复 AppBar：用正确转换 + 把「是否注册 AppBar」做成配置开关（用户可在设置中选择是否占用 WorkArea），而不是硬编码禁用。
- **依赖**：B1。**验收**：无 `if (false)`、无 `as unknown as number`；行为可配置或代码可删除。

#### B3 通道映射余项 + 通知中心 `P1` `进行中`
- **目标**：补齐 Avalonia 版与 v1.1 功能基线的差距。
- **待完成**：`notification:show/dismiss` 真实现（当前仅 Toast、无 dismiss）；`reminder:selectSound/playTest`（铃声选择 UI）；`appearance:get/set/changed`（玻璃参数编辑）；`oobe:closeAndOpenMain` 联动；`display:list/sidebarTarget`。
- **依赖**：B1（确认在 Avalonia 侧做）。**风险**：低；均为 UI 联动工作。

#### B4 自动隐藏 AppBar `P1` `已达成`
- **目标**：C# 侧 `ABM_SETAUTOHIDEBAR` 注册 + 鼠标贴边唤出，作为「913dc35 用户抱怨」的正面解：既不偷 WorkArea，又能不被全屏应用遮挡。
- **依赖**：B1；实现可参照 `src/SmartSideBAR.Windows/AppBar/AppBarService.cs`（已含测试基建 FakeAppBarApi）。
- **验收**：全屏白板演示时侧栏不遮挡、贴边唤出 ≤200ms。

#### B5 录屏补全 `P1` `已达成`
- **目标**：麦克风混流接入管线（`config.recorder.mic` 已有配置未消费）；多显示器选屏（`GraphicsCaptureItemProbe` 当前固定主屏，需枚举 HMONITOR + 选择 UI）。
- **依赖**：无（Avalonia 侧）；录屏端到端校验归 A5。
- **风险**：多显示器捕获在 19041 SDK 上已有 COM 互操作先例（勘误 #5），工作量可控但真机验证成本高。

#### B6 悬浮球与面板补全 `P1` `进行中`
- **目标**：闲时点击穿透（`WS_EX_TRANSPARENT`）、IME 徽标/录屏状态环；提醒面板 UI、USB/打印机/任务管理器内嵌面板化。
- **当前进度**：悬浮球位置持久化已随 f81477d 落地；其余未动。
- **依赖**：B1。

### Track C · 质量工程（P1-P2）

#### C1 覆盖率门禁 0 → 80% `P1` `进行中`（阈值已提至 40%, 待达标后提至 80）
- **目标**：ci.yml 阈值位（ci.yml:68）分阶段提升：随 A2 复活后先实测现状基线 → 提到 40% → 80%。C# 侧当前约 40+ 用例，重点补 `LongshotService`、`GraphicsCaptureRecorder`、`WindowManager`。
- **依赖**：A2。

#### C2 格式与静态检查门禁固化 `P1` `已达成`（随 A2 生效, format 检查已接入 CI）
- **待完成**：`dotnet format --verify-no-changes` 随 A2 生效后，补本地 pre-commit 钩子（或说明依赖 CI）；评估 TS 侧恢复被全局关闭的 eslint 规则（至少 `no-explicit-any` 对新代码生效，可用 overrides 限定 `src/main`、`src/renderer`）。
- **依赖**：A2。

#### C3 TS 侧测试补齐 `P2（若 B1 决策归档 Electron 则仅保 smoke）` `已达成`
- **目标**：为 `config.ts`（deepMerge/迁移）、`scheduler.ts`（周期推进）、`hotkey.ts`（冲突检测）补单测——三者均为纯逻辑，易测；`@vue/test-utils` 已装未用。
- **依赖**：B1。

#### C4 缺陷修复包 `P1` `已达成`
- **目标**：清掉 R11 与排查中发现的确定缺陷。
- **待完成**：① recorder.ts:101/133 监听器错配泄漏（成功路径从不移除 `recorder:ready` 上的 handler）；② `SettingsPanel.vue:189` 过期默认版本；③ ToastService 加队列与去重；④ `SidebarViewModel.OpenLink` 异常提示；⑤ `WindowManager.cs:360` `_ = appBar.Attach(...)` fire-and-forget 改为记录失败日志。
- **依赖**：①-⑤ 相互独立；①在 Electron 栈（随 B1 定去留），③-⑤ 在 Avalonia 主线。

#### C5 巨型文件重构 `P2` `进行中`（Electron 侧已清 AppBar 死代码; C# 侧未拆）
- **目标**：`main.ts`（1011 行）与 `manager.ts`（935 行）拆分；manager.ts 中侧边栏 X 轴定位 `side === 'right' ? …` 逻辑重复 5 处（createSidebar/resizeMain/dockMain/undockMain/onDisplayChanged）、`Math.round(RAIL_BASE*uiScale)` 重复 6 处，收敛为单一 `computeSidebarRect()`。
- **依赖**：B1（决定是否值得投入）；C3 建议先行以保重构安全网。

#### C6 双栈行为契约 `P2（仅当 Electron 延迟归档）` `已跳过`（B1 决策单栈, 无需契约）
- **目标**：维护「TS ↔ C# 行为对照表」（配置键/热键解析/调度语义/悬浮球限制），差异即 bug。
- **依赖**：B1 结果为「双栈并行维持」时才立项。

### Track D · 性能（P1-P2）

#### D1 冷启动 ~3.5s → ≤1.2s `P1` `进行中`（ReadyToRun + 延迟加载已落地, 待真机测基线）
- **方案**：单文件自包含解压为第一大头 → 实验三选一：ReadyToRun / 放弃单文件改多文件分发（安装器本就打目录）→ 启动路径延迟加载（录屏/长截图模块）。
- **依赖**：无；A5 真机基线先行。**验收**：bench-p28 三次中位数 ≤1.2s。

#### D2 空闲内存 87.5MB → ≤80MB `P2` `进行中`（ToastService 窗口复用已随 C4 做）
- **方向**：WinRT/GraphicsCapture 句柄持有审查、Avalonia 渲染线程、ToastService 窗口复用（与 C4-③ 合并做）。
- **依赖**：A5 实测数据。

#### D3 性能基准与 E2E 冒烟入 CI `P2` `已达成`（perf-bench job 已接入 ci.yml）
- **待完成**：`scripts/bench-p28.mjs` 已支持 `--exe` 参数，接入 CI 定时任务（如每周）跑冷启动/内存/CPU，报告归档；`SSB_SMOKE_EXIT_MS` 等冒烟钩子接入 + 崩溃截图上传 artifact。
- **依赖**：A2（CI 可用）。

#### D4 TS 侧性能债（随 B1 决策）`P2` `已跳过`（B1 决策 Electron 冻结, 不投入）
- **项**：`config:set` 全量同步 `writeFileSync`（config.ts:134,162）改防抖异步；长截图 O(n²) metadata 重读（longshot.ts:209-213）；截图同步写盘（capture.ts:126,128,161）。**仅当 Electron 继续维护时投入。**
- **依赖**：B1。

### Track E · 运营与文档（P3）

#### E1 v2.0-beta Release 流程 `P3` `进行中`（A4 流水线已就绪, 待打 tag）
- **待完成**：tag 规范（v2*）→ A4 流水线 → GitHub Release 草稿 → 签名 secrets 配置（注意 remaining-work 环境备忘：cert/pfx-password.txt 是 DPAPI 串，CI 需明文口令单独入 secret）。
- **依赖**：A1、A2、A4。

#### E2 旧版迁移公告 `P3` `已达成`
- Electron v1.x → Avalonia 版切换说明（配置互认已实现：`%APPDATA%\SmartSideBAR\config.json` + 旧目录自动迁移）。
- **依赖**：E1。

#### E3 Electron 归档 `P3` `进行中`（B2 已清, B3 通道补齐中）
- **待完成**：FE-PARITY 76 通道逐行勾验（remaining-work 附录 A 仍欠）→ 归档声明（README/分支保护/CI 调整）→ B2 清理随之执行。
- **依赖**：B1、B3（通道补齐到位）。

#### E4 文档修复 `P1（低成本高收益，建议提前）` `已达成`
- **待完成**：① 解决 `docs/project-audit-and-roadmap.md:1,241` 的合并冲突标记（保留 HEAD 版、补变更记录尾）；② README 增加「双栈架构与当前发布形态」章节，修正安装指引指向真实产物；③ remaining-work.md 已被本清单承接，标注状态或合并。
- **依赖**：无。

#### E5 MSIX 通道评估 `P3` `已达成`
- **依赖**：E1 完成后评估（ADR-M7）。

---

## 四、依赖关系图

```
A1(签名出库) ──────────────┐
A2(CI复活) ──┬── C1(覆盖率) │
             ├── C2(格式门禁)│
             ├── D3(基准入CI)│
             └── A4(可复现安装包) ── E1(v2.0-beta Release) ── E2(迁移公告) ── E5(MSIX)
A3(版本单一源) ── A4 ┘
A5(真机回归) ──(发现问题回流)──→ B5/C4/D1/D2
B1(单栈决策) ══════ 开关 ══════→ B2 / C3 / C6 / D4 / E3
B3/B4/B5/B6(功能补全) ── B3+ ──→ E3(归档条件)
C4(缺陷修复) ── 独立可立即做（③④⑤项）
E4(文档修复) ── 独立可立即做
```

关键结论：**A2 是最多任务的总前置**；**B1 决定约 1/3 任务的取舍**；A1/C4/E4 无前置，可当天开工。

## 五、建议执行顺序（前三个迭代）

| 迭代 | 任务 | 理由 |
|------|------|------|
| 第 1 周 | A1、A2、A3、C4-①②、E4 | 全部为当天/两天级工作量；消除最高危安全项与门禁空转；清掉确定缺陷 |
| 第 2 周 | B1 决策会 + ADR、A4、C4-③④⑤ | 决策解锁 1/3 清单；发布流水线打通到 Release 草稿 |
| 第 3 周起 | A5 真机矩阵、D1、C1、B4/B5/B6 | 真机验收与性能达标并行；功能补全按 B1 结论取舍 |

## 六、证据索引（关键 file:line）

- AppBar 禁用：`src/main/windows/manager.ts:203`；死调用点 :806/:904/:918；死函数 :118-135
- BigInt 类型谎言：`src/main/services/appbar.ts:19`（正确写法见提交 913dc35 的上一版）
- 签名口令：`electron-builder.yml:40`
- CI 死 job：`.github/workflows/ci.yml:45`（.sln vs .slnx）、:104/:107（iss 文件名不符）、:68（阈值 0）
- 录屏监听器泄漏：`src/main/services/recorder.ts:101`（注册于 recorder:ready）vs :133（自 overlay:ready 移除）
- 安装器依赖本地产物：`installer/SmartSideBAR-v1.3.iss:41`；版本自相矛盾 :20 vs :30,33
- 过期版本：`src/renderer/components/SettingsPanel.vue:189`、`build/installer.nsh:17,24`
- 文档冲突标记：`docs/project-audit-and-roadmap.md:1,241-243`
- 双栈漂移实例：`src/SmartSideBAR.Avalonia/WindowManager.cs:317`（悬浮球用主屏）vs Electron 侧用目标屏
