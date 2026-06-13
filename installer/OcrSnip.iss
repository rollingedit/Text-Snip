#define MyAppName "Text Snip"
#define MyAppExeName "OcrSnip.App.exe"
#define MyAppVersion GetEnv("OCRSNIP_VERSION")
#if MyAppVersion == ""
  #define MyAppVersion "1.0.0"
#endif

[Setup]
AppId={{2A3AE8AF-87D7-40C4-8C79-5158B3A9B89F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\Text Snip
DefaultGroupName={#MyAppName}
OutputBaseFilename=Text-Snip-Setup-x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\OcrSnip.App\Assets\OcrSnip.ico
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce
Name: "launchatlogin"; Description: "Start Text Snip at startup"; GroupDescription: "Startup:"; Flags: checkedonce
Name: "resetuserdata"; Description: "Reset Text Snip settings and logs"; GroupDescription: "Repair options:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\OcrSnip\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\artifacts\prereqs\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\Text Snip"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Text Snip"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "OcrSnip"; Flags: deletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "OcrSnip"; ValueData: """{app}\{#MyAppExeName}"" --tray"; Tasks: launchatlogin; Flags: uninsdeletevalue

[InstallDelete]
Type: files; Name: "{autodesktop}\OCR Snip.lnk"
Type: files; Name: "{group}\OCR Snip.lnk"
Type: files; Name: "{autodesktop}\Text Snip.lnk"
Type: files; Name: "{group}\Text Snip.lnk"
Type: filesandordirs; Name: "{userappdata}\OcrSnip"; Tasks: resetuserdata
Type: filesandordirs; Name: "{localappdata}\OcrSnip\logs"; Tasks: resetuserdata

[Run]
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing Visual C++ Runtime..."; Check: NeedsVCRedist
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Text Snip"; Flags: nowait postinstall skipifsilent

[Code]
function NeedsVCRedist(): Boolean;
var
  Version: string;
begin
  Result := True;
  if RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Version', Version) then
  begin
    Result := Version < 'v14.40';
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  if NeedsVCRedist and not IsAdminInstallMode then
  begin
    Result := 'Text Snip needs the Microsoft Visual C++ Runtime. Run this installer as administrator or install the x64 Visual C++ Runtime first.';
    Exit;
  end;

  Exec('taskkill.exe', '/IM {#MyAppExeName} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;
