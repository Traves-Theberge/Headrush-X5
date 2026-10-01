$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$bridge = Join-Path $root 'bin\service\Mx5BridgeCli.exe'
if (-not (Test-Path $bridge)) { & (Join-Path $root 'build-service.ps1') }
if (-not (Get-Command node.exe -ErrorAction SilentlyContinue)) { throw 'Node.js is required to run the local X5 API.' }
& node.exe (Join-Path $root 'service\server.js')
