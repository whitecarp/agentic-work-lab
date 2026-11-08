$ErrorActionPreference='Stop'
$taskApp=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskOut=Join-Path $taskApp 'artifacts\helper-060'
New-Item -ItemType Directory -Path $taskOut -Force | Out-Null
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskVb=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\Microsoft.VisualBasic.dll'
$taskSources=@(Get-ChildItem -LiteralPath (Join-Path $taskApp 'src') -Filter *.cs | Where-Object {$_.Name -ne 'HelperForm.cs'} | ForEach-Object FullName)
$taskArgs=@('/nologo','/langversion:5','/target:exe','/main:CodexProxyUiFixtureTests',('/out:'+(Join-Path $taskOut 'ui-tests.exe')),
    '/r:System.Core.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll','/r:System.Management.dll',('/r:'+$taskVb),
    ('/win32icon:'+(Join-Path $taskApp 'icon.ico')),('/resource:'+(Join-Path $taskApp 'assets\helper-mascot.png')+',HelperMascot'))
& $taskCompiler @taskArgs @taskSources (Join-Path $taskApp 'tests\CodexProxyUiFixtureTests.cs') (Join-Path $taskApp '..\..\shared\UpdateChecker.cs')
if($LASTEXITCODE -ne 0){throw 'Proxy UI fixture compilation failed'}
# Entry point constructs demo forms only; never calls Program.Main or displays a window.
& (Join-Path $taskOut 'ui-tests.exe') $taskOut
if($LASTEXITCODE -ne 0){throw 'Proxy UI fixture checks failed'}
