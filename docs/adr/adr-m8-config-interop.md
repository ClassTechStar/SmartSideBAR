# ADR-M8: 配置模型与 Electron 版逐字段互认

- **状态**: 已定
- **日期**: 2026-09
- **关联**: `src/SmartSideBAR.Core/Configuration/SidekickConfig.cs`

## 背景

v1.0/v1.2 是 Electron 栈，用户机器上已有 `config.json`。切换 Avalonia 后如果模型对不上，升级即丢配置。

## 决定

1. `SidekickConfig` 与 `shared/types.ts` 逐字段同构，JSON 命名 camelCase。
2. 枚举用字符串（`JsonStringEnumConverter`），人可读且与 TS 联合类型一致。
3. 持久化以 `JsonNode` 为准，保留未知字段——旧版本写过的键在新版本里不丢。
4. 读取顺序：内置默认 → `%ProgramData%` 策略层 → 用户层；null 值跳过、数组整体替换。
5. 旧目录（`%APPDATA%\smartsidebar`、`%ProgramData%\SeewoSidekick`）首启自动迁移，旧文件改名 `.migrated.bak`。
6. 写盘：白名单路径 + 原子替换（tmp → move）+ `.bak` 回滚；JSON null 视为「恢复默认」。

## 代价

- 新增字段必须同步 `ConfigJsonContext` 源生成声明，否则 `Set<T>` 会抛 `NotSupportedException`。
- 旧目录名带厂商字样（`SeewoSidekick`）是历史兼容事实，不再作为新安装路径。

## 变更记录

| 日期 | 变更 |
|------|------|
| 2026-09 | 初版：字段级互认 + 三层合并 |
| 2026-11 | 批量写入 `ApplyMany`；null 写入回落默认；策略段完成 OOBE 后锁定 |
