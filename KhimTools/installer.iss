; Script Inno Setup cho KhiemTools
#define MyAppName "KhiemTools"
#define MyAppVersion "2.1.0"
#define MyAppPublisher "Khim"
#define MyAppURL "https://github.com/nguyenkhiemkhiem079-boop/KhiemTools_"
#define MyAppExeName "KhiemTools.exe"

[Setup]
AppId={{6E8B9145-D6A4-4A3E-91C0-3EFA9D68BF2A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={commonappdata}\Autodesk\ApplicationPlugins\KhimTools.bundle
DisableProgramGroupPage=yes
OutputBaseFilename=KhiemTools_Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Revit Addin Bundle & DLLs
Source: "Deploy\PackageContents.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "Deploy\Legacy\*"; DestDir: "{app}\Contents\Legacy"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Deploy\Modern\*"; DestDir: "{app}\Contents\Modern"; Flags: ignoreversion recursesubdirs createallsubdirs

; Executable standalone nếu có
; Source: "dist\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
