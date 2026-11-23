# MVVM 重构基线（M0 护栏）

重构期间的判断标准：**行为不变**。任何阶段合并前，下面三组证据必须同时成立。

## 一、命令

```powershell
dotnet build SmartSideBAR.slnx -c Release        # 0 警告
dotnet test  SmartSideBAR.slnx -c Release        # 全绿
dotnet format SmartSideBAR.slnx --verify-no-changes
```

## 二、7 条 headless 冒烟链路（`SmokeTests.cs`）

| # | 链路 | 覆盖点 |
|---|---|---|
| 1 | Rail 点击 → 面板打开 | 工作区/面板状态机、按钮命中 |
| 2 | 批注画圆 → 转形 → 工具条两级 | InkCanvas 转形、工具条 |
| 3 | 工作区切换 → rail 重建裁剪 | 工作区预设与策略过滤 |
| 4 | 提醒面板：种子提醒渲染 + 铃声入口 | 提醒面板、通知数据 |
| 5 | Toast → 通知中心留档/清除 | 通知中心生命周期 |
| 6 | 链接：新增 → 卡片网格 + 分类页签 | LinksService/面板刷新 |
| 7 | 录屏态：录制中 → 面板计时卡 | 录屏面板分支 |

## 三、3 组像素结构基线（`SmokeTests.cs`）

不做 golden 哈希（CI/字体/GPU 差异必抖），断言结构不变量：

| 基线 | 断言 |
|---|---|
| Rail 胶囊 | 72px 宽；中央列 >50% 不透明且平均亮度 >200（经典白面） |
| 面板白底 | 452px 宽；x=100/200/300/420 四列 >40% 不透明亮白像素 |
| 主题对比翻转 | 浅色胶囊平均亮度 >180；深色显著更暗 |

## 四、行为矩阵（重构时逐项勾验）

- 侧栏：三种触发形态、dock/undock、全屏自动收起、工作区切换。
- 设置：9 个分页可打开；保存后逐键落盘（`ConfigApplyManyTests`）；主题/强调色即时生效。
- 主题：经典/玻璃/深色/高对比四种组合下，rail 与面板无白块、文字可读（`SettingsThemeTests`）。
- 面板：8 个面板在启用/策略禁用两种状态下行为一致（`PanelCatalogTests`）。
- 窗口：AppBar 注册失败回退 Topmost（`AppBarServiceTests`）。

## 五、M1 的已知取舍

`SidebarWindow.ApplyTheme()` 目前在主题切换时会**重建当前打开的面板**（面板是 C# 手搓、颜色在构造时固化）。
M1 只把窗口 chrome 与 axaml 页面改成 `DynamicResource`；**重建逻辑保留到 M3**（面板全部迁移后删除），
以免 M1 阶段出现"切换主题后面板还是旧配色"的回归。
