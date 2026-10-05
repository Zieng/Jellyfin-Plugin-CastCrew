#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
VERSION="${1:-2.0.0}"
rm -rf artifacts
mkdir -p artifacts
for tfm in net8.0 net9.0; do
  dotnet publish src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj -c Release -f "$tfm" -p:Version="$VERSION" --output "artifacts/publish-$tfm"
  folder="CastCrew_${VERSION}_jellyfin-${tfm#net}"
  mkdir -p "artifacts/$folder"
  cp "artifacts/publish-$tfm/Jellyfin.Plugin.CastCrew.dll" "artifacts/$folder/"
  [ ! -f "artifacts/publish-$tfm/Jellyfin.Plugin.CastCrew.pdb" ] || cp "artifacts/publish-$tfm/Jellyfin.Plugin.CastCrew.pdb" "artifacts/$folder/"
  (cd "artifacts/$folder" && zip -q -r "../${folder}.zip" .)
done
sha256sum artifacts/*.zip | tee artifacts/SHA256SUMS.txt
