@echo off
rem ============================================================
rem  SmartSideBAR 自签名代码签名证书 - 信任移除脚本
rem  在每台电脑上以【管理员身份】运行一次。
rem  作用: 从本机证书存储区移除 SmartSideBAR 根证书
rem    (CN=Seewo Sidekick Team, SHA-1 F5C074ED87AC9EE6180501BCD7D87E75C1544771)
rem  适用场景:
rem    1) 卸载 SmartSideBAR 时恢复最小信任面 (install-trust.cmd 的逆操作)
rem    2) 证书轮换后清理旧信任根 (方案附录 C.1 / G1 治理)
rem ============================================================
net session >nul 2>&1
if %errorlevel% neq 0 (
  echo [错误] 请以管理员身份运行本脚本: 右键 =^> 以管理员身份运行
  pause
  exit /b 1
)
certutil -delstore Root "F5C074ED87AC9EE6180501BCD7D87E75C1544771"
if %errorlevel% neq 0 (echo [提示] 根存储中未找到该证书或移除失败) else (echo [完成] 已从"受信任的根证书颁发机构"移除)
certutil -delstore TrustedPublisher "F5C074ED87AC9EE6180501BCD7D87E75C1544771"
if %errorlevel% neq 0 (echo [提示] 受信任发布者中未找到该证书或移除失败) else (echo [完成] 已从"受信任的发布者"移除)
echo.
echo [完成] SmartSideBAR 证书已从本机信任库移除 (全部用户生效)。
pause
