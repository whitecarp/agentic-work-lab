$ErrorActionPreference = 'Stop'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskOutput = Join-Path ([IO.Path]::GetTempPath()) ('WhaleProxyFixtures-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskOutput | Out-Null
$taskExecutable = Join-Path $taskOutput 'ProxyDiscoveryTests.exe'
& $taskCompiler /nologo /langversion:5 /target:exe "/out:$taskExecutable" /r:System.Core.dll (Join-Path $PSScriptRoot '..\src\ProxyDiscovery.cs') (Join-Path $PSScriptRoot 'ProxyDiscoveryTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Proxy fixture compilation failed' }
& $taskExecutable
if ($LASTEXITCODE -ne 0) { throw 'Proxy discovery fixtures failed' }
Write-Output ('Fixture executable: ' + $taskExecutable)
