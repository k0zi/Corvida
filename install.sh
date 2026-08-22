#!/usr/bin/env bash
#
# Corvida installer — sets up Podman (default) or Docker from scratch and runs
# the REST API + PostgreSQL via Compose, exposed on port 5083, plus the browser
# UI on 8183 (HTTP) and 8283 (HTTPS).
#
# Usage:
#   ./install.sh [--engine podman|docker] [--port 5083] [--dir ~/corvida-api]
#                [--web-port 8183] [--web-https-port 8283] [--no-web]
#                [--repo <git-url>] [--branch <name>]
#
# Can also be run standalone (e.g. via curl | bash), in which case it clones
# the repo into --dir instead of using the current checkout.

set -euo pipefail

# ---- defaults ---------------------------------------------------------
ENGINE="podman"
PORT="5083"
WEB_PORT="8183"
WEB_HTTPS_PORT="8283"
WITH_WEB="yes"
REPO_URL="https://github.com/k0zi/Corvida.git"
BRANCH=""
INSTALL_DIR=""

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
if [[ -f "$SCRIPT_DIR/docker-compose.yml" ]]; then
  INSTALL_DIR="$SCRIPT_DIR"
fi

# ---- args ---------------------------------------------------------------
while [[ $# -gt 0 ]]; do
  case "$1" in
    --engine) ENGINE="$2"; shift 2 ;;
    --port) PORT="$2"; shift 2 ;;
    --web-port) WEB_PORT="$2"; shift 2 ;;
    --web-https-port) WEB_HTTPS_PORT="$2"; shift 2 ;;
    --no-web) WITH_WEB="no"; shift ;;
    --dir) INSTALL_DIR="$2"; shift 2 ;;
    --repo) REPO_URL="$2"; shift 2 ;;
    --branch) BRANCH="$2"; shift 2 ;;
    -h|--help)
      sed -n '2,14p' "${BASH_SOURCE[0]}"
      exit 0
      ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

if [[ "$ENGINE" != "podman" && "$ENGINE" != "docker" ]]; then
  echo "error: --engine must be 'podman' or 'docker' (got '$ENGINE')" >&2
  exit 1
fi

[[ -n "$INSTALL_DIR" ]] || INSTALL_DIR="$HOME/corvida-api"

log()  { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m!!\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31mERROR:\033[0m %s\n' "$*" >&2; exit 1; }

need_sudo() {
  if [[ $EUID -ne 0 ]]; then
    command -v sudo >/dev/null 2>&1 || die "root privileges required and 'sudo' not found"
    sudo "$@"
  else
    "$@"
  fi
}

# ---- distro / package manager -------------------------------------------
detect_pkg_manager() {
  if command -v apt-get >/dev/null 2>&1; then echo apt
  elif command -v dnf >/dev/null 2>&1; then echo dnf
  elif command -v yum >/dev/null 2>&1; then echo yum
  elif command -v pacman >/dev/null 2>&1; then echo pacman
  elif command -v zypper >/dev/null 2>&1; then echo zypper
  else echo unknown
  fi
}

PKG_MGR="$(detect_pkg_manager)"

install_podman() {
  log "Installing Podman + Compose provider ($PKG_MGR)"
  case "$PKG_MGR" in
    apt)
      need_sudo apt-get update -y
      need_sudo apt-get install -y podman podman-compose uidmap slirp4netns
      ;;
    dnf)
      need_sudo dnf install -y podman podman-compose
      ;;
    yum)
      need_sudo yum install -y podman podman-compose
      ;;
    pacman)
      need_sudo pacman -Sy --noconfirm podman podman-compose
      ;;
    zypper)
      need_sudo zypper install -y podman podman-compose
      ;;
    *)
      die "unsupported package manager — install Podman manually: https://podman.io/docs/installation"
      ;;
  esac
}

install_docker() {
  die "Docker is not installed. Install it first (e.g. https://docs.docker.com/engine/install/) or re-run with --engine podman to auto-install Podman instead."
}

ensure_engine() {
  if command -v "$ENGINE" >/dev/null 2>&1; then
    log "$ENGINE already installed ($($ENGINE --version))"
    return
  fi

  if [[ "$ENGINE" == "podman" ]]; then
    install_podman
  else
    install_docker
  fi

  command -v "$ENGINE" >/dev/null 2>&1 || die "$ENGINE installation failed"
}

