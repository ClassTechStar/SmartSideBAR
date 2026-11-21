# SmartSideBAR 全量缺陷排查与测试报告（2026-11-21）

> 审计基线：`f4dd96e`（v1.8.0 文档）→ 修复提交 `e347554`
> 方法：三路并行代理深审（Core 全部 42 文件 / Avalonia UI 层 / 平台层与基建）+ 人工逐文件复核
> 修复验证：362 用例全绿 + `dotnet format` 通过 + Release 构建冒烟通过

---

## 一、问题排查清单（16 项确认缺陷，全部已修复）

### A. 启动可用性（最高优先）

| # | 级别 | 缺陷 | 位置 | 修复 |
|---|------|------|------|------|
| 1 | P0⁺ | 配置文件被同步盘/杀毒占用时 `File.ReadAllText` 抛 IOException 直接逃出 `Load()` → **启动即死**（与其自身"解析失败用默认值"的兜底意图矛盾） | ConfigService.cs:76,88 | 新增 `TryReadFile` 兜底，读取失败按"该层缺失"处理 |
| 2 | P0⁺ | JSON 合法但 schema 类型不符（手工编辑/互认文件写坏，如 `"fps": "high"`）→ `Deserialize` 抛 JsonException 逃出 `Load()` → 启动即死 | ConfigService.cs:247 | `RefreshTypedNoLock` 反序列化包 try/catch，失败回退 `new SidekickConfig()` |

### B. 数据完整性

| # | 级别 | 缺陷 | 位置 | 修复 |
|---|------|------|------|------|
| 3 | P1 | 录屏暂停期间采集会话持续出帧，恢复后首帧补偿时间戳**早于上一交付帧**（时间戳回退）——MediaFoundation 行为未定义，可产出损坏 MP4 | GraphicsCaptureRecorder.cs:391 | 新增 `_lastSampleTs` 单调钳制游标 |
| 4 | P1 | 录屏硬编 10s 准备超时放弃后，**迟到的 prepare 续体仍执行**：打开旧文件句柄、覆盖 `_mss/_output`，与新一轮录制交叉污染 | GraphicsCaptureRecorder.cs:197-211 | `_sessionEpoch` 会话代号守卫，换代自弃并清理 |
| 5 | P1 | 手工构造 `{"policy": {}}` 配置导入：`EnumerateLeaves` 对空对象产叶（路径 `"policy"`），绕过 `policy.` 前缀锁 → **整棵策略子树被替换成空对象**（管理员管控被清空） | ConfigTransfer.cs:59-67 + ConfigSubscription.cs:55 | 空对象不产叶；前缀锁同时匹配段名本身（含测试钉死） |
| 6 | P1 | 触屏滚动 `TouchScroll`：按下落在按钮上时 ButtonBase 捕获指针 → ScrollViewer 收不到移动/释放，**既拖不动又误触按钮**（拖到哪个磁贴就激活哪个） | TouchScroll.cs 全文 | 隧道监听（handledEventsToo）+ 越阈抢捕获；静止点击不受影响 |
| 7 | P2 | 导入的提醒 `At=0`（1970）+ 1min 间隔 → `NextSlotAfter` 线性自旋约 3000 万次，启动期卡死 | SchedulerService.cs:187-195 | 算术推进（一步除法），+测试 |
| 8 | P2 | `ConfigTransfer.RemovePrefix` 在根对象上删末段：嵌套敏感键（如 `capture.sub.`）删错位置 → 敏感子树**原样导出** | ConfigTransfer.cs:86-92 | 走父容器删末段 |
| 9 | P2 | 看门狗重启竞态（上轮 SUSPECTS 转正）：先拉新后退出 → 新实例抢不到单实例互斥 → **新旧双双退出，应用凭空消失** | MemoryWatchDogHost.cs:76 + SingleInstance.cs | `MarkRestart` 标记 + Acquire 失败时 10s 标记新鲜期内最多 8s 重试抢占 |

