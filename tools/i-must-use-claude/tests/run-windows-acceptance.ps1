param([string]$OutputDirectory, [switch]$CompileOnly)
$ErrorActionPreference = 'Stop'
if (-not $CompileOnly) {
    if ($env:GITHUB_ACTIONS -ne 'true' -or $env:WHALE_E2E_ALLOW_SYSTEM_CHANGES -ne '1' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:RUNNER_OS -ne 'Windows') {
        throw 'Refusing real acceptance: explicitly enabled GitHub-hosted Windows runners only.'
    }
    if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { throw 'RUNNER_TEMP is required.' }
    $OutputDirectory = Join-Path $env:RUNNER_TEMP ('whale-acceptance-' + [guid]::NewGuid().ToString('N').Substring(0,8))
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { throw 'CompileOnly requires an explicit isolated OutputDirectory.' }
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
$taskBuild = $taskOutput + '-build'
if (Test-Path -LiteralPath $taskBuild) { throw 'Build directory must be new.' }
New-Item -ItemType Directory -Path $taskBuild | Out-Null
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskSource = Join-Path $taskRepo 'apps/whale-claude-helper/src'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskVb = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8/Microsoft.VisualBasic.dll'
if (-not (Test-Path -LiteralPath $taskVb)) { $taskVb = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/Microsoft.VisualBasic.dll' }
$taskSources = @('BrowserLocaleService.cs','RepairEngine.cs','FontService.cs','CacheService.cs','NetworkDnsService.cs','CodexProxySettings.cs') | ForEach-Object { Join-Path $taskSource $_ }
$taskExe = Join-Path $taskBuild 'WindowsAcceptance.exe'
& $taskCompiler /nologo /langversion:5 /target:exe /main:WindowsAcceptance "/out:$taskExe" /r:System.Core.dll /r:System.Drawing.dll /r:System.Management.dll /r:System.Web.Extensions.dll "/r:$taskVb" $taskSources (Join-Path $PSScriptRoot 'WindowsAcceptance.cs')
if ($LASTEXITCODE -ne 0) { throw 'Windows acceptance compilation failed.' }
if ($CompileOnly) { Write-Output "Compiled only; no acceptance operation ran: $taskExe"; return }
try {
    & $taskExe $taskOutput
    if ($LASTEXITCODE -ne 0) { throw "Windows acceptance failed (exit $LASTEXITCODE). Review acceptance-results.json." }
} finally {
    if (Test-Path -LiteralPath (Join-Path $taskOutput 'acceptance-results.json')) {
        $taskReports = Join-Path $env:RUNNER_TEMP 'acceptance-results'
        New-Item -ItemType Directory -Path $taskReports -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $taskOutput 'acceptance-results.json') -Destination (Join-Path $taskReports 'workflows.json')
    }
}
