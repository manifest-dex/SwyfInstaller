# SwyfInstaller

Installer backend + app for [SWYF Custom AI](../custom-ai/README.md)
(`manifest-dex/SwyfInstaller`). Requires .NET 10 SDK to build; the built
apps run on plain Windows x64 with no extra setup.

## App (recommended)

`SwyfInstaller.Gui` is a basic WPF app (WPF-UI dark theme, MVVM with
CommunityToolkit.Mvvm) that calls the backend (`SwyfInstaller/`) directly —
no commands, no flags, no separate console window. One compact view: game
folder (auto-detected on startup, Browse/Detect with validation),
Install / Update / Uninstall buttons with a progress bar and plain-language
status, an F8 hint, and a collapsible details log with Copy. The app checks
for updates silently on startup and offers them in the header.

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

1. Scans this repo's releases (newest first) for the latest
   `SWYF-Custom-AI-*-win-x64.zip` plus its `.sha256` asset, skipping
   setup-only releases.
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

The game folder is auto-detected on startup from Steam: registry roots
(confirmed via `steam.exe`) plus every `libraryfolders.vdf` library
(same approach as Element), looking for the folder containing
`Scam With Your Friends.exe`. It can also be picked in the app.
