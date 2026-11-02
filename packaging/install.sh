#!/usr/bin/env bash
# Builds the corvida package for this distribution and installs it with the native package manager (apt on
# Debian/Ubuntu, dnf on Fedora/RHEL, zypper on openSUSE/SUSE):
#
#   corvida   /usr/lib/corvida, /usr/bin/corvida        desktop app
#             /usr/lib/corvida/mcp, /usr/bin/corvida-mcp MCP server for AI tools
#
# The server (API, web UI, PostgreSQL) is not a package: it runs in Podman containers, installed from the
# app's Settings -> Install page or with the units in packaging/quadlet/.
#
# Usage: packaging/install.sh [--no-build]
#        packaging/install.sh --uninstall
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts/packages"
PACKAGE=corvida
SUDO=""
[[ $EUID -eq 0 ]] || SUDO=sudo

build=true
uninstall=false
for arg in "$@"; do
    case "$arg" in
        --no-build) build=false ;;
        --uninstall) uninstall=true ;;
        *) echo "Usage: $0 [--no-build | --uninstall]" >&2; exit 2 ;;
    esac
done

if command -v apt-get >/dev/null; then
    format=deb
    install_cmd=(apt-get install -y --reinstall)
    remove_cmd=(apt-get remove -y)
    installed() { dpkg-query -W -f='${Status}' "$1" 2>/dev/null | grep -q '^install ok installed'; }
elif command -v dnf >/dev/null; then
    format=rpm
    install_cmd=(dnf install -y)
    remove_cmd=(dnf remove -y)
    installed() { rpm -q "$1" >/dev/null 2>&1; }
elif command -v zypper >/dev/null; then
    format=rpm
    install_cmd=(zypper --non-interactive install --allow-unsigned-rpm --force)
    remove_cmd=(zypper --non-interactive remove)
    installed() { rpm -q "$1" >/dev/null 2>&1; }
else
    echo "Unsupported distribution: apt-get, dnf or zypper is required." >&2
    exit 1
fi

if $uninstall; then
    if ! installed "$PACKAGE"; then
        echo "Nothing to remove."
        exit 0
    fi
    $SUDO "${remove_cmd[@]}" "$PACKAGE"
    echo "Removed: $PACKAGE"
    exit 0
fi

$build && "$ROOT/packaging/build-packages.sh" "$format"

# deb: corvida_<ver>_amd64.deb, rpm: corvida-<ver>-1.x86_64.rpm.
if [[ $format == deb ]]; then pattern="$OUT/${PACKAGE}_*.deb"; else pattern="$OUT/${PACKAGE}-[0-9]*.rpm"; fi
# shellcheck disable=SC2086
file="$(ls -t $pattern 2>/dev/null | head -n 1)"
if [[ -z "$file" ]]; then
    echo "No $PACKAGE .$format package in $OUT; run packaging/build-packages.sh first." >&2
    exit 1
fi

# apt needs an absolute (or ./) path to treat the argument as a file; the path is absolute.
$SUDO "${install_cmd[@]}" "$file"
echo "Installed: $(basename "$file")"
