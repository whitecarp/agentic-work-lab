param([string]$OutputDirectory=(Join-Path ([IO.Path]::GetTempPath()) ('WhaleDnsLeakMeasurementTests-'+[Guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
$taskProject=Split-Path -Parent $PSScriptRoot
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$taskOutput=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'DnsLeakMeasurementTests.exe'
$taskSources=@((Join-Path $taskProject 'src\DnsLeakMeasurementService.cs'),(Join-Path $PSScriptRoot 'DnsLeakMeasurementTests.cs'))
& $taskCompiler /nologo /langversion:5 /optimize+ /target:exe /platform:anycpu /main:DnsLeakMeasurementTests "/out:$taskOutput" /r:System.Core.dll /r:System.Web.Extensions.dll $taskSources
if ($LASTEXITCODE -ne 0) { throw 'DNS leak measurement tests did not compile' }
# Measurement calls use only an injected in-memory transport and virtual clock.
& $taskOutput
if ($LASTEXITCODE -ne 0) { throw 'DNS leak measurement tests failed' }
Get-Item -LiteralPath $taskOutput | Select-Object FullName,Length
