param(
    [Parameter(Mandatory=$true)][string]$AppExe,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$LauncherAssembly = ''
)
$ErrorActionPreference = 'Stop'
$captureRoot = Split-Path -Parent $PSScriptRoot
$captureOutput = [IO.Path]::GetFullPath($OutputDirectory)
$captureApp = (Resolve-Path -LiteralPath $AppExe).Path
New-Item -ItemType Directory -Path $captureOutput -Force | Out-Null
$captureCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$captureHarness = Join-Path $captureOutput 'UiCaptureHarness.exe'
& $captureCompiler /nologo /target:exe /r:System.Drawing.dll /r:System.Windows.Forms.dll "/out:$captureHarness" (Join-Path $PSScriptRoot 'UiCaptureHarness.cs')
if ($LASTEXITCODE -ne 0) { throw 'Visual fixture compilation failed.' }
$captureArgs = @($captureApp, $captureOutput)
if ($LauncherAssembly) { $captureArgs += (Resolve-Path -LiteralPath $LauncherAssembly).Path }
& $captureHarness @captureArgs
if ($LASTEXITCODE -ne 0) { throw 'Visual fixture capture failed.' }
$captureHash = (Get-FileHash -LiteralPath $captureApp -Algorithm SHA256).Hash.ToLowerInvariant()
@{App=$captureApp;Sha256=$captureHash;CapturedUtc=[DateTime]::UtcNow.ToString('o');Method='Offscreen synthetic Windows Forms fixtures; DrawToBitmap; no system display setting changes';ScaleCopies='100/125/150 are resampled image previews, not independent OS DPI runs'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $captureOutput 'build-receipt.json') -Encoding UTF8
Write-Output "Visual fixtures saved to $captureOutput"
