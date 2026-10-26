# ADR-001: 技术栈定在 Avalonia

- **状态**: 已定
- **日期**: 2026-09-30
- **关联**: optimization-backlog-2026-09-30.md Track B / R4

## 背景

当时仓库里有两套完整实现：

| 栈 | 技术 | 位置 | 测试 | CI | 发布物 |
|----|------|------|------|-----|--------|
| Electron | Electron 30 + Vue 3 + TS | `src/main`, `src/renderer` | 4 个 smoke | typecheck+lint+test | 否 |
| Avalonia | C# .NET 10 + Avalonia 11 | `src/SmartSideBAR.*` | 40+ 用例 | 构建+测试+覆盖率 | **是** |

几个事实：

1. 装出去的 exe 是 Avalonia 编出来的（Inno 和 NSIS 打包脚本指的都是 `SmartSideBAR.Avalonia.exe`）
2. C# 侧的 AppBar 有完整实现，测试也齐（FakeAppBarApi）；TS 侧只有 smoke
3. Electron 那边的 CI 注释里自己写着「v1.2.0 冻结维护」
4. 但用户反馈过来的修复（外观对齐、2.5K 适配、SVG 图标、AppBar 禁用）两条线都在改，双栈并行维护是真的在发生
5. 两边互不引用，逻辑已经开始走岔

## 决定

**主线只留 Avalonia (C# / .NET 10)。Electron 不再做功能，只修安全问题，条件够了就归档。**

## 为什么

1. 发出去的程序是 Avalonia 编的，继续在 Electron 上修等于修的没发、发的没修
2. C# 侧 40+ 测试加覆盖率门禁，TS 侧 4 个 smoke
3. Electron 的 AppBar 因为 BigInt 类型问题被 `if (false)` 关掉了；C# 侧 `AppBarService` 是能用的
4. 双线维护意味着每个用户反馈都改两遍。而且已经漂了：悬浮球多显示器定位，`WindowManager.cs:317` 用主屏、Electron 用目标屏
5. 目标机器是 4-8GB 的教学一体机。.NET 单文件 14.7MB，Electron 包 100MB 起

## 落地

马上做：

- Electron 侧 AppBar 死代码清掉（`if (false)` / `updateAppBarPos` / BigInt 相关）
- TS 测试只留 smoke，不再补单测
- 巨型文件重构、TS 性能优化都不投入
- 不做双栈行为契约（已经没有双栈了）

继续投入：

- 功能补全、质量工程、性能优化全部在 Avalonia 侧

Electron 归档要满足的条件：

1. FE-PARITY 76 通道逐行勾验完成
2. README/文档都指向 Avalonia 主线
3. CI 的 electron job 只留安全门禁（typecheck+lint+test）
4. 归档声明写进 README + 分支保护

## 如果反悔的代价

- 每个功能改动 x2 人天
- 配置/热键/调度/悬浮球逻辑要持续双向同步
- 类似悬浮球多显示器偏差这种漂移 bug 会一直出现
- 大概占总维护成本的 40-50%

## 变更记录

| 日期 | 变更 |
|------|------|
| 2026-09-30 | 初版：定 Avalonia 主线，Electron 冻结→归档 |
