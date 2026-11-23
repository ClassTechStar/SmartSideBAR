# 插件化 / JSON 动作契约 v1 方案（草案，未实施）

> 目标：让第三方**不改代码、不加载 DLL**，仅通过 JSON 声明就能给 SmartSideBAR 增加动作入口
> （打开备课平台、一键切工作区、按条件执行等），并复用现有的规则引擎、面板注册表与扩展状态页。
> 定位：这是"从工具到平台"的第一步，但**第一批只做声明式动作，不做代码插件**。
>
> **落地状态（2026-11-23）**：E1 已完成 —— `src/SmartSideBAR.Core/Actions`（清单模型 / 校验 / 分层合并）+
> 12 项单测；E2–E4（宿主扫描、执行器、扩展动作面板）未开始。

## 一、现状与可复用资产

| 资产 | 现状 | 在方案中的角色 |
|---|---|---|
| `WindowRouter.Dispatch(action)` | 硬编码 switch，词表约 18 个（capture / record / dock / workspace:* / settings …） | 内置动作执行器，契约里的 `dispatch` 直接复用 |
| `Ruleset` / `AutomationRules` / `AutomationEngine` | 条件（课堂状态/全屏/小时/星期）→ 动作 id，边沿触发 | 扩展动作的触发条件直接复用同一套条件模型 |
| `PanelRegistry` / `PanelCatalog` | 数据化面板定义 + 工厂 | 新增「扩展动作」面板的注册入口 |
| `ExtensionRegistry` / `ExtensionHost` | 清单登记、API 版本校验、失败记录，无消费方（已接到设置页） | 扩展清单与加载失败的统一上报处 |
| `ConfigService` 三层配置 + policy 段 | 白名单写入、策略锁定 | 扩展源目录与"是否允许外部动作"的策略开关 |
| `ConfigJsonContext` | System.Text.Json 源生成 | 动作清单解析（AOT/Trim 友好） |

结论：**不需要新架构**，只需要在 Core 增加"动作清单模型 + 目录 + 校验"，在 Avalonia 增加"执行器 + 一个面板"。

## 二、非目标（v1 明确不做）

- 不加载第三方 DLL / 不做 `AssemblyLoadContext`（`PublishTrimmed` 下不可控，且引入代码执行信任问题）。
- 不做远程插件商店 / 自动下载。
- 不允许内核驱动、服务安装、管理员提权。
- 不开放任意进程启动与脚本执行（v2 再议，且需单独 ADR）。
- 不允许覆盖内置动作 id（只允许新增）。

## 三、契约 v1

### 3.1 文件位置与加载顺序

```
内置（随程序集）:  Assets/actions/*.json          → AvaloniaResource, 只读
用户（可写）:      %APPDATA%\SmartSideBAR\actions\*.json
策略（管理员）:    %ProgramData%\SmartSideBAR\actions\*.json   → 可禁用/覆盖用户动作
```

- 同名文件：策略层 > 用户层 > 内置层（后者不能覆盖前者的 `enabled=false`）。
- 单文件 ≤ 256 KB，单目录 ≤ 50 个文件；超出即整文件拒绝并记失败原因。
- 文件编码 UTF-8；解析失败只影响该文件，不影响其他扩展与主程序。

### 3.2 清单格式

```json
{
  "schemaVersion": 1,
  "id": "com.example.lesson-tools",
  "name": "备课工具箱",
  "version": "1.0.0",
  "author": "示例作者",
  "minHostVersion": "1.8.0",
  "actions": [
    {
      "id": "lesson.openPlatform",
      "title": "打开智慧教育平台",
      "description": "从侧栏一键进入国家中小学智慧教育平台",
      "icon": "builtin:link",
      "group": "teaching",
      "order": 10,
      "rail": false,
      "command": { "type": "open-url", "url": "https://www.zxx.edu.cn" },
      "conditions": [
        { "kind": "LessonState", "mode": "Equals", "value": "OnClass" }
      ],
      "allMustMatch": true
    },
    {
      "id": "lesson.classWorkspace",
      "title": "切到上课工作区",
      "icon": "builtin:grid",
      "command": { "type": "dispatch", "action": "workspace:上课" }
    }
  ]
}
```

字段规则：

