# Jellyfin-Plugin-CastCrew

CastCrew is a Jellyfin plugin that provides a Cast & Crew browsing experience in Jellyfin Web with search, sorting, pagination, and person detail navigation, plus optional user-home navigation integration for a `Cast&Crew` entry near Home/Favorites.

## Screenshots

I blurred the actor avators to avoid possible copyright violation.

![Cast & Crew Browse](docs/images/cast-crew-browse.png)

<details>
<summary>Navigation Demo (GIF)</summary>

![Navigation Entry](docs/images/navigation-entry.gif)

</details>

## Installation

### From Plugin Repository (recommended)

1. In Jellyfin, go to **Dashboard → Plugins → Repositories**.
2. Add a new repository:
   - **Name:** `CastCrew`
   - **URL:** `https://zieng.github.io/Jellyfin-Plugin-CastCrew/manifest.json`
3. Go to **Catalog**, find **CastCrew**, and click **Install**.
4. Restart Jellyfin.

Jellyfin will automatically select the correct version for your server (10.10.x or 10.11.x).

### Manual Installation

1. Download the correct zip from [Releases](https://github.com/Zieng/Jellyfin-Plugin-CastCrew/releases):
   - `CastCrew_<version>_jellyfin-10.10.zip` for Jellyfin 10.10.x
   - `CastCrew_<version>_jellyfin-10.11.zip` for Jellyfin 10.11.x
2. Extract into your Jellyfin plugins directory (e.g., `<data>/plugins/CastCrew_<version>/`).
3. Restart Jellyfin.

## Current Implementation

- Server-side plugin scaffold in C# targeting Jellyfin `10.10.7` (net8.0) and `10.11.11` (net9.0) via multi-target build.
- Plugin page registration includes admin configuration plus a conditional installer-safe fallback `Cast&Crew` main-menu page when web-root sync fails (for example on read-only web roots).
- Automatic Jellyfin web `config.json` sync to add/remove a Cast&Crew navigation link.
- Optional helper script (`castcrew-top-banner-link.js`) is synchronized into Jellyfin web to render CastCrew content inside the native home-shell route.
- Cast&Crew navigation targets the embedded home route (`/web/#/home?tab=cast_crew`) so native Jellyfin sidebar/top-banner chrome stays intact.
- `/web/cast-crew.html` remains as a compatibility URL that redirects to the embedded home route.
- CastCrew UI entry/rendering is web-shell based; native Jellyfin clients that do not load Jellyfin Web will not show the Cast&Crew page/menu.
- Dedicated server adapter endpoints at `GET /CastCrew/Actors`, `GET /CastCrew/Directors`, and `GET /CastCrew/Producers` for stable cast/crew query contracts.
- Person query path aligned with Jellyfin `Persons` behavior (`InternalPeopleQuery`) so cast/crew lists populate reliably on Jellyfin 10.10.x and 10.11.x.
- Packaging automation runs on GitHub Actions `ubuntu-latest`, and produces a platform-neutral plugin zip usable by Jellyfin on Linux, Windows, and macOS.
- Embedded cast & crew view includes:
  - Role tabs (`Actors`, `Directors`, `Producers`)
  - Person card grid
  - Search with grouped name/description matches
  - Sorting (`Name`, `DateCreated`, `Random`) with sort order control
  - Favorites/tag/country-region filters
  - Pagination
  - Loading / empty / error states
  - Route compatibility detection and fallback strategy for person detail navigation
  - Localization-ready UI strings (`en`, `zh`) and accessibility-focused semantics
- Admin configuration page at `Configuration/config.html` for:
  - Default page size
  - Default sort mode
  - Top-banner entry behavior
  - Route preference override (`Auto`, `HashBang`, `Hash`)
- Unit tests for query normalization (paging/sort/filter/route preferences) and route preference normalization.

## Configuration Defaults

- `DefaultPageSize`: `50` (min `10`, max `200`)
- `DefaultSortBy`: `Name` (`Name` or `DateCreated`)
- `EnableCastCrewMainMenuEntry`: `true`
- `DetailRoutePreference`: `Auto` (`Auto`, `HashBang`, `Hash`)

## Adapter API Contract

- Endpoints:
  - `GET /CastCrew/Actors`
  - `GET /CastCrew/Directors`
  - `GET /CastCrew/Producers`
- Query:
  - `startIndex`, `limit`, `searchTerm`, `sortBy`, `sortOrder`, `isFavorite`, `tag`, `productionLocation`, optional `userId`
- Response:
  - `Items`, `TotalRecordCount`, `StartIndex`, `PageSize`, `SortBy`, `DetailRoutePreference`
  - `AvailableTags`, `AvailableProductionLocations`
  - optional grouped search payload: `NameMatchItems`, `NameMatchCount`, `DescriptionMatchItems`, `DescriptionMatchCount`

## Host Notes (read-only web roots)

- Some installs use a read-only web root (for example `C:\Program Files\Jellyfin\Server\jellyfin-web` on Windows installer installs, or `/Applications/Jellyfin.app/.../jellyfin-web` on macOS app bundles).
- In that mode, CastCrew cannot write `config.json`, inject `castcrew-top-banner-link.js`, or update `/web/cast-crew.html`, so the Cast&Crew sidebar/top-banner entry will not auto-sync.
- When auto-sync fails, CastCrew exposes a fallback `Cast&Crew` plugin-page menu entry so browsing still works without writable web assets.
- If you need automatic Cast&Crew-link/script syncing, run Jellyfin with a writable `--webdir` (for example, a copied web directory under your user data path).
- Example startup override (Windows):
  - `jellyfin --webdir "%LOCALAPPDATA%\Jellyfin\custom-web" --datadir "%LOCALAPPDATA%\Jellyfin"`
- Example startup override (macOS):
  - `jellyfin --webdir "$HOME/Library/Application Support/jellyfin/custom-web" --datadir "$HOME/Library/Application Support/jellyfin"`

## Client and Platform Limitations

- CastCrew navigation/page rendering is implemented in Jellyfin Web (`/web/#/home?tab=cast_crew`) via web resource sync and script injection.
- Desktop browsers and mobile browsers using Jellyfin Web are supported.
- Native mobile clients that render their own UI (without Jellyfin Web) do not automatically expose the CastCrew menu/page.
- CastCrew server endpoints (`/CastCrew/*`) remain available on the host even when a specific client app does not expose the plugin UI.

## Development Commands

- Prerequisite: .NET SDK 8+ (plugin multi-targets `net8.0` and `net9.0`; test projects set `RollForward=Major` to run on newer runtimes when needed).
- Restore:
  - `dotnet restore src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj`
- Build:
  - `dotnet build src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj`
- Run tests:
  - `dotnet test tests/Jellyfin.Plugin.CastCrew.Tests/Jellyfin.Plugin.CastCrew.Tests.csproj`
- Run a single test:
  - `dotnet test tests/Jellyfin.Plugin.CastCrew.Tests/Jellyfin.Plugin.CastCrew.Tests.csproj --filter "FullyQualifiedName~CastCrewActorQueryNormalizerTests"`
- Run integration tests (requires running Jellyfin + auth):
  - `CASTCREW_RUN_INTEGRATION_TESTS=true CASTCREW_BASE_URL=http://127.0.0.1:8096 CASTCREW_API_KEY=<your_api_key> dotnet test tests/Jellyfin.Plugin.CastCrew.IntegrationTests/Jellyfin.Plugin.CastCrew.IntegrationTests.csproj`
- Build a release zip locally:
  - `dotnet publish src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj --configuration Release --output artifacts/publish && VERSION=$(sed -n '/<Version>/{s:.*<Version>\(.*\)</Version>.*:\1:p;q;}' src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj) && mkdir -p artifacts/CastCrew_${VERSION} && cp artifacts/publish/Jellyfin.Plugin.CastCrew.dll artifacts/CastCrew_${VERSION}/ && (cd artifacts && zip -r CastCrew_${VERSION}.zip CastCrew_${VERSION})`

### Integration test environment variables

- `CASTCREW_RUN_INTEGRATION_TESTS`: set to `true` to enable integration execution.
- `CASTCREW_BASE_URL`: Jellyfin host base URL (default `http://127.0.0.1:8096`).
- Auth options:
  - `CASTCREW_API_KEY` **or**
  - `CASTCREW_USERNAME` + `CASTCREW_PASSWORD`
- Optional:
  - `CASTCREW_USER_ID` to force a specific user context in CastCrew endpoint queries.

## Project Structure

- `src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj`
- `src/Jellyfin.Plugin.CastCrew/CastCrewPlugin.cs`
- `src/Jellyfin.Plugin.CastCrew/PluginServiceRegistrator.cs`
- `src/Jellyfin.Plugin.CastCrew/Api/`
- `src/Jellyfin.Plugin.CastCrew/Services/`
- `src/Jellyfin.Plugin.CastCrew/Configuration/`
- `src/Jellyfin.Plugin.CastCrew/Web/actors.html`
- `src/Jellyfin.Plugin.CastCrew/Web/cast-crew-standalone.html`
- `tests/Jellyfin.Plugin.CastCrew.Tests/`
- `tests/Jellyfin.Plugin.CastCrew.IntegrationTests/`

## AI Development Support (Copilot)

- Global instructions: `.github/copilot-instructions.md`
- Path-scoped instructions: `.github/instructions/`
- Reusable Copilot skills: `.github/skills/`

Key repo skills currently available:

- `castcrew-feature-development`
- `castcrew-route-compatibility`
- `castcrew-test-runtime-compatibility`

## Milestone Plan Status

1. Milestones A-G from `DESIGN.md` are complete.
2. Packaging/release automation is in `.github/workflows/package-plugin.yml` and publishes `CastCrew_<Version>.zip` artifacts on tags/workflow dispatch.
