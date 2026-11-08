param([Parameter(Mandatory=$true)][string]$OutputDirectory, [string]$PythonPath = '')
$ErrorActionPreference = 'Stop'
$translatorRoot = Split-Path -Parent $PSScriptRoot
$translatorOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $translatorOutput -Force | Out-Null
$translatorCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$translatorWpf = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
$translatorRefs = @('/nologo','/target:exe','/r:System.Core.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll',"/r:$translatorWpf\UIAutomationClient.dll","/r:$translatorWpf\UIAutomationTypes.dll","/r:$translatorWpf\WindowsBase.dll")
$translatorSources = @('src\WindowIcon.cs','src\Program.cs','src\CompanionForm.cs','src\SettingsPanel.cs','src\Favorites.cs','src\FavoritesForm.cs','src\Ui.cs','src\SourceWindow.cs','src\SourceWindowPickerForm.cs','src\SelectionClipboard.cs','src\ComposeForm.cs','src\WhaleBadge.cs','src\Theme.cs','src\SourceHighlighter.cs','src\SelectionWatcher.cs','src\LanguagePacks.cs','src\Glossary.cs','src\TranslationHistory.cs','..\..\shared\UpdateChecker.cs')
$translatorPriorData = $env:DAFEIYU_DATA_DIR
$translatorPriorEncoding = $env:PYTHONIOENCODING
$translatorResults = New-Object 'Collections.Generic.List[object]'
Push-Location -LiteralPath $translatorRoot
try {
    $env:PYTHONIOENCODING = 'utf-8'
    $translatorSuites = @('AppTests','ComposeOfflineRetryTests','HighlightTests','LanguagePackModeTests','OfflineModeTests','OfflineRetryTests','SelectionClipboardTests','SelectionWatcherTests','WindowLockTests','WindowPickerTests','ReaderTests','SelectionReaderTests','SelectionReaderIntegrationTests','ScreenWindowPickerTests')
    foreach($translatorOptional in @('MainUiTests','ThemeUiTests','FavoritesTests','FavoritesFormTests','FavoriteButtonTests')) {
        if(Test-Path -LiteralPath (Join-Path 'tests' ($translatorOptional+'.cs'))) { $translatorSuites += $translatorOptional }
    }
    foreach($translatorSuite in $translatorSuites) {
        $translatorExe = Join-Path $translatorOutput ($translatorSuite+'.exe')
        $translatorLog = Join-Path $translatorOutput ($translatorSuite+'.txt')
        $env:DAFEIYU_DATA_DIR = Join-Path $translatorOutput ($translatorSuite+'-data')
        $translatorSuiteSources = $translatorSources
        $translatorOptions = @('/resource:resources\window-icon.ico,Dafeiyu.WindowIcon.ico')
        if($translatorSuite -eq 'ReaderTests') { $translatorSuiteSources=@('src\ClaudeReader.cs') }
        if($translatorSuite -in @('SelectionReaderTests','SelectionReaderIntegrationTests')) { $translatorSuiteSources=@('src\SelectionReader.cs') }
        if($translatorSuite -eq 'ScreenWindowPickerTests') { $translatorSuiteSources=@('src\ScreenWindowPicker.cs'); $translatorOptions += '/define:PICKER_ISOLATED_TESTS' }
        & $translatorCompiler @translatorRefs @translatorOptions "/main:$translatorSuite" "/out:$translatorExe" @translatorSuiteSources (Join-Path 'tests' ($translatorSuite+'.cs')) 2>&1 | Tee-Object -FilePath $translatorLog
        if($LASTEXITCODE -ne 0) { $translatorResults.Add(@{Suite=$translatorSuite;Stage='compile';Passed=$false}); continue }
        $translatorArgs=@(); if($translatorSuite -eq 'AppTests') { $translatorArgs=@('--offline') }
        & $translatorExe @translatorArgs 2>&1 | Tee-Object -FilePath $translatorLog -Append
        $translatorResults.Add(@{Suite=$translatorSuite;Stage='run';Passed=($LASTEXITCODE -eq 0)})
    }
    try {
        & .\tests\run-glossary-history.ps1 -OutputDirectory (Join-Path $translatorOutput 'glossary') -PythonPath $PythonPath 2>&1 | Tee-Object -FilePath (Join-Path $translatorOutput 'GlossaryHistoryTests.txt')
        $translatorResults.Add(@{Suite='GlossaryHistoryTests';Passed=$true})
    } catch { $translatorResults.Add(@{Suite='GlossaryHistoryTests';Passed=$false}); Write-Warning $_.Exception.Message }
    try {
        & .\packaging\run-launcher-tests.ps1 2>&1 | Tee-Object -FilePath (Join-Path $translatorOutput 'PortableLauncherTests.txt')
        $translatorResults.Add(@{Suite='PortableLauncherTests';Passed=$true})
    } catch { $translatorResults.Add(@{Suite='PortableLauncherTests';Passed=$false}); Write-Warning $_.Exception.Message }
    if($PythonPath) {
        # Native unittest output uses stderr; merge without treating it as a PowerShell terminating error.
        $translatorErrorMode=$ErrorActionPreference; $ErrorActionPreference='Continue'
        try { & $PythonPath -B -m unittest discover -s tests -p 'test_*.py' -v 2>&1 | Tee-Object -FilePath (Join-Path $translatorOutput 'python-tests.txt'); $translatorPythonExit=$LASTEXITCODE }
        finally { $ErrorActionPreference=$translatorErrorMode }
        $translatorResults.Add(@{Suite='Python';Passed=($translatorPythonExit -eq 0)})
    }
    $translatorResults.ToArray() | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $translatorOutput 'results.json') -Encoding UTF8
    if(@($translatorResults | Where-Object { -not $_.Passed }).Count) { throw 'Regression failures; see results.json and per-suite logs.' }
    Write-Output ('All '+$translatorResults.Count+' regression suites passed. Fixtures use isolated preferences and no live Google requests.')
} finally { $env:DAFEIYU_DATA_DIR=$translatorPriorData; $env:PYTHONIOENCODING=$translatorPriorEncoding; Pop-Location }
