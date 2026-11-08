$ErrorActionPreference = 'Stop'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskVb = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Microsoft.VisualBasic.dll'
if (-not (Test-Path -LiteralPath $taskVb)) {
    $taskVb = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\Microsoft.VisualBasic.dll'
}
$taskOutput = Join-Path ([System.IO.Path]::GetTempPath()) ('WhaleCodexProxyTests-' + [guid]::NewGuid().ToString('N') + '.exe')
$taskSource = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\CodexProxySettings.cs'))
$taskTests = Join-Path $PSScriptRoot 'CodexProxySettingsTests.cs'
& $taskCompiler /nologo /optimize+ /target:exe /langversion:5 "/out:$taskOutput" /r:System.Core.dll /r:System.Web.Extensions.dll "/r:$taskVb" $taskSource $taskTests
if ($LASTEXITCODE -ne 0) { throw 'Codex proxy fixture compilation failed.' }
& $taskOutput
if ($LASTEXITCODE -ne 0) { throw 'Codex proxy fixtures failed.' }
