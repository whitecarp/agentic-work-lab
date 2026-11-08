param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$Version = '0.9.6',
    [string]$ApplicationExe = 'ClaudeChinese.exe',
    [string]$OutputName = '大肥鱼翻译.exe',
    [string]$VCRedistDirectory = '',
    [string]$RuntimeRoot = '',
    [string]$BuildRoot = '',
    [switch]$StageOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectDirectory = [IO.Path]::GetFullPath($ProjectRoot)
$packagingDirectory = Join-Path $projectDirectory 'packaging'
$portableBuildRoot = if ($BuildRoot) { [IO.Path]::GetFullPath($BuildRoot) } else { Join-Path $projectDirectory 'artifacts\portable-build' }
$runtimeDirectory = if ($RuntimeRoot) { [IO.Path]::GetFullPath($RuntimeRoot) } else { $projectDirectory }
$buildDirectory = Join-Path $portableBuildRoot ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
$stageDirectory = Join-Path $buildDirectory 'stage'
$utf8 = New-Object Text.UTF8Encoding($false)
if ($Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw 'Version must contain three or four numeric components.' }
if ([IO.Path]::GetFileName($OutputName) -ne $OutputName -or -not $OutputName.EndsWith('.exe',[StringComparison]::OrdinalIgnoreCase)) { throw 'OutputName must be an EXE filename, not a path.' }
[IO.Directory]::CreateDirectory($stageDirectory) | Out-Null

