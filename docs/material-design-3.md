# Material Design 3 设计规范（本项目）

> 适用范围：**所有新建页面与组件**。既有面板（`PanelKit`）保持 v1.2 视觉基线不变，
> 通过本文末尾的映射表逐步对齐，不做破坏性重排。
>
> 规格来源：Material Design 3 Expressive + `mochi-compose`（Android Jetpack Compose 库，
> 定位 "MD3 colors and type, iOS-style interaction and motion"）。
> **只移植设计规格数值，不复用其 Kotlin 代码**——本项目是 Avalonia/C#，技术栈不同，
> 组件实现按 Avalonia 的控件模型重写。

## 1. 单一真源

| 层级 | 位置 | 说明 |
|---|---|---|
| 规格数据 | `design-tokens.json` → `m3` 段 | 唯一可改的地方 |
| C# 镜像 | `Core/Design/DesignTokens.cs` → `M3Tokens` | 默认值必须与 JSON 一致（有测试双向校验） |
| Avalonia 适配 | `Avalonia/Ui/M3.cs` | 令牌 → `CornerRadius` / `BoxShadows` / `TimeSpan` / `IEasing` / `FontWeight` |
| 组件工厂 | `M3.Card` / `M3.Button` / `M3.TextField` / `M3.Chip` / `M3.ListRow` | 新建页面优先用这些 |

**禁止**：在页面/面板里直接写圆角、阴影、动画时长、字号字面量。要新值 → 改 JSON。

## 2. 形状系统（Shape）

MD3 Expressive 九档，对齐 `mochi-compose` 的 `MochiShapes`（原库为 G2 连续曲率，
Avalonia 无等价实现，采用标准圆角，曲率差异不改变可读性与层级关系）：

| 令牌 | 值 (dip) | 用途 |
|---|---|---|
| `extraSmall` | 4 | 勾选框、小徽标、密集控件 |
| `small` | 8 | 输入框、Chip、列表内小件 |
| `medium` | 12 | **卡片、面板内区块**（默认） |
| `large` | 16 | 对话框、抽屉 |
| `largeIncreased` | 20 | 大卡片、分组容器 |
| `extraLarge` | 28 | 底部面板、全屏面板 |
| `extraLargeIncreased` | 32 | 大模态 |
| `extraExtraLarge` | 48 | 特殊容器（少用） |
| `full` | 999 | 胶囊：按钮、Badge、开关轨道 |

代码：`M3.CornerMedium`、`M3.CornerFull` …

## 3. 色彩系统（Color）

seed = 品牌色 `#2B6EE0`，标准 MD3 tonal 映射，**light / dark 两套都必须可用**。

| 角色 | 用途 | 禁止 |
|---|---|---|
| `primary` / `onPrimary` | 主操作、选中态 | 不要用 primary 做大面积背景 |
| `primaryContainer` / `onPrimaryContainer` | 主色弱化容器 | — |
| `secondary` / `tertiary` | 次要操作、辅助强调 | 不要与 primary 混用表达同一层级 |
| `error` / `errorContainer` | 错误、危险操作 | 不要用红色表达"重要" |
| `surface` / `onSurface` | 页面底色与正文 | — |
| `surfaceContainerLowest…Highest` | **分层容器阶梯**（卡片/浮层/模态） | 跨两级以上使用会丢失层级 |
| `outline` / `outlineVariant` | 边框、分隔线 | 不要用纯黑做描边 |
| `inverseSurface` / `inverseOnSurface` | Snackbar、Tooltip | — |

取色：`M3.Role(M3ColorRole.Primary, dark: true)`。

**语义约束**：同屏最多 1 个 primary 强调；错误色只用于错误；不用颜色单独传达状态（须配文字/图标）。

### 3.1 深色主题

**明暗全局统一（2026-11-22 起）**：侧栏、面板、浮动球、设置页、独立窗口
的浅色与深色**全部走 M3 不透明 surface 阶梯**，明暗对称、与深色同一套语义角色。

