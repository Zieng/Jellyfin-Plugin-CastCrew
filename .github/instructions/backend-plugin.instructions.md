---
description: Backend C# guidance for CastCrew plugin architecture, contracts, and service wiring.
applyTo:
  - "src/Jellyfin.Plugin.CastCrew/**/*.cs"
  - "src/Jellyfin.Plugin.CastCrew/*.csproj"
---

# Backend Plugin Instructions

## Architecture boundaries

- Keep HTTP request/response models in `Api/`.
- Keep business/query logic in `Services/`.
- Keep plugin configuration defaults and bounds in `Configuration/PluginConfiguration.cs`.
- Keep service DI registration in `PluginServiceRegistrator.cs`.

## Change rules

- Prefer extending existing normalizers/services over duplicating logic.
- Keep API output stable unless a contract change is intentional and documented.
- If adding hosted page files, update `.csproj` `EmbeddedResource` entries.
- Preserve plugin identity constants (`Id`, key page names) unless explicitly migrating behavior.

## Validation

- Build and run tests after backend changes.