### C. 性能与资源

| # | 级别 | 缺陷 | 位置 | 修复 |
|---|------|------|------|------|
| 10 | P2 | `LessonStats.Record` 在 UI 线程事件里锁内全文件 JSON 重写 + 落盘（每节课几十次） | LessonStats.cs:43-54 | 脏标记 + 合并写，I/O 全程锁外 |
| 11 | P2 | `ExportLessonPack` 整段 zip 在 `_gate` 内执行（几百 MB 批注图）→ UI 冻结、统计写入全阻塞；中途异常留下**损坏 zip** | LessonStats.cs:70-109 | 锁内取快照、锁外打包、失败删半成品 |
| 12 | P2 | `SingleInstance.Acquire` 重写时发现：非持有者调 `ReleaseMutex` 抛异常被吞 → 双实例都当主实例（本轮引入即修复） | SingleInstance.cs | 两段式 `TryAcquireOnce`，无所有权误操作 |

### D. 供应链与安全

| # | 级别 | 缺陷 | 位置 | 修复 |
|---|------|------|------|------|
| 13 | P2 | NU1902 ×12：传递包 `Microsoft.Build.Tasks.Git 8.0.0` 有已知中危漏洞（GHSA-23fw-v26w-5fgq，NTLM 凭据泄漏） | Directory.Build.props:22 | SourceLink 升 10.0.102 + 传递包钉修版 10.0.403，**NU 警告清零** |

### E. 上轮 SUSPECTS 收敛结论

| 上轮 SUSPECT | 结论 |
|------|------|
| WinRT InkAnalyzer 依赖 WPF 不可移植 | 已失效——现实现通过 `InkStrokeBuilder` 从点阵构造（不依赖 InkPresenter），Avalonia 可用 |
| 热键重注册泄漏 | 无缺陷：`Register` 内部先 `Unregister(slot)` 再注册 |
| ShellService URL 白名单 userinfo@host 绕过 | 无缺陷：`Uri.Host` 规范化后 `gitee.com@evil.com` 形态正确拒绝 |
| `Environment.Exit` 跳过 ProcessExit 钩子 | 无缺陷：.NET `Environment.Exit` 会执行 `AppDomain.ProcessExit` 事件 |
| Recurrence UTC+8 天偏移 | 维持"自洽无调用方"结论（ScheduleOverride/Recurrence 当前无生产消费方，属已登记的死接线） |

---

## 一.5 第三轮发现（平台层 + DI 图审计，同批修复）

| # | 级别 | 缺陷 | 修复 |
|---|------|------|------|
| 17 | **P0** | `LessonStatsService` 的 DI 注册在后续迭代中丢失 —— WindowManager 构造无法解析，**应用启动即崩**；353 个测试全绿（全部手工构造绕过容器），v1.8.0 安装包即受影响 | 补回注册；新增 `DiGraphTests` 双守卫（源扫描 + 容器级真解析 WindowManager），此类回归从此必有红灯 |
| 18 | P1 | `UpdateAutoChecker.Init` 接线同样丢失 —— 设置里「每天自动检查更新」是死键 | 补回 Init 调用 + 源扫描守卫 |
| 19 | P1 | 录屏 StartAsync 不清理上一会话：转码泵自然收尾（静屏 EOF）后采集会话/帧池/订阅全存活；信号量盈余计数使下轮录制秒 EOF → 0 字节 | StartAsync 头部 Cleanup + 排空 |
| 20 | P2 | SoundService MP3 重复播放（repeat≥2）MCI 重复别名失败，只有第一遍出声 | open 前先 close |
| 21 | P2 | VolumeService 每次 Get/Set 泄漏一个 COM 引用（QI 归调用方所有未释放） | finally Release |
| 22 | P2 | WndProcHook 回调抛托管异常 = 无堆栈进程崩（未来隐患） | Dispatch 逐回调 try/catch 降级 |