- `AppTheme.From` 与 `AppTheme.M3Palette` **明暗都同源**：内容色逐项相等，
  由 `SettingsThemeTests` 锁定（浅色 11 字段 + 深色 11 字段）。
- 例外：rail 胶囊的渐变与边光环是**侧栏形态语言**（玻璃浅色 / 纯白经典面），
  保留 v1.2 参数。
- 用户的 `appearance.liquidGlass` 开关仍控制 rail 胶囊质感。
- 浅色层级方向与深色相反：**页面底 surface 最亮（近白），卡片 surfaceContainer 稍暗**；
  深色则卡片比页面底亮。像素验证：surface (254,251,255) / surfaceContainer (241,236,244)。

**深色全局统一（2026-11-22 起）**：侧栏、面板、浮动球、设置页、独立窗口
（崩溃提示 / OOBE）的深色**全部走 M3 不透明 surface 阶梯**，
不再有"侧栏玻璃面 vs 设置页面"的双轨深色。

- `AppTheme.From(dark)` 与 `AppTheme.M3Palette(dark)` **同源**：内容色逐项相等，
  由 `SettingsThemeTests.侧栏深色与设置页深色同源` 锁定。
- 例外：rail 胶囊的渐变与边光环是**侧栏形态语言**（不是配色语义），
  保留 v1.2 参数，由 `侧栏保留rail胶囊形态` 锁定。
- 用户的 `appearance.liquidGlass` 开关仍控制 rail 胶囊质感。


- 两套色板都必须在设计规范页可见（`设置 → 设计规范 → 语义色彩` 分「浅色 / 深色」两组陈列）。
- 深色不是浅色反相：`primary` 取同色系**亮版**（`#B0C6FF`），上的前景必须用 `onPrimary`（`#002E6A`）——
  深色下用白字会对比不足，这是 `M3.Button` 的 Filled 变体已内置的规则。
- 容器层级用 `surfaceContainerLowest…Highest` 阶梯，不要用透明度叠出来（叠层在深色下会发灰）。
- 描边用 `outline` / `outlineVariant`，不要用纯白低透明度描边。
- `Palette` 的深色派生见 `AppTheme.M3Palette(dark, accent)`：全部取自 M3 深色语义角色，无硬编码。
- **所有窗口都要同时换 Palette 与 `RequestedThemeVariant`**：Fluent 控件模板（ComboBox/CheckBox/Button）
  按主题变体取色，只换 Palette 不换变体会在深色页面留下白底控件。
- 独立窗口（崩溃提示 / OOBE / 更新进度）用 `AppTheme.CurrentM3()`：按应用当前主题变体取 M3 深/浅语义色，
  与设置页同源；构造时同样要设 `RequestedThemeVariant`。
- 例外：`MiniBoardWindow`（白板）保持白底黑字，属功能语义，不做深色。


## 4. 排版系统（Typography）

MD3 15 档 type scale（单位 dip；字体族由 `MiSansFont.axaml` 全局提供）：

| 档位 | 字号 | 行高 | 字重 | 字距 | 用途 |
|---|---|---|---|---|---|
| displayLarge/Medium/Small | 57/45/36 | 64/52/44 | 400 | -0.25/0/0 | 大屏展示（侧栏基本不用） |
| headlineLarge/Medium/Small | 32/28/24 | 40/36/32 | 400 | 0 | 页面主标题 |
| titleLarge/Medium/Small | 22/16/14 | 28/24/20 | 400/500/500 | 0/.15/.1 | 区块标题、对话框标题 |
| bodyLarge/Medium/Small | 16/14/12 | 24/20/16 | 400 | .5/.25/.4 | 正文 |
| labelLarge/Medium/Small | 14/12/11 | 20/16/16 | 500 | .1/.5/.5 | 按钮、Chip、辅助标签 |

代码：`M3.Type(textBlock, M3TypeScale.TitleMedium)`。

**行高必须一起设**：只设字号会让中文行距塌陷。

## 5. 动效系统（Motion）

