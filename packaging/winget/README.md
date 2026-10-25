# winget 分发（Trump 裁决落地：渠道 > 功能）

本目录是 [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) 的 manifest 源。
winget 是零门槛分发通路：学校电教管理员一行命令装机，教师无需知道 GitHub 是什么。

## 每次发版的提交流程

1. 确认 GitHub Release 已挂上新版安装包（CI release job 或本地 `iscc` 后手动上传）。
2. 计算哈希：`sha256sum dist/SmartSideBAR-v<ver>-Setup.exe`。
3. 复制本目录三份 yaml 到 winget-pkgs 仓库的
   `manifests/c/ClassTechStar/SmartSideBAR/<版本号>/`，更新
   `PackageVersion` / `InstallerUrl` / `InstallerSha256`。
4. 用 winget 自带校验：`winget validate .`（本地目录）。
5. PR 到 microsoft/winget-pkgs（标题 `New version: ClassTechStar.SmartSideBAR version <ver>`）。

## 本地校验

```powershell
winget install --manifest .   # 从本地 manifest 走一遍真实安装
```
