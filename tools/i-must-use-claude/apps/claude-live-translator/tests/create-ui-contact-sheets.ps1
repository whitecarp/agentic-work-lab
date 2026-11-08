param([Parameter(Mandatory=$true)][string]$CaptureDirectory, [string]$BaselineDirectory = '')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sheetRoot = [IO.Path]::GetFullPath($CaptureDirectory)
$sheetImages = Join-Path $sheetRoot 'scaled-preview-100'
function Add-SheetImage($sheetGraphics, [string]$sheetName, [string]$sheetCaption, [int]$sheetX, [int]$sheetY, [int]$sheetWidth, [int]$sheetHeight) {
    Add-FileImage $sheetGraphics (Join-Path $sheetImages ($sheetName+'.png')) $sheetCaption $sheetX $sheetY $sheetWidth $sheetHeight
}
function Add-FileImage($sheetGraphics, [string]$sheetPath, [string]$sheetCaption, [int]$sheetX, [int]$sheetY, [int]$sheetWidth, [int]$sheetHeight) {
    $sheetPicture = [Drawing.Image]::FromFile($sheetPath)
    try {
        $sheetGraphics.DrawString($sheetCaption, $script:sheetCaptionFont, $script:sheetInk, [Drawing.PointF]::new($sheetX, $sheetY))
        $sheetFactor = [Math]::Min($sheetWidth / $sheetPicture.Width, ($sheetHeight-32) / $sheetPicture.Height)
        $sheetTarget = [Drawing.Rectangle]::new($sheetX, $sheetY+32, [int]($sheetPicture.Width*$sheetFactor), [int]($sheetPicture.Height*$sheetFactor))
        $sheetGraphics.DrawImage($sheetPicture, $sheetTarget)
    } finally { $sheetPicture.Dispose() }
}
$sheetCaptionFont = [Drawing.Font]::new('Segoe UI', 13, [Drawing.FontStyle]::Bold)
$sheetHeadingFont = [Drawing.Font]::new('Segoe UI', 20, [Drawing.FontStyle]::Bold)
$sheetNoteFont = [Drawing.Font]::new('Segoe UI', 10)
$sheetInk = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(56,78,95))
$sheetBitmap = [Drawing.Bitmap]::new(1660, 1600)
$sheetGraphics = [Drawing.Graphics]::FromImage($sheetBitmap)
try {
    $sheetGraphics.Clear([Drawing.Color]::FromArgb(236,243,248))
    $sheetGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $sheetGraphics.DrawString('Whale Translator 0.9.3 - visual review', $sheetHeadingFont, $sheetInk, [Drawing.PointF]::new(24,16))
    $sheetGraphics.DrawString('Actual controls at 144 DPI (150%); resized for this sheet. Synthetic text only. Classic capture chrome; no system scale changes.', $sheetNoteFont, $sheetInk, [Drawing.PointF]::new(25,52))
    Add-SheetImage $sheetGraphics 'zh-main-online-history' 'Chinese / selected window + history' 24 88 530 720
    Add-SheetImage $sheetGraphics 'zh-settings' 'Chinese / settings' 578 88 310 720
    Add-SheetImage $sheetGraphics 'zh-picker' 'Chinese / window picker' 916 88 720 720
    Add-SheetImage $sheetGraphics 'en-main-online-empty' 'English / no window selected' 24 836 530 720
    Add-SheetImage $sheetGraphics 'en-settings' 'English / settings' 578 836 310 720
    Add-SheetImage $sheetGraphics 'en-picker' 'English / window picker' 916 836 720 720
    $sheetBitmap.Save((Join-Path $sheetRoot 'MAIN-SETTINGS-PICKER.png'), [Drawing.Imaging.ImageFormat]::Png)
    $sheetChinese = $sheetBitmap.Clone([Drawing.Rectangle]::new(0,0,1660,832), $sheetBitmap.PixelFormat)
    try { $sheetChinese.Save((Join-Path $sheetRoot 'ZH-MAIN-SETTINGS-PICKER.png'), [Drawing.Imaging.ImageFormat]::Png) } finally { $sheetChinese.Dispose() }
    if ($BaselineDirectory) {
        $compareBitmap = [Drawing.Bitmap]::new(1200, 960)
        $compareGraphics = [Drawing.Graphics]::FromImage($compareBitmap)
        try {
            $compareGraphics.Clear([Drawing.Color]::FromArgb(236,243,248))
            $compareGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $compareGraphics.DrawString('Whale Translator - before / after', $sheetHeadingFont, $sheetInk, [Drawing.PointF]::new(24,16))
            $compareGraphics.DrawString('Visual comparison only. Each image is resized to fit; this is not a pixel-size or independent DPI comparison.', $sheetNoteFont, $sheetInk, [Drawing.PointF]::new(25,52))
            $compareGraphics.DrawString('Before: original 0.9.2 native-app capture. After: 0.9.3 offscreen synthetic fixture at actual 144 DPI.', $sheetNoteFont, $sheetInk, [Drawing.PointF]::new(25,74))
            Add-FileImage $compareGraphics (Join-Path $BaselineDirectory 'zh-native-app-main-selected.png') 'Before / 0.9.2 - Chinese, selected window' 24 112 560 800
            Add-SheetImage $compareGraphics 'zh-main-online-history' 'After / 0.9.3 - Chinese, selected window' 616 112 560 800
            $compareBitmap.Save((Join-Path $sheetRoot 'BEFORE-AFTER-ZH.png'), [Drawing.Imaging.ImageFormat]::Png)
        } finally { $compareGraphics.Dispose();$compareBitmap.Dispose() }
    }
} finally {
    $sheetGraphics.Dispose();$sheetBitmap.Dispose();$sheetCaptionFont.Dispose();$sheetHeadingFont.Dispose();$sheetNoteFont.Dispose();$sheetInk.Dispose()
}
Write-Output (Join-Path $sheetRoot 'MAIN-SETTINGS-PICKER.png')
