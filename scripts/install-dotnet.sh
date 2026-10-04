#!/usr/bin/env bash
set -euo pipefail
INSTALL_DIR="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"
mkdir -p "$INSTALL_DIR"
for version in 8.0 9.0; do
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel "$version" --install-dir "$INSTALL_DIR" --no-path
done
export PATH="$INSTALL_DIR:$PATH"
dotnet --list-sdks
