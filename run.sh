#!/usr/bin/env bash
set -euo pipefail
dotnet restore ./MudClientStarter.sln
dotnet run --project ./src/MudClient.App/MudClient.App.csproj
