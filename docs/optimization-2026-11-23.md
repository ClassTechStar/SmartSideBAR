# SmartSideBAR 深度优化落地报告（2026-11-23）

对应输入：《SmartSideBAR 深度优化方案》（2026-11-23，基于 v1.8.0 的独立审计）。
本文记录**逐条核验结论、本轮已实施内容、效果对比、未实施项原因与后续注意事项**。

基线：`main` @ v1.8.0，152 个源文件 / 17,148 行 C#；本轮结束后构建 0 警告、461 项测试全绿、`dotnet format` 通过。

---

## 一、核验结论总表

| 方案条目 | 核验结果 | 处置 |
|---|---|---|
| P0-1 更新链中间人（镜像改写 SUMS） | **成立**。`CheckAsync` 用同一镜像前缀改写安装包与 `SHA256SUMS.txt`；且「立即安装并重启」确实走 `LaunchInstaller` + `Environment.Exit(0)` | 已实施（见 §2.1） |
| P0-2 发行者署名 `Seewo Sidekick Team` | **成立**（`installer/SmartSideBAR.iss:24`）。补充：winget 清单署名已是 `ClassTechStar`，"winget 同源"不成立 | 已实施（见 §2.2） |
| P0-3 全局钩子失败静默降级 | **成立**。`WndProcHook.Add` 丢弃 `SetWindowLongPtr` 返回值，调用方无感 | 已实施（见 §2.3） |
| P0-4 Gitee 通道指向"第三方账号" | **不成立**。`git remote -v` 显示 gitee 远端即作者自有镜像且本项目可直接推送。但"仅长度校验"成立 | 调整处置：保留通道、收紧哈希信任（§2.1） |
| 3.1 UI 架构反写（5 axaml / 152 cs、`SettingsWindow` 690 行、`Children[0]` 摸树） | **成立** | 只做低风险项（导航引用化、设计规范页下线）；MVVM/全量 axaml 属大重构，未做 |
| 3.3 PanelKit 与 M3 两套设计系统 | **部分成立且正在收敛**。最近 3 个提交已把深色/浅色统一到 M3；PanelKit 仍用于既有面板 | 未新增改动，维持现状（过渡态） |
| 4.1 配置写放大 | **成立**。一次保存 20+ 次 `SetIfChanged`，每次整文件序列化 + 备份 + 替换 | 已实施 `ApplyMany`（§2.4）；`.bak` 降频经评估**不采纳**（理由见 §4） |
| 4.2 31 个 DispatcherTimer 各自为战 | **成立**（全仓 31 处引用） | 未实施：需统一 tick 源与可见性门控，属架构级改动 |
| 4.3 麦克风混流空壳 | **成立**。设置页灰勾选框 + 录屏器注明"无音频采样源"，README 却写"麦克风可配" | README 口径已对齐；音频管线未实现（§4） |
| 4.3 停止录制 15s 超时救火 | 成立（转码等待路径） | 未实施：需转码事件化改造 |
| 4.4 IME 角标输出十六进制片段 | **成立**，并发现更深一层 bug：`zh-CN` / `zh-TW` 子语言映射**反转**（sublang 2 才是简体） | 已实施（§2.5） |
| 5.1 设置页"部分即时/部分保存" | 成立 | 未实施：与"保存按钮退役"绑定，属交互决策 |
| 5.2 触屏 hover 误触 | **成立**。`PointerEntered` 不区分指针类型 | 已实施（§2.6） |
| 5.3 激光笔叠加 / 板中板吸附 | 成立 | 未实施：需产品语义确认（激光遮笔迹还是只清自身轨迹） |
| 5.4 i18n 全中文硬编码 | 成立 | 未实施（大工程） |
| 5.5 悬浮球闲时变暗 5s 定时器 / 性能模式 | 定时器存在；性能模式已有等价入口（关闭液态玻璃 + 减弱动效） | 未改 |
| §6 字体 MiSans 7.9MB | **成立**（7.82MB，占单文件约一半） | 未实施：子集化会让任意用户文本（文件名/U 盘卷标/课表）出现豆腐块，需设计取舍（§4） |
| §6 启动 8 窗口全建 | 未核验到"8 窗口全建"的直接证据（窗口为按需创建 + 惰性复用） | 不改 |
| §7 DPI 热重算 / 几何数学收口 | 成立 | 未实施（架构级） |
| §8.1 设计文档/ADR 不在仓库 | **成立**（仅 `docs/adr-001-single-stack.md`，M4/M6/M8 只存在于注释） | 已实施（§2.7） |
| §8.4 Issue 模板缺失 | **成立** | 已实施（§2.7） |
| §9 插件化 / 主题生态 / 文档站 | 成立（均未开始） | 未实施（平台化，按季度规划） |