对齐 `MochiMotionTokens`，**用物理弹簧的真实解析解**驱动，不是缓动近似曲线。

### 5.1 物理模型

对阶跃输入求解二阶阻尼振子：

```
x'' + 2ζω₀x' + ω₀²x = 0 ,  x(0)=0, x'(0)=0
ω₀ = 2π / response ,  ζ = damping
```

三支解析解（`Core/Design/SpringCurve.cs`）：

| 区间 | 解 | 行为 |
|---|---|---|
| ζ < 1（欠阻尼） | `1 − e^(−ζω₀t)(cos ω_d t + (ζω₀/ω_d) sin ω_d t)`，`ω_d = ω₀√(1−ζ²)` | 真实过冲与回弹 |
| ζ = 1（临界） | `1 − e^(−ω₀t)(1 + ω₀t)` | 平稳落定，不过冲 |
| ζ > 1（过阻尼） | `1 − (r₂e^(r₁t) − r₁e^(r₂t))/(r₂−r₁)` | 慢根收敛，拖尾更长 |

**过冲量**为解析值 `e^(−ζπ/√(1−ζ²))`，测试逐档核对。
**动画时长**取包络衰减到 0.1% 的时间：`ln(1000)/(decay·ω₀)`，
其中过阻尼用慢根 `decay = ζ − √(ζ²−1)`（直接用 ζω₀ 会低估收敛时间）。

### 5.2 档位表

| 档位 | response | damping | 理论过冲 | 用途 |
|---|---|---|---|---|
| `page` | 0.42s | 1.00 | 0% | 页面切换、侧推 |
| `present` | 0.42s | 0.86 | ≈1% | 浮层升起、Toast |
| `pop` | 0.36s | 0.78 | ≈2% | 对话框、气泡 |
| `control` | 0.34s | 0.86 | ≈1% | 控件位移、拖动吸附 |
| `settle` | 0.28s | 1.00 | 0% | 收起、高度变化、箭头 |
| `bounce` | 0.34s | 0.55 | ≈13% | 角标冒出、选中框 |
| `pressDown` | 0.22s | 0.62 | ≈8% | 按下 |
| `pressRelease` | 0.38s | 0.62 | ≈8% | 松开回弹 |
| `fade` | — | — | — | 淡入 150ms（tween） |
| `fadeOut` | — | — | — | 淡出 120ms（tween） |

代码：

```csharp
var (duration, easing) = M3.Spring(M3.Settle);   // 任意档位
transitions.Add(new DoubleTransition
{
    Property = OpacityProperty,
    Duration = M3.FadeTime,                       // 透明度走 tween
    Easing = M3.FadeEase,
});
// 位移/尺寸用 M3.SettleTime + M3.SettleEase
```

`design-tokens.json` 里的 `ms` / `easing` 保留为**降级参考值**（Avalonia 之外的宿主或调试对照），
运行时以 `responseSec` / `damping` 计算弹簧为准。

**编排规则（来自 mochi）**：同一次变化里的多个元素用**同一档**，才会同时起步、同时停；
透明度单独走 fade，不与位移共用时长。系统开启"减少动效"时必须降级为直接切换。

## 6. 间距与触控

| 令牌 | 值 (dip) | 用途 |
|---|---|---|
| `spacing.xs` | 4 | 图标与文本 |
| `spacing.s` | 8 | 紧凑元素间 |
| `spacing.m` | 12 | 卡片内边距（小） |
| `spacing.l` | 16 | **卡片内边距（默认）**、列表左右 |
| `spacing.xl` | 24 | 区块之间 |
| `spacing.xxl` | 32 | 页面级分节 |

**触控目标**：MD3 默认 48×48；mochi 硬下限 44。本项目取
`M3.MinTargetDip = 48`（默认控件高）、`M3.MinTouchDip = 44`（保底）。
任何可点元素低于 44 → 用 `M3.EnsureTouchTarget()` 兜住。

## 7. 组件规格

