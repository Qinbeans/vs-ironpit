# Iron Pit: a Vintage Story mod project

Smelt iron in a bellows-driven fire pit with a crucible. Read `DESIGN.md` for the plan.

This project is a work in progress. Bellows heat a fire pit, and a crucible of iron nuggets (plus optional borax) refines into iron at 1500°C, or 1400°C with enough borax. Both are untested in game; pig iron and the 2/3 rule are not written yet. See `DESIGN.md` section 10.

## What you need

- Vintage Story installed on your computer (the game is not included in this zip).
- Docker Desktop (or another Docker engine).
- VS Code with the "Dev Containers" extension.

## Step 1: tell the container where your game is

The container compiles against your game's DLLs, so it needs your install folder. Set an environment variable named `VINTAGE_STORY` on your **host machine** (not inside the container), then restart VS Code and Docker.

- Windows (PowerShell): `setx VINTAGE_STORY "C:\Users\YOU\AppData\Roaming\Vintagestory"`
- macOS/Linux: add `export VINTAGE_STORY="/path/to/vintagestory"` to your shell profile.
- Linux Flatpak: no variable is needed. The container detects `at.vintagestory.VintageStory` automatically.

Use the folder that contains `VintagestoryAPI.dll`. If you are unsure which folder, look for that file.
For Flatpak, the detection uses its `active` deployment path, so it continues to work after game updates.

## Step 2: open in the container

1. Unzip and open the `vs-ironpit` folder in VS Code.
2. Run "Dev Containers: Reopen in Container" from the command palette.
3. Wait for setup. It prints whether it found the game DLL.

## Step 3: build

```
cd mod
dotnet build
```

A successful build produces `mod/bin/IronPit.dll`.

## Step 4: try it in game

The container cannot run the game window, so this part happens on your host:

1. Run `bash scripts/package.sh` in the container. It writes `dist/ironpit-<version>.zip`; copy that zip into the game's `Mods` folder and skip step 2. Or, by hand: make a folder named `ironpit` inside the game's `Mods` folder.
2. Copy into it: `modinfo.json`, the `assets` folder, and `bin/IronPit.dll`.
3. Start the game, create a world, and check `Logs/client-main.log` for `[IronPit] loaded`.

## Game version note

This project targets **1.22 (.NET 10)**. If you play 1.21 or older, change `net10.0` to `net8.0` in `mod/IronPit.csproj`, `"version": "10.0"` to `"8.0"` in `.devcontainer/devcontainer.json`, and the `game` version in `mod/modinfo.json`.

## Using Claude with this project

The `.claude/skills/` folder has four skills for Claude Code:

| Skill | Use |
|-------|-----|
| `vs-mod-dev` | build, package, and debug workflow |
| `csharp-for-beginners` | explains C# as you go |
| `ironpit-design` | keeps changes consistent with `DESIGN.md` |
| `vs-api-lookup` | verifies game API and vanilla values instead of guessing |

Start Claude Code inside the container (`claude` in the terminal) and ask for the next milestone from `DESIGN.md`.

## What is unverified

- Everything marked [VERIFY] in `DESIGN.md` (temperatures, unit sizes, borax role).
- The .NET and template versions were taken from the official wiki and NuGet pages at the time of writing. Check them if the build complains.
- The code compiles against Vintage Story 1.22.7 but has not been tested in game yet.

## License

MIT. See `LICENSE`. This covers the mod's own code and assets only; Vintage Story and its assets belong to Anego Studios and are not included.
