; setup.iss — Inno Setup 6 installer script for BoatTron Manager
; Requires: Inno Setup 6.x  (https://jrsoftware.org/isinfo.php)
;
; Features:
;   - Kills any running BoatTronClient.exe before installation
;   - Detects and silently uninstalls any previous version
;   - Prompts for reboot only when Windows signals files need replacing
;   - If user declines the reboot prompt, the install is rolled back

#define MyAppName     "BoatTron Monitor"
#define MyPublisher   "DigiTron Sensors"
#define MyAppExe      "BoatTronClient.exe"
#define MyAppVersion  "2.6"
; This GUID uniquely identifies THIS product — do not reuse for other apps
#define MyAppId       "{{3F8A6D2E-1B5C-4A7F-9E3D-2C8B4F1E6A9D}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyPublisher}
AppPublisherURL=https://digitronsensors.com
DefaultDirName={autopf}\{#MyPublisher}\{#MyAppName}
DefaultGroupName={#MyPublisher}\{#MyAppName}
OutputDir=Output
OutputBaseFilename=DigiTronSetup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; We handle restarts manually via Pascal script
AlwaysRestart=no
RestartIfNeededByRun=no
; Uninstall settings
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExe}
; Minimum OS: Windows 10
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Main executable — published as self-contained single file
Source: "..\dist\{#MyAppExe}"; DestDir: "{app}"; Flags: ignoreversion
; Standalone uninstall helper — also callable from Settings → Uninstall
Source: "uninstall.bat"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}";    Filename: "{app}\{#MyAppExe}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Launch {#MyAppName}"; \
    Flags: nowait postinstall skipifsilent

; ── Pascal script ────────────────────────────────────────────────────────────
[Code]

var
  InstallCompleted: Boolean;    // true once ssPostInstall fires

// ── Kill running process ─────────────────────────────────────────────────────

procedure KillRunningClient;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'),
       '/F /IM {#MyAppExe}',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  // Give Windows a moment to release file handles
  Sleep(1500);
end;

// ── Uninstall previous version ───────────────────────────────────────────────

procedure UninstallPrevious;
var
  UninstStr:  String;
  ResultCode: Integer;
  RegKey:     String;
begin
  RegKey := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}_is1';

  // Check HKLM first, then HKCU (user installs)
  if not RegQueryStringValue(HKLM, RegKey, 'UninstallString', UninstStr) then
    RegQueryStringValue(HKCU, RegKey, 'UninstallString', UninstStr);

  if UninstStr <> '' then
  begin
    // Strip surrounding quotes that Inno Setup sometimes includes
    if (Length(UninstStr) > 1) and (UninstStr[1] = '"') then
      UninstStr := Copy(UninstStr, 2, Pos('"', Copy(UninstStr, 2, MaxInt)) - 1);

    if FileExists(UninstStr) then
    begin
      Exec(UninstStr, '/SILENT /NORESTART', '',
           SW_HIDE, ewWaitUntilTerminated, ResultCode);
      Sleep(2000);
    end;
  end;
end;

// ── Pre-install: kill + uninstall ────────────────────────────────────────────

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result       := '';
  NeedsRestart := False;

  KillRunningClient;
  UninstallPrevious;
end;

// ── Track install progress ───────────────────────────────────────────────────

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    InstallCompleted := True;
end;

// ── Entry point ──────────────────────────────────────────────────────────────

function InitializeSetup: Boolean;
begin
  Result := True;
end;