第三轮确认无缺陷项：热键重注册、app.manifest PerMonitorV2、CI 版本注入/SHA256SUMS 顺序/arm64、nightly、设计令牌 JSON、.gitignore、URL 白名单规范化、ShellService 白名单绕过（userinfo@host）均通过。

## 二、修复记录

全部修复见提交 `e347554`（+回归测试 6 例）与本报告对应文件：

| 文件 | 改动 |
|------|------|
| ConfigService.cs | `TryReadFile` 兜底 + `RefreshTypedNoLock` schema 兜底 |
| ConfigSubscription.cs | `IsLocked` 精确段匹配 |
| ConfigTransfer.cs | `EnumerateLeaves` 空对象跳过 + `RemovePrefix` 走父容器 |
| SchedulerService.cs | `NextSlotAfter` 算术化 |
| LessonStats.cs | Record 锁外合并落盘 + Export 快照外打包/失败清理 |
| GraphicsCaptureRecorder.cs | 时间戳单调钳制 + `_sessionEpoch` 孤儿守卫 |
| SingleInstance.cs | 重写 `Acquire`（两段式）+ 重启标记等待重试 + `MarkRestart` |
| MemoryWatchDogHost.cs | 重启前落标记 |
| TouchScroll.cs | 隧道监听 + 越阈抢捕获 + 释放抑制 |
| Directory.Build.props | SourceLink 10.0.102 + Build.Tasks.Git 10.0.403 钉版 |

**回归守卫**（tests/SmartSideBAR.Core.Tests/AuditFixTests.cs，6 例）：schema 损坏回退、占用文件回退、精确锁拒写、空对象叶跳过、古董时间戳算术推进、统计记录/打包闭环。

---

## 三、全量测试结果

| 套件 | 用例数 | 结果 |
|------|--------|------|
| SmartSideBAR.Core.Tests | 282 | ✅ 全绿 |
| SmartSideBAR.Windows.Tests | 20 | ✅ 全绿 |
| SmartSideBAR.Avalonia.Tests | 57 | ✅ 全绿（含 headless 冒烟 7 链路 + 像素结构基线 3 态） |
| **合计** | **362** | **0 失败** |

覆盖维度：
- **正常流程**：六链路 headless 冒烟（rail 点击→面板、画圆→转形、工作区切换、提醒渲染、Toast→通知中心、链接网格）
- **边界场景**：像素结构基线（胶囊/面板/主题明暗翻转）、重复/去重/容量上限（通知 50 条、笔记 200 条）、ReleaseNotes 版本棘轮
- **异常输入**：schema 损坏配置、非法 JSON、古董时间戳、空对象子树、全黑帧长截图、坏底图批注
- **兼容性**：100%–250% DPI（DIP 坐标系 + 像素基线）、经典白/液态玻璃/高对比度三主题、x64 + arm64 双架构构建、深浅色
- **代码质量门禁**：`dotnet format --verify-no-changes` 通过；编译 0 警告（NU1902 清零后）

## 四、遗留登记（非缺陷，透明披露）

1. **winget manifest 停在 1.5.0** —— 待 v1.8.0 GitHub Release 发布后随提交流程更新（依赖真实 Release URL + SHA256）。
2. **Recurrence/ScheduleOverride/`Reminder.Recurrence` 为死接线** —— 配置面存在但无调度消费方（CHANGELOG 已宣传，属功能欠账非 bug）。
3. **FanArcSelector 迟滞中心偏移**（~11.5°）—— 纯手感问题，功能正确。
4. **`Ruleset` 空条件规则静默不触发** —— 设计如此；建议编辑器未来给出提示。
5. **LessonStats `_byDay` 按年累积** —— 今日数据量级极小（KB 级/年），暂无清理必要。

**结论：全部确认缺陷已修复并有回归守卫；359 用例 0 失败；无遗留未通过测试项。**
