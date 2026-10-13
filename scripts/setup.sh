#!/usr/bin/env bash
# Runs once after the dev container is created.
set -u

echo "== .NET SDK =="
dotnet --list-sdks

echo
echo "== Vintage Story install check =="
if [ -f "$VINTAGE_STORY/VintagestoryAPI.dll" ]; then
  echo "OK: found VintagestoryAPI.dll in $VINTAGE_STORY"
else
  echo "WARNING: VintagestoryAPI.dll not found in $VINTAGE_STORY"
  echo "Set VINTAGE_STORY on your host machine to your game folder, then rebuild the container."
  echo "See README.md, step 1."
fi

echo
echo "== Installing the official Vintage Story mod templates =="
dotnet new install VintageStory.Mod.Templates || echo "Template install failed (offline?). Not required for this project."

echo
echo "Setup finished. Next: cd mod && dotnet build"
