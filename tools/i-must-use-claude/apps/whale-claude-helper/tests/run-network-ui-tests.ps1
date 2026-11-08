param([string]$OutputDirectory=(Join-Path ([IO.Path]::GetTempPath()) ('WhaleNetworkUiTests-'+[Guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
$taskProject=Split-Path -Parent $PSScriptRoot
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskVb=Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Microsoft.VisualBasic.dll'
if (-not (Test-Path -LiteralPath $taskVb)) { $taskVb=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\Microsoft.VisualBasic.dll' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$taskOutput=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'NetworkUiTests.exe'
$taskSources=@(Get-ChildItem -LiteralPath (Join-Path $taskProject 'src') -Filter *.cs | Where-Object { $_.Name -ne 'HelperForm.cs' } | ForEach-Object FullName)
$taskSources+=Join-Path $taskProject '..\..\shared\UpdateChecker.cs'
$taskSources+=Join-Path $PSScriptRoot 'NetworkUiTests.cs'
& $taskCompiler /nologo /langversion:5 /optimize+ /target:exe /platform:anycpu /main:NetworkUiTests "/out:$taskOutput" "/win32icon:$taskProject\icon.ico" "/resource:$taskProject\assets\helper-mascot.png,HelperMascot" /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Management.dll "/r:$taskVb" $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Network UI tests did not compile' }
# The test creates only hidden demo forms and never calls Show, ShowDialog, or Application.Run.
& $taskOutput (Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'renders')
if ($LASTEXITCODE -ne 0) { throw 'Network UI tests failed' }