| 字段 | 规则 |
|---|---|
| `schemaVersion` | 必须 = 1；未知版本整文件拒绝 |
| `id` | `^[A-Za-z0-9][A-Za-z0-9._-]{2,63}$`（允许 camelCase，禁止空格/路径符）；全局唯一；保留前缀 `builtin.` / `sys.` 禁止使用 |
| `version` | 语义化版本字符串，仅展示与排障 |
| `minHostVersion` | 高于当前程序版本时整包跳过（记失败原因，不阻断其他扩展） |
| `actions[].id` | 同上；执行时带命名空间前缀，避免与内置动作冲突 |
| `icon` | v1 只允许 `builtin:<图标名>`（来自 `Assets/icons`）；不允许任意路径 |
| `group` | 仅用于「扩展动作」面板内分组标题 |
| `order` | 面板内排序 |
| `rail` | v1 固定忽略（保留字段，v1.1 才允许注入 rail） |
| `command` | 见 §3.3；未知 `type` → 该动作拒绝，不影响同包其他动作 |
| `conditions` | 复用 `RuleConditionKind/Mode` 的字符串枚举；为空 = 无条件下手动触发 |
| `allMustMatch` | 与 `AutomationRuleConfig` 同义 |

### 3.3 命令白名单（v1）

| `type` | 参数 | 行为 | 风险 |
|---|---|---|---|
| `open-url` | `url` | 默认浏览器打开 | 仅允许 `http/https`；禁止 `file:`、`javascript:`、`ms-*`、自定义协议 |
| `open-path` | `path` | 资源管理器打开目录/文件 | 必须展开环境变量后位于用户目录或 `%ProgramData%` 内；拒绝 UNC 与 `..` 穿越 |
| `dispatch` | `action` | 调用现有 `WindowRouter.Dispatch` | 仅允许内置动作白名单（见下） |
| `workspace` | `name` | 切换工作区 | 工作区必须存在于 `workspace.presets` |
| `copy-text` | `text` | 写剪贴板 | 文本 ≤ 4 KB |
| `toast` | `text`, `level` | 轻提示 | level ∈ info/success/warn/error |

`dispatch` 允许的动作子集（其余一律拒绝并记录）：

```
capture / annotate / longshot / record / ime / taskmgr / settings
sidebar-toggle / dock / reminder / usb / printer / miniboard / workspace:*
```

明确排除：`update`、`install*`、任何未来涉及文件替换或提权的动作。

### 3.4 禁止项

- 无法执行的动作：`run-process` / `shell` / `script` / `powershell` / `cmd`（v1 直接拒绝并提示"当前版本不支持"）。
- 不允许清单里出现可执行文件路径、DLL 路径、注册表路径。
- 不允许覆盖 `policy.` 配置、不允许读写 `%APPDATA%` 之外的用户数据。

## 四、代码结构

### 4.1 Core（纯逻辑，可单测）

```
src/SmartSideBAR.Core/Actions/
├── ActionManifest.cs        ActionPackage / ActionDefinition / ActionCommand 记录
├── ActionCatalog.cs         解析、校验、去重、分层合并；产出 Loaded + Failures
├── ActionValidation.cs      纯函数：id/URL/路径/命令类型/大小限制
└── ActionConditionMapper.cs ActionDefinition.Conditions → Rules.RuleGroup（复用现有求值）
```

建议记录（源生成序列化）：

```csharp
public sealed record ActionPackage(
    int SchemaVersion,
    string Id,
    string Name,
    string? Version = null,
    string? Author = null,
    string? MinHostVersion = null,
    IReadOnlyList<ActionDefinition>? Actions = null);

public sealed record ActionDefinition(
    string Id,
    string Title,
    string? Description = null,
    string? Icon = null,
    string? Group = null,
    int Order = 0,
    bool Rail = false,
    ActionCommand? Command = null,
    IReadOnlyList<AutomationConditionConfig>? Conditions = null,
    bool AllMustMatch = true);
```

失败记录复用 `ExtensionLoadFailure`（`Id` = 文件或动作 id，`Reason` = 具体原因），统一进入 `ExtensionRegistry.Failures`，设置页「自动化 → 扩展状态」直接可见。

### 4.2 Avalonia

```
src/SmartSideBAR.Avalonia/
├── Actions/ActionHost.cs          启动时扫描三处目录 → ActionCatalog.Load → 注册扩展清单
├── Actions/ActionExecutor.cs      命令白名单分发（open-url/open-path/dispatch/...）
└── Ui/Panels/ExtensionActionsPanel.axaml(.cs)   动作磁贴列表（按 group 分组）
```

- `ExtensionActionsPanel` 通过 `PanelCatalog.Register(PanelKind.ExtensionActions, ...)` 注册；
  只有存在有效外部动作时才在 rail 显示（避免空面板）。
- 动作磁贴执行前做二次校验（防目录在运行期被替换）。
- 条件类动作接入 `WindowManager.EvaluateAutomation()` 的同一节拍，不另起计时器。

### 4.3 执行流程

```
启动 → 扫描 actions/*.json → 校验 → ActionCatalog
                                   │
                              ExtensionRegistry（清单/失败）
                                   │
rail「扩展动作」→ 磁贴点击 ──► ActionExecutor ──► open-url / open-path
                                   │                 dispatch → WindowRouter
                                   └─ 条件动作：课堂状态/全屏变化 → AutomationEngine → ActionExecutor
```

