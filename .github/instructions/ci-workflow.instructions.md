---
description: CI/CD workflow guidance for the CastCrew plugin packaging pipeline.
applyTo:
  - ".github/workflows/*.yml"
---

# CI Workflow Instructions

## Manifest changelog

- When making user-visible changes (features, fixes, behavior changes), update the `changelog` strings in the manifest generation step of `package-plugin.yml` to reflect what changed in the current version.
- Keep changelog entries concise and user-facing (not implementation details).

## Version alignment

- The `targetAbi` values in the manifest must match the baseline `JellyfinVersion` in the `.csproj` (e.g., `10.11.0` → `"targetAbi": "10.11.0.0"`).
- The `JellyfinVersion` in the `.csproj` must be the **minimum** supported server version for each target framework series, not the latest.
- If target frameworks are added/removed in the `.csproj`, update the corresponding publish, package, and manifest steps.

## Validation

- After workflow changes, verify the workflow YAML is valid (`actions` syntax).
- Ensure artifact names, zip paths, and folder names remain consistent across steps.
