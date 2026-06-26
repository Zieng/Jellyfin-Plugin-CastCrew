---
description: Guidance for CastCrew embedded web pages and host compatibility behavior.
applyTo:
  - "src/Jellyfin.Plugin.CastCrew/Web/**/*.html"
  - "src/Jellyfin.Plugin.CastCrew/Configuration/**/*.html"
---

# Web UI Instructions

## Framework and host model

- Keep frontend implementation framework-free (vanilla JS + IIFE style).
- Use Jellyfin host globals (`ApiClient`, `Dashboard`) instead of introducing build tooling.

## Compatibility behavior

- Preserve API call fallback (`ApiClient.getJSON` → `fetch` with request headers).
- Preserve route navigation fallback chain and route preference compatibility behavior.
- Keep user-facing strings centralized in the in-file translation map.

## Security and UX constraints

- Continue escaping API-provided text before HTML interpolation.
- Maintain accessibility semantics (`aria-*`, status regions, button labels).
- Keep state transitions centralized (loading, empty, error, pagination).
