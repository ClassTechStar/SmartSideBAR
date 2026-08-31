; build/installer.nsh - NSIS 自定义安装脚本

; 自定义宏: 安装完成后执行
; P1-10: 安装包已降级为 per-user + asInvoker (普通用户免 UAC)。
; HKLM / ProgramData 写入仅管理员上下文有意义 —— 按账户类型分支:
;   管理员   → HKLM 检测键 + ProgramData 策略层配置 (多机统一部署场景)
;   普通用户 → HKCU 检测键; 跳过 ProgramData (无写入权限),
;              运行时配置由 DEFAULT_CONFIG + %APPDATA% 用户配置兜底, 功能不受影响
!macro customInstall
  UserInfo::GetAccountType
  Pop $0

  ${If} $0 == "Admin"
    ; 写入注册表项,供其他程序检测
    WriteRegStr HKLM "SOFTWARE\SeewoSidekick" "InstallPath" "$INSTDIR"
    WriteRegStr HKLM "SOFTWARE\SeewoSidekick" "Version" "1.1.0"

    ; 创建 ProgramData 配置目录
    CreateDirectory "$PROGRAMDATA\SeewoSidekick"

    ; 首次安装时复制默认配置到 ProgramData (策略层)
    ${IfNot} ${FileExists} "$PROGRAMDATA\SeewoSidekick\config.json"
      CopyFiles "$INSTDIR\resources\app\defaults\config.json" "$PROGRAMDATA\SeewoSidekick\config.json"
    ${EndIf}
  ${Else}
    ; 普通用户: 检测键写入 HKCU
    WriteRegStr HKCU "SOFTWARE\SeewoSidekick" "InstallPath" "$INSTDIR"
    WriteRegStr HKCU "SOFTWARE\SeewoSidekick" "Version" "1.1.0"
  ${EndIf}
!macroend

; 卸载时清理
!macro customUnInstall
  ; 删除注册表项 (两种安装上下文都清理, 键不存在时 DeleteRegKey 静默成功)
  DeleteRegKey HKLM "SOFTWARE\SeewoSidekick"
  DeleteRegKey HKCU "SOFTWARE\SeewoSidekick"

  ; 询问是否删除用户配置
  MessageBox MB_YESNO|MB_ICONQUESTION "是否同时删除用户配置和日志文件?" IDNO skipCleanup
    RMDir /r "$APPDATA\SeewoSidekick"
    RMDir /r "$LOCALAPPDATA\SeewoSidekick"
  skipCleanup:
!macroend
