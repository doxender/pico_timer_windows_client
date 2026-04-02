$iscc = $env:LOCALAPPDATA + "\Programs\Inno Setup 6\ISCC.exe"
& $iscc "C:\ComputerSource\PicoTimer2\WindowsClient\installer\setup.iss"
exit $LASTEXITCODE
