param(
    [Parameter(Mandatory = $true)]
    [string]$Name,

    [Parameter(Mandatory = $true)]
    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'

$repoPath = [System.IO.Path]::GetFullPath($RepoRoot)
$exportsPath = [System.IO.Path]::GetFullPath((Join-Path $repoPath 'src\MudClient.App\Assets\Map\Locations\CalibrationExports'))
$expectedPrefix = $repoPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not $exportsPath.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Katalog eksportow znajduje sie poza repozytorium: $exportsPath"
}

if (-not (Test-Path -LiteralPath $exportsPath -PathType Container)) {
    [pscustomobject]@{
        name = $Name
        exportsDirectory = $exportsPath
        deleted = @()
    } | ConvertTo-Json -Depth 3
    exit 0
}

$safeName = [System.IO.Path]::GetFileName($Name.Trim())
if ([string]::IsNullOrWhiteSpace($safeName) -or $safeName -ne $Name.Trim()) {
    throw 'Nazwa krainy nie moze zawierac elementow sciezki.'
}

$deleted = @(
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
            $_.Name
        }
)

[pscustomobject]@{
    name = $safeName
    exportsDirectory = $exportsPath
    deleted = $deleted
} | ConvertTo-Json -Depth 3
