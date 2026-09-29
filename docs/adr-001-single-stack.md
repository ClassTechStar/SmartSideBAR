# ADR-001: 单栈路线决策 —— Avalonia 为主线

- **状态**: 已接受 (Accepted)
- **日期**: 2026-09-30
- **决策者**: SmartSideBAR 项目组
- **关联**: optimization-backlog-2026-09-30.md Track B / R4

## 背景

仓库同时存在两套完整实现：

| 栈 | 技术 | 位置 | 测试 | CI | 发布物 |
|----|------|------|------|-----|--------|
| Electron | Electron 30 + Vue 3 + TS | `src/main`, `src/renderer` | 仅 smoke 4 用例 | typecheck+lint+test | 否 |
| Avalonia | C# .NET 10 + Avalonia 11 | `src/SmartSideBAR.*` | 40+ 用例 | 构建+测试+覆盖率 | **是** (Inno/NSIS 均打包 Avalonia exe) |

关键事实：
1. **实际发布物是 Avalonia exe**（Inno `installer/SmartSideBAR-v1.3.iss` 与 NSIS 均打包 `SmartSideBAR.Avalonia.exe`）
2. C# 侧 AppBar/测试/覆盖率基建更完整（含 FakeAppBarApi 测试替身）
3. CI 注释自认 Electron 为「v1.2.0 冻结维护」
4. 但近期用户反馈修复（外观对齐、2.5K 适配、SVG 图标、913dc35 AppBar 禁用）**两条栈都在改**——双栈维护正在真实发生
5. 两套实现互不引用（Electron 全库 0 处引用 Avalonia/dotnet），逻辑已开始漂移

## 决策

**Avalonia (C# / .NET 10) 为唯一主线栈。Electron 栈停止功能开发，仅保安全修复，条件满足后归档。**

## 理由

1. **发布物一致性**: 安装器打包的是 Avalonia exe，继续在 Electron 上开发等于修的产品不发、发的产品不修
2. **质量基建**: C# 侧 40+ 测试用例 + 覆盖率门禁 + format 检查 vs TS 侧 4 个 smoke 用例
3. **技术债**: Electron 侧 AppBar 已因 BigInt 类型谎言禁用（`if (false)`），C# 侧 `AppBarService` 有完整可用实现（含测试）
4. **维护成本**: 双栈使每个用户反馈修两遍，且 FloatBall 多显示器定位已现偏差（`WindowManager.cs:317` 用主屏 vs Electron 用目标屏）
5. **性能**: 目标设备为 4-8GB 教学一体机，.NET 自包含单文件 14.7MB vs Electron 包 100MB+

## 影响

### 立即执行
- **B2**: Electron 侧 AppBar 死代码清理（`if (false)` / `updateAppBarPos` 死函数 / BigInt 谎言）
- **C3**: TS 测试降级为仅保 smoke（不再补单测）
- **C5**: 巨型文件重构不投入 Electron 侧
- **D4**: TS 性能债不投入
- **C6**: 双栈行为契约**不立项**（已无双栈）

### 持续投入
- **B3-B6**: 功能补全全部在 Avalonia 侧
- **C1-C2**: 质量工程聚焦 C# 侧
- **D1-D3**: 性能优化聚焦 Avalonia 侧

### Electron 归档条件 (E3)
1. FE-PARITY 76 通道逐行勾验完成（B3 通道补齐到位）
2. README/文档全部指向 Avalonia 主线
3. CI electron job 保留为安全门禁（typecheck+lint+test），不再跑功能测试
4. 归档声明写入 README + 分支保护

## 保留 Electron 的成本（若反悔）

- 每个功能改动 x2 人天
- 配置/热键/调度/悬浮球逻辑需持续双向同步
- FloatBall 多显示器偏差等漂移 bug 持续出现
- 约占总维护成本 40-50%

## 变更记录

| 日期 | 变更 |
|------|------|
| 2026-09-30 | 初版：Avalonia 为主线，Electron 冻结→归档 |
