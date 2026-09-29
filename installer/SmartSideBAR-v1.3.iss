; SmartSideBAR Inno Setup 安装脚本 (Avalonia 主线)
; A4: 产物目录改为 CI publish 输出 artifacts/app（可复现, 不再依赖本地产物）
; 版本号由 scripts/sync-version.mjs 从 VERSION 文件注入 (A3 单一事实源)
; 编译: "C:\Program Files\Inno Setup 7\ISCC.exe" installer\SmartSideBAR-v1.3.iss
; CI 编译: 由 .github/workflows/ci.yml release job 自动执行

#define AppName "SmartSideBAR"
#define AppVersion "2.0.0"
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
OutputBaseFilename=SmartSideBAR-v2.0.0-Setup
SetupIconFile={#SetupIconFile}
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
PrivilegesRequired=lowest
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
DisableReadyPage=no
VersionInfoVersion=2.0.0.0
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright=Apache-2.0
VersionInfoProductVersion=2.0.0.0
VersionInfoProductName={#AppName}
WizardStyle=modern

[Messages]
WelcomeLabel2=这将安装 [name/ver] 到你的电脑。%n%n基于 1.2 版本开发：悬浮球圆形镜头样式与展开扇形菜单、全屏自动收缩侧边栏。%n%n建议先卸载旧版本再继续。

[Files]
Source: "..\artifacts\app\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

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
