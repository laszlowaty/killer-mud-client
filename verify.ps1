param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path $PSScriptRoot).Path
$solution = Join-Path $repoRoot "KillerMudClient.sln"
$artifactRoot = Join-Path $repoRoot "artifacts\verify"

function Assert-PathInsideRepository([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $prefix = $repoRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing operation outside repository: $fullPath"
    }
}

function Stop-ArtifactTestProcesses {
    if ($env:OS -ne "Windows_NT" -or -not (Test-Path -LiteralPath $artifactRoot)) {
        return
    }

    $artifactPrefix = [IO.Path]::GetFullPath($artifactRoot) + [IO.Path]::DirectorySeparatorChar
    $processes = Get-Process -Name "MudClient.*.Tests", "testhost" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Path -and $_.Path.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)
        }

    foreach ($process in $processes) {
        Write-Warning "Stopping orphaned test process $($process.Id) from the artifact directory."
        Stop-Process -Id $process.Id -Force -ErrorAction Stop
    }
}

function Clear-VerificationArtifacts {
    Assert-PathInsideRepository $artifactRoot
    Stop-ArtifactTestProcesses
    if (Test-Path -LiteralPath $artifactRoot) {
        Remove-Item -LiteralPath $artifactRoot -Recurse -Force
    }
}

function Invoke-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet exited with code ${LASTEXITCODE}: dotnet $($Arguments -join ' ')"
    }
}

Clear-VerificationArtifacts

try {
    Invoke-DotNet -Arguments @(
        "restore", $solution,
        "--artifacts-path", $artifactRoot
    )
    Invoke-DotNet -Arguments @(
        "build", $solution,
        "--configuration", $Configuration,
        "--no-restore",
        "--artifacts-path", $artifactRoot
    )

    foreach ($testProject in @(
        "tests\MudClient.Core.Tests\MudClient.Core.Tests.csproj",
        "tests\MudClient.App.Tests\MudClient.App.Tests.csproj"
    )) {
        Invoke-DotNet -Arguments @(
            "test", (Join-Path $repoRoot $testProject),
            "--configuration", $Configuration,
            "--no-build",
            "--no-restore",
            "--artifacts-path", $artifactRoot,
            "--results-directory", (Join-Path $artifactRoot "TestResults"),
            "--blame-hang-timeout", "60s"
        )
    }
}
finally {
    if ($KeepArtifacts) {
        Write-Host "Artifacts kept in: $artifactRoot"
    }
    else {
        Clear-VerificationArtifacts
    }
}
