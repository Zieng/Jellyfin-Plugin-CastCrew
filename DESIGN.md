# Jellyfin-Plugin-CastCrew Design Document

Version: 2.1  
Status: Active (Milestones A-G Complete)  
Target Host: Jellyfin 10.10.x / 10.11.x  
Plugin Type: Server Plugin (C#) + Hosted Web Page

## 1. Background

CastCrew adds a dedicated Cast & Crew module to Jellyfin so users can browse all actors, directors, producers, and other crew members, and open person detail pages with biography and related media.

## 2. Product Goals

1. Add a `Cast&Crew` entry in Jellyfin main navigation.
2. Provide a cast & crew index page similar to media library browsing.
3. Clicking a person opens Jellyfin native person detail page.
4. Keep implementation compatible with Jellyfin 10.11.x plugin architecture.

## 3. Scope

### In Scope

1. Server-side plugin scaffold and page registration.
2. Cast & Crew menu entry and hosted browsing page.
3. Person list with search, sort, and pagination.
4. Person click-to-detail navigation.
5. Basic loading, empty, and error states.

### Out of Scope

1. Metadata editing for people.
2. Scraper or metadata pipeline customization.
3. Replacing Jellyfin native person detail page.
4. Modifying Jellyfin core source.
5. Native mobile-client UI integration outside Jellyfin Web.

## 4. Architecture Decision

The implementation uses a server plugin (C#) that hosts a web page resource.

Rationale:

1. Aligns with existing local plugin references.
2. More stable main menu integration via `IHasWebPages` and `PluginPageInfo`.
3. Better long-term compatibility than pure web-injection-only approach.

## 5. Functional Design

### 5.1 Navigation

1. Plugin synchronizes Jellyfin web `config.json` `menuLinks` to provide a `Cast&Crew` navigation link.
2. Plugin synchronizes a helper script into Jellyfin web (`castcrew-top-banner-link.js`) to render CastCrew content inside the native home-shell content area.
3. The primary Cast&Crew entry points to `/web/#/home?tab=cast_crew`.
4. Plugin synchronizes `/web/cast-crew.html` as a compatibility redirect to the embedded home-shell route.
5. When web-root sync fails (for example read-only installer web roots), plugin exposes a fallback `EnableInMainMenu` Cast&Crew page backed by embedded `Web/actors.html`.
6. Menu icon target is person.

### 5.2 Cast & Crew Page Behavior

1. Load cast/crew persons via CastCrew server adapter endpoints (`/CastCrew/Actors`, `/CastCrew/Directors`, `/CastCrew/Producers`) based on active role tab.
2. Support query controls:
   - Search by person name.
   - Sort by Name, DateCreated, or Random with ascending/descending controls.
   - Filter by favorites, tags, and country/region.
   - Pagination with previous/next.
3. Render person cards with image, name, and biography snippet inside Jellyfin home-shell content area.
4. On click, navigate to native Jellyfin details route using fallback strategy.

### 5.3 Data Contract (Current)

1. Source endpoints:
   - `CastCrew/Actors`
   - `CastCrew/Directors`
   - `CastCrew/Producers`
2. Query fields:
   - `startIndex`, `limit`
   - `searchTerm`
   - `sortBy` (`Name`, `DateCreated`, `Random`)
   - `sortOrder` (`Ascending`, `Descending`)
   - `isFavorite`
   - `tag`
   - `productionLocation`
   - optional `userId`
3. Result model:
   - `Items` (`BaseItemDto[]`)
   - `TotalRecordCount`
   - `StartIndex`
   - `PageSize`
   - `SortBy`
   - `DetailRoutePreference`
   - `AvailableTags`
   - `AvailableProductionLocations`
   - optional grouped search fields: `NameMatchItems`, `NameMatchCount`, `DescriptionMatchItems`, `DescriptionMatchCount`

## 6. Non-Functional Requirements

1. Compatible with Jellyfin 10.10.x (.NET 8) and 10.11.x (.NET 9) via multi-target build.
2. Works on desktop and mobile layouts within Jellyfin Web-based clients.
3. Graceful UI fallback for missing images or API failures.
4. Avoid hard dependency on undocumented web route internals.

## 7. Risk and Mitigation

1. Risk: Native person route shape varies by web build.
   - Mitigation: Centralize route fallback logic in cast & crew page.
2. Risk: Menu placement and web root writeability vary across host builds (notably Windows installer installs under `Program Files` and macOS app bundles under `/Applications`).
   - Mitigation: Keep adapter endpoint independent of web patching; use best-effort menu/script/page sync and document writable `--webdir` requirement for automatic navigation updates.
3. Risk: Large person libraries may be expensive to load.
   - Mitigation: Paginated query and incremental loading.
4. Risk: Native Jellyfin mobile clients may not render web-injected CastCrew navigation/page surfaces.
   - Mitigation: Keep `/CastCrew/*` endpoints independent and treat Web UI integration as the primary supported UX surface.

## 8. Implementation Status (Updated)

### Completed

1. Created plugin project and package references.
2. Implemented plugin class with `IHasWebPages` registration.
3. Added plugin configuration model.
4. Added assembly metadata.
5. Added cast & crew UI resources (`cast-crew-standalone.html`, `castcrew-top-banner-link.js`, and `actors.html`) with:
   - Person grid
   - Search and sorting
   - Pagination
   - Loading, empty, and error states
   - Person card click navigation fallback
6. Added repository README with current status and milestones.
7. Added dedicated server controller/service adapter (`CastCrewController` + `CastCrewActorQueryService`) for actor queries.
8. Refactored frontend page to consume adapter responses.
9. Added admin configuration page for page size, default sort, top-banner behavior, and route preference.
10. Improved route resolver compatibility detection and fallback behavior.
11. Added localization-ready string map and accessibility-focused page semantics.
12. Added unit tests for query normalization logic.
13. Added lightweight opt-in integration tests that exercise `/CastCrew/Actors` against a running Jellyfin host.
14. Added web `config.json` sync for `Cast&Crew` navigation plus installer-safe fallback menu-page exposure when web-root sync fails.
15. Added top-banner Cast&Crew-link/script synchronization through Jellyfin web `config.json` and `index.html` patching when host web assets are writable.
16. Aligned actor retrieval with Jellyfin `Persons` query behavior to return populated results on 10.11.x.
17. Embedded Cast&Crew content into Jellyfin home-shell route (`#/home?tab=cast_crew`) and kept `/web/cast-crew.html` as a compatibility redirect.
18. Added `/CastCrew/Directors` and `/CastCrew/Producers` endpoints with shared query normalization/contract behavior.
19. Added packaging/release automation via GitHub Actions (`.github/workflows/package-plugin.yml`) to build/test and produce versioned plugin zip artifacts.
20. Multi-target build support for Jellyfin 10.10.x (net8.0) and 10.11.x (net9.0), producing separate plugin DLLs per host version.

### Pending

1. No open items remain for milestones A-G.

## 9. Milestone Plan for Copilot CLI

1. Milestone A: Plugin scaffold hardening and build pipeline. ✅
2. Milestone B: Actor API adapter layer on server side. ✅
3. Milestone C: Front-end refactor to consume adapter endpoints. ✅
4. Milestone D: Route resolver hardening for person detail navigation. ✅
5. Milestone E: Admin config page and persisted options. ✅
6. Milestone F: Tests and packaging. ✅
7. Milestone G: Directors and Producers endpoints. ✅

## 10. Acceptance Criteria

1. `Cast&Crew` appears in the Jellyfin Web main menu for authenticated users by default (unless disabled in plugin configuration).
2. Cast & Crew page can list and paginate person results.
3. Search and sorting work as expected.
4. Clicking a person opens Jellyfin person detail page.
5. Error and empty states are user-visible and non-blocking.
6. Plugin behavior remains functional on Jellyfin 10.11.x.
7. Main-menu/page behavior is guaranteed for Jellyfin Web clients; native non-web clients are not required to expose CastCrew UI.

## 11. Reference Baseline Used
Local server reference: ../code/jellyfin (https://github.com/jellyfin/jellyfin)

This document is the canonical design baseline for subsequent code generation tasks.
