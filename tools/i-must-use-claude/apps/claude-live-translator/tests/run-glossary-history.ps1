param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\glossary-history-tests'),[string]$PythonPath = '')
$ErrorActionPreference='Stop'
$translatorSourceRoot=Split-Path -Parent $PSScriptRoot
$translatorCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$translatorWpf=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
$translatorTestOutput=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $translatorTestOutput -Force | Out-Null
$translatorTestExe=Join-Path $translatorTestOutput 'GlossaryHistoryTests.exe'
$translatorPriorData=$env:DAFEIYU_DATA_DIR
$translatorPriorBackend=$env:DAFEIYU_FIXTURE_BACKEND
Push-Location -LiteralPath $translatorSourceRoot
try {
    & $translatorCompiler /nologo /target:exe /main:GlossaryHistoryTests "/out:$translatorTestExe" /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll "/r:$translatorWpf\UIAutomationClient.dll" "/r:$translatorWpf\UIAutomationTypes.dll" "/r:$translatorWpf\WindowsBase.dll" /resource:resources\window-icon.ico,Dafeiyu.WindowIcon.ico src\WindowIcon.cs src\Program.cs src\CompanionForm.cs src\SettingsPanel.cs src\Favorites.cs src\FavoritesForm.cs src\Ui.cs src\SourceWindow.cs src\SourceWindowPickerForm.cs src\SelectionClipboard.cs src\ComposeForm.cs src\WhaleBadge.cs src\Theme.cs src\SourceHighlighter.cs src\SelectionWatcher.cs src\LanguagePacks.cs src\Glossary.cs src\TranslationHistory.cs ..\..\shared\UpdateChecker.cs tests\GlossaryHistoryTests.cs
    if($LASTEXITCODE -ne 0){throw 'Glossary/history fixture compilation failed.'}
    $env:DAFEIYU_DATA_DIR=Join-Path $translatorTestOutput ('glossary-history-fixture-'+[Guid]::NewGuid().ToString('N'))
    $env:DAFEIYU_FIXTURE_BACKEND=''
    if($PythonPath){
        $translatorPython=(Get-Command $PythonPath -ErrorAction Stop).Source
        $translatorBackend=Join-Path $translatorTestOutput 'backend'
        New-Item -ItemType Directory -Path $translatorBackend -Force | Out-Null
        $translatorEncoding=New-Object Text.UTF8Encoding($false)
        [IO.File]::WriteAllText((Join-Path $translatorBackend 'launcher.json'),(@{python=$translatorPython;script='backend/google_worker.py'}|ConvertTo-Json -Compress),$translatorEncoding)
        $translatorFixture=@'
import os, sys, socket
sys.path.insert(0, os.environ['DAFEIYU_FIXTURE_BACKEND'])
def blocked(*args, **kwargs):
    raise AssertionError('Network forbidden in fixture')
socket.create_connection = blocked
socket.socket.connect = blocked
import google_worker as g
factory = g.GoogleTranslator
g.GoogleTranslator = lambda: factory(lambda text, direction: text, 0)
g.main()
'@
        [IO.File]::WriteAllText((Join-Path $translatorBackend 'google_worker.py'),$translatorFixture,$translatorEncoding)
        $translatorFixture=@'
import os, sys, socket
sys.path.insert(0, os.environ['DAFEIYU_FIXTURE_BACKEND'])
def blocked(*args, **kwargs):
    raise AssertionError('Network forbidden in fixture')
socket.create_connection = blocked
socket.socket.connect = blocked
import offline_worker as o
factory = o.OfflineTranslator
o.OfflineTranslator = lambda: factory(lambda text, direction: text)
o.installed_packages = lambda: []
o.main()
'@
        [IO.File]::WriteAllText((Join-Path $translatorBackend 'offline_worker.py'),$translatorFixture,$translatorEncoding)
        $env:DAFEIYU_FIXTURE_BACKEND=Join-Path $translatorSourceRoot 'backend'
    }
    & $translatorTestExe
    if($LASTEXITCODE -ne 0){throw 'Glossary/history fixture tests failed.'}
} finally {
    $env:DAFEIYU_DATA_DIR=$translatorPriorData
    $env:DAFEIYU_FIXTURE_BACKEND=$translatorPriorBackend
    Pop-Location
}
