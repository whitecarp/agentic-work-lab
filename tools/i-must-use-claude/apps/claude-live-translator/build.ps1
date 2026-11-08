param([string]$OutputDirectory=$PSScriptRoot)
$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot
$compilerPath = (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe')
$wpfPath = (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF')
Push-Location -LiteralPath $projectPath
try {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    & $compilerPath /nologo /target:winexe "/out:$OutputDirectory\ClaudeChinese.exe" /win32icon:icon.ico /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll "/r:$wpfPath\UIAutomationClient.dll" "/r:$wpfPath\UIAutomationTypes.dll" "/r:$wpfPath\WindowsBase.dll" /resource:resources\window-icon.ico,Dafeiyu.WindowIcon.ico src\WindowIcon.cs src\Program.cs src\CompanionForm.cs src\SettingsPanel.cs src\Favorites.cs src\FavoritesForm.cs src\Ui.cs src\SourceWindow.cs src\SourceWindowPickerForm.cs src\SelectionClipboard.cs src\ComposeForm.cs src\WhaleBadge.cs src\Theme.cs src\SourceHighlighter.cs src\SelectionWatcher.cs src\LanguagePacks.cs src\Glossary.cs src\TranslationHistory.cs ..\..\shared\UpdateChecker.cs
    if ($LASTEXITCODE -ne 0) { throw 'App compilation failed' }
    & $compilerPath /nologo /target:exe "/out:$OutputDirectory\ClaudeReader.exe" "/r:$wpfPath\UIAutomationClient.dll" "/r:$wpfPath\UIAutomationTypes.dll" "/r:$wpfPath\WindowsBase.dll" src\ClaudeReader.cs
    if ($LASTEXITCODE -ne 0) { throw 'Reader compilation failed' }
    & $compilerPath /nologo /target:exe "/out:$OutputDirectory\SelectionReader.exe" "/r:$wpfPath\UIAutomationClient.dll" "/r:$wpfPath\UIAutomationTypes.dll" "/r:$wpfPath\WindowsBase.dll" src\SelectionReader.cs
    if ($LASTEXITCODE -ne 0) { throw 'Selection reader compilation failed' }
    & $compilerPath /nologo /target:exe "/out:$OutputDirectory\ClaudeSender.exe" "/r:$wpfPath\UIAutomationClient.dll" "/r:$wpfPath\UIAutomationTypes.dll" "/r:$wpfPath\WindowsBase.dll" src\ClaudeSender.cs
    if ($LASTEXITCODE -ne 0) { throw 'Sender compilation failed' }
    Write-Output 'Build succeeded'
} finally { Pop-Location }
