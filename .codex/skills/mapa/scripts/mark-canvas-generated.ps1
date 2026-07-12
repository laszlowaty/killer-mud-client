param(
    [Parameter(Mandatory = $true)]
    [string]$CalibrationJson
)

$ErrorActionPreference = 'Stop'
$path = [System.IO.Path]::GetFullPath($CalibrationJson)
if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
    throw "Nie znaleziono pliku kalibracji: $path"
}

$data = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
$data.isBlankCanvas = $false
$data | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $path -Encoding utf8

[pscustomobject]@{
    calibrationJson = $path
    isBlankCanvas = $false
} | ConvertTo-Json
