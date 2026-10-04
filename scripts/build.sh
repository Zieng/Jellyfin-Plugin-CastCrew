#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
dotnet restore src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj
dotnet build src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj -c Release

dotnet test tests/Jellyfin.Plugin.CastCrew.Tests/Jellyfin.Plugin.CastCrew.Tests.csproj -c Release
