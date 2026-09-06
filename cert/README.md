# SmartSideBAR 代码签名证书材料

本目录只保留**可公开分发**的材料（公钥 `.cer` 与信任部署/清理脚本）。
私钥与口令**绝不入库**（.gitignore 已按 G1 治理精确排除）：

| 文件 | 性质 | 是否入库 |
|---|---|---|
| `SmartSideBAR-root.cer` | 根证书（公钥） | ✅ 入库 |
| `install-trust.cmd` | 导入信任脚本 | ✅ 入库 |
| `uninstall-trust.cmd` | 移除信任脚本（下个安装包默认附带） | ✅ 入库 |
| `SmartSideBAR-codesign.pfx` | **私钥** | ❌ 禁止 |
| `pfx-password.txt` | **明文口令** | ❌ 禁止 |
| `thumbprint.txt` | 私钥指纹记录 | ❌ 禁止（本地备忘） |

## 当前根证书信息

- 主题：`CN=Seewo Sidekick Team`
- SHA-1 指纹：`F5C074ED87AC9EE6180501BCD7D87E75C1544771`

## 2026-09-06 审计结论（G1 修正）

《优化方案_Master_MLP_v1.md》曾判定 pfx/口令「被 git 跟踪」，经核实
（`git log --all -- cert/` 无任何历史），**私钥从未提交进 git**，仅存在于本地
磁盘。因此无需历史重写（附录 C.2 的 3a 分支），本轮处置为：

1. `.gitignore` 由整目录忽略改为**按文件类型精确忽略**，公钥材料可入库审计；
2. 新增 `uninstall-trust.cmd`，后续安装包默认附带、卸载时移除旧信任根（最小信任面）；
3. 若未来发现私钥确有外泄迹象，按方案附录 C.1 吊销重签（自签名链的"吊销"
   实际动作 = uninstall-trust 移除旧根 + 换发新根）。

## 轮换密钥的标准流程（附录 C.1 摘要）

```powershell
# 1) 生成新代码签名密钥（私钥不可导出，直接存本机证书存储区）
$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=Seewo Sidekick Team" `
    -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(3)
# 2) 导出根公钥入库（覆盖 SmartSideBAR-root.cer 后提交）
Export-Certificate -Cert $cert -FilePath .\SmartSideBAR-root.cer
# 3) 若需 pfx 备份：导出到 TEMP，口令进密码管理器，**不得放回本目录**
Export-PfxCertificate -Cert $cert -FilePath $env:TEMP\cs.pfx -Password (Read-Host -AsSecureString)
# 4) signtool 本地签名用证书存储区指纹：signtool sign /sha1 <新指纹> ...
```
