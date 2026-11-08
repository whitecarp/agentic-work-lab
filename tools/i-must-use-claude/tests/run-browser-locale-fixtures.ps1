$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskSources = Join-Path $taskRepo 'apps/whale-claude-helper/src'
$taskOutput = Join-Path ([IO.Path]::GetTempPath()) ('whale-browser-fixture-' + [guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $taskOutput | Out-Null
$taskExe = Join-Path $taskOutput 'BrowserLocaleFixtureTests.exe'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $taskCompiler /nologo /langversion:5 /target:exe "/out:$taskExe" /r:System.Core.dll /r:System.Web.Extensions.dll (Join-Path $taskSources 'BrowserLocaleService.cs') (Join-Path $taskSources 'RepairEngine.cs') (Join-Path $PSScriptRoot 'BrowserLocaleFixtureTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Browser locale fixture compilation failed.' }
& $taskExe
if ($LASTEXITCODE -ne 0) { throw 'Browser locale fixtures failed.' }
