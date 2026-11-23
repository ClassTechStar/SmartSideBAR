# ADR-M6: 输入法切换直调 Win32，不起 PowerShell 守护进程

- **状态**: 已定
- **日期**: 2026-10
- **关联**: `src/SmartSideBAR.Windows/Input/ImeService.cs`

## 背景

v1.2 用常驻 PowerShell 读/切输入法布局：启动慢（<50ms 级延迟）、常驻进程额外占内存，且在一体机上偶发被安全软件拦。

## 决定

改用 `GetKeyboardLayout` / `LoadKeyboardLayout` / `PostMessage(WM_INPUTLANGCHANGEREQUEST)` 直调：

1. 延迟从 <50ms 降到 <5ms。
2. 无子进程、无脚本宿主，行为可预期。
3. 角标文案抽到 Core 纯函数 `ImeBadge.Format`：
   - 中文按子语言显示 `zh-CN / zh-TW / zh-HK / zh-SG / zh-MO`（sublang 1=TW、2=CN、3=HK、4=SG、5=MO）。
   - 其他语言显示三字母 ISO 码（JPN/KOR/ENG…），不再输出裸十六进制片段。

## 代价

- 依赖目标窗口响应 `WM_INPUTLANGCHANGEREQUEST`；个别自绘输入框可能不接收，失败时保留当前布局并记录日志。

## 变更记录

| 日期 | 变更 |
|------|------|
| 2026-10 | 初版：Win32 直调替代 PowerShell |
| 2026-11 | 角标改 ISO 码；修正 zh-CN/zh-TW 子语言映射反转 |
