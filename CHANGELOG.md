# 更新日志

本项目版本号写在 `Directory.Build.props` 的 `<Version>`。
格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/)，版本号遵循语义化版本。

## [1.6.0] - 2026-10

竞赛版。

### 新增
- 离线墨迹转文字、智能课堂、课堂统计、演示模式
- 面板注册表（`PanelCatalog`）与面板内容注册表（`PanelRegistry`）
- 扇形菜单角度判定（死区 + 迟滞）、拖拽采样合并、触屏滚动、分页转场
- 亮度/音量快捷调节（能力探测，设备不支持时自动隐藏）
- 应用内更新检查（GitHub Release，安装包名与大小校验）
- 配置导入导出（导出脱敏）、历史默认值迁移、跨权限单实例识别
- 时间状态机、内存看门狗、崩溃可见化、通知优先级队列
- 配置键级订阅与锁定键（策略段 `policy.` 不可被用户写）

### 修复
- CI 覆盖率门禁空转：测试项目补 `coverlet.collector`，门禁增加显式校验
- 高频移动事件导致的拖拽抖动（采样合并 + 边界一次性 DPI 换算）
- 全屏误收缩：支持 `display.excludedFullscreenWindows` 排除名单

## [1.5.0] - 2026-09

- 侧栏 + 悬浮球 + 扇形菜单主形态定稿
- 三层配置（默认 → ProgramData 策略 → APPDATA 用户）
- 设计令牌单一来源（`design-tokens.json`）
- headless 冒烟测试与像素基线断言
- Inno 安装包与 winget 清单

## [1.0.0 - 1.2.0] - 2026 早期

Electron 栈初版与过渡版本，源码存于 `archive/electron-final` 分支；
仓库根下 `SmartSideBAR-1.0.0/1.1/1.2.0` 三个目录是当时的界面快照，仅作对照，不参与构建。
