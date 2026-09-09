; SmartSideBAR v1.3.1 Inno Setup 安装脚本 (Avalonia 版)
; 产物目录: artifacts/v1.3-fix
; 编译: "C:\Program Files\Inno Setup 7\ISCC.exe" installer\SmartSideBAR-v1.3.iss

#define AppName "SmartSideBAR"
#define AppVersion "1.3.1"
#define AppPublisher "Seewo Sidekick Team"
#define AppExe "SmartSideBAR.Avalonia.exe"
#define SetupIconFile "..\build\icon.ico"

[Setup]
AppId={{6F3A9C21-7B4E-4D8A-9C13-2E5F81B0D7AA}}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\smartsidebar
DefaultGroupName={#AppName}
OutputDir=..\dist
OutputBaseFilename=SmartSideBAR-v1.3.1-Setup
SetupIconFile={#SetupIconFile}
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
PrivilegesRequired=lowest
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
DisableReadyPage=no
VersionInfoVersion=1.3.0.0
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright=Apache-2.0
VersionInfoProductVersion=1.3.0.0
VersionInfoProductName={#AppName}
WizardStyle=modern

[Messages]
WelcomeLabel2=这将安装 [name/ver] 到你的电脑。%n%n基于 1.2 版本开发：悬浮球圆形镜头样式与展开扇形菜单、全屏自动收缩侧边栏。%n%n建议先卸载旧版本再继续。

[Files]
Source: "..\artifacts\v1.3-fix\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务:"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "启动 {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
