; SmartSideBAR / 希沃侧边快捷键工具 1.1.0 安装包脚本
; 打包完整 Electron 运行时 + v1.1 全部更新（液态玻璃 UI / 镜头图标圆球）
Unicode true
!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "LogicLib.nsh"

!define STAGE "C:\Users\18948\AppData\Local\Temp\sidekick-v3\setup-stage"
!define APP_NAME "希沃侧边快捷键工具"
!define APP_EXE "希沃侧边快捷键工具.exe"
!define APP_VERSION "1.1.0"
!define REG_UNINST "Software\Microsoft\Windows\CurrentVersion\Uninstall\SeewoSidekick"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "C:\Users\18948\AppData\Local\Temp\sidekick-v3\SmartSideBAR-Setup-1.1.0.exe"
InstallDir "$PROGRAMFILES\seewo-sidekick"
InstallDirRegKey HKLM "SOFTWARE\SeewoSidekick" "InstallPath"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
Icon "C:\Users\18948\sidekick-unpacked\build\icon.ico"
UninstallIcon "C:\Users\18948\sidekick-unpacked\build\icon.ico"

VIProductVersion "1.1.0.0"
VIAddVersionKey /LANG=2052 "ProductName" "${APP_NAME}"
VIAddVersionKey /LANG=2052 "FileDescription" "${APP_NAME} 安装程序"
VIAddVersionKey /LANG=2052 "FileVersion" "${APP_VERSION}.0"
VIAddVersionKey /LANG=2052 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=2052 "LegalCopyright" "MIT License"

!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ; 结束正在运行的实例，避免文件占用
  nsExec::Exec 'taskkill /f /im "${APP_EXE}"'
  Pop $0
  Sleep 600
FunctionEnd

Section "Install" SEC01
  SetOutPath "$INSTDIR"
  File /r "${STAGE}\*.*"

  ; ===== 对齐 build/installer.nsh customInstall（修正：NSIS 无 $PROGRAMDATA 常量，读环境变量） =====
  WriteRegStr HKLM "SOFTWARE\SeewoSidekick" "InstallPath" "$INSTDIR"
  WriteRegStr HKLM "SOFTWARE\SeewoSidekick" "Version" "${APP_VERSION}"
  ReadEnvStr $R0 "PROGRAMDATA"
  ${If} $R0 != ""
    CreateDirectory "$R0\SeewoSidekick"
    ${IfNot} ${FileExists} "$R0\SeewoSidekick\config.json"
      CopyFiles "$INSTDIR\resources\app\defaults\config.json" "$R0\SeewoSidekick\config.json"
    ${EndIf}
  ${EndIf}

  ; ===== 快捷方式 =====
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0

  ; ===== 卸载器与卸载注册表 =====
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${REG_UNINST}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${REG_UNINST}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "${REG_UNINST}" "Publisher" "SeewoSidekick Team"
  WriteRegStr HKLM "${REG_UNINST}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKLM "${REG_UNINST}" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${REG_UNINST}" "QuietUninstallString" "$INSTDIR\Uninstall.exe /S"
  WriteRegDWORD HKLM "${REG_UNINST}" "NoModify" 1
  WriteRegDWORD HKLM "${REG_UNINST}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${REG_UNINST}" "EstimatedSize" $0

  ; 注册表卸载信息写入完成后，按需启动应用
  ${IfNot} ${Silent}
    MessageBox MB_YESNO|MB_ICONQUESTION "安装完成，现在启动 ${APP_NAME} 吗？" IDNO +2
    Exec '"$INSTDIR\${APP_EXE}"'
  ${EndIf}
SectionEnd

Section "Uninstall"
  nsExec::Exec 'taskkill /f /im "${APP_EXE}"'
  Pop $0
  Sleep 600
  RMDir /r "$INSTDIR"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  DeleteRegKey HKLM "${REG_UNINST}"
  DeleteRegKey HKLM "SOFTWARE\SeewoSidekick"
  ; 保留用户配置与日志（$PROGRAMDATA\SeewoSidekick、%APPDATA%\SeewoSidekick）
SectionEnd
