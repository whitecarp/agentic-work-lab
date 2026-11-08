param([string]$OutputDirectory=(Join-Path $PSScriptRoot '..\artifacts\helper-tests'))
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest

# Explicit fixture entry points only. Never run Program.Main, the live diagnostic
# transport, UiReadOnlyTests, SystemToolsTests or FontServiceTests here.
$appRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot=Join-Path $appRoot 'artifacts'
$outputRoot=[IO.Path]::GetFullPath($OutputDirectory)
if(-not $outputRoot.StartsWith($artifactsRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Fixture output must stay below this app artifacts directory.'
}
foreach($candidate in @($appRoot,$outputRoot)) {
    $cursor=$candidate
    while($cursor) {
        if(Test-Path -LiteralPath $cursor) {
            if(((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Fixture paths must not follow reparse points.'
            }
        }
        $cursor=[IO.Path]::GetDirectoryName($cursor)
    }
}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not(Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler was not found.' }
$vb=Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Microsoft.VisualBasic.dll'
if(-not(Test-Path -LiteralPath $vb)) { $vb=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\Microsoft.VisualBasic.dll' }
if(-not(Test-Path -LiteralPath $vb)) { throw 'Microsoft.VisualBasic reference assembly was not found.' }

$uiSources=@('src\AssemblyInfo.cs','src\Program.cs','src\OptimizerForm.cs','src\NetworkAssistantForm.cs','src\NetworkDnsService.cs','src\DnsLeakMeasurementService.cs','src\MaintenanceForm.cs','src\CodexProxyForm.cs','src\ProcessCloseService.cs','src\ProcessCloseDialog.cs','src\ProxyDiscovery.cs','src\CodexProxySettings.cs','src\ScoreMeasurement.cs',
    'src\OptimizerService.cs','src\BrowserLocaleService.cs','src\ResultText.cs','src\RepairEngine.cs',
    'src\ReportExporter.cs','src\CacheService.cs','src\DiagnosticsService.cs','src\SystemTools.cs',
    'src\FontService.cs','src\Theme.cs','src\WhaleBadge.cs','tests\MaintenanceUiFixtureTests.cs','..\..\shared\UpdateChecker.cs')
$suites=@(
    @{Name='CacheServiceTests';EntryPoint='CacheServiceTests';Sources=@('src\CacheService.cs','tests\CacheServiceTests.cs')},
    @{Name='RepairEngineTests';EntryPoint='RepairEngineTests';Sources=@('src\RepairEngine.cs','src\ReportExporter.cs','tests\RepairEngineTests.cs')},
    @{Name='DiagnosticsServiceTests';EntryPoint='WhaleClaudeHelper.DiagnosticsServiceTests';Sources=@('src\DiagnosticsService.cs','tests\DiagnosticsServiceTests.cs')},
    @{Name='MaintenanceUiFixtureTests';EntryPoint='MaintenanceUiFixtureTests';Sources=$uiSources}
)
foreach($suite in $suites) {
    foreach($source in $suite.Sources) { if(-not(Test-Path -LiteralPath (Join-Path $appRoot $source))) { throw ('Missing fixture source: '+$source) } }
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
# Keep generated fixture paths short for .NET Framework's MAX_PATH behavior.
# The location is newly created, never points at Claude configuration, and is retained as evidence.
$fixtureRoot=Join-Path ([IO.Path]::GetTempPath()) ('whale-fixtures-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
$results=@()
foreach($suite in $suites) {
    $exe=Join-Path $outputRoot ($suite.Name+'.exe')
    $arguments=@('/nologo','/target:exe','/optimize+','/platform:anycpu',('/main:'+$suite.EntryPoint),('/out:'+$exe),
        '/r:System.Core.dll','/r:System.Web.Extensions.dll','/r:System.Management.dll',
        '/r:System.Windows.Forms.dll','/r:System.Drawing.dll',('/r:'+$vb))
    $arguments+=@($suite.Sources | ForEach-Object { Join-Path $appRoot $_ })
    & $compiler @arguments
    if($LASTEXITCODE -ne 0) { throw ($suite.Name+' fixture compilation failed.') }
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=$exe
    $start.WorkingDirectory=$fixtureRoot
    $start.UseShellExecute=$false
    $start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true
    $start.RedirectStandardError=$true
    $start.StandardOutputEncoding=[Text.Encoding]::Default
    $start.StandardErrorEncoding=[Text.Encoding]::Default
    $start.EnvironmentVariables['TEMP']=$fixtureRoot
    $start.EnvironmentVariables['TMP']=$fixtureRoot
    $process=New-Object Diagnostics.Process
    $process.StartInfo=$start
    try {
        [void]$process.Start()
        $stdout=$process.StandardOutput.ReadToEndAsync()
        $stderr=$process.StandardError.ReadToEndAsync()
        if(-not $process.WaitForExit(60000)) {
            $process.Kill()
            throw ($suite.Name+' exceeded the 60-second fixture limit.')
        }
        $text=$stdout.GetAwaiter().GetResult()
        $errors=$stderr.GetAwaiter().GetResult()
        if($text) { Write-Output $text.TrimEnd() }
        if($errors) { Write-Output $errors.TrimEnd() }
        $exitCode=$process.ExitCode
        $results+=[pscustomobject]@{Suite=$suite.Name;ExitCode=$exitCode;StandardOutput=$text;StandardError=$errors}
        $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputRoot 'fixture-results.json') -Encoding UTF8
        if($exitCode -ne 0) { throw ($suite.Name+' failed. See the retained fixture results.') }
    } finally { $process.Dispose() }
}
Write-Output ('All four isolated fixture suites passed. Output: '+$outputRoot)
Write-Output ('Synthetic fixture evidence retained at: '+$fixtureRoot)
