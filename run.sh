#!/usr/bin/env bash
set -euo pipefail
dotnet restore ./KillerMudClient.sln
dotnet run --project ./src/MudClient.App/MudClient.App.csproj
