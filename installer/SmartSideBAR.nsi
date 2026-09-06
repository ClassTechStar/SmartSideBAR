; SmartSideBAR NSIS 安装脚本 —— Wave C 发布 (方案 §7.3 release job 配套)
; 本机无 makensis 时, 使用 dist/ 下的免工具绿色安装包 (install.cmd / uninstall.cmd);
; CI (windows-latest + choco install nsis) 直接执行本脚本产出标准安装器。
; 构建: makensis installer/SmartSideBAR.nsi  (需先执行 dotnet publish 到 artifacts/app)
; 签名: CI 在打包前对 exe 执行 signtool (证书从 secrets 注入, 绝不入库 §7.4)

!define APP_NAME "SmartSideBAR"
!define APP_VERSION "2.0.0"
!define APP_PUBLISHER "Seewo Sidekick Team"
!define APP_EXE "SmartSideBAR.exe"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "..\dist\SmartSideBAR-安装包-${APP_VERSION}.exe"
InstallDir "$LOCALAPPDATA\${APP_NAME}"
RequestExecutionLevel user       ; per-user 安装, 无需管理员 (P1-10 语义)
SetCompressor /SOLID lzma
Unicode true

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=2052 "ProductName" "${APP_NAME}"
VIAddVersionKey /LANG=2052 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=2052 "LegalCopyright" "Apache-2.0"

Page directory
Page instfiles
UninstPage uninstConfirm
UninstPage instfiles

Section "安装"
  SetOutPath "$INSTDIR"
  File /oname=${APP_EXE} "..\artifacts\app\SmartSideBAR.Avalonia.exe"
  File "..\artifacts\app\*.dll"

  ; 可选信任脚本 (自签名链, 默认不执行 —— 最小信任面 §7.4)
  File "..\cert\install-trust.cmd"
  File "..\cert\uninstall-trust.cmd"
  File "..\cert\SmartSideBAR-root.cer"

  ; 开始菜单快捷方式
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\卸载.lnk" "$INSTDIR\Uninstall.exe"

  ; 自启动 (HKCU, asInvoker)
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${APP_NAME}" '"$INSTDIR\${APP_EXE}" /silent'

  ; 卸载器
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayName" "${APP_NAME} ${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher" "${APP_PUBLISHER}"
SectionEnd

Section "Uninstall"
  ; 先退应用 (AppBar 正确注销, WorkArea 恢复)
  ExecWait 'taskkill /IM ${APP_EXE} /F'
  Delete "$INSTDIR\${APP_EXE}"
  Delete "$INSTDIR\*.dll"
  Delete "$INSTDIR\Uninstall.exe"
  Delete "$INSTDIR\install-trust.cmd"
  Delete "$INSTDIR\uninstall-trust.cmd"
  Delete "$INSTDIR\SmartSideBAR-root.cer"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\${APP_NAME}\*.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${APP_NAME}"
SectionEnd
