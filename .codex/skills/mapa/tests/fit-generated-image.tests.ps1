$ErrorActionPreference = 'Stop'

$skillRoot = Split-Path -Parent $PSScriptRoot
$script = Join-Path $skillRoot 'scripts\fit-generated-image.ps1'
$directory = Join-Path ([IO.Path]::GetTempPath()) ("KillerMudClient.MapaFit.Tests\" + [Guid]::NewGuid().ToString('N'))

try {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    Add-Type -AssemblyName System.Drawing
    $sourcePath = Join-Path $directory 'source.png'
    $targetPath = Join-Path $directory 'target.png'
    $outputPath = Join-Path $directory 'output.png'

    $source = New-Object System.Drawing.Bitmap(300, 100)
    for ($x = 0; $x -lt 300; $x++) {
        $color = if ($x -lt 100) { [System.Drawing.Color]::Red }
            elseif ($x -lt 200) { [System.Drawing.Color]::Lime }
            else { [System.Drawing.Color]::Blue }
        for ($y = 0; $y -lt 100; $y++) { $source.SetPixel($x, $y, $color) }
    }
    $source.Save($sourcePath, [System.Drawing.Imaging.ImageFormat]::Png)
    $source.Dispose()

    $target = New-Object System.Drawing.Bitmap(100, 100)
    $target.Save($targetPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $target.Dispose()

    $result = & $script -SourceImage $sourcePath -TargetImage $targetPath -OutputImage $outputPath | ConvertFrom-Json
    if ($result.targetWidth -ne 100 -or $result.targetHeight -ne 100) { throw 'Unexpected target dimensions.' }

    $output = [System.Drawing.Image]::FromFile($outputPath)
    try {
        if ($output.Width -ne 100 -or $output.Height -ne 100) { throw 'Unexpected output dimensions.' }
        foreach ($point in @(@(5, 50), @(50, 50), @(94, 50))) {
            $pixel = ([System.Drawing.Bitmap]$output).GetPixel($point[0], $point[1])
            if ($pixel.G -lt 220 -or $pixel.R -gt 40 -or $pixel.B -gt 40) {
                throw 'The source was stretched instead of uniformly scaled and cropped.'
            }
        }
    }
    finally {
        $output.Dispose()
    }

    Write-Output 'fit-generated-image: PASS'
}
finally {
    if (Test-Path -LiteralPath $directory) { Remove-Item -LiteralPath $directory -Recurse -Force }
}
