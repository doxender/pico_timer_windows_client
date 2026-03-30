; setup.iss — Inno Setup 6 installer script for BoatTron Manager
; Requires: Inno Setup 6.x  (https://jrsoftware.org/isinfo.php)
;
; Features:
;   - 10-second "DigiTron Sensors" splash banner before wizard starts
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
  RestartIsRequired: Boolean;   // true if Inno Setup scheduled file-replace on reboot
  UserDeclinedRestart: Boolean; // true if user clicked NO at the reboot prompt
  InstallCompleted: Boolean;    // true once ssPostInstall fires

// ── Splash (10 seconds) ─────────────────────────────────────────────────────

procedure ShowSplash;
var
  F:    TSetupForm;
  Lbl1: TNewStaticText;
  Lbl2: TNewStaticText;
  StartTick: DWORD;
begin
  F := CreateCustomForm;
  try
    F.Width         := 640;
    F.Height        := 220;
    F.Position      := poScreenCenter;
    F.BorderStyle   := bsNone;
    F.Color         := $00181818;   // near-black background

    Lbl1 := TNewStaticText.Create(F);
    Lbl1.Parent     := F;
    Lbl1.Caption    := 'DigiTron Sensors';
    Lbl1.Font.Name  := 'Segoe UI';
    Lbl1.Font.Size  := 40;
    Lbl1.Font.Style := [fsBold];
    Lbl1.Font.Color := $00E8E8E8;   // near-white
    Lbl1.AutoSize   := True;
    Lbl1.Left       := (F.ClientWidth  - Lbl1.Width)  div 2;
    Lbl1.Top        := (F.ClientHeight - Lbl1.Height) div 2 - 20;

    Lbl2 := TNewStaticText.Create(F);
    Lbl2.Parent     := F;
    Lbl2.Caption    := 'BoatTron Manager  v{#MyAppVersion}  —  Installing...';
    Lbl2.Font.Name  := 'Segoe UI';
    Lbl2.Font.Size  := 12;
    Lbl2.Font.Color := $00999999;
    Lbl2.AutoSize   := True;
    Lbl2.Left       := (F.ClientWidth  - Lbl2.Width)  div 2;
    Lbl2.Top        := Lbl1.Top + Lbl1.Height + 14;

    F.Show;
    F.Update;

    // Wait 10 seconds with message processing so the form stays responsive
    StartTick := GetTickCount;
    repeat
      Application.ProcessMessages;
      Sleep(50);
    until (GetTickCount - StartTick) >= 10000;

    F.Hide;
  finally
    F.Free;
  end;
end;

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

// ── Track whether a file replacement was deferred to reboot ─────────────────
//    Inno Setup sets NeedRestart to true internally when it schedules a
//    MOVEFILE_DELAY_UNTIL_REBOOT operation.  We mirror that here so our
//    finish-page rollback logic can see it.

function NeedRestart: Boolean;
begin
  // RestartIsRequired is set by CurStepChanged when ssInstall completes
  // and IsRestarting() returns true (Inno Setup internal flag).
  Result := RestartIsRequired;
end;

// ── Track install progress ───────────────────────────────────────────────────

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    InstallCompleted := True;
    // Check if Inno Setup internally needs a reboot (deferred file ops)
    RestartIsRequired := IsRestarting;
  end;
end;

// ── Detect user declining reboot and trigger rollback ────────────────────────

function NextButtonClick(CurPageID: Integer): Boolean;
var
  UninstStr:  String;
  ResultCode: Integer;
  RegKey:     String;
begin
  Result := True;

  if (CurPageID = wpFinished) and InstallCompleted and RestartIsRequired then
  begin
    // wpFinished has a YesRadio/NoRadio when restart is needed.
    // If user selected NoRadio (don't restart), roll back.
    if WizardForm.NoRadio.Checked then
    begin
      UserDeclinedRestart := True;

      if MsgBox('You chose not to restart now.' + #13#10 +
                'The installation cannot complete without a restart.' + #13#10#13#10 +
                'The installation will be rolled back.' + #13#10 +
                'Click OK to uninstall, or Cancel to keep the partial install.',
                mbConfirmation, MB_OKCANCEL) = IDOK then
      begin
        // Run the uninstaller we just created
        RegKey := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}_is1';
        if not RegQueryStringValue(HKLM, RegKey, 'UninstallString', UninstStr) then
          RegQueryStringValue(HKCU, RegKey, 'UninstallString', UninstStr);

        if UninstStr <> '' then
        begin
          if (Length(UninstStr) > 1) and (UninstStr[1] = '"') then
            UninstStr := Copy(UninstStr, 2, Pos('"', Copy(UninstStr, 2, MaxInt)) - 1);
          if FileExists(UninstStr) then
            Exec(UninstStr, '/SILENT /NORESTART', '',
                 SW_HIDE, ewWaitUntilTerminated, ResultCode);
        end;
      end;
    end;
  end;
end;

// ── Entry point — show splash then proceed ───────────────────────────────────

function InitializeSetup: Boolean;
begin
  ShowSplash;
  Result := True;
end;
