# SmartSideBAR 开发规则（必须遵守）

> 本文件描述**当前代码真实遵守的约定**，不是理想愿景。新增代码违反其中任何一条，review 直接打回。
> 规则结构借鉴 VeriRandom 的 `docs/project_rules.md`（把"硬性规则"与"怎么写才符合本项目"分开写）。

## 项目概述

| 类别 | 技术 | 用途 |
|---|---|---|
| 语言与运行时 | C# / .NET 10（`net10.0-windows10.0.19041.0`） | 应用、平台能力与测试 |
| UI | Avalonia 11 | 侧栏、悬浮球、面板 |
| 平台能力 | Win32 / COM / WMI（仅 `SmartSideBAR.Windows`） | 应用栏、截图录屏、设备与音量 |
| 配置 | `System.Text.Json` 源生成（`ConfigJsonContext`） | 三层配置（默认 → ProgramData 策略 → APPDATA 用户） |
| 测试 | xUnit + Avalonia.Headless | 单测 + headless 冒烟 |

分层与命名空间职责见 [`docs/namespaces.md`](namespaces.md)。

## 硬性规则（违反就会出问题）

1. **三层不许倒挂**：`Core` 不得引用 `Windows`/`Avalonia`；`Windows` 不得引用 `Avalonia`；Win32/COM/WMI 只能在 `Windows` 层。
2. **面板必须注册**：新增面板 = 在 `Ui/Panels/PanelCatalog.cs` 注册一条 `PanelDefinition` + 一个工厂。禁止在 `SidebarWindow.ShowPanel` 里加 `switch` 分支。
3. **设计令牌是唯一色彩/尺寸来源**：颜色、圆角、间距、动效时长一律取 `DesignTokens`（即 `design-tokens.json`）。需要新值 → 改令牌文件，不要在控件里写死。
4. **配置键有白名单**：新配置字段必须同时进 `SidekickConfig` 与 `ConfigJsonContext`；写入走 `ConfigService.Set("点分路径", value)`，绕过白名单直接改 JSON 一律不接受。
5. **策略段不可被用户写**：`policy.` 前缀的键由 ProgramData 策略层下发，`ConfigSubscription` 默认锁定，UI 相应控件置灰。
6. **时间必须可测**：涉及"当前时间"的逻辑（提醒、状态机、日志轮转）一律经 `TimeProvider` 或纯函数注入，禁止在 Core 里直接 `DateTime.Now`。
7. **面板/窗口不 `new` 服务**：可复用服务走 `SidebarServices` 或构造注入，避免每处各建一份导致状态不一致。
8. **测试自建临时目录**：涉及文件系统的测试用独立临时目录并在 `Dispose` 清理，禁止读写真实配置路径。
9. **文档与代码同变更**：改动证明/威胁面/配置语义的行为，必须在同一次提交里更新 `THREAT_MODEL.md`（规则见该文件）。只改代码不改文档 = 未完成。
10. **构建门禁全绿才能合并**：`dotnet build` 0 错误 0 警告、`dotnet test` 全过、`dotnet format --verify-no-changes` 通过、CI 覆盖率 ≥40%。

## 怎么写才符合本项目

### 新增业务面板
```csharp
// 1) 定义元数据（id/名称/图标/顺序）
Register(PanelKind.MyFeature,
    new PanelDefinition("myFeature", "我的功能", "fluent:star", Order: 9),
    c => new MyFeaturePanel(c.Services, c.Palette));
```
面板类继承 `SidebarPanel`，实现 `Build()`；内容整体可替换，刷新走 `Refresh()`。
**不要在控件事件处理器里同步重建面板**（原生崩溃来源之一），统一 `Refresh()`。

### 新增配置项
1. `SidekickConfig` 加属性（带默认值）；
2. `ConfigJsonContext` 登记类型（集合类型不登记会在 `Set<T>` 时抛异常）；
3. UI 读写一律 `Services.Config.Set("display.xxx", value)`；
4. 需要按键订阅 → `Services.Config.Subscriptions.Subscribe("display.xxx", handler)`。

### 新增提醒/定时逻辑
放到 `Core/Scheduling`，注入 `TimeProvider`；一次性/间隔/整点走 `SchedulerService.AddReminder`，重复规则用 `Recurrence`。

### 新增通知
`ToastService.Instance.Show(source, NotificationLevel.Warn, message)` —— 队列会按来源/级别排序，无需自行排队。

## 门禁速查

```bash
dotnet build SmartSideBAR.slnx -c Release        # 0 错误 0 警告
dotnet test SmartSideBAR.slnx -c Release         # 全过
dotnet format SmartSideBAR.slnx --verify-no-changes
```
