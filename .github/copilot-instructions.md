# Copilot Instructions for Jellyfin-Plugin-CastCrew

## Build, test, and lint commands

| Task | Command |
| --- | --- |
| Restore dependencies | `dotnet restore src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj` |
| Build plugin | `dotnet build src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj` |
| Run all tests | `dotnet test tests/Jellyfin.Plugin.CastCrew.Tests/Jellyfin.Plugin.CastCrew.Tests.csproj` |
| Run a single test | `dotnet test tests/Jellyfin.Plugin.CastCrew.Tests/Jellyfin.Plugin.CastCrew.Tests.csproj --filter "FullyQualifiedName~CastCrewActorQueryNormalizerTests"` |
| Run integration tests | `CASTCREW_RUN_INTEGRATION_TESTS=true CASTCREW_BASE_URL=http://127.0.0.1:8096 CASTCREW_API_KEY=<key> dotnet test tests/Jellyfin.Plugin.CastCrew.IntegrationTests/Jellyfin.Plugin.CastCrew.IntegrationTests.csproj` |
| Build release zip locally | `dotnet publish src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj --configuration Release --framework net9.0 --output artifacts/publish && VERSION=$(sed -n '/<Version>/{s:.*<Version>\(.*\)</Version>.*:\1:p;q;}' src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj) && mkdir -p artifacts/CastCrew_${VERSION} && cp artifacts/publish/Jellyfin.Plugin.CastCrew.dll artifacts/CastCrew_${VERSION}/ && (cd artifacts && zip -r CastCrew_${VERSION}.zip CastCrew_${VERSION})` |
| Lint/format check | No dedicated lint command is configured in this repository. |

The plugin and test projects multi-target `net8.0` (Jellyfin 10.10.x) and `net9.0` (Jellyfin 10.11.x). Test projects set `RollForward=Major`, so tests can run on newer installed runtimes (for example .NET 10) when neither .NET 8 nor .NET 9 runtime is present locally.

## Local integration testing and cleanup

The developer runs a local Jellyfin instance (see `local_jellyfin_credentials.tmp` for credentials). When performing integration tests that require starting/restarting Jellyfin:

**CRITICAL: Environment cleanup after testing**

1. **Never leave background Jellyfin processes running.** After integration testing, always kill any Jellyfin process that was started by the agent. The user's normal Jellyfin app (launched from `/Applications/Jellyfin.app`) needs exclusive access to port 8096.
2. **Preferred approach**: Use the already-running Jellyfin instance for integration tests instead of restarting it. Only restart if you need to reload a newly-deployed plugin DLL.
3. **If you must restart Jellyfin**: After testing completes, kill the process you started so the user can re-open Jellyfin normally from the macOS app.
4. **Deploy-only (no restart)**: When deploying a plugin DLL, simply copy it to the plugins folder. The user will restart Jellyfin manually, or you can note that a restart is needed.
5. **Cleanup checklist** (run at end of integration testing):
   ```bash
   # Find and kill any agent-started Jellyfin processes
   # (The user's app uses "Jellyfin Server" parent; agent uses direct jellyfin CLI)
   ps aux | grep '[j]ellyfin.*--datadir' | grep -v 'Jellyfin Server' | awk '{print $2}' | xargs kill 2>/dev/null
   ```

**Plugin deployment path:** `~/Library/Application Support/jellyfin/plugins/CastCrew_0.1.0.0/Jellyfin.Plugin.CastCrew.dll`

## Copilot customization layout

- **Global baseline:** `.github/copilot-instructions.md` (this file).
- **Path-scoped instructions:** `.github/instructions/*.instructions.md` with `applyTo` globs.
- **Reusable skills:** `.github/skills/**/SKILL.md`.

Current skills:
- `castcrew-feature-development`
- `castcrew-route-compatibility`
- `castcrew-test-runtime-compatibility`

When making changes to architecture/workflow conventions, keep these locations in sync.

## High-level architecture

