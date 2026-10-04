# CastCrew 2.0 Deployment Guide

## Requirements

- Jellyfin 10.10.x: .NET 8 / `net8.0`
- Jellyfin 10.11.x: .NET 9 / `net9.0`
- Git 2.30+
- Internet access for NuGet and metadata providers

## Install SDKs

Linux/macOS:

```bash
./scripts/install-dotnet.sh
export PATH="$HOME/.dotnet:$PATH"
```

Windows PowerShell:

```powershell
.\scripts\install-dotnet.ps1
$env:PATH="$HOME\.dotnet;$env:PATH"
```

## Build and test

```bash
./scripts/build.sh
```

or:

```powershell
.\scripts\build.ps1
```

## Create plugin packages

```bash
./scripts/package.sh 2.0.0
```

The script creates separate `net8.0` and `net9.0` Jellyfin plugin ZIPs and SHA-256 checksums.

## Install

Stop Jellyfin, extract the matching ZIP into the Jellyfin plugins directory, then restart Jellyfin. The DLL must be directly inside the plugin package directory.

## Metadata configuration

Open **Dashboard → Plugins → CastCrew** and independently enable TMDB, MASex and Netflav. Enter a TMDB Bearer Token when TMDB is enabled. The MASex and Netflav actor URLs are configurable because external sites can change their routes.

## Git release

The repository keeps its complete `.git` history. To create and push branch `2.0` and tag `2.0` from the current `main`:

```bash
./scripts/release-push.sh
```

Do not place GitHub credentials in source files or scripts. Use Git Credential Manager, SSH, or an authenticated GitHub CLI session.
