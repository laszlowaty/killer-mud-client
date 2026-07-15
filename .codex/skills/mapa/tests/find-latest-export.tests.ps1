$ErrorActionPreference = 'Stop'

$skillRoot = Split-Path -Parent $PSScriptRoot
$script = Join-Path $skillRoot 'scripts\find-latest-export.ps1'
$repo = Join-Path ([IO.Path]::GetTempPath()) ("KillerMudClient.MapaSkill.Tests\" + [Guid]::NewGuid().ToString('N'))
$locations = Join-Path $repo 'src\MudClient.App\Assets\Map\Locations'
$exports = Join-Path $locations 'CalibrationExports'

try {
    New-Item -ItemType Directory -Path $exports -Force | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $locations 'arras.png'), [byte[]]@(1))
    [IO.File]::WriteAllBytes((Join-Path $exports 'arras-20260714-120000.png'), [byte[]]@(1))
    [IO.File]::WriteAllBytes((Join-Path $exports 'arras-20260714-120000-composite.png'), [byte[]]@(1))

    @{
        imageFile = 'arras.png'
        layerName = 'Arras'
        generationPrompt = 'Forest city around a stone temple.'
        isBlankCanvas = $false
        rooms = @(
            @{
                roomId = 42
                vnum = '1001'
                name = 'Droga przy kaplicy'
                sector = 'droga'
                x = 10
                y = 20
            }
        )
        markers = @()
        roomOffsets = @()
        imageElements = @(
            @{
                id = 'temple-1'
                assetFile = 'EditorAssets\Budynki\temple.png'
                imageX = 120
                imageY = 80
                width = 64
                height = 96
                rotation = 15
                opacity = 1
                zIndex = 0
            }
        )
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $exports 'arras-20260714-120000.json')

    $result = & $script -Name 'arras' -RepoRoot $repo | ConvertFrom-Json
    if (-not $result.hasManualComposition) { throw 'Manual composition was not detected.' }
    if (-not (Test-Path -LiteralPath $result.compositePng)) { throw 'compositePng was not returned.' }
    if (@($result.imageElements).Count -ne 1) { throw 'imageElements were not returned.' }
    if ($result.imageElements[0].id -ne 'temple-1') { throw 'Unexpected image element.' }
    if ($result.generationPrompt -ne 'Forest city around a stone temple.') { throw 'generationPrompt was not returned.' }
    if ($result.rooms[0].sector -ne 'droga') { throw 'Room sector was not returned.' }

    [IO.File]::WriteAllBytes((Join-Path $exports 'arras-legacy.png'), [byte[]]@(1))
    @{
        imageFile = 'arras.png'
        layerName = 'Arras'
        isBlankCanvas = $false
        rooms = @()
        markers = @()
        roomOffsets = @()
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $exports 'arras-legacy.json')
    (Get-Item -LiteralPath (Join-Path $exports 'arras-legacy.json')).LastWriteTimeUtc = [DateTime]::UtcNow.AddMinutes(1)

    $legacy = & $script -Name 'arras' -RepoRoot $repo | ConvertFrom-Json
    if ($legacy.hasManualComposition) { throw 'Legacy export was incorrectly marked as a manual composition.' }
    if ($null -ne $legacy.compositePng) { throw 'Legacy export unexpectedly returned compositePng.' }
    if (@($legacy.imageElements).Count -ne 0) { throw 'Legacy export unexpectedly returned imageElements.' }
    if ($legacy.generationPrompt -ne '') { throw 'Legacy export unexpectedly returned generationPrompt.' }

    Write-Output 'find-latest-export: PASS'
}
finally {
    if (Test-Path -LiteralPath $repo) { Remove-Item -LiteralPath $repo -Recurse -Force }
}
