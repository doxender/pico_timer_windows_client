# bump_version.ps1 - Increment minor version and patch csproj, setup.iss, MainForm.cs
#
# version.txt holds MAJOR.MINOR (e.g. "2.8").
# Each build increments MINOR: 2.8 -> 2.9 -> 2.10 etc.
# For a new major feature, manually edit version.txt (e.g. change to "3.0")
# and the next build will patch everything to that version.

$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$versionFile = Join-Path $scriptDir "version.txt"
$appDir      = Join-Path $scriptDir ".."

# Read current version and split into major/minor
$current = (Get-Content $versionFile -Raw).Trim()
$parts   = $current -split '\.'
$major   = [int]$parts[0]
$minor   = [int]$parts[1]

$oldVer = "$major.$minor"

# Bump minor
$minor++
$newVer = "$major.$minor"

# Write back
Set-Content $versionFile "$newVer`n"

Write-Host "Bumping $oldVer to $newVer"

# Patch BoatTronClient.csproj
$csproj = Join-Path $appDir "BoatTronClient.csproj"
(Get-Content $csproj -Raw) `
    -replace "<Version>$([regex]::Escape($oldVer))\.0</Version>",                  "<Version>$newVer.0</Version>" `
    -replace "<FileVersion>$([regex]::Escape($oldVer))\.0\.0</FileVersion>",        "<FileVersion>$newVer.0.0</FileVersion>" `
    -replace "<AssemblyVersion>$([regex]::Escape($oldVer))\.0\.0</AssemblyVersion>","<AssemblyVersion>$newVer.0.0</AssemblyVersion>" |
    Set-Content $csproj -NoNewline

# Patch setup.iss
$iss = Join-Path $scriptDir "setup.iss"
(Get-Content $iss -Raw) `
    -replace "#define MyAppVersion\s+`"$([regex]::Escape($oldVer))`"", "#define MyAppVersion  `"$newVer`"" |
    Set-Content $iss -NoNewline

# Patch MainForm.cs version strings
$mainForm = Join-Path $appDir "MainForm.cs"
(Get-Content $mainForm -Raw) `
    -replace ([regex]::Escape("v$oldVer")), "v$newVer" |
    Set-Content $mainForm -NoNewline

Write-Host "Done. Version is now $newVer"