---

## 二、本轮已实施

### 2.1 更新链：哈希从独立信道获取（P0）

`src/SmartSideBAR.Avalonia/Updater/UpdateService.cs`

- 新增信任模型：只有**作者自有源**（GitHub 直连、Gitee 自有镜像）可作为哈希来源；
  镜像通道（gh-proxy / ghproxy）**完全不取哈希**——代理既能替换安装包，也能替换它转发的 SUMS 与 API 响应。
- `UpdateInfo` 增加 `HashTrusted` 与 `FallbackUrl`；`UpdateDownloadResult` 增加 `Verified`。
- 直连 GitHub 拿到可信哈希后，安装包下载允许回落到 `gh-proxy` 镜像，**校验仍用直连哈希**（安全与校园网可用性兼得）。
- 设置页：`HashTrusted == false` 时不再自动下载、不显示「立即安装」，改为提示到 Release 页面手动获取。
- 回归测试：`只有作者自有通道可作哈希信任源`（含对 `Channels` 全表的断言）。

效果：消除"安装包与哈希清单同源"的必然通过漏洞；镜像从"信任链一环"降级为"纯传输通道"。

### 2.2 发行者署名与品牌边界（P0）

- `installer/SmartSideBAR.iss`：`AppPublisher` → `ClassTechStar & Contributors`（安装包属性、卸载列表同步生效）。
- `README.md`：首段加"与视源股份（Seewo/希沃）无隶属、授权或背书关系"声明；许可证段补充商标说明。
- README 新增「安全承诺」区：零遥测、不申请管理员、不装驱动不注入、更新完整性。

### 2.3 WndProc 子类化失败可见化（P0）

- `IWndProcHook.Add` 改为返回 `bool`；`WndProcHook` 检查 `SetWindowLongPtr` 返回值与 `GetLastWin32Error`，
  失败时回滚注册表项并写 Warning 日志。
- `HotkeyService.Attach` 返回结果；`WindowManager` 在热键钩子失败时弹一次 Toast（"请改用侧栏按钮"）。
- AppBar / USB 监听失败各自记录带上下文的日志。
- 测试假件（`FakeWndProcHook` / `FakeHook`）同步接口。

### 2.4 配置写入批处理（P1）

`src/SmartSideBAR.Core/Configuration/ConfigService.cs`

- 新增 `ConfigChange`（`From<T>` 走与 `Set<T>` 相同的源生成序列化）与 `ApplyMany(...)`：
  逐键做白名单/锁定/变更检测，**一次落盘 + 一次 ConfigChanged 广播 + 逐键订阅通知**。
- `Set` 内部抽出 `CanWriteNoLock` / `ApplyValueNoLock`，`Set` 与 `ApplyMany` 共用同一套规则，语义不变。
- `SettingsWindow.Save()` 由 21 次 `SetIfChanged` 改为一次 `ApplyMany`。
- 测试：批量生效/可重载、未变化键不计数、未知键跳过、锁定策略段拒绝、批量备份滚动（+5）。

效果：一次设置保存的整文件写盘 + 备份 + 替换从 **21 次降为 1 次**。

