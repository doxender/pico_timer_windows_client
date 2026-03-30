@echo off
setlocal EnableDelayedExpansion

:: build.bat — Build BoatTron Manager and produce DigiTronSetup.exe
::
:: Requirements:
::   .NET 8 SDK    : https://dot.net/download
::   Inno Setup 6  : https://jrsoftware.org/isinfo.php
::
:: Usage: build.bat [release|debug]
:: Default: release

set CONFIG=%1
if "%CONFIG%"=="" set CONFIG=release

set APP_DIR=%~dp0..
set DIST_DIR=%APP_DIR%\dist
set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

:: Allow override of ISCC path via environment
if not "%ISCC_PATH%"=="" set ISCC="%ISCC_PATH%"

echo.
echo ╔══════════════════════════════════════════════════════╗
echo ║   DigiTron Sensors — BoatTron Manager Build         ║
echo ╚══════════════════════════════════════════════════════╝
echo.

:: ── Check dotnet SDK ────────────────────────────────────────────────────────
dotnet --version >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo ERROR: dotnet SDK not found. Download from https://dot.net/download
    exit /b 1
)
for /f %%v in ('dotnet --version') do set DOTNET_VER=%%v
echo .NET SDK : %DOTNET_VER%

:: ── Check Inno Setup ────────────────────────────────────────────────────────
if not exist %ISCC% (
    echo ERROR: Inno Setup compiler not found at %ISCC%
    echo        Install Inno Setup 6 from https://jrsoftware.org/isinfo.php
    echo        or set ISCC_PATH environment variable to its full path.
    exit /b 1
)
echo Inno Setup : found

:: ── Publish self-contained single-file EXE ──────────────────────────────────
echo.
echo [1/2] Publishing BoatTron Manager (%CONFIG%)...
echo.

if exist "%DIST_DIR%" rmdir /s /q "%DIST_DIR%"

dotnet publish "%APP_DIR%\BoatTronClient.csproj" ^
    --configuration %CONFIG% ^
    --runtime win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:PublishReadyToRun=true ^
    --output "%DIST_DIR%"

if %ERRORLEVEL% neq 0 (
    echo.
    echo ERROR: dotnet publish failed.
    exit /b 1
)

if not exist "%DIST_DIR%\BoatTronClient.exe" (
    echo ERROR: Expected output not found: %DIST_DIR%\BoatTronClient.exe
    exit /b 1
)
echo Publish OK — %DIST_DIR%\BoatTronClient.exe

:: ── Build Inno Setup installer ───────────────────────────────────────────────
echo.
echo [2/2] Building installer...
echo.

if not exist "%~dp0Output" mkdir "%~dp0Output"

%ISCC% "%~dp0setup.iss"

if %ERRORLEVEL% neq 0 (
    echo.
    echo ERROR: Inno Setup compilation failed.
    exit /b 1
)

echo.
echo ════════════════════════════════════════════════════════
echo   BUILD COMPLETE
echo   Installer: %~dp0Output\DigiTronSetup.exe
echo ════════════════════════════════════════════════════════
echo.
