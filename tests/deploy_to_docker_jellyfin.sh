#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_PATH="$ROOT_DIR/src/Jellyfin.Plugin.CastCrew/Jellyfin.Plugin.CastCrew.csproj"
PUBLISH_DIR="$ROOT_DIR/artifacts/local-docker-publish"

BUILD_CONFIGURATION="${BUILD_CONFIGURATION:-Release}"
TARGET_FRAMEWORK="${TARGET_FRAMEWORK:-net9.0}"
VERSION_PREFIX="${VERSION_PREFIX:-0.1}"
STARTUP_TIMEOUT_SECONDS="${STARTUP_TIMEOUT_SECONDS:-180}"
CLEANUP_ONLY="false"

DOCKER_JELLYFIN_URL="${DOCKER_JELLYFIN_URL:-http://localhost:8098}"
DOCKER_JELLYFIN_USERNAME="${DOCKER_JELLYFIN_USERNAME:-root}"
DOCKER_JELLYFIN_PASSWORD="${DOCKER_JELLYFIN_PASSWORD:-test}"
DOCKER_JELLYFIN_API_KEY="${DOCKER_JELLYFIN_API_KEY:-}"

DOCKER_CONTAINER_NAME="${DOCKER_CONTAINER_NAME:-jellyfin-local}"
DOCKER_IMAGE="${DOCKER_IMAGE:-jellyfin/jellyfin:10.11.3}"
DOCKER_HOST_PORT="${DOCKER_HOST_PORT:-8098}"
DOCKER_CONFIG_DIR="${DOCKER_CONFIG_DIR:-/tmp/jf-local/config}"
DOCKER_CACHE_DIR="${DOCKER_CACHE_DIR:-/tmp/jf-local/cache}"
DOCKER_MEDIA_DIR="${DOCKER_MEDIA_DIR:-/tmp/jf-local/media}"

log() {
  echo "[docker-deploy] $*"
}

fail() {
  echo "[docker-deploy] ERROR: $*" >&2
  exit 1
}

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || fail "Missing required command: $1"
}

print_usage() {
  cat <<'EOF'
Usage: deploy_to_docker_jellyfin.sh [--cleanup]

Options:
  --cleanup   Remove CastCrew plugin folders, restart container, and verify plugin removal.
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
    if curl -fs "${DOCKER_JELLYFIN_URL}/System/Info/Public" >/dev/null; then
      return 0
    fi
    sleep 2
  done

  return 1
}

build_login_payload() {
  python3 -c 'import json,sys; print(json.dumps({"Username": sys.argv[1], "Pw": sys.argv[2]}))' \
    "$DOCKER_JELLYFIN_USERNAME" \
    "$DOCKER_JELLYFIN_PASSWORD"
}

