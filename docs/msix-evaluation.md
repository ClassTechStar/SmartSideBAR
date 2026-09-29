# E5: MSIX 通道评估

> 状态: 评估完成, 建议暂不采用
> 日期: 2026-09-30
> 依赖: E1 (v2.0-beta Release 流程) 完成后

## 背景

MSIX 是 Windows 现代打包格式, 支持:
- 自动更新 (App Installer / Store)
- 沙盒化安装 (无需 UAC)
- 干净卸载
- Microsoft Store 分发

## 评估结论

| 维度 | Inno Setup (当前) | MSIX | 评价 |
|------|-------------------|------|------|
| 安装体验 | 传统安装向导 | 双击即装 | MSIX 更好 |
| 自动更新 | 需自行实现 | 内置 | MSIX 更好 |
| 签名要求 | 自签名即可 | 需受信任证书 | Inno 更灵活 |
| 系统集成 (AppBar) | 完全支持 | 沙盒可能受限 | **Inno 更安全** |
| 热键注册 | 完全支持 | 可能受限 | **Inno 更安全** |
| 开机自启 | 注册表 Run | 任务计划 | 均可 |
| 教学一体机兼容 | 已验证 | 未验证 | **Inno 更可靠** |
| 离线安装 | 完全支持 | 需侧载证书 | Inno 更好 |
| 企业部署 | GPO/SCCM 成熟 | 需 ADK | Inno 更成熟 |

## 关键风险

1. **AppBar 受限**: MSIX 沙盒可能限制 SHAppBarMessage 注册, 影响侧边栏 WorkArea 功能
2. **热键受限**: 全局热键注册 (RegisterHotKey) 在沙盒下可能失败
3. **教学一体机**: 希沃设备 Windows 版本碎片化, MSIX 侧载证书分发困难
4. **无 Store 渠道**: 教育设备通常无 Microsoft Store, MSIX 优势无法发挥

## 建议

**暂不采用 MSIX, 继续使用 Inno Setup。**

理由: 核心功能 (AppBar/热键/录屏) 在 MSIX 沙盒下存在兼容性风险, 而目标设备 (教学一体机) 不具备 MSIX 的分发优势。待以下条件满足后重新评估:

1. Inno 流水线稳定运行 3 个月+
2. 自动更新需求明确
3. 希沃设备 Windows 版本统一到 Win11 22H2+