| 组件 | 规格 | 工厂 |
|---|---|---|
| 按钮 Filled/Tonal/Outlined/Text | 胶囊（full）、高 48、水平 24 / 垂直 12 | `M3.Button(...)` |
| 卡片 | medium 圆角、elevation 1、内边距 16、1px outlineVariant 描边 | `M3.Card(...)` |
| 输入框 | small 圆角、高 48、内边距 16/12、outline 边框 | `M3.TextField(...)` |
| Chip | small 圆角、高 32、内边距 12/6、选中用 primaryContainer + primary 边框 | `M3.Chip(...)` |
| 列表行 | 单行 ≥48 / 双行 ≥56、leading 16 间距、标题 bodyLarge + 副标题 bodySmall | `M3.ListRow(...)` |

交互约定（mochi / MD3 共同要求）：
- 按下有可见反馈（不用纯色变化表达"按下"，要配合缩放或水波）。
- 禁用态保持文字可读（不要整体 30% 透明了事）。
- 图标按钮至少 44×44 点击区，视觉图标可小。
- 不允许悬停态与按下态共用同一视觉。

## 8. 与既有 `PanelKit` 的映射

既有面板不改数值（`DesignTokensTests` 锁定 v1.2 基线），新建组件按右列取值：

| PanelKit 用法 | 现值 | M3 对应 |
|---|---|---|
| `Shape.RadiusXs`（勾选框） | 4 | `extraSmall` 4（一致） |
| `Shape.RadiusS`（按钮/输入） | 6 | `small` 8（略增 2） |
| `Shape.RadiusM`（卡片） | 10 | `medium` 12（略增 2） |
| `Shape.RadiusL`（对话框） | 16 | `large` 16（一致） |
| `Spacing.Xs…Xl` | 4/8/12/16/20 | `xs…xl` 4/8/12/16/24 |
| `Motion.AnimFastMs` 120 | 微反馈 | `fadeOut` 120 |
| `Motion.PanelMs` 180 | 面板展开 | `control` 340（新组件用新档位，旧面板保持） |
| `Shadow.E1/E2/E3` | 自定义三级 | `elevation` 1/2/3 |

## 9. 一致性校验

| 校验 | 位置 | 内容 |
|---|---|---|
| 令牌双向一致 | `DesignTokensTests.M3_*` | JSON ↔ C# 默认值逐项相等 |
| 刻度完整性 | `M3DesignTests` | 形状严格递增、动效十档齐全、排版十五档齐全 |
| 触控下限 | `M3DesignTests` | `MinTouchDip ≥ 44`、`MinTargetDip ≥ 48` |
| 组件规格 | `M3ComponentTests` | 按钮高 48、卡片圆角＝medium、输入框高 48 |
| 弹簧物理 | `SpringCurveTests` | ω₀=2π/response、过冲与解析解一致、临界阻尼不过冲、过阻尼收敛更慢 |
| 深色主题 | `DesignSpecPageTests` | 深色 palette 下可渲染、8+ 按钮达标、明暗两套色板同时在场 |
| 硬编码扫描 | `M3DesignTests.新组件无硬编码` | 扫描 `M3.cs`，禁止出现十六进制颜色与裸圆角数字 |

## 10. 与 mochi-compose 的差异（有意为之）

| mochi | 本项目 | 原因 |
|---|---|---|
| G2 连续曲率圆角 | 标准圆角 | Avalonia 无等价 API；曲率不影响层级与可读性 |
| 物理弹簧 | **同款解析解**（`SpringCurve`），经 `IEasing` 适配进 Avalonia | 无差异；Avalonia 原生无 spring，用自定义 easing 桥接 |
| iOS 式侧滑返回、橡皮筋 | 不适用 | 桌面侧栏无手势导航语义 |
| 玻璃材质（MochiGlass） | 保留本项目既有液态玻璃开关 | 已有成熟实现，不引入第二套材质系统 |
| Ionicons 图标 | 保留现有 SVG 图标集 | 换图标集属破坏性视觉变更 |
