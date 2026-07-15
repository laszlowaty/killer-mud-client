param(
    [Parameter(Mandatory = $true)]
    [string]$Name,
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
$mapDirectory = Join-Path $RepoRoot 'src\MudClient.App\Assets\Map'
$locations = Join-Path $mapDirectory 'Locations'
$exports = Join-Path $locations 'CalibrationExports'

if (-not (Test-Path $exports)) {
    throw "Nie znaleziono katalogu eksportow: $exports"
}

$normalized = $Name.Trim().ToLowerInvariant()
$candidates = foreach ($file in Get-ChildItem $exports -Filter '*.json' -File) {
    try {
        $data = Get-Content $file.FullName -Raw | ConvertFrom-Json
        $imageFile = [string]$data.imageFile
        $baseName = [IO.Path]::GetFileNameWithoutExtension($imageFile)
        $layerName = [string]$data.layerName
        if ($file.BaseName.ToLowerInvariant().Contains($normalized) -or
            $baseName.ToLowerInvariant().Contains($normalized) -or
            $layerName.ToLowerInvariant().Contains($normalized)) {
            $png = [IO.Path]::ChangeExtension($file.FullName, '.png')
            $composite = Join-Path $file.DirectoryName ($file.BaseName + '-composite.png')
            $target = Join-Path $locations $imageFile
            if ((Test-Path $png) -and (Test-Path $target)) {
                [pscustomobject]@{
                    File = $file
                    Data = $data
                    ExportPng = $png
                    CompositePng = if (Test-Path $composite) { $composite } else { $null }
                    TargetImage = $target
                }
            }
        }
    } catch {
        # Pomin uszkodzony lub niekompletny eksport i szukaj starszego kompletnego pakietu.
    }
}

$latest = $candidates | Sort-Object { $_.File.LastWriteTimeUtc } -Descending | Select-Object -First 1
if ($null -eq $latest) {
    throw "Nie znaleziono kompletnego eksportu PNG+JSON dla: $Name"
}

$calibration = Join-Path $locations ($([IO.Path]::GetFileNameWithoutExtension($latest.Data.imageFile)) + '.calibration.json')
$imageElements = @(@($latest.Data.imageElements) | Where-Object { $null -ne $_ })
[pscustomobject]@{
    name = $Name
    exportName = [IO.Path]::GetFileNameWithoutExtension([string]$latest.Data.imageFile)
    exportJson = $latest.File.FullName
    exportPng = $latest.ExportPng
    compositePng = $latest.CompositePng
    targetImage = $latest.TargetImage
    calibrationJson = if (Test-Path $calibration) { $calibration } else { $null }
    manifest = Join-Path $locations 'manifest.json'
    layerName = $latest.Data.layerName
    generationPrompt = [string]$latest.Data.generationPrompt
    isBlankCanvas = [bool]$latest.Data.isBlankCanvas
    rooms = @($latest.Data.rooms)
    markers = @($latest.Data.markers)
    roomOffsets = @($latest.Data.roomOffsets)
    imageElements = $imageElements
    hasManualComposition = ($null -ne $latest.CompositePng -and $imageElements.Count -gt 0)
} | ConvertTo-Json -Depth 8
