$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$taskApp=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskImage=[Drawing.Image]::FromFile((Join-Path $PSScriptRoot 'helper-mascot.png'))
$taskSizes=@(256,128,64,48,32,24,16)
$taskFrames=[Collections.Generic.List[byte[]]]::new()
$taskPreview=Join-Path $taskApp 'artifacts\helper-060'
New-Item -ItemType Directory -Path $taskPreview -Force | Out-Null
try {
    foreach($taskSize in $taskSizes){
        $taskBitmap=[Drawing.Bitmap]::new($taskSize,$taskSize,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $taskGraphics=[Drawing.Graphics]::FromImage($taskBitmap)
        $taskStream=[IO.MemoryStream]::new()
        try {
            $taskGraphics.Clear([Drawing.Color]::Transparent)
            $taskGraphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $taskGraphics.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality
            $taskGraphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $taskGraphics.DrawImage($taskImage,[Drawing.Rectangle]::new(0,0,$taskSize,$taskSize))
            $taskBitmap.Save($taskStream,[Drawing.Imaging.ImageFormat]::Png)
            $taskFrames.Add($taskStream.ToArray())
            if($taskSize -eq 32){$taskBitmap.Save((Join-Path $taskPreview 'icon-32.png'),[Drawing.Imaging.ImageFormat]::Png)}
        }finally{$taskGraphics.Dispose();$taskBitmap.Dispose();$taskStream.Dispose()}
    }
    $taskOutput=[IO.File]::Create((Join-Path $taskApp 'icon.ico'))
    $taskWriter=[IO.BinaryWriter]::new($taskOutput)
    try {
        $taskWriter.Write([uint16]0);$taskWriter.Write([uint16]1);$taskWriter.Write([uint16]$taskSizes.Count)
        $taskOffset=6+16*$taskSizes.Count
        for($taskIndex=0;$taskIndex -lt $taskSizes.Count;$taskIndex++){
            $taskDimension=if($taskSizes[$taskIndex] -eq 256){0}else{$taskSizes[$taskIndex]}
            $taskWriter.Write([byte]$taskDimension);$taskWriter.Write([byte]$taskDimension)
            $taskWriter.Write([byte]0);$taskWriter.Write([byte]0);$taskWriter.Write([uint16]1);$taskWriter.Write([uint16]32)
            $taskWriter.Write([uint32]$taskFrames[$taskIndex].Length);$taskWriter.Write([uint32]$taskOffset)
            $taskOffset+=$taskFrames[$taskIndex].Length
        }
        foreach($taskFrame in $taskFrames){$taskWriter.Write($taskFrame)}
    }finally{$taskWriter.Dispose();$taskOutput.Dispose()}
    Write-Output ('Icon written: 256, 128, 64, 48, 32, 24, 16 px; source '+$taskImage.PixelFormat)
}finally{$taskImage.Dispose()}
