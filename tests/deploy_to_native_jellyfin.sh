#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_PATH="$ROOT_DIR/src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj"
PUBLISH_DIR="$ROOT_DIR/artifacts/local-native-publish"

BUILD_CONFIGURATION="${BUILD_CONFIGURATION:-Release}"
TARGET_FRAMEWORK="${TARGET_FRAMEWORK:-net9.0}"
VERSION_PREFIX="${VERSION_PREFIX:-0.1}"
STARTUP_TIMEOUT_SECONDS="${STARTUP_TIMEOUT_SECONDS:-180}"
CLEANUP_ONLY="false"

NATIVE_JELLYFIN_URL="${NATIVE_JELLYFIN_URL:-http://localhost:8096}"
NATIVE_JELLYFIN_USERNAME="${NATIVE_JELLYFIN_USERNAME:-admin}"
NATIVE_JELLYFIN_PASSWORD="${NATIVE_JELLYFIN_PASSWORD:-12345678}"
NATIVE_JELLYFIN_API_KEY="${NATIVE_JELLYFIN_API_KEY:-}"
NATIVE_JELLYFIN_APP_PATH="${NATIVE_JELLYFIN_APP_PATH:-/Applications/Jellyfin.app}"
NATIVE_JELLYFIN_DATA_DIR="${NATIVE_JELLYFIN_DATA_DIR:-$HOME/Library/Application Support/jellyfin}"

log() {
  echo "[native-deploy] $*"
}

fail() {
  echo "[native-deploy] ERROR: $*" >&2
  exit 1
}

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || fail "Missing required command: $1"
}

print_usage() {
  cat <<'EOF'
Usage: deploy_to_native_jellyfin.sh [--cleanup]

Options:
  --cleanup   Remove CastCrew plugin folders, restart Jellyfin, and verify plugin removal.
EOF
}

parse_args() {
  while (($# > 0)); do
    case "$1" in
      --cleanup)
        CLEANUP_ONLY="true"
        ;;
      -h|--help)
        print_usage
        exit 0
        ;;
      *)
        fail "Unknown option: $1"
        ;;
    esac
    shift
  done
}

wait_for_http() {
  local timeout_seconds="$1"
  local deadline=$((SECONDS + timeout_seconds))

  while ((SECONDS < deadline)); do
    if curl -fs "${NATIVE_JELLYFIN_URL}/System/Info/Public" >/dev/null; then
      return 0
    fi
    sleep 2
  done

  return 1
}

build_login_payload() {
  python3 -c 'import json,sys; print(json.dumps({"Username": sys.argv[1], "Pw": sys.argv[2]}))' \
    "$NATIVE_JELLYFIN_USERNAME" \
    "$NATIVE_JELLYFIN_PASSWORD"
}

fetch_access_token() {
  if [[ -n "$NATIVE_JELLYFIN_API_KEY" ]]; then
    printf '%s' "$NATIVE_JELLYFIN_API_KEY"
    return 0
  fi

  local login_payload
  login_payload="$(build_login_payload)"

  local auth_response
  auth_response="$(
    curl -fsS \
      -H 'Content-Type: application/json' \
      -H 'Accept: application/json' \
      -H 'X-Emby-Authorization: MediaBrowser Client="CastCrewDeployScript", Device="CLI", DeviceId="castcrew-native-deploy", Version="1.0"' \
      -d "$login_payload" \
      "${NATIVE_JELLYFIN_URL}/Users/AuthenticateByName"
  )"

  printf '%s' "$auth_response" | python3 -c '
import json, sys
data = json.load(sys.stdin)
token = data.get("AccessToken", "")
if not token:
    raise SystemExit("No AccessToken in Jellyfin auth response.")
print(token)
'
}

fetch_castcrew_version() {
  local token="$1"
  local plugins_response
  plugins_response="$(curl -fsS -H "Accept: application/json" -H "X-Emby-Token: ${token}" "${NATIVE_JELLYFIN_URL}/Plugins")"

  printf '%s' "$plugins_response" | python3 -c '
import json, sys
for plugin in json.load(sys.stdin):
    if plugin.get("Name") == "CastCrew":
        print(plugin.get("Version", ""))
        break
'
}

wait_for_expected_plugin_version() {
  local expected_version="$1"
  local deadline=$((SECONDS + STARTUP_TIMEOUT_SECONDS))

  while ((SECONDS < deadline)); do
    if wait_for_http 2; then
      local token
      token="$(fetch_access_token 2>/dev/null || true)"
      if [[ -n "$token" ]]; then
        local actual_version
        actual_version="$(fetch_castcrew_version "$token" 2>/dev/null || true)"
        if [[ "$actual_version" == "$expected_version" ]]; then
          log "Verified CastCrew version: $actual_version"
          return 0
        fi
      fi
    fi
    sleep 2
  done

  return 1
}

wait_for_plugin_removed() {
  local deadline=$((SECONDS + STARTUP_TIMEOUT_SECONDS))

  while ((SECONDS < deadline)); do
    if wait_for_http 2; then
      local token
      token="$(fetch_access_token 2>/dev/null || true)"
      if [[ -n "$token" ]]; then
        local actual_version
        actual_version="$(fetch_castcrew_version "$token" 2>/dev/null || true)"
        if [[ -z "$actual_version" ]]; then
          log "Verified CastCrew plugin removal."
          return 0
        fi
      fi
    fi
    sleep 2
  done

  return 1
}

