param([string]$Version = (Get-Content (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim())

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must use major.minor.patch.' }

$releaseDir = Join-Path $PSScriptRoot 'dist'
$buildDir = Join-Path $PSScriptRoot "bin\package-$Version"
$packageName = "X5Control-$Version-windows-portable"
$archive = Join-Path $releaseDir "$packageName.zip"
$checksum = Join-Path $releaseDir "$packageName.sha256"

& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $buildDir
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw 'Build failed.' }
& (Join-Path $PSScriptRoot 'build-service.ps1') -OutputDirectory (Join-Path $buildDir 'bin\service')
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw 'MX5 bridge build failed.' }
New-Item -ItemType Directory -Path (Join-Path $buildDir 'service') -Force | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'service\server.js') (Join-Path $buildDir 'service\server.js') -Force
Copy-Item (Join-Path $PSScriptRoot 'run-service.ps1') (Join-Path $buildDir 'run-service.ps1') -Force
Copy-Item (Join-Path $PSScriptRoot 'build-service.ps1') (Join-Path $buildDir 'build-service.ps1') -Force

$exe = Join-Path $buildDir 'X5Control.exe'
$xaml = Join-Path $buildDir 'MainWindow.xaml'
if (-not (Test-Path $exe) -or -not (Test-Path $xaml)) { throw 'Package input is incomplete.' }
$fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion
if ($fileVersion -ne "$Version.0") { throw "Executable version $fileVersion does not match $Version. Update AssemblyInfo.cs." }

$instructions = @"
X5 Control $Version - Windows portable edition

1. Extract every file in this ZIP to a writable folder.
2. Connect and power on the VX5 by USB.
3. Double-click X5Control.exe.
4. Select the VX5 MIDI output in Advanced for preset, A/B, and Talk commands.

Requires Windows with .NET Framework 4.8. Keep MainWindow.xaml next to the EXE.
Local preset names and custom patches are written to presets.user.json in this folder.
To move an existing preset library, close the app and copy presets.user.json
from the old app folder into this extracted folder.

For MX5 live USB control, install Node.js and start run-service.ps1, then use
MX5 > Connect and scan in the app. Compatible MX5 Bridge firmware is required.
Stock firmware supports performance MIDI through an external USB MIDI interface
connected to the MX5 3.5 mm MIDI input. Live MX5 hardware operation is unverified.

See README.md for controls and device limitations.
"@
$instructionsPath = Join-Path $buildDir 'START-HERE.txt'
[System.IO.File]::WriteAllText($instructionsPath, $instructions, [System.Text.UTF8Encoding]::new($false))
Copy-Item (Join-Path $PSScriptRoot 'README.md') (Join-Path $buildDir 'README.md') -Force
Copy-Item (Join-Path $PSScriptRoot 'CHANGELOG.md') (Join-Path $buildDir 'CHANGELOG.md') -Force

New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
Compress-Archive -Path (Join-Path $buildDir '*') -DestinationPath $archive -CompressionLevel Optimal -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText($checksum, "$hash *$packageName.zip`n", [System.Text.UTF8Encoding]::new($false))
Write-Host "Package: $archive"
Write-Host "SHA256:  $hash"
