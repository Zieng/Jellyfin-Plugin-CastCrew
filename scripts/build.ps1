$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
dotnet restore src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj
dotnet build src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj -c Release
dotnet test tests/Jellyfin.Plugin.CastCrew.Tests/Jellyfin.Plugin.CastCrew.Tests.csproj -c Release
