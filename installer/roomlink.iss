; RoomLink installer script (Inno Setup 6)

#define MyAppName "RoomLink"
#define MyAppVersion "0.2.0"
#define MyAppExeName "RoomLink.exe"

[Setup]
AppId={{7E2D1A6F-3C4B-4E9A-B5D8-ROOMLINK0A1}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=RoomLink Project
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=..\dist
OutputBaseFilename=RoomLinkSetup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; Flags: unchecked
Name: "startup"; Description: "Start RoomLink when Windows starts"; Flags: unchecked

[Files]
Source: "..\dist\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Auto-start task writes a Run-key entry (checked task only)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; \
  Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up settings.json if the user removes the app entirely
; (kept simple: settings live in %APPDATA%\RoomLink)
