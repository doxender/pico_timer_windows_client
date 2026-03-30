@echo off
:: uninstall.bat — cleanly uninstall BoatTron Manager
::
:: - Kills any running instance
:: - Runs the Inno Setup uninstaller (removes app files + registry keys)
:: - Removes user settings from %APPDATA%
:: - Reports clean or warns if anything was not found

setlocal EnableDelayedExpansion

set APP_ID={3F8A6D2E-1B5C-4A7F-9E3D-2C8B4F1E6A9D}
set REG_KEY=SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\%APP_ID%_is1
set APP_EXE=BoatTronClient.exe
set SETTINGS_DIR=%APPDATA%\DigiTronSensors\BoatTronClient

echo.
echo BoatTron Manager Uninstaller
echo ════════════════════════════
echo.

:: ── Kill running client ──────────────────────────────────────────────────────
echo Stopping %APP_EXE% if running...
taskkill /F /IM %APP_EXE% >nul 2>&1
timeout /t 2 /nobreak >nul

:: ── Find and run Inno Setup uninstaller ─────────────────────────────────────
set UNINST=
for /f "tokens=2*" %%A in (
    'reg query "HKLM\%REG_KEY%" /v UninstallString 2^>nul'
) do set UNINST=%%B

if "%UNINST%"=="" (
    for /f "tokens=2*" %%A in (
        'reg query "HKCU\%REG_KEY%" /v UninstallString 2^>nul'
    ) do set UNINST=%%B
)

if "%UNINST%"=="" (
    echo   WARNING: No previous installation found in registry.
    echo            App may have already been removed or was never installed.
) else (
    echo Uninstalling...
    :: Strip surrounding quotes if present
    set UNINST=!UNINST:"=!
    if exist "!UNINST!" (
        "!UNINST!" /SILENT /NORESTART
        echo   Uninstaller finished.
    ) else (
        echo   WARNING: Uninstaller not found at: !UNINST!
    )
)

:: ── Remove user settings ─────────────────────────────────────────────────────
if exist "%SETTINGS_DIR%" (
    echo Removing user settings from %SETTINGS_DIR%...
    rmdir /s /q "%SETTINGS_DIR%"
    echo   Settings removed.
) else (
    echo   No user settings directory found (already clean).
)

:: ── Verify registry is gone ──────────────────────────────────────────────────
reg query "HKLM\%REG_KEY%" >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo   WARNING: Registry key still present — manual cleanup may be needed.
) else (
    echo   Registry clean.
)

echo.
echo Uninstall complete.
echo.
pause