ensure_compose() {
  if "$ENGINE" compose version >/dev/null 2>&1; then
    log "$ENGINE compose available"
    return
  fi

  if [[ "$ENGINE" == "podman" ]]; then
    log "No compose provider found for Podman, installing podman-compose"
    case "$PKG_MGR" in
      apt) need_sudo apt-get install -y podman-compose ;;
      dnf) need_sudo dnf install -y podman-compose ;;
      yum) need_sudo yum install -y podman-compose ;;
      pacman) need_sudo pacman -Sy --noconfirm podman-compose ;;
      zypper) need_sudo zypper install -y podman-compose ;;
      *) command -v pip3 >/dev/null 2>&1 && pip3 install --user podman-compose \
           || die "install a compose provider for Podman manually (podman-compose or docker-compose)" ;;
    esac
    "$ENGINE" compose version >/dev/null 2>&1 || die "podman compose still unavailable after installing podman-compose"
  else
    die "docker compose plugin is missing — install docker-compose-plugin"
  fi
}

# ---- get the repo ---------------------------------------------------------
fetch_repo() {
  if [[ -f "$INSTALL_DIR/docker-compose.yml" ]]; then
    log "Using existing checkout at $INSTALL_DIR"
    return
  fi

  command -v git >/dev/null 2>&1 || {
    log "Installing git"
    case "$PKG_MGR" in
      apt) need_sudo apt-get install -y git ;;
      dnf) need_sudo dnf install -y git ;;
      yum) need_sudo yum install -y git ;;
      pacman) need_sudo pacman -Sy --noconfirm git ;;
      zypper) need_sudo zypper install -y git ;;
      *) die "install git manually" ;;
    esac
  }

  log "Cloning $REPO_URL into $INSTALL_DIR"
  local clone_args=(--depth 1)
  [[ -n "$BRANCH" ]] && clone_args+=(--branch "$BRANCH")
  git clone "${clone_args[@]}" "$REPO_URL" "$INSTALL_DIR"
}

# ---- .env / secrets ---------------------------------------------------------
write_env() {
  local env_file="$INSTALL_DIR/.env"
  if [[ -f "$env_file" ]]; then
    log ".env already exists, keeping it"
    return
  fi

  local password
  password="$(head -c 24 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 24)"

  log "Generating $env_file with a random Postgres password"
  cat > "$env_file" <<EOF
POSTGRES_PASSWORD=$password
EOF
  chmod 600 "$env_file"
}

# ---- port override ---------------------------------------------------------
# docker-compose.yml already targets 5083 by default; only touch it if the
# caller asked for a different port.
apply_port_override() {
  [[ "$PORT" == "5083" ]] && return

  log "Overriding API port to $PORT via compose override file"
  cat > "$INSTALL_DIR/docker-compose.override.yml" <<EOF
services:
  api:
    ports:
      - "${PORT}:${PORT}"
    environment:
      ASPNETCORE_HTTP_PORTS: "${PORT}"
EOF
}

# The web container talks to the API over the compose network on its default port, so a custom
# --port only changes what the host sees. nginx needs telling either way.
write_web_override() {
  if [[ "$WITH_WEB" != "yes" ]]; then
    return
  fi

  cat > "$INSTALL_DIR/docker-compose.web.yml" <<EOF
services:
  web:
    ports:
      - "${WEB_PORT}:80"
      - "${WEB_HTTPS_PORT}:443"
EOF
}

# Compose files to use: the base one, plus the port override when there is one, plus the web
# ports. --no-web drops the whole service with a scale of zero.
compose_files() {
  local files=(-f docker-compose.yml)
  [[ -f "$INSTALL_DIR/docker-compose.override.yml" ]] && files+=(-f docker-compose.override.yml)
  [[ -f "$INSTALL_DIR/docker-compose.web.yml" ]] && files+=(-f docker-compose.web.yml)
  printf '%s\n' "${files[@]}"
}

