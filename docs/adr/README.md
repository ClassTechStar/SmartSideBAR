# 架构决策记录 (ADR)

代码注释里引用的 `ADR-xxx` 编号都在这里有对应文档。写新决策时复制一份模板：背景 → 决定 → 代价 → 变更记录。

| 编号 | 标题 | 状态 | 位置 |
|---|---|---|---|
| ADR-001 | 技术栈定在 Avalonia（Electron 归档） | 已定 | [../adr-001-single-stack.md](../adr-001-single-stack.md) |
| ADR-M4 | 录屏走 Windows.Graphics.Capture + MediaFoundation 最小闭环 | 已定 | [adr-m4-recorder.md](adr-m4-recorder.md) |
| ADR-M6 | 输入法切换直调 Win32，不起 PowerShell 守护进程 | 已定 | [adr-m6-ime.md](adr-m6-ime.md) |
| ADR-M8 | 配置模型与 Electron 版逐字段互认 | 已定 | [adr-m8-config-interop.md](adr-m8-config-interop.md) |

约定：

- 新增决策先落文档再落代码；改决策时在文末「变更记录」追加一行，不重写历史。
- 补丁编号（P0-3 / B2 / K4 之类）只说明「当时为什么」，具体时间交给 `git blame`。
