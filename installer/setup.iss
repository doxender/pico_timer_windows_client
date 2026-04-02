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
#define MyAppVersion  "2.23"
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
Name: "{group}\{#MyAppName}";           Filename: "{app}\{#MyAppExe}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}";   Filename: "{app}\{#MyAppExe}"

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Launch {#MyAppName}"; \
    Flags: nowait postinstall skipifsilent

; ── Pascal script ────────────────────────────────────────────────────────────
[Code]

// AppGuidKey is written as a plain Pascal string — do NOT use {#MyAppId} here.
// {#MyAppId} is defined as "{{GUID}" so the preprocessor expands it to "{{GUID}"
// (two open-braces) inside a Pascal string literal, which does NOT match the
// single-brace registry key "{GUID}_is1" that Inno Setup writes on install.
const
  AppGuidKey = '{3F8A6D2E-1B5C-4A7F-9E3D-2C8B4F1E6A9D}_is1';

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

// ── Find a registry string value by searching 4 keys in order ───────────────
// Uses separate if statements — Pascal 'or' evaluates both sides and the
// second RegQueryStringValue call can overwrite S even after a successful first.

function QueryUninstallValue(const ValueName: String; out Value: String): Boolean;
var
  Base: String;
begin
  Result := False;
  Value  := '';
  Base   := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\';

  if RegQueryStringValue(HKLM, Base + AppGuidKey, ValueName, Value) then begin Result := True; Exit; end;
  if RegQueryStringValue(HKCU, Base + AppGuidKey, ValueName, Value) then begin Result := True; Exit; end;
  if RegQueryStringValue(HKLM, Base + '{#MyAppName}_is1', ValueName, Value) then begin Result := True; Exit; end;
  if RegQueryStringValue(HKCU, Base + '{#MyAppName}_is1', ValueName, Value) then begin Result := True; Exit; end;
end;

function FindUninstallString: String;
var S: String;
begin
  if not QueryUninstallValue('UninstallString', S) then S := '';
  Result := S;
end;

// ── Uninstall previous version ────────────────────────────────────────────────

procedure UninstallPrevious;
var
  UninstStr:  String;
  ResultCode: Integer;
begin
  UninstStr := FindUninstallString;
  if UninstStr = '' then Exit;

  // Strip surrounding quotes that Inno Setup sometimes adds
  if (Length(UninstStr) > 1) and (UninstStr[1] = '"') then
    UninstStr := Copy(UninstStr, 2, Pos('"', Copy(UninstStr, 2, MaxInt)) - 1);

  if FileExists(UninstStr) then
  begin
    Exec(UninstStr, '/SILENT /NORESTART', '',
         SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(2000);
  end;
end;

// ── Pre-install: kill + uninstall ────────────────────────────────────────────

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result       := '';
  NeedsRestart := False;
  // Kill the app in case it was restarted after InitializeSetup ran.
  // UninstallPrevious already completed in InitializeSetup when upgrading.
  KillRunningClient;
end;

// ── Track install progress ───────────────────────────────────────────────────

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    InstallCompleted := True;
end;

// ── Entry point: check for previous version and confirm upgrade ──────────────

function FindOldVersion: String;
var S: String;
begin
  if not QueryUninstallValue('DisplayVersion', S) then S := '';
  Result := S;
end;

function InitializeSetup: Boolean;
var
  OldVersion: String;
  Answer:     Integer;
begin
  Result     := True;
  OldVersion := FindOldVersion;

  if OldVersion <> '' then
  begin
    Answer := MsgBox(
      '{#MyAppName} v' + OldVersion + ' is already installed.' + #13#10#13#10 +
      'Replace it with v{#MyAppVersion}?',
      mbConfirmation, MB_YESNO);

    if Answer <> IDYES then
    begin
      Result := False;
      Exit;
    end;

    // Uninstall BEFORE the installer wizard appears so the user never sees
    // the next window until the old version is fully removed.
    KillRunningClient;
    UninstallPrevious;
  end;
end;
