<#
.SYNOPSIS
Konwertuje eksport PNG z Nortantis na tło Starego Kontynentu używane przez aplikację.

.DESCRIPTION
Domyślnie czyta tools/Nortantis/Exports/old-continent-master.png, skaluje obraz bez
zmiany proporcji do szerokości 8192 px, podmienia old-continent-overview.png i
aktualizuje granice świata w manifeście teł lokacji.

.EXAMPLE
.\tools\Nortantis\Convert-ExportToMapBackground.ps1

.EXAMPLE
.\tools\Nortantis\Convert-ExportToMapBackground.ps1 -InputPath C:\Mapy\stary-kontynent.png
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$InputPath,
    [string]$TargetPath,
    [string]$ManifestPath,
    [ValidateRange(1, 32768)]
    [int]$TargetWidth = 8192,
    [int]$AreaId = 1,
    [double]$Z = 0,
    [double]$MinX = -849,
    [double]$MinY = -604,
    [double]$MaxX = 482,
    [double]$MaxY = 192
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))

if ([string]::IsNullOrWhiteSpace($InputPath)) {
    $InputPath = Join-Path $PSScriptRoot 'Exports\old-continent-master.png'
}

if ([string]::IsNullOrWhiteSpace($TargetPath)) {
    $TargetPath = Join-Path $repoRoot 'src\MudClient.App\Assets\Map\Locations\old-continent-overview.png'
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $repoRoot 'src\MudClient.App\Assets\Map\Locations\manifest.json'
}

$sourcePath = (Resolve-Path -LiteralPath $InputPath).Path
$targetPath = [IO.Path]::GetFullPath($TargetPath)
$manifestPath = (Resolve-Path -LiteralPath $ManifestPath).Path
$targetDirectory = Split-Path -Parent $targetPath

if ([IO.Path]::GetExtension($sourcePath) -ne '.png') {
    throw "Eksport Nortantis musi być plikiem PNG: $sourcePath"
}

if ($MaxX -le $MinX -or $MaxY -le $MinY) {
    throw 'Granice mapy są nieprawidłowe. MaxX/MaxY muszą być większe od MinX/MinY.'
}

if (-not (Test-Path -LiteralPath $targetDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
}

$manifest = @(Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json)
$manifestEntry = $manifest | Where-Object {
    $_.areaId -eq $AreaId -and [double]$_.z -eq $Z
} | Select-Object -First 1

if ($null -eq $manifestEntry) {
    throw "Nie znaleziono wpisu areaId=$AreaId, z=$Z w manifeście: $manifestPath"
}

Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Bitmap]::new($sourcePath)
$targetHeight = [int][Math]::Round($source.Height * $TargetWidth / $source.Width)
$sourceRatio = $source.Width / [double]$source.Height
$boundsRatio = ($MaxX - $MinX) / [double]($MaxY - $MinY)
$aspectDifference = [Math]::Abs($sourceRatio - $boundsRatio) / $boundsRatio

if ($aspectDifference -gt 0.01) {
    $source.Dispose()
    throw ('Proporcje eksportu ({0:F4}) różnią się od proporcji granic mapy ({1:F4}) o więcej niż 1%. ' +
        'Sprawdź ustawienia projektu Nortantis albo podaj poprawne granice.') -f $sourceRatio, $boundsRatio
}

$operation = "Konwersja $($source.Width)x$($source.Height) -> ${TargetWidth}x${targetHeight} i podmiana tła mapy"
if (-not $PSCmdlet.ShouldProcess($targetPath, $operation)) {
    $source.Dispose()
    return
}

$imageTempPath = Join-Path $targetDirectory ('.{0}.{1}.tmp.png' -f [IO.Path]::GetFileNameWithoutExtension($targetPath), [Guid]::NewGuid().ToString('N'))
$manifestDirectory = Split-Path -Parent $manifestPath
$manifestTempPath = Join-Path $manifestDirectory ('.manifest.{0}.tmp.json' -f [Guid]::NewGuid().ToString('N'))

try {
    $result = [System.Drawing.Bitmap]::new(
        $TargetWidth,
        $targetHeight,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($result)
    try {
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.DrawImage(
            $source,
            [System.Drawing.Rectangle]::new(0, 0, $TargetWidth, $targetHeight),
            0,
            0,
            $source.Width,
            $source.Height,
            [System.Drawing.GraphicsUnit]::Pixel)
        $result.Save($imageTempPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $result.Dispose()
    }

    $manifestEntry.minX = $MinX
    $manifestEntry.minY = $MinY
    $manifestEntry.maxX = $MaxX
    $manifestEntry.maxY = $MaxY
    $manifestJson = $manifest | ConvertTo-Json -Depth 20
    [IO.File]::WriteAllText(
        $manifestTempPath,
        $manifestJson + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))

    $validationImage = [System.Drawing.Image]::FromFile($imageTempPath)
    try {
        if ($validationImage.Width -ne $TargetWidth -or $validationImage.Height -ne $targetHeight) {
            throw "Wygenerowany obraz ma nieprawidłowy rozmiar: $($validationImage.Width)x$($validationImage.Height)"
        }
    }
    finally {
        $validationImage.Dispose()
    }

    Get-Content -Raw -LiteralPath $manifestTempPath | ConvertFrom-Json | Out-Null
    Move-Item -LiteralPath $imageTempPath -Destination $targetPath -Force
    Move-Item -LiteralPath $manifestTempPath -Destination $manifestPath -Force

    [pscustomobject]@{
        SourcePath = $sourcePath
        TargetPath = $targetPath
        ManifestPath = $manifestPath
        SourceSize = "$($source.Width)x$($source.Height)"
        TargetSize = "${TargetWidth}x${targetHeight}"
        Bounds = "X: $MinX..$MaxX, Y: $MinY..$MaxY"
        OutputBytes = (Get-Item -LiteralPath $targetPath).Length
    }
}
finally {
    $source.Dispose()
    if (Test-Path -LiteralPath $imageTempPath) {
        Remove-Item -LiteralPath $imageTempPath -Force
    }
    if (Test-Path -LiteralPath $manifestTempPath) {
        Remove-Item -LiteralPath $manifestTempPath -Force
    }
}
