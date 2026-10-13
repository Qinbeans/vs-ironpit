#!/usr/bin/env bash
# Builds the mod and zips it as dist/ironpit-<version>.zip.
# Copy that zip into the game's Mods folder (no need to unzip).
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root/mod"
dotnet build -c Release
version="$(grep -o '"version": *"[^"]*"' modinfo.json | head -1 | sed 's/.*"\([^"]*\)"$/\1/')"
stage="$(mktemp -d)"
cp modinfo.json "$stage/"
cp "$root/LICENSE" "$stage/"
cp -r assets "$stage/"
cp bin/IronPit.dll "$stage/"
mkdir -p "$root/dist"
out="$root/dist/ironpit-$version.zip"
rm -f "$out"
(cd "$stage" && zip -qr "$out" .)
rm -rf "$stage"
echo "Packaged: $out"
