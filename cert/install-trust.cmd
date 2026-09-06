@echo off
rem ============================================================
rem  SmartSideBAR 自签名代码签名证书 - 信任部署脚本
rem  在每台目标电脑上以【管理员身份】运行一次即可。
rem  作用: 把 SmartSideBAR-root.cer 导入
rem    1) 本地计算机"受信任的根证书颁发机构"
rem    2) 本地计算机"受信任的发布者"
rem  导入后, 本机所有用户安装/运行已签名的 SmartSideBAR 安装包
rem  都会显示发布者 "Seewo Sidekick Team", 不再弹"未知发布者"。
rem  注意: SmartScreen 信誉与证书信任是两套机制, 新证书初期
rem  仍可能出现蓝色"Windows 已保护你的电脑"提示, 点"更多信息
rem  > 仍要运行"即可; 下载量增加或改用商业证书后消失。
rem ============================================================
net session >nul 2>&1
if %errorlevel% neq 0 (
  echo [错误] 请以管理员身份运行本脚本: 右键 =^> 以管理员身份运行
  pause
  exit /b 1
)
certutil -addstore -f Root "%~dp0SmartSideBAR-root.cer"
if %errorlevel% neq 0 (echo [错误] 导入根存储失败 & pause & exit /b 1)
certutil -addstore -f TrustedPublisher "%~dp0SmartSideBAR-root.cer"
if %errorlevel% neq 0 (echo [错误] 导入受信任发布者失败 & pause & exit /b 1)
echo.
echo [完成] SmartSideBAR 证书已加入本机信任库 (全部用户生效)。
pause
