param(
    [Parameter(Mandatory = $true)]
    [string]$Name,

    [Parameter(Mandatory = $true)]
    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'

$repoPath = [System.IO.Path]::GetFullPath($RepoRoot)
$locationsPath = [System.IO.Path]::GetFullPath((Join-Path $repoPath 'src\MudClient.App\Assets\Map\Locations'))
$exportsPath = [System.IO.Path]::GetFullPath((Join-Path $repoPath 'src\MudClient.App\Assets\Map\Locations\CalibrationExports'))
$expectedPrefix = $repoPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not $locationsPath.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $exportsPath.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Katalog map znajduje sie poza repozytorium.'
}

$safeName = [System.IO.Path]::GetFileName($Name.Trim())
if ([string]::IsNullOrWhiteSpace($safeName) -or $safeName -ne $Name.Trim()) {
    throw 'Nazwa krainy nie moze zawierac elementow sciezki.'
}

$deleted = [System.Collections.Generic.List[string]]::new()
if (Test-Path -LiteralPath $exportsPath -PathType Container) {
    Get-ChildItem -LiteralPath $exportsPath -File |
        Where-Object {
            ($_.Extension -ieq '.json' -or $_.Extension -ieq '.png') -and
            $_.BaseName.StartsWith("$safeName-", [System.StringComparison]::OrdinalIgnoreCase)
        } |
        ForEach-Object {
            $resolvedFile = [System.IO.Path]::GetFullPath($_.FullName)
            $exportsPrefix = $exportsPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
            if (-not $resolvedFile.StartsWith($exportsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Plik znajduje sie poza katalogiem eksportow: $resolvedFile"
            }

            Remove-Item -LiteralPath $resolvedFile -Force
            $deleted.Add($_.Name)
        }
}

$calibrationPath = [System.IO.Path]::GetFullPath((Join-Path $locationsPath "$safeName.calibration.json"))
$locationsPrefix = $locationsPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $calibrationPath.StartsWith($locationsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Plik kalibracji znajduje sie poza katalogiem map: $calibrationPath"
}

if (Test-Path -LiteralPath $calibrationPath -PathType Leaf) {
    Remove-Item -LiteralPath $calibrationPath -Force
    $deleted.Add([System.IO.Path]::GetFileName($calibrationPath))
}

[pscustomobject]@{
    name = $safeName
    exportsDirectory = $exportsPath
    deleted = @($deleted)
} | ConvertTo-Json -Depth 3
