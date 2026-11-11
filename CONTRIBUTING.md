# 贡献指南

感谢你为 SmartSideBAR 提交问题、建议或代码。这是一个面向教学大屏（Windows 10/11）的常驻侧边工具，**稳定性优先于功能密度**——课堂上出问题没有重来的机会。

## 提交 Issue

- 用 [GitHub Issues](https://github.com/ClassTechStar/SmartSideBAR/issues) 报缺陷或提需求。
- 报缺陷请给：复现步骤、预期/实际行为、版本号（设置 → 关于）、系统与缩放比例、日志（设置 → 诊断 → 导出诊断包）。
- 不要在公开 Issue、日志或截图中提交任何凭据、令牌或个人信息。

## 开发环境

| 类别 | 技术 | 用途 |
|---|---|---|
| 语言与运行时 | C# / .NET 10 | 应用、平台能力与测试 |
| UI | Avalonia 11 | 侧栏、悬浮球、面板 |
| 平台能力 | Win32 / COM / WMI（仅 `SmartSideBAR.Windows`） | 应用栏、截图录屏、设备、音量 |
| 测试 | xUnit + Avalonia.Headless | 单测 + headless 冒烟 |

前置：[.NET SDK 10.0.x](https://dotnet.microsoft.com/download/dotnet/10.0)、Git。

## 获取与验证

```bash
git clone https://github.com/ClassTechStar/SmartSideBAR.git
cd SmartSideBAR

dotnet restore SmartSideBAR.slnx
dotnet build SmartSideBAR.slnx -c Release --no-restore
dotnet test SmartSideBAR.slnx -c Release --no-build
dotnet format SmartSideBAR.slnx --verify-no-changes --no-restore
```

## 硬性规则

写代码前请读 [`docs/project_rules.md`](docs/project_rules.md)；分层职责见 [`docs/namespaces.md`](docs/namespaces.md)。要点：

1. **三层不倒挂**：Core 不碰 Win32/UI；平台能力只在 `SmartSideBAR.Windows`。
2. **面板走注册表**（`PanelCatalog`），不在窗口里加 `switch`。
3. **色彩/尺寸走设计令牌**（`design-tokens.json`），不写死颜色。
4. **配置键走白名单**（`ConfigService.Set`），新字段同步 `SidekickConfig` 与 `ConfigJsonContext`。
5. **涉及威胁面的改动同次提交更新 `THREAT_MODEL.md`**（该文件写了「只改代码不改文档=未完成」）。
6. **测试自建临时目录**并在 `Dispose` 清理。

## PR 要求

- 保持一个 PR 只做一件事，便于 review 与回滚。
- 说明做了什么、为什么、怎么验证的；UI 改动附前后对照。
- 不引入新的第三方依赖，除非能说明为什么标准库做不到。
- 提交信息用中文短句，动词开头，行为描述而不是文件名列表。
