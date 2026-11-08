$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$output=Join-Path $repoRoot 'artifacts\updater-tests'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe ("/out:"+(Join-Path $output 'UpdateCheckerTests.exe')) /r:System.Core.dll /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll (Join-Path $repoRoot 'shared\UpdateChecker.cs') (Join-Path $PSScriptRoot 'UpdateCheckerTests.cs')
if($LASTEXITCODE -ne 0){throw 'Updater fixture build failed'}
& (Join-Path $output 'UpdateCheckerTests.exe')
if($LASTEXITCODE -ne 0){throw 'Updater fixture tests failed'}
