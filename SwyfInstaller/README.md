# SwyfInstaller

Installer backend + app for [SWYF Custom AI](../custom-ai/README.md)
(`manifest-dex/SwyfInstaller`). Requires .NET 10 SDK to build; the built
apps run on plain Windows x64 with no extra setup.

## App (recommended)

`SwyfInstaller.Gui` is a WPF app (WPF-UI dark theme, MVVM with
CommunityToolkit.Mvvm) that calls the backend (`SwyfInstaller/`) directly —
no commands, no flags, no separate console window. 3-step layout: find the
game (Browse/Detect with validation), install or manage the mod with a real
progress bar and plain-language status, then play (F8 in-game). Includes a
self-update banner, a troubleshooting help section, and a copyable details
log. App startup checks for updates silently.

```powershell
dotnet publish SwyfInstaller.Gui/SwyfInstaller.Gui.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o SwyfInstaller.Gui/publish
# run SwyfInstaller.Gui/publish/SwyfInstallerGui.exe
```

## Console fallback

```powershell
dotnet build SwyfInstaller/SwyfInstaller.csproj -c Release
# interactive menu (no parameters needed)
dotnet SwyfInstaller/bin/Release/net10.0-windows/SwyfInstaller.dll
```

The menu offers `install`, `update`, `verify`, `uninstall` and game-folder
setup. Everything is asked interactively — there are no command-line
parameters (apart from `selftest`, a build check with no network/game).

## What update does

1. Queries `releases/latest` on GitHub and picks
   `SWYF-Custom-AI-*-win-x64.zip` plus its `.sha256` asset.
2. Downloads both, hashes the ZIP with SHA-256 and compares it against the
   published checksum file **and** the API asset digest. Any mismatch aborts
   before the game folder is touched.
3. Removes old package files only (`Install/Play/Uninstall Custom AI.cmd`,
   `CustomAI/README.md`, `CustomAI/package/`). `settings.json`, session,
   `member-keys/` and `CustomAI/backup/` are always kept.
4. Extracts the new package, records a per-file SHA-256 manifest under
   `%APPDATA%/SwyfInstaller/manifest.json`, then runs
   `Install Custom AI.cmd` to re-apply the patch.

`verify` re-hashes the installed files against that manifest and reports
OK / CHANGED / MISSING per file. `uninstall` runs the package's own
uninstaller, then cleans leftover package files.

The game folder is auto-detected from Steam libraries (folder containing
`Scam With Your Friends.exe`) or picked in the app.
