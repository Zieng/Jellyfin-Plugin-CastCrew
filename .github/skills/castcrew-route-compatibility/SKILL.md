---
name: castcrew-route-compatibility
description: Use when changing actor-card navigation or Jellyfin person-detail route behavior, including hash/hashbang fallback compatibility.
---

# CastCrew Route Compatibility

## When to use

- Actor detail navigation fails or opens wrong pages.
- Jellyfin web route shape differs across versions/builds.
- Route preference behavior (`Auto`, `HashBang`, `Hash`) must change.

## Route system in this repo

- Frontend route handling lives in `src/Jellyfin.Plugin.CastCrew/Web/cast-crew-standalone.html` (user-home path) and `src/Jellyfin.Plugin.CastCrew/Web/actors.html` (legacy embedded page).
- Backend returns `DetailRoutePreference` from `GET /CastCrew/Actors`.
- Config source is `PluginConfiguration.DetailRoutePreference`.

## Safe change procedure

1. Keep preference normalization aligned:
   - C#: `CastCrewActorQueryNormalizer.NormalizeDetailRoutePreference`
   - JS: `normalizeRoutePreference`
2. Preserve multi-strategy fallbacks:
   - `Dashboard.navigate(...)`
   - `window.location.hash`
   - `window.location.href`
3. Keep hash prefix and detail-path ordering logic centralized:
   - `getRoutePrefixes`
   - `getDetailPathOrder`
   - `buildPersonRoutes`
4. Verify route behavior with both default auto mode and explicit forced mode.

## Regression checks

- Clicking actor cards still navigates reliably.
- Search/sort/pagination behavior is unaffected.
- No hard dependency on a single undocumented route form.
