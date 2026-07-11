$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$solution = Join-Path $repoRoot "KillerMudClient.sln"
$appProject = Join-Path $repoRoot "src\MudClient.App\MudClient.App.csproj"

dotnet restore $solution
dotnet build $solution
dotnet run --project $appProject
