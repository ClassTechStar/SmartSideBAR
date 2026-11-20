; SmartSideBAR Inno Setup 安装脚本 (Avalonia 主线)
; 产物目录: CI publish 输出 artifacts/app（可复现, 不依赖本地产物）
; 版本号单一事实源: Directory.Build.props <Version>。
;   发版由 CI 用 ISCC /DAppVersion=x.y.z 注入 (值从 Directory.Build.props 提取),
;   下面的 #define 仅作本地手工编译的兜底默认值 —— 发版不再手改任何版本字面量。
; 编译: "C:\Program Files\Inno Setup 7\ISCC.exe" /DAppVersion=x.y.z installer\SmartSideBAR.iss
; AppId 与 v1.3 一致 —— 已装 v1.3 的机器安装本包即原地升级

#define AppName "SmartSideBAR"
#ifndef AppVersion
  #define AppVersion "1.6.0"
#endif
; 架构注入 (P1-6): CI 对 arm64 构建传 /DAppArch=arm64 /DArchAllowed=arm64 /DAppSrcDir=..\artifacts\app-arm64
; 默认 x64, 产物名带架构记号 (SmartSideBAR-vX.Y.Z-x64-Setup.exe), winget/应用内更新按记号选包
#ifndef AppArch
  #define AppArch "x64"
#endif
#ifndef ArchAllowed
  #define ArchAllowed "x64compatible"
#endif
#ifndef AppSrcDir
  #define AppSrcDir "..\artifacts\app"
#endif
#define AppPublisher "Seewo Sidekick Team"
#define AppExe "SmartSideBAR.Avalonia.exe"
#define SetupIconFile "..\build\icon.ico"

[Setup]
AppId={{6F3A9C21-7B4E-4D8A-9C13-2E5F81B0D7AA}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\smartsidebar
DefaultGroupName={#AppName}
OutputDir=..\dist
OutputBaseFilename=SmartSideBAR-v{#AppVersion}-{#AppArch}-Setup
SetupIconFile={#SetupIconFile}
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
PrivilegesRequired=lowest
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
DisableReadyPage=no
WizardStyle=modern
; 录屏走 Windows.Graphics.Capture, 需 Win10 1803+ (17763)
MinVersion=10.0.17763
; 不允许在非目标架构上误装 (x64 包放行 x64compatible, arm64 包只放行 arm64)
ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode={#ArchAllowed}
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright=Apache-2.0
VersionInfoProductVersion={#AppVersion}.0
VersionInfoProductName={#AppName}

[Messages]
WelcomeLabel2=这将安装 [name/ver] 到你的电脑。%n%n1.5 版：工作区一键切换（上课/批注模式）、墨迹识别转形、板中板、通知中心、快捷设置大磁贴、希沃式触发条、悬浮球扇形菜单与全屏批注压感笔。%n%n建议先卸载旧版本再继续。

[Files]
Source: "{#AppSrcDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务:"
; 自启独立成任务: 不绑在 desktopicon 上, 且与应用内
; 「开机自启动」开关 (.NET Registry Run, 值带 /silent) 互相覆盖。
Name: "autostart"; Description: "开机自动启动 {#AppName}"; GroupDescription: "附加任务:"; Flags: unchecked

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"" /silent"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "启动 {#AppName}"; Flags: nowait postinstall

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
{ 应用内设置页也能写同一个 Run 值; 用户取消勾选自启时安装器不该在卸载前留下它。 }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#AppName}');
end;
