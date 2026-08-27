; build/installer.nsh - NSIS 自定义安装脚本

; 自定义宏: 安装完成后执行
!macro customInstall
  ; 写入注册表项,供其他程序检测
  WriteRegStr HKLM "SOFTWARE\SeewoSidekick" "InstallPath" "$INSTDIR"
  WriteRegStr HKLM "SOFTWARE\SeewoSidekick" "Version" "1.1.0"
  
  ; 创建 ProgramData 配置目录
  CreateDirectory "$PROGRAMDATA\SeewoSidekick"
  
  ; 首次安装时复制默认配置到 ProgramData
  ${IfNot} ${FileExists} "$PROGRAMDATA\SeewoSidekick\config.json"
    CopyFiles "$INSTDIR\resources\app\defaults\config.json" "$PROGRAMDATA\SeewoSidekick\config.json"
  ${EndIf}
!macroend

; 卸载时清理
!macro customUnInstall
  ; 删除注册表项
  DeleteRegKey HKLM "SOFTWARE\SeewoSidekick"
  
  ; 询问是否删除用户配置
  MessageBox MB_YESNO|MB_ICONQUESTION "是否同时删除用户配置和日志文件?" IDNO skipCleanup
    RMDir /r "$APPDATA\SeewoSidekick"
    RMDir /r "$LOCALAPPDATA\SeewoSidekick"
  skipCleanup:
!macroend