fetch_access_token() {
  if [[ -n "$DOCKER_JELLYFIN_API_KEY" ]]; then
    printf '%s' "$DOCKER_JELLYFIN_API_KEY"
    return 0
  fi

  local login_payload
  login_payload="$(build_login_payload)"

  local auth_response
  auth_response="$(
    curl -fsS \
      -H 'Content-Type: application/json' \
      -H 'Accept: application/json' \
      -H 'X-Emby-Authorization: MediaBrowser Client="CastCrewDeployScript", Device="CLI", DeviceId="castcrew-docker-deploy", Version="1.0"' \
      -d "$login_payload" \
      "${DOCKER_JELLYFIN_URL}/Users/AuthenticateByName"
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
  plugins_response="$(curl -fsS -H "Accept: application/json" -H "X-Emby-Token: ${token}" "${DOCKER_JELLYFIN_URL}/Plugins")"

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

ensure_container_exists() {
  if ! docker container inspect "$DOCKER_CONTAINER_NAME" >/dev/null 2>&1; then
    log "Docker container '$DOCKER_CONTAINER_NAME' not found. Creating it."
    mkdir -p "$DOCKER_CONFIG_DIR" "$DOCKER_CACHE_DIR" "$DOCKER_MEDIA_DIR"

    docker create \
      --name "$DOCKER_CONTAINER_NAME" \
      -p "${DOCKER_HOST_PORT}:8096" \
      -v "${DOCKER_CONFIG_DIR}:/config" \
      -v "${DOCKER_CACHE_DIR}:/cache" \
      -v "${DOCKER_MEDIA_DIR}:/media:ro" \
      "$DOCKER_IMAGE" >/dev/null
    return 0
  fi

  local media_source
  media_source="$(docker inspect -f '{{range .Mounts}}{{if eq .Destination "/media"}}{{.Source}}{{end}}{{end}}' "$DOCKER_CONTAINER_NAME")"
  if [[ "$media_source" == "$DOCKER_MEDIA_DIR" ]]; then
    return 0
  fi

  log "Recreating container '$DOCKER_CONTAINER_NAME' to enforce /media mount: $DOCKER_MEDIA_DIR"

  local is_running image config_source host_port
  is_running="$(docker inspect -f '{{.State.Running}}' "$DOCKER_CONTAINER_NAME")"
  image="$(docker inspect -f '{{.Config.Image}}' "$DOCKER_CONTAINER_NAME")"
  config_source="$(docker inspect -f '{{range .Mounts}}{{if eq .Destination "/config"}}{{.Source}}{{end}}{{end}}' "$DOCKER_CONTAINER_NAME")"
  host_port="$(docker inspect -f '{{with index (index .NetworkSettings.Ports "8096/tcp") 0}}{{.HostPort}}{{end}}' "$DOCKER_CONTAINER_NAME")"

  [[ -n "$config_source" && -d "$config_source" ]] || config_source="$DOCKER_CONFIG_DIR"
  [[ -n "$host_port" ]] || host_port="$DOCKER_HOST_PORT"
  mkdir -p "$config_source" "$DOCKER_CACHE_DIR" "$DOCKER_MEDIA_DIR"

  if [[ "$is_running" == "true" ]]; then
    docker stop "$DOCKER_CONTAINER_NAME" >/dev/null
  fi

  docker rm "$DOCKER_CONTAINER_NAME" >/dev/null

  docker create \
    --name "$DOCKER_CONTAINER_NAME" \
    -p "${host_port}:8096" \
    -v "${config_source}:/config" \
    -v "${DOCKER_CACHE_DIR}:/cache" \
    -v "${DOCKER_MEDIA_DIR}:/media:ro" \
    "$image" >/dev/null
}

stop_container_if_running() {
  local is_running
  is_running="$(docker inspect -f '{{.State.Running}}' "$DOCKER_CONTAINER_NAME")"
  if [[ "$is_running" == "true" ]]; then
    log "Stopping Docker container: $DOCKER_CONTAINER_NAME"
    docker stop "$DOCKER_CONTAINER_NAME" >/dev/null
  else
    log "Docker container already stopped: $DOCKER_CONTAINER_NAME"
  fi
}

get_config_mount_source() {
  docker inspect -f '{{range .Mounts}}{{if eq .Destination "/config"}}{{.Source}}{{end}}{{end}}' "$DOCKER_CONTAINER_NAME"
}

seed_dummy_media_library() {
  log "Seeding dummy media library in: $DOCKER_MEDIA_DIR"
  mkdir -p \
    "$DOCKER_MEDIA_DIR/subfolder1" \
    "$DOCKER_MEDIA_DIR/subfolder2" \
    "$DOCKER_MEDIA_DIR/subfolder3"

  local movie_files=(
    "$DOCKER_MEDIA_DIR/subfolder1/The Godfather (1972).mkv"
    "$DOCKER_MEDIA_DIR/subfolder1/Inception (2010).mkv"
    "$DOCKER_MEDIA_DIR/subfolder2/The Dark Knight (2008).mkv"
    "$DOCKER_MEDIA_DIR/subfolder2/Parasite (2019).mkv"
    "$DOCKER_MEDIA_DIR/subfolder3/Pulp Fiction (1994).mkv"
    "$DOCKER_MEDIA_DIR/subfolder3/Spirited Away (2001).mkv"
  )

  local movie_file
  for movie_file in "${movie_files[@]}"; do
    : > "$movie_file"
  done
}

deploy_plugin() {
  local version="$1"
  local plugin_dll="${PUBLISH_DIR}/Jellyfin.Plugin.CastCrew.dll"

  [[ -f "$plugin_dll" ]] || fail "Built plugin DLL not found: $plugin_dll"
  clean_plugin_folders

  local config_source target_dir
  config_source="$(get_config_mount_source)"
  target_dir="${config_source}/plugins/CastCrew_${version}"

  log "Deploying plugin to: $target_dir"
  mkdir -p "$target_dir"
  cp "$plugin_dll" "$target_dir/Jellyfin.Plugin.CastCrew.dll"
}

clean_plugin_folders() {
  local config_source
  config_source="$(get_config_mount_source)"
  [[ -n "$config_source" ]] || fail "Container '$DOCKER_CONTAINER_NAME' does not have a /config mount."

  local plugins_dir="${config_source}/plugins"
  mkdir -p "$plugins_dir"

  log "Cleaning old CastCrew plugin folders in: $plugins_dir"
  find "$plugins_dir" -maxdepth 1 -mindepth 1 -type d -name 'CastCrew_*' -exec rm -rf {} +
}

start_container() {
  log "Starting Docker container: $DOCKER_CONTAINER_NAME"
  docker start "$DOCKER_CONTAINER_NAME" >/dev/null
}

main() {
  parse_args "$@"

  require_cmd curl
  require_cmd python3
  require_cmd docker
  if [[ "$CLEANUP_ONLY" != "true" ]]; then
    require_cmd dotnet
  fi

  if [[ "$CLEANUP_ONLY" == "true" ]]; then
    if ! docker container inspect "$DOCKER_CONTAINER_NAME" >/dev/null 2>&1; then
      log "Container '$DOCKER_CONTAINER_NAME' not found. Nothing to clean up."
      return 0
    fi

    log "Running cleanup mode."
    stop_container_if_running
    clean_plugin_folders
    start_container

    log "Waiting for Jellyfin to become reachable at $DOCKER_JELLYFIN_URL"
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

  seed_dummy_media_library
  ensure_container_exists
  stop_container_if_running
  deploy_plugin "$version"
  start_container

  log "Waiting for Jellyfin to become reachable at $DOCKER_JELLYFIN_URL"
  wait_for_http "$STARTUP_TIMEOUT_SECONDS" || fail "Jellyfin did not become reachable in time."

  log "Verifying deployed CastCrew plugin version"
  wait_for_expected_plugin_version "$version" || fail "CastCrew version verification failed (expected: $version)."

  log "Deployment complete. CastCrew version $version is active."
}

main "$@"