- This repository contains one Jellyfin server plugin project at `src/Jellyfin.Plugin.CastCrew`, a unit-test project at `tests/Jellyfin.Plugin.CastCrew.Tests`, and an integration-test project at `tests/Jellyfin.Plugin.CastCrew.IntegrationTests`, multi-targeting Jellyfin `10.10.7` (net8.0) and `10.11.11` (net9.0).
- `CastCrewPlugin` is the plugin entrypoint. It extends `BasePlugin<PluginConfiguration>` and implements `IHasWebPages` for the admin config page while syncing user-home navigation through `CastCrewWebConfigPatcher`.
- The user-facing Cast & Crew UI is rendered by synchronized `Web/castcrew-top-banner-link.js` inside the native Jellyfin home-shell route (`/web/#/home?tab=cast_crew`) and consumes plugin-owned adapter APIs (`GET /CastCrew/Actors`, `GET /CastCrew/Directors`, `GET /CastCrew/Producers`); `Web/cast-crew-standalone.html` remains a compatibility redirect resource. This Web integration is the primary supported UX surface.
- `CastCrewController` delegates actor querying to `CastCrewActorQueryService`, which uses `ILibraryManager` + `IDtoService` and normalizes query/config behavior via `CastCrewActorQueryNormalizer`.
- Admin settings are served by embedded `Configuration/config.html` and persisted through `PluginConfiguration`.
- Key plugin config values: `DefaultPageSize`, `DefaultSortBy`, `EnableCastCrewMainMenuEntry`, `DetailRoutePreference`.
- Integration tests are opt-in (`CASTCREW_RUN_INTEGRATION_TESTS=true`) and require a live Jellyfin host plus credentials.
- Packaging/release automation is implemented in `.github/workflows/package-plugin.yml`, producing `CastCrew_<Version>.zip` artifacts from release builds. The workflow runs on `ubuntu-latest`, but the produced package is host-platform agnostic for Jellyfin Linux/Windows/macOS.
- `DESIGN.md` is the canonical implementation baseline and milestone source for this plugin.

## Key conventions specific to this codebase

- Keep plugin identity and navigation metadata stable unless intentionally migrating behavior:
  - plugin ID GUID in `CastCrewPlugin.Id`
  - cast & crew page registration stays hidden from dashboard plugin drawer (`EnableInMainMenu = false`).
  - top-banner/sidebar link is managed by `CastCrewWebConfigPatcher` via `config.json` `menuLinks` and points to `/web/#/home?tab=cast_crew`.
  - route rendering is handled by synchronized `Web/castcrew-top-banner-link.js` inside Jellyfin web.
  - standalone compatibility page content is sourced from embedded `Web/cast-crew-standalone.html`.
- For hosted plugin pages, use the same 3-part registration flow:
  1. Add the file under `Web/`.
  2. Register it in `.csproj` as `EmbeddedResource`.
  3. Reference it from `PluginPageInfo.EmbeddedResourcePath` using the plugin namespace prefix.
- For standalone user-home pages, use `CastCrewWebConfigPatcher` to:
  1. sync the embedded `Web/*.html` resource into the Jellyfin web root,
  2. register/remove a `menuLinks` entry in `/web/config.json`.
- For configuration pages, mirror Jellyfin plugin conventions:
  1. Put admin UI under `Configuration/config.html`.
  2. Embed the file via `.csproj`.
  3. Use `ApiClient.getPluginConfiguration`/`updatePluginConfiguration` in the page script.
- Frontend code in `Web/actors.html` and `Web/cast-crew-standalone.html` is framework-free vanilla JS (IIFE); avoid introducing a frontend build pipeline unless requested.
- CastCrew navigation/page visibility depends on Jellyfin Web surfaces; do not assume native mobile clients expose web-injected plugin navigation.
- Keep the existing host compatibility fallbacks:
  - API fetch path prefers `ApiClient.getJSON`, then falls back to `fetch` with `ApiClient.getRequestHeaders`.
  - Person navigation route selection is compatibility-aware (`Auto`, `HashBang`, `Hash`) with fallback from `Dashboard.navigate` to hash/url navigation.
- Continue escaping API-provided strings via `escapeHtml` before HTML interpolation in card rendering.
- Assembly metadata is intentionally manual: `GenerateAssemblyInfo` is disabled in `.csproj`, and attributes are defined in `Properties/AssemblyInfo.cs`.
