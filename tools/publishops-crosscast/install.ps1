param([string]$Python)
$ErrorActionPreference = 'Stop'
$runtimeRoot = Join-Path $env:LOCALAPPDATA 'WhiteCarpVideoPublisher'
$venvPython = Join-Path $runtimeRoot 'venv/Scripts/python.exe'
if (-not (Test-Path -LiteralPath $venvPython)) {
    $candidates = @()
    if ($Python) { $candidates += $Python }
    $candidates += (Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')
    $foundPython = Get-Command python -ErrorAction SilentlyContinue
    if ($foundPython) { $candidates += $foundPython.Source }
    $selectedPython = $null
    foreach ($candidate in $candidates) {
        if (-not (Test-Path -LiteralPath $candidate)) { continue }
        & $candidate -c 'import sys; sys.exit(0 if (3,10) <= sys.version_info[:2] < (3,13) else 1)' 2>$null
        if ($LASTEXITCODE -eq 0) { $selectedPython = $candidate; break }
    }
    if (-not $selectedPython) { throw 'Python 3.10-3.12 required. Ask your agent to install it, then pass -Python FULL_PATH.' }
    New-Item -ItemType Directory -Force -Path $runtimeRoot | Out-Null
    & $selectedPython -m venv (Join-Path $runtimeRoot 'venv')
    if ($LASTEXITCODE -ne 0) { throw 'Virtual environment creation failed.' }
}
& $venvPython -m pip uninstall -y whitecarp-video-publisher
if ($LASTEXITCODE -ne 0) { throw 'Legacy package upgrade failed; runtime data was retained.' }
& $venvPython -m pip install $PSScriptRoot
if ($LASTEXITCODE -ne 0) { throw 'Package installation failed. No login or publication was attempted.' }
& $venvPython -m video_publisher doctor
if ($LASTEXITCODE -ne 0) { throw 'Local dependency check failed.' }
Write-Output ('Command: ' + (Join-Path $runtimeRoot 'venv/Scripts/vpub.exe'))
