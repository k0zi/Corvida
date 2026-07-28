#!/usr/bin/env bash
# Packages a self-contained linux-x64 publish output into an installable .deb.
# Invoked from Corvida.csproj's CreateDebPackage MSBuild target — see Corvida.csproj.
set -euo pipefail

PUBLISH_DIR=$1
VERSION=$2
OUTPUT_DIR=$3
PROJECT_DIR=$4

if ! command -v dpkg-deb >/dev/null 2>&1; then
    echo "error: dpkg-deb not found. Install it (e.g. 'sudo apt install dpkg') to build a .deb package." >&2
    exit 1
fi

if [ ! -f "$PUBLISH_DIR/Corvida" ]; then
    echo "error: expected published binary at '$PUBLISH_DIR/Corvida' — was the linux-x64 self-contained publish successful?" >&2
    exit 1
fi

PKGROOT=$(mktemp -d)
trap 'rm -rf "$PKGROOT"' EXIT

mkdir -p "$PKGROOT/opt/corvida"
cp -r "$PUBLISH_DIR"/. "$PKGROOT/opt/corvida/"
chmod +x "$PKGROOT/opt/corvida/Corvida"

mkdir -p "$PKGROOT/usr/bin"
ln -s /opt/corvida/Corvida "$PKGROOT/usr/bin/corvida"

mkdir -p "$PKGROOT/usr/share/applications"
cp "$PROJECT_DIR/Packaging/corvida.desktop" "$PKGROOT/usr/share/applications/corvida.desktop"

mkdir -p "$PKGROOT/usr/share/icons/hicolor/256x256/apps"
cp "$PROJECT_DIR/Assets/corvida.png" "$PKGROOT/usr/share/icons/hicolor/256x256/apps/corvida.png"

INSTALLED_SIZE=$(du -sk "$PKGROOT/opt/corvida" | cut -f1)

mkdir -p "$PKGROOT/DEBIAN"
cat > "$PKGROOT/DEBIAN/control" <<EOF
Package: corvida
Version: $VERSION
Section: utils
Priority: optional
Architecture: amd64
Installed-Size: $INSTALLED_SIZE
Maintainer: David Kozma <k0zi@outlook.com>
Homepage: https://github.com/k0zi/Corvida
Description: Kanban board manager
 Corvida is a cross-platform Kanban board manager with support for local
 file storage or a hosted REST API backend.
EOF

mkdir -p "$OUTPUT_DIR"
DEB_PATH="$OUTPUT_DIR/corvida_${VERSION}_amd64.deb"
dpkg-deb --root-owner-group --build "$PKGROOT" "$DEB_PATH"

echo "Created $(du -h "$DEB_PATH" | cut -f1) package: $DEB_PATH"
