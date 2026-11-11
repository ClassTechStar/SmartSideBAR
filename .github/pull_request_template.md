<!--
感谢你的贡献！请按需填写下面的栏目。
感谢你愿意花时间让课堂工具更可靠。
-->

## 变更内容 / What changed

<!-- 做了什么、为什么这样做。一句话说清目的，再列要点。 -->

## 关联 Issue / Related issue

<!-- 例如：Closes #123；没有可写「无」 -->

## 验证方式 / How it was verified

<!-- 运行了哪些 build / test / format；UI 或平台相关改动请说明手动验证步骤与设备环境 -->

## 兼容性与迁移影响 / Compatibility and migration

<!-- 涉及配置字段、数据格式、行为变化时说明；没有可写「无」 -->

## 检查清单 / Checklist

- [ ] `dotnet build` 0 错误 0 警告
- [ ] `dotnet test` 全过
- [ ] `dotnet format --verify-no-changes` 通过
- [ ] 新配置字段已同步 `SidekickConfig` 与 `ConfigJsonContext`
- [ ] 涉及威胁面的改动已更新 `THREAT_MODEL.md`
- [ ] UI 改动未破坏像素基线（或已说明理由）
