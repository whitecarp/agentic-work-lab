param([Parameter(Mandatory=$true)][string]$IntegrationDirectory, [Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$exe=Join-Path $root 'artifacts\NativeUiTests.exe'
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source=@(Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName) + @([IO.Path]::GetFullPath((Join-Path $root '..\..\shared\UpdateChecker.cs')))
$test=@(Get-ChildItem $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName)
& $csc /nologo /langversion:5 /target:exe /main:WhalePack.Tests.TestRunner ('/out:'+$exe) ('/win32manifest:'+(Join-Path $root 'app.manifest')) /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ('/resource:'+(Join-Path $root 'assets\icon.ico')+',WhalePack.icon.ico') ('/resource:'+(Join-Path $root 'assets\whale.png')+',WhalePack.whale.png') @source @test
if($LASTEXITCODE -ne 0){throw 'Native DPI test compile failed'}
& $exe --screenshots $IntegrationDirectory $OutputDirectory --native
if($LASTEXITCODE -ne 0){throw 'Native DPI rendering failed'}
