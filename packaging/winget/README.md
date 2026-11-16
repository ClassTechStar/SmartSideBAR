# 用 winget 分发

这个目录是 [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) 的 manifest 模板。
装 winget 之后学校电教管理员一行命令就能装机，老师不用管 GitHub 是什么。

## 日常发版（自动）

1. CI 的 release job 在打 v* tag 时构建安装包并生成 `SHA256SUMS.txt`，随 Release 草稿一起挂出。
2. 维护者把 Release 草稿转为正式发布后，`.github/workflows/winget.yml` 自动向
   microsoft/winget-pkgs 发出含真实 SHA256 的提交 PR（`WINGET_TOKEN` 仓库密钥需预先配置）。
3. 合入 winget-pkgs 后即可 `winget install ClassTechStar.SmartSideBAR`。

## 人工兜底（机器人不可用时）

1. 确认 GitHub Release 上已经挂了新版安装包与 `SHA256SUMS.txt`。
2. 把本目录三份 yaml 复制到 winget-pkgs 仓库的
   `manifests/c/ClassTechStar/SmartSideBAR/<版本号>/`，更新
   `PackageVersion` / `InstallerUrl` / `InstallerSha256`（以 `SHA256SUMS.txt` 为准）。
3. 本地校验：`winget validate .`。
4. 往 microsoft/winget-pkgs 发 PR（标题 `New version: ClassTechStar.SmartSideBAR version <ver>`）。

## 本地试装

```powershell
winget install --manifest .   # 用本地 manifest 真装一遍
```
