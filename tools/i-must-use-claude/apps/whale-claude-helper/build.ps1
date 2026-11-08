param([string]$OutputDirectory=(Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference='Stop'
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskVb=Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Microsoft.VisualBasic.dll'
if (-not (Test-Path -LiteralPath $taskVb)) { $taskVb=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\Microsoft.VisualBasic.dll' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Push-Location -LiteralPath $PSScriptRoot
try {
 & $taskCompiler /nologo /langversion:5 /optimize+ /target:winexe /platform:anycpu "/out:$OutputDirectory\大肥鱼环境助手.exe" /win32icon:icon.ico /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Management.dll "/r:$taskVb" /resource:third-party\FuckClaude-LICENSE.txt,UpstreamLicense /resource:assets\score-page.html,ScorePage /resource:assets\helper-mascot.png,HelperMascot src\AssemblyInfo.cs src\Program.cs src\OptimizerForm.cs src\NetworkAssistantForm.cs src\NetworkDnsService.cs src\DnsLeakMeasurementService.cs src\MaintenanceForm.cs src\CodexProxyForm.cs src\ProcessCloseService.cs src\ProcessCloseDialog.cs src\ProxyDiscovery.cs src\CodexProxySettings.cs src\DiagnosticsService.cs src\ReportExporter.cs src\ScoreMeasurement.cs src\OptimizerService.cs src\BrowserLocaleService.cs src\ResultText.cs src\RepairEngine.cs src\CacheService.cs src\SystemTools.cs src\FontService.cs src\Theme.cs src\WhaleBadge.cs ..\..\shared\UpdateChecker.cs
 if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
 Get-Item -LiteralPath (Join-Path $OutputDirectory '大肥鱼环境助手.exe') | Select-Object FullName,Length
} finally { Pop-Location }
