# Docker Integration Test Environment

This directory contains the Docker Compose configuration for manual testing of the CastCrew plugin in a containerized Jellyfin environment.

## Automated tests (Testcontainers)

The primary Docker tests run via `dotnet test` using Testcontainers (no manual setup needed):

```bash
dotnet test tests/Jellyfin.Plugin.CastCrew.IntegrationTests/Jellyfin.Plugin.CastCrew.IntegrationTests.csproj \
  --filter "FullyQualifiedName~CastCrewDockerIntegrationTests"
```

These tests auto-skip when Docker is not available.

## Manual testing with Docker Compose

For hands-on debugging or manual verification:

```bash
# Build the plugin
dotnet publish src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj \
  --configuration Release --framework net8.0 --output artifacts/docker-test

# Deploy plugin into config volume
mkdir -p tests/docker/config/data/plugins/CastCrew_0.1.0.1
cp artifacts/docker-test/Jellyfin.Plugin.CastCrew.dll tests/docker/config/data/plugins/CastCrew_0.1.0.1/

# Start Jellyfin
cd tests/docker
docker compose up -d

# View logs
docker logs -f jellyfin-castcrew-test

# Stop and clean up
docker compose down
rm -rf config/
```

Jellyfin will be available at http://localhost:8196.
