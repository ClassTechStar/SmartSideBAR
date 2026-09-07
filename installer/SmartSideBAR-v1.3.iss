; SmartSideBAR v1.3 Inno Setup 安装脚本
; 编译: ISCC.exe installer/SmartSideBAR-v1.3.iss

#define AppName "SmartSideBAR"
#define AppVersion "1.3.0"
#define AppPublisher "Seewo Sidekick Team"
#define AppExe "SmartSideBAR.Avalonia.exe"
#define SetupIconFile "..\build\icon.ico"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=..\dist
OutputBaseFilename=SmartSideBAR-v1.3-Setup
SetupIconFile={#SetupIconFile}
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
VersionInfoVersion=1.3.0.0
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright=Apache-2.0
VersionInfoProductVersion=1.3.0.0
VersionInfoProductName={#AppName}

[Files]
Source: "..\artifacts\v1.3\SmartSideBAR.Avalonia.exe"; DestDir: "{app}"; DestName: "{#AppExe}"
Source: "..\artifacts\v1.3\*.dll"; DestDir: "{app}"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "启动 SmartSideBAR"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
