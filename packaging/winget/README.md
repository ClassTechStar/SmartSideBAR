# 用 winget 分发

这个目录是 [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) 的 manifest 源。
装 winget 之后学校电教管理员一行命令就能装机，老师不用管 GitHub 是什么。

## 每次发版要做的

1. 确认 GitHub Release 上已经挂了新版安装包（CI 的 release job 会传，或者本地 `iscc` 打完手动传）。
2. 算哈希：`sha256sum dist/SmartSideBAR-v<ver>-Setup.exe`。
3. 把本目录三份 yaml 复制到 winget-pkgs 仓库的
   `manifests/c/ClassTechStar/SmartSideBAR/<版本号>/`，更新
   `PackageVersion` / `InstallerUrl` / `InstallerSha256`。
4. 本地校验：`winget validate .`。
5. 往 microsoft/winget-pkgs 发 PR（标题 `New version: ClassTechStar.SmartSideBAR version <ver>`）。

## 本地试装

```powershell
winget install --manifest .   # 用本地 manifest 真装一遍
```