# ---- run it ---------------------------------------------------------------
compose_up() {
  local -a files
  mapfile -t files < <(compose_files)

  if [[ "$WITH_WEB" == "yes" ]]; then
    log "Building and starting Corvida.Api + PostgreSQL + web UI with $ENGINE compose"
    log "The first build compiles the WebAssembly bundle and can take several minutes"
    (cd "$INSTALL_DIR" && "$ENGINE" compose "${files[@]}" up -d --build)
  else
    log "Building and starting Corvida.Api + PostgreSQL with $ENGINE compose (web UI skipped)"
    (cd "$INSTALL_DIR" && "$ENGINE" compose "${files[@]}" up -d --build db api)
  fi
}

wait_for_api() {
  log "Waiting for the API to come up on port $PORT"
  local tries=60
  # /api/auth/status rather than /api/boards: it is always anonymous, so the probe keeps
  # working once login is switched on (curl -f treats the 401 as a failure and would loop
  # here until the timeout). /health is not an option — it is only mapped in Development.
  until curl -fsS "http://localhost:${PORT}/api/auth/status" >/dev/null 2>&1; do
    tries=$((tries - 1))
    if [[ $tries -le 0 ]]; then
      warn "API did not respond within the timeout — check logs with:"
      warn "  (cd $INSTALL_DIR && $ENGINE compose logs -f api)"
      return 1
    fi
    sleep 2
  done
  log "API is up"
}

wait_for_web() {
  if [[ "$WITH_WEB" != "yes" ]]; then
    return 0
  fi

  log "Waiting for the web UI to come up on port $WEB_PORT"
  local tries=60
  # Probing through the proxy rather than just fetching index.html: nginx serves the static
  # bundle the moment it starts, so a plain "/" check would pass while the API leg is still
  # returning 502 and the UI would come up broken.
  until curl -fsS "http://localhost:${WEB_PORT}/api/auth/status" >/dev/null 2>&1; do
    tries=$((tries - 1))
    if [[ $tries -le 0 ]]; then
      warn "Web UI did not respond within the timeout — check logs with:"
      warn "  (cd $INSTALL_DIR && $ENGINE compose logs -f web)"
      return 1
    fi
    sleep 2
  done
  log "Web UI is up"
}

# ---- main ---------------------------------------------------------------
main() {
  log "Installing Corvida.Api with $ENGINE on port $PORT"
  ensure_engine
  ensure_compose
  fetch_repo
  write_env
  apply_port_override
  write_web_override
  compose_up
  wait_for_api || true
  wait_for_web || true

  local web_summary=""
  if [[ "$WITH_WEB" == "yes" ]]; then
    web_summary="  Web UI:         http://localhost:${WEB_PORT}
                  https://localhost:${WEB_HTTPS_PORT}  (self-signed certificate)
"
  fi

  # Every compose invocation needs the same file list the installer used.
  local compose_args=""
  [[ -f "$INSTALL_DIR/docker-compose.override.yml" ]] && compose_args+=" -f docker-compose.override.yml"
  [[ -f "$INSTALL_DIR/docker-compose.web.yml" ]] && compose_args+=" -f docker-compose.web.yml"
  [[ -n "$compose_args" ]] && compose_args=" -f docker-compose.yml${compose_args}"

  cat <<SUMMARY

Corvida is running.

  API base URL:   http://localhost:${PORT}
${web_summary}  Install dir:    ${INSTALL_DIR}
  DB password:    see ${INSTALL_DIR}/.env

The web UI needs no configuration — nginx serves it and proxies the API on the same origin.\n\nPoint the desktop app / MCP server at it by setting, in
{AppData}/Corvida/settings.json:

  "StorageMode": 1,
  "ServerUrl": "http://localhost:${PORT}/"

LOGIN IS OFF BY DEFAULT — anyone who can reach port ${PORT} has full access.
Turn it on from the desktop app's Settings page, or directly:

  curl -X POST http://localhost:${PORT}/api/auth/enable \\
    -H 'Content-Type: application/json' \\
    -d '{"Email":"you@example.com","Password":"choose-a-strong-one","DisplayName":"Admin"}'

Whoever calls that first becomes the only administrator, so do it now rather than later.

Manage the stack with:
  (cd ${INSTALL_DIR} && ${ENGINE} compose${compose_args} ps)
  (cd ${INSTALL_DIR} && ${ENGINE} compose${compose_args} logs -f api)
  (cd ${INSTALL_DIR} && ${ENGINE} compose${compose_args} down)
SUMMARY
}

main "$@"
