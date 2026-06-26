---
name: castcrew-test-runtime-compatibility
description: Use when dotnet test fails locally due to missing target runtime (for example net9 tests on a machine with only .NET 10 runtime installed).
---

# CastCrew Test Runtime Compatibility

## When to use

- `dotnet test` aborts with `You must install or update .NET`.
- `testhost.dll` requests `Microsoft.NETCore.App` version `9.0.0` but only newer major runtime is installed.

## Repo baseline

- Plugin and tests target `net9.0`.
- Test project intentionally sets `RollForward=Major` so tests can run on newer runtimes in local environments.

## Troubleshooting steps

1. Check installed runtimes:
   ```bash
   dotnet --list-runtimes
   ```
2. Verify test project runtime behavior:
   - `tests/Jellyfin.Plugin.CastCrew.Tests/Jellyfin.Plugin.CastCrew.Tests.csproj`
   - Ensure `TargetFramework` and `RollForward` are correct.
3. Re-run tests:
   ```bash
   dotnet test tests/Jellyfin.Plugin.CastCrew.Tests/Jellyfin.Plugin.CastCrew.Tests.csproj
   ```

## Documentation sync

If runtime behavior changes, update:

- `README.md` development prerequisites.
- `.github/copilot-instructions.md` build/test command notes.
