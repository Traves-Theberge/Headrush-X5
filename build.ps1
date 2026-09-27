param([string]$OutputDirectory = 'bin')
$ErrorActionPreference = 'Stop'
$targetDir = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $PSScriptRoot $OutputDirectory }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $compiler)) { throw 'The .NET Framework C# compiler is required.' }
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
$assemblyRoot = Join-Path $env:WINDIR 'Microsoft.NET\assembly'
function Get-FrameworkAssembly([string]$name) {
  $file = Get-ChildItem $assemblyRoot -Recurse -Filter "$name.dll" -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
  if (-not $file) { throw "Missing .NET Framework assembly: $name" }
  return $file
}
$refs = @('WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml') | ForEach-Object { '/reference:' + (Get-FrameworkAssembly $_) }
$sources = Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -Recurse | Select-Object -ExpandProperty FullName
& $compiler /nologo /target:winexe /out:"$targetDir\X5Control.exe" $refs $sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item (Join-Path $PSScriptRoot 'src\Ui\StageWindow.xaml') (Join-Path $targetDir 'MainWindow.xaml') -Force
Write-Host "Built $targetDir\X5Control.exe"