find_native_jellyfin_pids() {
  ps ax -o pid= -o command= | awk '/\/Contents\/MacOS\/jellyfin/ && /--datadir/ { print $1 }'
}

stop_native_jellyfin() {
  local native_pids
  native_pids="$(find_native_jellyfin_pids | tr '\n' ' ')"

  if [[ -z "${native_pids// }" ]]; then
    log "No running native Jellyfin process found."
    return 0
  fi

  for pid in $native_pids; do
    log "Stopping Jellyfin process PID $pid"
    kill "$pid" || true
  done

  local deadline=$((SECONDS + 60))
  while ((SECONDS < deadline)); do
    native_pids="$(find_native_jellyfin_pids | tr '\n' ' ')"
    if [[ -z "${native_pids// }" ]]; then
      return 0
    fi
    sleep 1
  done

  native_pids="$(find_native_jellyfin_pids | tr '\n' ' ')"
  for pid in $native_pids; do
    log "Force stopping Jellyfin process PID $pid"
    kill -9 "$pid" || true
  done
}

start_native_jellyfin() {
  [[ -d "$NATIVE_JELLYFIN_APP_PATH" ]] || fail "Jellyfin app not found at: $NATIVE_JELLYFIN_APP_PATH"

  log "Starting Jellyfin app: $NATIVE_JELLYFIN_APP_PATH"
  if open -a "$NATIVE_JELLYFIN_APP_PATH" >/dev/null 2>&1; then
    if wait_for_http 20; then
      return 0
    fi
    log "App launch did not start the server in time. Falling back to direct server launch."
  else
    log "App launch command failed. Falling back to direct server launch."
  fi

  local jellyfin_bin="${NATIVE_JELLYFIN_APP_PATH}/Contents/MacOS/jellyfin"
  local ffmpeg_bin="${NATIVE_JELLYFIN_APP_PATH}/Contents/MacOS/ffmpeg"
  local web_dir="${NATIVE_JELLYFIN_APP_PATH}/Contents/Resources/jellyfin-web"

  [[ -x "$jellyfin_bin" ]] || fail "Jellyfin server binary not found: $jellyfin_bin"

  nohup "$jellyfin_bin" \
    --webdir "$web_dir" \
    --ffmpeg "$ffmpeg_bin" \
    --datadir "$NATIVE_JELLYFIN_DATA_DIR" \
    >/tmp/castcrew-native-jellyfin.log 2>&1 &
}

clean_plugin_folders() {
  local plugins_dir="${NATIVE_JELLYFIN_DATA_DIR}/plugins"
  mkdir -p "$plugins_dir"

  log "Cleaning old CastCrew plugin folders in: $plugins_dir"
  find "$plugins_dir" -maxdepth 1 -mindepth 1 -type d -name 'CastCrew_*' -exec rm -rf {} +
}

deploy_plugin() {
  local version="$1"
  local plugins_dir="${NATIVE_JELLYFIN_DATA_DIR}/plugins"
  local target_dir="${plugins_dir}/CastCrew_${version}"
  local plugin_dll="${PUBLISH_DIR}/Jellyfin.Plugin.CastCrew.dll"

  [[ -f "$plugin_dll" ]] || fail "Built plugin DLL not found: $plugin_dll"
  clean_plugin_folders

  log "Deploying plugin to: $target_dir"
  mkdir -p "$target_dir"
  cp "$plugin_dll" "$target_dir/Jellyfin.Plugin.CastCrew.dll"
}

main() {
  parse_args "$@"

  require_cmd curl
  require_cmd python3
  require_cmd open
  if [[ "$CLEANUP_ONLY" != "true" ]]; then
    require_cmd dotnet
  fi

  if [[ "$CLEANUP_ONLY" == "true" ]]; then
    log "Running cleanup mode."
    stop_native_jellyfin
    clean_plugin_folders
    start_native_jellyfin

    log "Waiting for Jellyfin to become reachable at $NATIVE_JELLYFIN_URL"
    wait_for_http "$STARTUP_TIMEOUT_SECONDS" || fail "Jellyfin did not become reachable in time."

    log "Verifying CastCrew plugin removal"
    wait_for_plugin_removed || fail "CastCrew plugin removal verification failed."

    log "Cleanup complete. CastCrew plugin is removed."
    return 0
  fi

  local version="${VERSION_PREFIX}.$(date +%y%j).$(date +%H%M)"
  log "Using plugin version: $version"

  rm -rf "$PUBLISH_DIR"
  mkdir -p "$PUBLISH_DIR"

  log "Publishing plugin ($TARGET_FRAMEWORK)"
  dotnet publish "$PROJECT_PATH" \
    --nologo \
    --configuration "$BUILD_CONFIGURATION" \
    --framework "$TARGET_FRAMEWORK" \
    -p:Version="$version" \
    --output "$PUBLISH_DIR"

  stop_native_jellyfin
  deploy_plugin "$version"
  start_native_jellyfin

  log "Waiting for Jellyfin to become reachable at $NATIVE_JELLYFIN_URL"
  wait_for_http "$STARTUP_TIMEOUT_SECONDS" || fail "Jellyfin did not become reachable in time."

  log "Verifying deployed CastCrew plugin version"
  wait_for_expected_plugin_version "$version" || fail "CastCrew version verification failed (expected: $version)."

  log "Deployment complete. CastCrew version $version is active."
}

main "$@"
