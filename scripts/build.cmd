@echo off
setlocal
cd /d "%~dp0\.."

echo [CastCrew] Restoring dependencies...
dotnet restore src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj
if %errorlevel% neq 0 exit /b %errorlevel%

echo [CastCrew] Building Release configuration...
dotnet build src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj -c Release
if %errorlevel% neq 0 exit /b %errorlevel%

echo [CastCrew] Running test suite...
dotnet test tests/Jellyfin.Plugin.CastCrew.Tests/Jellyfin.Plugin.CastCrew.Tests.csproj -c Release
if %errorlevel% neq 0 exit /b %errorlevel%

echo [CastCrew] Build and tests completed successfully!
endlocal