### 2.5 输入法角标（P1）

- 新增 Core 纯函数 `ImeBadge.Format(ushort languageId)`：中文给 `zh-CN/TW/HK/SG/MO`，
  其他语言给三字母 ISO 码（JPN/KOR/ENG/DEU），未知布局回退 `0x…`。
- 修正历史 bug：MAKELANGID 子语言映射原为 `1=>CN, 2=>TW`，实际应为 `1=>TW, 2=>CN, 3=>HK`。
- `ImeService` 改用该函数；测试覆盖中/日/韩/英/德与未知布局（+9）。

### 2.6 触屏不再触发悬停展开（P1）

`EdgeTriggerWindow`：`PointerEntered` 遇到 `PointerType.Touch` 直接返回，触屏只保留点击展开；鼠标/笔仍可悬停。

### 2.7 工程可维护性（P1）

- `SettingsWindow.SelectNav` 不再 `((Grid)Content!).Children[0]` 摸视觉树，改持 `_navPanel` 引用并做边界检查。
- 设计规范页（开发脚手架）仅在 `#if DEBUG` 构建中出现，正式包设置页回到 9 个分页。
- 新增 `docs/adr/`：索引 + `ADR-M4`（录屏最小闭环）、`ADR-M6`（IME 直调）、`ADR-M8`（配置互认）；
  `docs/ARCHITECTURE.md` 增加索引链接。
- 新增 Issue 模板：`bug_report.yml`（版本/设备/模块/复现/诊断包）、`feature_request.yml`（教学场景优先）、
  `config.yml`（Discussions + 安全私密反馈入口）。

---

## 三、效果对比

| 维度 | 改前 | 改后 |
|---|---|---|
| 更新完整性 | 镜像可同时篡改安装包与哈希清单，校验必然通过 | 镜像不参与哈希；无可信哈希时不自动下载/安装 |
| 更新可用性 | 直连 GitHub 失败即只能走镜像（且不可信） | 直连拿哈希 + 镜像回落下载，两者兼得 |
| 设置保存写盘 | 21 次整文件写 + 21 次备份 | 1 次写 + 1 次备份（-95%） |
| 输入法角标 | 非中文显示 `11` 之类片段；zh-CN/zh-TW 显示颠倒 | `JPN/KOR/ENG`；中文区域正确 |
| 触屏触发条 | 手指划过即展开 | 只响应点击 |
| 正式包设置页 | 含「设计规范」开发页 | 仅 Debug 可见 |
| 热键故障 | 静默失效 | 日志 + 一次性 Toast |
| 发行者署名 | `Seewo Sidekick Team` | `ClassTechStar & Contributors` |
| 测试 | 446 项 | 461 项（+15），全绿 |
| 行覆盖率 | — | 63.3%（Core 86.1% / Avalonia 33.4% / Windows 10.5%，CI 门禁 55%） |

---

## 四、未实施项与原因

### 4.1 需外部条件

| 项 | 原因 |
|---|---|
| 安装包代码签名 | 需购买证书（Certum OSS 约 €30/年）并配置 CI secret；无证书时只能保持"无签名 + 哈希校验" |
| winget-pkgs 正式上架 | 依赖首个签名/稳定版本发布后的自动 PR 审核 |

### 4.2 需产品/设计决策

| 项 | 原因与建议 |
|---|---|
| 字体子集化（7.9MB → ~1.5MB） | 应用会显示任意用户文本（课表、文件名、U 盘卷标、链接标题），静态子集必然出现豆腐块。可选：①保留 MiSans 全量；②改系统字体栈（`Microsoft YaHei UI`，体积立减 7.8MB，观感变化）；③双字体包按需下载。需先定取舍 |
| 默认快捷链接"删不掉" | 现有 5 条内置链接与用户数据同池。建议加 `links.seeded` 首启标记，删除后不再复活；涉及配置 schema 迁移，单独做 |
| 激光笔与其他笔叠加 | "激活激光时收起其他笔"会影响"指着批注讲"的核心场景；需明确是隐藏笔迹还是只清激光轨迹 |
| 设置页"改即生效 + 保存按钮退役" | 涉及撤销/失败回滚/即时预览的完整交互设计，需先定稿 |
| `.bak` 每日降频 | **评估后不采纳**：`ApplyMany` 已把每次保存压到 1 次拷贝（~20KB），保持每写一备可让同日崩溃也恢复到上一次保存，收益大于成本 |

