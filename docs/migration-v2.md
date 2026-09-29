# SmartSideBAR v2.0 迁移公告

> 发布日期: 2026-09-30
> 适用范围: 从 Electron v1.x 升级到 Avalonia v2.0 的用户

## 你为什么要升级?

SmartSideBAR v2.0 将底层架构从 Electron 迁移到 .NET Avalonia，带来:

- **安装包从 ~100MB 降到 ~15MB** —— 更快下载, 更少磁盘占用
- **空闲内存从 ~220MB 降到 ~80MB** —— 4GB 教学一体机更流畅
- **冷启动从 ~3.5s 降到 ~1.2s** —— 开机自启后秒开
- **全屏白板演示不被遮挡** —— AppBar 自动隐藏 (B4)

## 配置会丢失吗?

**不会。** v2.0 自动识别并迁移 v1.x 的配置:

| 配置项 | 位置 | 迁移 |
|--------|------|------|
| 用户配置 | `%APPDATA%\SmartSideBAR\config.json` | 自动互认, 无需操作 |
| 旧目录配置 | `%APPDATA%\SeewoSidekick\config.json` | 首次启动自动迁移 |
| 策略层配置 | `%PROGRAMDATA%\SeewoSidekick\config.json` | 自动读取 |
| 自定义链接/提醒/热键 | 同上 | 全部保留 |

## 升级步骤

1. **卸载旧版** (推荐, 但非必须):
   - 设置 → 应用 → SmartSideBAR → 卸载
   - 或控制面板 → 程序和功能
2. **下载新版**: 从 [Releases](https://github.com/ClassTechStar/SmartSideBAR/releases) 下载 `SmartSideBAR-v2.0.0-Setup.exe`
3. **安装**: 双击运行, 按向导操作
4. **验证**: 首次启动后, 检查侧边栏、自定义链接、提醒是否正常

> 如需保留配置, 卸载时选择「不删除用户配置」。

## 有 breaking change 吗?

| 变更 | 影响 |
|------|------|
| 安装路径 | `%LOCALAPPDATA%\Programs\smartsidebar` (与 v1.x 相同) |
| 注册表检测键 | `HKCU/HKLM\SOFTWARE\SeewoSidekick` (不变) |
| 配置文件格式 | JSON schema v2, 兼容 v1 字段 |
| 快捷键配置 | 字段名不变, 自动迁移 |
| Electron 专属功能 | 极少数 UI 差异, 核心功能完整保留 |

## 遇到问题?

1. 打开侧边栏 → 设置 → 诊断 → 导出诊断包
2. 提交 [Issue](https://github.com/ClassTechStar/SmartSideBAR/issues) 并附上诊断包