## 五、安全模型

1. **声明式白名单**：没有代码执行面，最坏情况是"打开一个被允许的 URL/目录"。
2. **来源可见**：设置页扩展状态区展示每个动作包的文件路径、版本、有效动作数。
3. **策略可关**：新增配置键 `policy.allowExternalActions`（默认 `true`），
   管理员置 `false` 时只加载内置动作；用户层不可覆盖（走现有 `policy.` 锁定）。
4. **文件系统边界**：`open-path` 解析后必须落在用户目录或 `%ProgramData%`，拒绝 UNC/相对路径/符号链接逃逸。
5. **协议边界**：只允许 `http/https`，禁止 `file:`、`javascript:`、`ms-settings:`、`shell:`。
6. **不可提权**：所有动作以当前用户权限执行；不触发 UAC。
7. **体积与数量上限**：单文件 256 KB / 50 文件，防解析 DoS。
8. **失败隔离**：单包/单动作失败只记录，不影响主程序与其他扩展（与 Go 插件的"坏插件不拖垮主进程"同思路）。

## 六、版本与兼容

- `schemaVersion` 只增不改；新增字段必须可缺省，未知字段忽略。
- `minHostVersion` 用现有 `VersionComparer` 比较；不满足时整包跳过并在设置页提示"需要 vX.Y.Z"。
- 宿主 API 版本继续由 `ExtensionRegistry.CurrentApiVersion` 管理；
  动作契约与 API 版本解耦（前者是数据格式，后者是注册接口）。
- v1 契约冻结后，v2 若要加 `run-process`，必须满足：显式用户开关 + 参数数组（不经 shell）+ 可执行文件签名校验 + 单独 ADR。

## 七、官方示例（随包内置，作为文档与回归样本）

1. `teaching-platform.json`：`open-url` 打开国家中小学智慧教育平台。
2. `class-workspace.json`：`dispatch` 切「上课」工作区 + 条件 `LessonState=OnClass`。
3. `usb-reminder-quiet.json`：条件 `LessonState=OnClass` → `toast`（演示"上课时静默提示"的替代写法）。

每个示例都在 README 的功能表之外单独说明"这是 JSON 扩展示例，不是内置功能"。

## 八、测试计划

| 类别 | 用例 |
|---|---|
| 解析 | 合法包、缺字段、未知 schemaVersion、超大文件、非法 JSON |
| 校验 | id 正则/重复、保留前缀、未知 command.type、非 http(s) URL、路径穿越、UNC、`minHostVersion` 过高 |
| 合并 | 用户包覆盖内置同名文件、策略层禁用、内置动作 id 冲突拒绝 |
| 执行器 | 每种命令的白名单分支；`dispatch` 不在子集内被拒 |
| 失败上报 | 失败进入 `ExtensionRegistry.Failures`，设置页可读 |
| 条件 | 与 `Ruleset` 同一组用例复用（课堂状态/全屏/时段） |

全部为 Core/执行器纯逻辑测试，不需要真实网络与桌面。

## 九、分阶段交付

| 阶段 | 内容 | 验收 |
|---|---|---|
| E1 | Core 模型 + 解析/校验 + 合并 + 单测 | `ActionCatalog` 覆盖合法/非法/合并三类用例 |
| E2 | ActionHost 扫描 + ActionExecutor + 策略开关 | 手工放置一个 JSON 即可执行 `open-url` / `dispatch` |
| E3 | 「扩展动作」面板 + 设置页扩展状态接线 | 磁贴按 group 展示；失败原因可见 |
| E4 | 官方示例 + 文档（契约说明、如何写第一个动作）+ rail 注入（v1.1） | 第三方仅凭文档可写出并跑通一个动作包 |

工作量级：E1 S / E2 M / E3 M / E4 S；E1–E2 不依赖 MVVM 重构，可并行。

## 十、风险与待决策

| 项 | 说明 | 建议 |
|---|---|---|
| rail 注入 | 外部动作直接进 rail 会破坏"精选模块"的视觉纪律 | v1 只进「扩展动作」面板，v1.1 再评估 |
| 图标资源 | 任意路径有读取风险 | v1 只允许内置图标 id；v2 允许包内相对路径 + 大小/类型校验 |
| `open-path` 边界 | 教师可能需要打开 D 盘课件目录 | 先只允许用户目录 + ProgramData；实际反馈再放宽（每次放宽都补测试） |
| 动作包签名 | 无签名时无法证明作者 | 声明式动作风险低，v1 不做签名；`run-process` 之前必须补 |
| 与内置动作重名 | 会让人误以为是官方功能 | 保留 `builtin.` 前缀并在 UI 标注"来自扩展" |