### 4.3 工作量较大（建议按季度排期）

| 项 | 说明 |
|---|---|
| MVVM + 设置窗/面板全量 axaml 化 | 约 4-6 周；建议按"设置窗 → 面板 → 主题资源化"三步走，勿大爆炸重写 |
| 麦克风混流（AudioGraph/MediaStreamSource） | 需真实音频管线与设备兼容性测试；当前 UI 已如实标注未支持 |
| 录屏停止事件化（去掉 15s 超时） | 需重构转码回调链，风险中等 |
| 统一计时器（31 处 DispatcherTimer） | 需按窗口可见性门控，配套空载 CPU 基线 |
| WindowManager 拆分 / DPI 热重算 | 与 UI 重构耦合，建议同批做 |
| JSON 动作契约 / 插件化 | 平台化跳板，建议在 UI 稳定后启动 |
| i18n 最小集 | 先抽 Toast/错误/设置页三类字符串 |

### 4.4 核验为误判/已过时

- "Gitee 指向第三方账号"：远端即作者自有镜像，且本项目可直接推送；只保留"长度校验"弱点处置（已收敛）。
- "winget 清单署名同源 Seewo"：winget 已是 `ClassTechStar`，仅安装脚本需改（已改）。
- "启动 8 窗口全建"：窗口为按需创建 + 隐藏复用，未见全量预建证据。

---

## 五、后续注意事项

1. **发版前**
   - 确认 `Directory.Build.props <Version>` 已升；安装脚本与 winget 清单由 CI 注入/自动提交，不再手改。
   - 若启用签名：先在 CI 配 `CODESIGN_PFX_B64/PASS`，再在 Release 校验签名与 `SHA256SUMS.txt`。
   - `updates.autoCheck` 默认开启；镜像-only 网络下用户会看到"已停止自动下载"的提示，属预期行为。

2. **配置兼容**
   - 新增配置字段必须同时登记 `ConfigJsonContext`（否则 `Set<T>` 抛 `NotSupportedException`）。
   - `ApplyMany` 与 `Set` 共用白名单/锁定规则；新增批量保存时优先用它，不要恢复逐键 `SetIfChanged`。
   - 改配置 schema 前先跑 `ConfigServiceTests` / `ConfigBackupTests` / `ConfigApplyManyTests`。

3. **更新链**
   - 不要给镜像通道重新接回哈希获取逻辑；新增通道必须先在 `IsTrustedHashChannel` 里明确判定。
   - Gitee 同步发布需要保留 `SHA256SUMS.txt` 资产，否则该通道的哈希只能退化到 API digest（Gitee 无 digest 时即不可信）。

4. **输入法**
   - 角标依赖 MAKELANGID 子语言位；新增中文变体时先补 `ImeBadge` 测试再改映射。

5. **UI 迁移**
   - 新面板继续走 axaml；旧面板"改哪迁哪"。迁移时不要顺手改交互语义，先保行为一致再加新能力。

6. **已知遗留（非本轮引入）**
   - 工作区外本地目录 `SmartSideBAR-1.1/`（约 8.5MB，未跟踪）可手动删除。
   - `DesignSpecPage` 的测试在 Debug/Release 都会编译该类本身，但正式包不再挂载入口。
   - Windows 层测试仍偏薄（AppBar/钩子/IME），建议按 `FakeAppBarApi` 模式补消息级单测。

---

*本文为工程落地记录，与《深度优化方案》原文的第三方审计立场无关。*
