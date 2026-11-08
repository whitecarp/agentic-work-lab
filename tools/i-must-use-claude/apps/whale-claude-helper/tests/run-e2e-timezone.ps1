# Real time zone apply/undo through tzutil. Changes the machine's time zone: CI runners only.
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:WHALE_E2E_ALLOW_SYSTEM_CHANGES -ne '1' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'Refusing to run: this test changes the real Windows time zone and is meant only for disposable CI runners.'
}
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskOutput = Join-Path ([IO.Path]::GetTempPath()) ('WhaleTimeZoneE2E-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskOutput | Out-Null
$taskExecutable = Join-Path $taskOutput 'E2ETimeZoneTests.exe'
& $taskCompiler /nologo /langversion:5 /target:exe "/out:$taskExecutable" /r:System.Core.dll /r:System.Web.Extensions.dll (Join-Path $PSScriptRoot '..\src\SystemTools.cs') (Join-Path $PSScriptRoot 'E2ETimeZoneTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Time zone end-to-end test compilation failed' }
$taskReport = Join-Path $env:RUNNER_TEMP 'acceptance-results/timezone.json'
& $taskExecutable $taskReport
if ($LASTEXITCODE -ne 0) { throw 'Time zone end-to-end test failed' }
