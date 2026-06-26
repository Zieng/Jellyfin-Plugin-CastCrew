---
name: castcrew-feature-development
description: Use when implementing or refactoring CastCrew features that span backend API, plugin configuration, embedded web pages, and tests/docs in this repository.
---

# CastCrew Feature Development

## When to use

- Adding a new user-facing CastCrew capability.
- Refactoring behavior that touches both C# and embedded HTML/JS.
- Changing plugin configuration options that affect runtime behavior.

## Required touchpoints checklist

1. **Plugin model/config**
   - Update `src/Jellyfin.Plugin.CastCrew/Configuration/PluginConfiguration.cs`.
2. **Dependency wiring**
   - Register new services via `PluginServiceRegistrator`.
3. **API surface**
   - Keep request/response contracts in `src/Jellyfin.Plugin.CastCrew/Api/`.
4. **Service logic**
   - Put data/query behavior in `src/Jellyfin.Plugin.CastCrew/Services/`.
5. **UI resources**
   - Update `Web/*.html` or `Configuration/config.html` and ensure resources are embedded in `.csproj`.
6. **Docs/tests**
   - Update `README.md`, `DESIGN.md`, `.github/copilot-instructions.md` as needed.
   - Add/adjust tests in `tests/Jellyfin.Plugin.CastCrew.Tests/`.

## Implementation workflow

1. Change backend contracts and service logic first.
2. Update frontend to consume backend output contract (avoid implicit assumptions).
3. Ensure configuration defaults and guards align with frontend behavior.
4. Add tests for normalization/edge-case logic in service helpers.
5. Sync documentation for commands, defaults, and behavior changes.

## Validation commands

```bash
dotnet build src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj
dotnet test tests/Jellyfin.Plugin.CastCrew.Tests/Jellyfin.Plugin.CastCrew.Tests.csproj
```

## Repo-specific pitfalls

- Keep plugin target framework aligned with Jellyfin host compatibility.
- Do not forget `EmbeddedResource` entries when adding hosted pages.
- Preserve route fallback behavior in `Web/cast-crew-standalone.html` (and `Web/actors.html` when touching legacy embedded behavior) unless intentionally changing compatibility strategy.