function Assert-PlainPath([string]$Path) {
    $cursor = [IO.Path]::GetFullPath($Path)
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            if (((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Filesystem links are not packaged: $Path" }
        }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
}
function Copy-PayloadFile([string]$Source,[string]$RelativePath) {
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) { throw "Required payload file is missing: $Source" }
    Assert-PlainPath $Source
    $destination = [IO.Path]::GetFullPath((Join-Path $stageDirectory $RelativePath))
    if (-not $destination.StartsWith($stageDirectory + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Payload destination escaped the staging directory.' }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    [IO.File]::Copy($Source,$destination,$true)
}
function Copy-PayloadTree([string]$Source,[string]$RelativeRoot) {
    if (-not (Test-Path -LiteralPath $Source -PathType Container)) { throw "Required payload directory is missing: $Source" }
    Assert-PlainPath $Source
    # No package manager state, credentials, bytecode caches, or training/test data.
    $pending = New-Object 'Collections.Generic.Stack[string]'
    $pending.Push([IO.Path]::GetFullPath($Source))
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        foreach ($item in Get-ChildItem -LiteralPath $directory -Force) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Filesystem links are not packaged: $($item.FullName)" }
            if ($item.PSIsContainer) {
                if ($item.Name -notin @('__pycache__','tests','test','benchmarks','.git','.pytest_cache')) { $pending.Push($item.FullName) }
            } elseif ($item.Extension -notin @('.pyc','.pyo') -and $item.Name -notin @('direct_url.json','INSTALLER','REQUESTED')) {
                $relative = $item.FullName.Substring(([IO.Path]::GetFullPath($Source)).Length).TrimStart('\','/')
                Copy-PayloadFile $item.FullName (Join-Path $RelativeRoot $relative)
            }
        }
    }
}

$applicationSource = if ([IO.Path]::IsPathRooted($ApplicationExe)) { $ApplicationExe } else { Join-Path $projectDirectory $ApplicationExe }
Copy-PayloadFile $applicationSource 'ClaudeChinese.exe'
foreach ($name in @('ClaudeReader.exe','SelectionReader.exe','ClaudeSender.exe')) { Copy-PayloadFile (Join-Path ([IO.Path]::GetDirectoryName($applicationSource)) $name) $name }
foreach ($name in @('google_worker.py','offline_worker.py','glossary.py','package_manager.py','package_store.py','argos_catalog.json','launcher.json','MODEL-NOTICES.md')) {
    Copy-PayloadFile (Join-Path $projectDirectory ('backend\' + $name)) ('backend\' + $name)
}

# The embeddable Python distribution is a closed allowlist of top-level files.
$pythonSource = Join-Path $runtimeDirectory 'runtime\python'
foreach ($file in Get-ChildItem -LiteralPath $pythonSource -File) {
    if ($file.Extension -in @('.dll','.pyd') -or $file.Name -in @('python.exe','pythonw.exe','python312.zip','python312._pth','LICENSE.txt','python.cat')) {
        Copy-PayloadFile $file.FullName ('runtime\python\' + $file.Name)
    }
}
foreach ($name in @('python.exe','python312.dll','python312.zip','LICENSE.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $stageDirectory ('runtime\python\' + $name)))) { throw "Incomplete embeddable Python runtime: $name" }
}
# Bind imports only to the bundled standard library and selected packages, never global Python.
[IO.File]::WriteAllText((Join-Path $stageDirectory 'runtime\python\python312._pth'),"python312.zip`n.`n../venv/Lib/site-packages`nimport site`n",$utf8)
$siteSource = Join-Path $runtimeDirectory 'runtime\venv\Lib\site-packages'
foreach ($package in @('ctranslate2','sentencepiece','numpy','numpy.libs','yaml','_yaml')) {
    Copy-PayloadTree (Join-Path $siteSource $package) ('runtime\venv\Lib\site-packages\' + $package)
}
foreach ($distribution in @('ctranslate2','sentencepiece','numpy','pyyaml')) {
    $metadata = @(Get-ChildItem -LiteralPath $siteSource -Directory | Where-Object { $_.Name -like ($distribution + '-*.dist-info') })
    if ($metadata.Count -ne 1) { throw "Expected exactly one installed metadata directory for $distribution." }
    Copy-PayloadTree $metadata[0].FullName ('runtime\venv\Lib\site-packages\' + $metadata[0].Name)
}
# Language models are downloaded only after an explicit request in the app.
# Keep their attribution in packaging/licenses; never copy model data into this payload.

# Use only Microsoft's installed redistributable source, never system32 copies.
# VS 2022 is normally under Program Files; earlier versions use Program Files (x86).
$visualStudioRoots = @()
foreach ($programFilesRoot in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:ProgramW6432)) {
    if ([string]::IsNullOrWhiteSpace($programFilesRoot)) { continue }
    $visualStudioRoot = [IO.Path]::GetFullPath((Join-Path $programFilesRoot 'Microsoft Visual Studio'))
    if ($visualStudioRoots -notcontains $visualStudioRoot) { $visualStudioRoots += $visualStudioRoot }
}
if (-not $VCRedistDirectory) {
    $candidates = @()
    foreach ($visualStudioRoot in $visualStudioRoots) {
        if (-not (Test-Path -LiteralPath $visualStudioRoot -PathType Container)) { continue }
        foreach ($year in Get-ChildItem -LiteralPath $visualStudioRoot -Directory) {
            foreach ($edition in Get-ChildItem -LiteralPath $year.FullName -Directory) {
                $redist = Join-Path $edition.FullName 'VC\Redist\MSVC'
                if (-not (Test-Path -LiteralPath $redist)) { continue }
                foreach ($release in Get-ChildItem -LiteralPath $redist -Directory) {
                    if ($release.Name -notmatch '^\d+\.\d+\.\d+$') { continue }
                    $architecture = Join-Path $release.FullName 'x64'
                    if (Test-Path -LiteralPath $architecture) {
                        foreach ($crt in Get-ChildItem -LiteralPath $architecture -Directory -Filter 'Microsoft.VC*.CRT') {
                            $candidates += [pscustomobject]@{ Path=$crt.FullName; Version=[version]$release.Name; InstallRoot=$edition.FullName }
                        }
                    }
                }
            }
        }
    }
    $selected = $candidates | Sort-Object Version -Descending | Select-Object -First 1
    if (-not $selected) { throw 'No official x64 Visual C++ redist directory was found. Pass -VCRedistDirectory explicitly.' }
    $VCRedistDirectory = $selected.Path
}
$VCRedistDirectory = [IO.Path]::GetFullPath($VCRedistDirectory)
if ($VCRedistDirectory -notmatch '\\VC\\Redist\\MSVC\\[^\\]+\\x64\\Microsoft\.VC[^\\]+\.CRT$') { throw 'VCRedistDirectory must point to the official Visual Studio x64 CRT redistributable directory.' }
foreach ($dll in @('msvcp140.dll','vcruntime140.dll','vcruntime140_1.dll')) { Copy-PayloadFile (Join-Path $VCRedistDirectory $dll) ('runtime\python\' + $dll) }
$vsInstallRoot = $VCRedistDirectory.Substring(0,$VCRedistDirectory.IndexOf('\VC\Redist\',[StringComparison]::OrdinalIgnoreCase))
$redistLicenseRoot = Join-Path $vsInstallRoot 'Licenses'
if (-not (Test-Path -LiteralPath $redistLicenseRoot -PathType Container)) { throw 'The selected Visual Studio installation has no redistributable license directory.' }
$redistNotices = @(Get-ChildItem -LiteralPath $redistLicenseRoot -Filter Redist.txt -Recurse -File | Sort-Object FullName)
if ($redistNotices.Count -eq 0) { throw 'The installed Microsoft redistributable notice is missing.' }
Copy-PayloadFile $redistNotices[0].FullName 'licenses\Microsoft-Visual-Cpp-Redist.txt'
Copy-PayloadTree (Join-Path $packagingDirectory 'licenses') 'licenses'
$notices = @"
Dafeiyu Translator $Version - bundled runtime notices

Python: PSF License; full text in runtime/python/LICENSE.txt.
CTranslate2: MIT; full license in licenses/CTranslate2-4.8.4-LICENSE.txt.
SentencePiece: Apache-2.0; full license in licenses/SentencePiece-0.2.2-LICENSE.txt.
NumPy and bundled numerical libraries: license texts in numpy and numpy-*.dist-info.
PyYAML: MIT; license in pyyaml-*.dist-info.
Optional OPUS-MT / Argos language packages: attribution and URLs are in
backend/MODEL-NOTICES.md. The retained English/Chinese model author notices are
licenses/Argos-en-zh-1.9-README.md and licenses/Argos-zh-en-1.9-README.md.
The base application does not contain model weights or tokenizers.
Microsoft Visual C++ runtime: the named x64 runtime DLLs are copied from the installed
Visual Studio redistributable directory; see Microsoft-Visual-Cpp-Redist.txt.
App-local VC runtime deployment documentation:
https://learn.microsoft.com/cpp/windows/redistributing-visual-cpp-files

Windows 10/11 x64 and .NET Framework 4.8 remain platform requirements.
No Python installation, package manager, administrator access or GPU is required.
Offline language packages need a one-time download requested by the user. Installed
packages run locally and are stored separately under DafeiyuTranslator/packages.
Google mode sends selected text to Google. The portable launcher never downloads files.
"@
[IO.File]::WriteAllText((Join-Path $stageDirectory 'licenses\THIRD-PARTY-NOTICES.txt'),$notices,$utf8)

$entries = New-Object 'Collections.Generic.List[object]'
[long]$totalBytes = 0
foreach ($file in Get-ChildItem -LiteralPath $stageDirectory -File -Recurse | Sort-Object FullName) {
    $relative = $file.FullName.Substring($stageDirectory.Length+1).Replace('\','/')
    if ($relative -match '(?i)(^|/)(preferences\.json|glossary\.json|diagnostics|stanza|installer-cache|__pycache__|pip)(/|$)' -or
        $relative -match '(?i)^runtime/model(?:/|-zh-en/)' -or
        $relative -match '(?i)(^|/)(model\.bin|sentencepiece\.model)$' -or
        $relative -match '(?i)\.(argosmodel|zip)$' -and $relative -ne 'runtime/python/python312.zip') { throw "Forbidden payload file: $relative" }
    $entries.Add([ordered]@{ Path=$relative; Length=$file.Length; Sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() })
    $totalBytes += $file.Length
}
$manifest = [ordered]@{ Schema=1; Version=$Version; TotalBytes=$totalBytes; Files=$entries.ToArray() }
$manifestPath = Join-Path $buildDirectory 'payload.manifest.json'
[IO.File]::WriteAllText($manifestPath,($manifest | ConvertTo-Json -Depth 8 -Compress),$utf8)
$manifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
$receipt = [ordered]@{ Version=$Version; Distribution='base-with-on-demand-language-packs'; BundledModelCount=0; BundledModelBytes=0; IncludesOfflineRuntime=$true; LanguagePackStorage='DafeiyuTranslator/packages (DAFEIYU_DATA_DIR override supported by backend)'; LauncherDownloads=$false; BuildDirectory=$buildDirectory; StageDirectory=$stageDirectory; FileCount=$entries.Count; UncompressedBytes=$totalBytes; ManifestSha256=$manifestHash; VCRedistDirectory=$VCRedistDirectory; StageOnly=[bool]$StageOnly }
if (-not $StageOnly) {
    $zipPath = Join-Path $buildDirectory 'payload.zip'
    $zipStream = New-Object IO.FileStream($zipPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try {
        $zip = New-Object IO.Compression.ZipArchive($zipStream,[IO.Compression.ZipArchiveMode]::Create,$false,$utf8)
        try {
            foreach ($entry in $entries) {
                $source = Join-Path $stageDirectory $entry.Path.Replace('/','\')
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$source,$entry.Path,[IO.Compression.CompressionLevel]::NoCompression) | Out-Null
            }
        } finally { $zip.Dispose() }
    } finally { $zipStream.Dispose() }
    $archiveHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $archiveBytes = (Get-Item -LiteralPath $zipPath).Length
    $compressionSource = Join-Path $packagingDirectory 'NativeCompression.cs'
    Add-Type -Path $compressionSource
    $compressedPath = Join-Path $buildDirectory 'payload.lzms'
    $compressTimer = [Diagnostics.Stopwatch]::StartNew()
    $archiveData = [IO.File]::ReadAllBytes($zipPath)
    $compressedData = [DafeiyuPortable.NativeCompression]::Compress($archiveData)
    [IO.File]::WriteAllBytes($compressedPath,$compressedData)
    $compressTimer.Stop()
    # Verify the exact serialized payload before embedding it into the launcher.
    $restoredData = [DafeiyuPortable.NativeCompression]::Decompress([IO.File]::ReadAllBytes($compressedPath),$archiveBytes)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $restoredHash = [BitConverter]::ToString($sha.ComputeHash($restoredData)).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
    if ($restoredHash -ne $archiveHash) { throw 'Compressed payload round-trip verification failed.' }
    $archiveData=$null; $compressedData=$null; $restoredData=$null
    $payloadHash = (Get-FileHash -LiteralPath $compressedPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $metadataPath = Join-Path $buildDirectory 'PortableBuildInfo.cs'
    $assemblyVersion = if (($Version.Split('.')).Count -eq 3) { $Version + '.0' } else { $Version }
    $metadata = @"
using System.Reflection;
[assembly: AssemblyTitle("Dafeiyu Translator Portable")]
[assembly: AssemblyProduct("Dafeiyu Translator")]
[assembly: AssemblyVersion("$assemblyVersion")]
[assembly: AssemblyFileVersion("$assemblyVersion")]
namespace DafeiyuPortable { internal static class PortableBuildInfo {
 internal const string Version="$Version";
 internal const string PayloadSha256="$payloadHash";
 internal const string ArchiveSha256="$archiveHash";
 internal const long ArchiveBytes=$archiveBytes;
 internal const string ManifestSha256="$manifestHash";
} }
"@
    [IO.File]::WriteAllText($metadataPath,$metadata,$utf8)
    $outputPath = Join-Path $buildDirectory $OutputName
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $iconPath = Join-Path $projectDirectory 'icon.ico'
    if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) { throw 'The application icon is missing.' }
    $launcherManifest = Join-Path $packagingDirectory 'PortableLauncher.manifest'
    & $compiler /nologo /optimize+ /target:winexe /platform:x64 "/out:$outputPath" "/win32icon:$iconPath" "/win32manifest:$launcherManifest" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "/resource:$compressedPath,payload.lzms" "/resource:$manifestPath,payload.manifest.json" (Join-Path $packagingDirectory 'PortableLauncher.cs') $compressionSource $metadataPath (Join-Path $projectDirectory 'src\WindowIcon.cs') (Join-Path $projectDirectory 'src\WhaleBadge.cs') "/resource:$projectDirectory\resources\window-icon.ico,Dafeiyu.WindowIcon.ico"
    if ($LASTEXITCODE -ne 0) { throw 'Portable launcher compilation failed.' }
    $receipt['OutputPath']=$outputPath
    $receipt['OutputBytes']=(Get-Item -LiteralPath $outputPath).Length
    $receipt['OutputSha256']=(Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $receipt['PayloadSha256']=$payloadHash
    $receipt['PayloadBytes']=(Get-Item -LiteralPath $compressedPath).Length
    $receipt['Compression']='Windows-LZMS-64MiB-blocks'
    $receipt['CompressionMilliseconds']=$compressTimer.ElapsedMilliseconds
    $receipt['ArchiveBytes']=$archiveBytes
    $receipt['ArchiveSha256']=$archiveHash
    if ($receipt['OutputBytes'] -ge 30000000) { throw 'Portable EXE exceeds the 30 MB distribution budget.' }
}
$criticalInputs = [ordered]@{ 'ClaudeChinese.exe'=$applicationSource; 'backend/offline_worker.py'=(Join-Path $projectDirectory 'backend\offline_worker.py'); 'backend/google_worker.py'=(Join-Path $projectDirectory 'backend\google_worker.py'); 'backend/glossary.py'=(Join-Path $projectDirectory 'backend\glossary.py'); 'backend/package_manager.py'=(Join-Path $projectDirectory 'backend\package_manager.py'); 'backend/package_store.py'=(Join-Path $projectDirectory 'backend\package_store.py'); 'backend/argos_catalog.json'=(Join-Path $projectDirectory 'backend\argos_catalog.json'); 'backend/launcher.json'=(Join-Path $projectDirectory 'backend\launcher.json'); 'backend/MODEL-NOTICES.md'=(Join-Path $projectDirectory 'backend\MODEL-NOTICES.md') }
$inputHashes = [ordered]@{}
foreach ($relative in $criticalInputs.Keys) {
    $expected = ($entries | Where-Object { $_.Path -eq $relative }).Sha256
    $currentHash = (Get-FileHash -LiteralPath $criticalInputs[$relative] -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($currentHash -ne $expected) { throw "An input changed during packaging; rerun to capture the final files: $relative" }
    $inputHashes[$relative]=$currentHash
}
$receipt['InputSha256']=$inputHashes
$receiptPath = Join-Path $buildDirectory 'build-receipt.json'
[IO.File]::WriteAllText($receiptPath,($receipt | ConvertTo-Json -Depth 6),$utf8)
$receipt['ReceiptPath']=$receiptPath
$receipt | ConvertTo-Json -Depth 6
