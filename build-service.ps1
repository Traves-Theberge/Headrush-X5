param([string]$OutputDirectory = 'bin\service')
$ErrorActionPreference = 'Stop'
$targetDir = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $PSScriptRoot $OutputDirectory }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $compiler)) { throw 'The .NET Framework C# compiler is required.' }
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
& $compiler /nologo /target:exe /out:"$targetDir\Mx5BridgeCli.exe" (Join-Path $PSScriptRoot 'service\BridgeCli.cs')
if ($LASTEXITCODE -ne 0) { throw 'MX5 bridge compilation failed.' }
Write-Host "Built $targetDir\Mx5BridgeCli.exe"
