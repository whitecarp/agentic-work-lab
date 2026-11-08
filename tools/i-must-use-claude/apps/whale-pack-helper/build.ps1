param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'), [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '.NET Framework compiler csc.exe v4.0.30319 was not found.' }
$release = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release).Release
if ($release -lt 528040) { throw '.NET Framework 4.8 or later is required.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName) + @([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\shared\UpdateChecker.cs')))
$sharedArgs = @('/nologo', '/langversion:5', '/platform:anycpu', '/optimize+', '/utf8output', '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Web.Extensions.dll', '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll', ('/resource:' + (Join-Path $PSScriptRoot 'assets\icon.ico') + ',WhalePack.icon.ico'), ('/resource:' + (Join-Path $PSScriptRoot 'assets\whale.png') + ',WhalePack.whale.png'))
$exePath = Join-Path $OutputDirectory '大肥鱼打包助手.exe'
& $compilerPath @sharedArgs /target:winexe ('/out:' + $exePath) ('/win32icon:' + (Join-Path $PSScriptRoot 'assets\icon.ico')) ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')) @sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
# Runtime switches are initialized in Program.ConfigureRuntime; no product sidecar is emitted.
if (-not $SkipTests) {
    $testFiles = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' | ForEach-Object FullName)
    $testExe = Join-Path $OutputDirectory 'WhalePack.Tests.exe'
    & $compilerPath @sharedArgs /target:exe /main:WhalePack.Tests.TestRunner ('/out:' + $testExe) ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')) @sourceFiles @testFiles
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
Write-Output ('Built v0.1.1: ' + $exePath)
