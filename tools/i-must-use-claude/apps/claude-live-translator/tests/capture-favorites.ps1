param(
    [Parameter(Mandatory=$true)][string]$AppExe,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$favoriteOutput=[IO.Path]::GetFullPath($OutputDirectory)
$favoriteApp=(Resolve-Path -LiteralPath $AppExe).Path
New-Item -ItemType Directory -Path $favoriteOutput -Force | Out-Null
$favoriteCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$favoriteHarness=Join-Path $favoriteOutput 'FavoritesCaptureHarness.exe'
& $favoriteCompiler /nologo /target:exe /r:System.Drawing.dll /r:System.Windows.Forms.dll "/out:$favoriteHarness" (Join-Path $PSScriptRoot 'FavoritesCaptureHarness.cs')
if($LASTEXITCODE -ne 0){throw 'Favorite visual fixture compilation failed'}
foreach($favoritePercent in @(100,150)) {
    $favoriteScaleOutput=Join-Path $favoriteOutput ('render-'+$favoritePercent)
    & $favoriteHarness $favoriteApp $favoriteScaleOutput $favoritePercent
    if($LASTEXITCODE -ne 0){throw ('Favorite visual capture failed at '+$favoritePercent+'%')}
}
@{
    App=$favoriteApp
    Sha256=(Get-FileHash -LiteralPath $favoriteApp -Algorithm SHA256).Hash.ToLowerInvariant()
    CapturedUtc=[DateTime]::UtcNow.ToString('o')
    Method='Actual Windows Forms controls rendered in separate 96 DPI and 144 DPI thread contexts, DrawToBitmap. No image resampling; no global display setting changes.'
    Limits='Offscreen synthetic fixtures, classic capture title bars. Real desktop focus, taskbar and DWM composition are not checked.'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $favoriteOutput 'capture-receipt.json') -Encoding UTF8
