param([string]$OutputDirectory=(Join-Path ([IO.Path]::GetTempPath()) ('WhaleNetworkDnsTests-'+[Guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
$taskProject=Split-Path -Parent $PSScriptRoot
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$taskOutput=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'NetworkDnsTests.exe'
$taskSources=@((Join-Path $taskProject 'src\NetworkDnsService.cs'),(Join-Path $PSScriptRoot 'NetworkDnsTests.cs'))
& $taskCompiler /nologo /langversion:5 /optimize+ /target:exe /platform:anycpu /main:NetworkDnsTests "/out:$taskOutput" /r:System.Core.dll /r:System.Web.Extensions.dll $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Network DNS tests did not compile' }
# Only the injected constructor is used by this fixture-only test entry point.
& $taskOutput
if ($LASTEXITCODE -ne 0) { throw 'Network DNS tests failed' }
Get-Item -LiteralPath $taskOutput | Select-Object FullName,Length
