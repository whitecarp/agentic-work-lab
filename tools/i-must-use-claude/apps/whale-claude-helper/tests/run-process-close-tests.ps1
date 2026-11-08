$ErrorActionPreference = 'Stop'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskOutput = Join-Path ([IO.Path]::GetTempPath()) ('WhaleProcessCloseFixtures-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskOutput | Out-Null
$taskExecutable = Join-Path $taskOutput 'ProcessCloseTests.exe'
$taskIcon = Join-Path $PSScriptRoot '..\icon.ico'
& $taskCompiler /nologo /langversion:5 /define:PROCESS_CLOSE_FIXTURE /target:exe "/out:$taskExecutable" "/win32icon:$taskIcon" /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll (Join-Path $PSScriptRoot '..\src\ProcessCloseService.cs') (Join-Path $PSScriptRoot '..\src\ProcessCloseDialog.cs') (Join-Path $PSScriptRoot '..\src\Theme.cs') (Join-Path $PSScriptRoot 'ProcessCloseTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Process-close fixture compilation failed' }
& $taskExecutable
if ($LASTEXITCODE -ne 0) { throw 'Process-close fixtures failed' }
Write-Output ('Fixture executable: ' + $taskExecutable)
