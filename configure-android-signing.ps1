[CmdletBinding()]
param(
    [string]$Repository = "laszlowaty/killer-mud-client"
)

$ErrorActionPreference = "Stop"

if (-not [OperatingSystem]::IsWindows()) {
    throw "Ten skrypt przechowuje lokalne haslo przez Windows DPAPI i wymaga Windows."
}

function Set-GitHubSecret {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$Value,

        [Parameter(Mandatory)]
        [string]$TargetRepository
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = "gh"
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add("secret")
    $startInfo.ArgumentList.Add("set")
    $startInfo.ArgumentList.Add($Name)
    $startInfo.ArgumentList.Add("--repo")
    $startInfo.ArgumentList.Add($TargetRepository)

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo

    try {
        if (-not $process.Start()) {
            throw "Nie udalo sie uruchomic gh."
        }

        $process.StandardInput.Write($Value)
        $process.StandardInput.Close()
        $standardOutput = $process.StandardOutput.ReadToEnd()
        $standardError = $process.StandardError.ReadToEnd()
        $process.WaitForExit()

        if ($process.ExitCode -ne 0) {
            throw "Nie udalo sie ustawic sekretu ${Name}: $standardError$standardOutput"
        }
    }
    finally {
        $process.Dispose()
    }
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "Nie znaleziono GitHub CLI (gh)."
}

$jdkCandidates = @(
    $env:JAVA_HOME,
    (Join-Path $env:LOCALAPPDATA "Android\Jdk"),
    (Join-Path $env:ProgramFiles "Android\Android Studio\jbr")
) | Where-Object { $_ }
$keytoolCandidates = $jdkCandidates |
    ForEach-Object { Join-Path $_ "bin\keytool.exe" } |
    Where-Object { Test-Path -LiteralPath $_ }

$keytool = $keytoolCandidates | Select-Object -First 1
if (-not $keytool) {
    throw "Nie znaleziono keytool.exe. Ustaw JAVA_HOME albo zainstaluj JDK Android Studio."
}

$signingDirectory = Join-Path $env:LOCALAPPDATA "KillerMudClient\Signing"
$keystorePath = Join-Path $signingDirectory "killermud-release.keystore"
$passwordBackupPath = Join-Path $signingDirectory "killermud-release-password.dpapi"
$keyAlias = "killermud-release"

New-Item -ItemType Directory -Path $signingDirectory -Force | Out-Null

$keystoreExists = Test-Path -LiteralPath $keystorePath
$passwordBackupExists = Test-Path -LiteralPath $passwordBackupPath
if ($keystoreExists -xor $passwordBackupExists) {
    throw "Lokalna kopia podpisu jest niepelna. Sprawdz katalog: $signingDirectory"
}

if ($keystoreExists) {
    $encryptedPassword = [System.IO.File]::ReadAllText($passwordBackupPath).Trim()
    $securePassword = ConvertTo-SecureString $encryptedPassword
    $password = [System.Net.NetworkCredential]::new("", $securePassword).Password
}
else {
    $randomBytes = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
    $password = [Convert]::ToBase64String($randomBytes).TrimEnd("=").Replace("+", "-").Replace("/", "_")

    & $keytool `
        -genkeypair `
        -keystore $keystorePath `
        -storetype PKCS12 `
        -storepass $password `
        -keypass $password `
        -alias $keyAlias `
        -keyalg RSA `
        -keysize 4096 `
        -validity 10000 `
        -dname "CN=KillerMudClient, O=KillerMudClient"

    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $keystorePath)) {
        throw "Nie udalo sie utworzyc keystore Androida."
    }

    $securePassword = ConvertTo-SecureString $password -AsPlainText -Force
    $encryptedPassword = ConvertFrom-SecureString $securePassword
    [System.IO.File]::WriteAllText($passwordBackupPath, $encryptedPassword)
}

$keystoreBase64 = [Convert]::ToBase64String(
    [System.IO.File]::ReadAllBytes($keystorePath)
)

Set-GitHubSecret -Name "ANDROID_KEYSTORE_BASE64" `
    -Value $keystoreBase64 `
    -TargetRepository $Repository
Set-GitHubSecret -Name "ANDROID_KEYSTORE_ALIAS" `
    -Value $keyAlias `
    -TargetRepository $Repository
Set-GitHubSecret -Name "ANDROID_KEYSTORE_PASSWORD" `
    -Value $password `
    -TargetRepository $Repository
Set-GitHubSecret -Name "ANDROID_KEY_PASSWORD" `
    -Value $password `
    -TargetRepository $Repository

Write-Host "Gotowe. Staly podpis Androida skonfigurowano dla $Repository."
Write-Host "Lokalna kopia klucza: $keystorePath"
Write-Host "Kopia hasla DPAPI: $passwordBackupPath"
Write-Warning "Zarchiwizuj oba pliki. Kopia DPAPI jest czytelna tylko dla tego konta Windows na tym komputerze."
