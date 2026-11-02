#!/usr/bin/env bash
# Builds the Corvida Linux package (.deb and .rpm) into artifacts/packages/. One package, corvida, holds
# both the desktop app (src/Corvida/Corvida.csproj, /usr/lib/corvida) and the MCP server
# (src/Corvida.Mcp/Corvida.Mcp.csproj, /usr/lib/corvida/mcp, /usr/bin/corvida-mcp): the MCP server reads
# the settings the desktop app writes and is registered with AI tools from its Settings page, so the
# two always belong together and must not drift apart in version.
#
# The server side (API, web UI, PostgreSQL) ships as container images instead, see
# .github/workflows/container-images.yml and packaging/quadlet/.
#
# Versioning: Nerdbank.GitVersioning, version.json holds "<year>.<month>" (e.g. 26.11) and the git
# height becomes the patch number (26.11.0, 26.11.1, ...). When the month changes, version.json is
# updated to the current year/month first (commit it so the next builds count from there).
#
# A Release `dotnet publish` of the desktop project also runs this script (with --no-bump --skip-tests),
# see the CreateLinuxPackages target in src/Corvida/Corvida.csproj.
#
# Usage: packaging/build-packages.sh [--no-bump] [--skip-tests] [deb|rpm ...]
#        With no format arguments, both formats are built.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts/packages"
STAGE="$ROOT/artifacts/stage"
TOOLS="$ROOT/artifacts/tools"
NFPM_VERSION=2.48.0
NFPM_SHA256=6d7f9a28b2f397934403950fcbba83f598b08d02edbe79a0815b3b4897341750

usage() { echo "Usage: $0 [--no-bump] [--skip-tests] [deb|rpm ...]" >&2; exit 2; }

bump=true
tests=true
formats=()
for arg in "$@"; do
    case "$arg" in
        --no-bump) bump=false ;;
        --skip-tests) tests=false ;;
        deb|rpm) formats+=("$arg") ;;
        *) usage ;;
    esac
done
[[ ${#formats[@]} -gt 0 ]] || formats=(deb rpm)

# staged component -> project, executable name
project_of()  { case "$1" in desktop) echo src/Corvida/Corvida.csproj ;; mcp) echo src/Corvida.Mcp/Corvida.Mcp.csproj ;; esac; }
exe_of()      { case "$1" in desktop) echo Corvida ;; mcp) echo Corvida.Mcp ;; esac; }

cd "$ROOT"
dotnet tool restore >/dev/null

# ── version ──
expected="$(date +%y).$(date +%-m)"
current="$(sed -n 's/^ *"version": *"\([^"]*\)".*/\1/p' version.json)"
if [[ "$current" != "$expected" ]]; then
    if $bump; then
        # Not "nbgv set-version": that rewrites version.json and drops the other settings.
        sed -i "s/^\( *\"version\": *\"\)[^\"]*\"/\1$expected\"/" version.json
        echo "version.json: $current -> $expected (commit this change)"
    else
        echo "warning: version.json is $current, current month would be $expected" >&2
    fi
fi

version="$(dotnet nbgv get-version -v SimpleVersion)"
prerelease=""
if [[ "$(dotnet nbgv get-version -v PublicRelease)" != "True" ]]; then
    # Builds outside the main branch / release tags sort before the release of the same version.
    prerelease="g$(dotnet nbgv get-version -v GitCommitIdShort)"
fi
echo "Corvida version: $version${prerelease:+~$prerelease}"

# ── build ──
if $tests; then
    # Every WebApplicationFactory in Corvida.Api.Tests watches its config files; with inotify that exhausts
    # the default limit of 128 instances per user and the tests fail with an IOException. Polling avoids it.
    DOTNET_USE_POLLING_FILE_WATCHER=true dotnet test "$ROOT/src/Corvida.sln" -c Release
fi

mkdir -p "$STAGE" "$OUT"
for component in desktop mcp; do
    dir="$STAGE/$component"
    rm -rf "$dir"
    # SkipLinuxPackaging: the desktop project's publish would otherwise call this script again.
    dotnet publish "$ROOT/$(project_of "$component")" \
        -c Release -r linux-x64 --self-contained true -p:SkipLinuxPackaging=true -o "$dir"
    find "$dir" -name '*.pdb' -delete
    chmod +x "$dir/$(exe_of "$component")"
done
# Files from the checkout are staged too, so they get the same permission fix-up as the build output.
common="$STAGE/common"
rm -rf "$common"
mkdir -p "$common"
cp -r LICENSE THIRD_PARTY_NOTICES.md licenses packaging/corvida.desktop "$common/"
cp src/Corvida/Assets/corvida.png "$common/corvida.png"
find "$common" -type f -exec chmod 0644 {} +
# A umask of 002 would otherwise ship group-writable files.
chmod -R go-w "$STAGE"

# ── nfpm (single static binary, builds both formats without dpkg/rpmbuild) ──
nfpm="$(command -v nfpm || true)"
if [[ -z "$nfpm" ]]; then
    nfpm="$TOOLS/nfpm-$NFPM_VERSION/nfpm"
    if [[ ! -x "$nfpm" ]]; then
        archive="$TOOLS/nfpm_${NFPM_VERSION}_Linux_x86_64.tar.gz"
        mkdir -p "$(dirname "$nfpm")"
        curl -fsSL -o "$archive" \
            "https://github.com/goreleaser/nfpm/releases/download/v$NFPM_VERSION/nfpm_${NFPM_VERSION}_Linux_x86_64.tar.gz"
        echo "$NFPM_SHA256  $archive" | sha256sum -c --quiet -
        tar -xzf "$archive" -C "$(dirname "$nfpm")" nfpm
        rm "$archive"
    fi
fi

export CORVIDA_VERSION="$version" CORVIDA_PRERELEASE="$prerelease"
for format in "${formats[@]}"; do
    # "corvida-[0-9]*": an older build's separate corvida-mcp package is left alone.
    rm -f "$OUT/corvida_"*."$format" "$OUT/corvida-"[0-9]*."$format"
    "$nfpm" package --config "$ROOT/packaging/nfpm.yaml" --packager "$format" --target "$OUT/"
done
ls -1 "$OUT"
