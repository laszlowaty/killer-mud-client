param(
    [Parameter(Mandatory = $true)]
    [string]$SourceImage,
    [Parameter(Mandatory = $true)]
    [string]$TargetImage,
    [Parameter(Mandatory = $true)]
    [string]$OutputImage
)

$ErrorActionPreference = 'Stop'
$sourcePath = (Resolve-Path -LiteralPath $SourceImage).Path
$targetPath = (Resolve-Path -LiteralPath $TargetImage).Path
$outputPath = [IO.Path]::GetFullPath($OutputImage)
$outputDirectory = Split-Path -Parent $outputPath
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Image]::FromFile($sourcePath)
$target = [System.Drawing.Image]::FromFile($targetPath)
try {
    $targetWidth = $target.Width
    $targetHeight = $target.Height
    $scale = [Math]::Max($targetWidth / $source.Width, $targetHeight / $source.Height)
    $drawWidth = $source.Width * $scale
    $drawHeight = $source.Height * $scale
    $offsetX = ($targetWidth - $drawWidth) / 2
    $offsetY = ($targetHeight - $drawHeight) / 2

    $result = New-Object System.Drawing.Bitmap($targetWidth, $targetHeight)
    $graphics = [System.Drawing.Graphics]::FromImage($result)
    try {
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.DrawImage($source, [float]$offsetX, [float]$offsetY, [float]$drawWidth, [float]$drawHeight)
        $result.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $result.Dispose()
    }

    [pscustomobject]@{
        sourceImage = $sourcePath
        targetImage = $targetPath
        outputImage = $outputPath
        sourceWidth = $source.Width
        sourceHeight = $source.Height
        targetWidth = $targetWidth
        targetHeight = $targetHeight
        scale = $scale
        cropLeft = [Math]::Max(0, -$offsetX)
        cropTop = [Math]::Max(0, -$offsetY)
    } | ConvertTo-Json
}
finally {
    $target.Dispose()
    $source.Dispose()
}
