# SwyfInstaller

Console installer for [SWYF Custom AI](../custom-ai/README.md)
(`manifest-dex/SwyfInstaller`). Requires .NET 10 SDK to build; the built
apps run on plain Windows x64 with no extra setup.

## GUI

`SwyfInstaller.Gui` is a WPF front-end in the LuaShareX visual style
(WPF-UI dark theme, MVVM with CommunityToolkit.Mvvm): header with version
and update button, game-folder card with Browse/Detect, mod actions with a
real progress bar and status line, self-update banner, and a collapsible
details log. App startup checks for updates silently.

It drives the console tool next to it (`SwyfInstaller.exe`) for
install/update/verify/uninstall/detect, parsing its `##PROGRESS` lines for
the progress bar (`--progress --window` are always passed):

```powershell
dotnet publish SwyfInstaller.Gui/SwyfInstaller.Gui.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o SwyfInstaller.Gui/publish
Copy-Item SwyfInstaller/publish/SwyfInstaller.exe SwyfInstaller.Gui/publish/
# run SwyfInstaller.Gui/publish/SwyfInstallerGui.exe
```

## Console run

```powershell
dotnet build SwyfInstaller/SwyfInstaller.csproj -c Release
# interactive menu
dotnet SwyfInstaller/bin/Release/net10.0-windows/SwyfInstaller.dll
# or non-interactive, e.g.
dotnet SwyfInstaller/bin/Release/net10.0-windows/SwyfInstaller.dll update --yes
```

Menu / commands: `install`, `update`, `verify`, `uninstall`, `latest`,
`gamedir`, `detect`, `selftest`. Common flags: `--gamedir PATH`,
`--repo OWNER/REPO`, `--yes`, `--no-apply`, `--force`, `--full`,
`--window` (run package scripts in their own console, for GUIs),
`--progress` (emit `##PROGRESS n` lines for machine-readable progress).

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
   `Install Custom AI.cmd` to re-apply the patch (skipped with `--no-apply`).

`verify` re-hashes the installed files against that manifest and reports
OK / CHANGED / MISSING per file. `uninstall` runs the package's own
uninstaller, then cleans leftover package files (`--full` also removes
settings, session and backups after confirmation).

The game folder is auto-detected from Steam libraries (folder containing
`Scam With Your Friends.exe`) or set via menu / `--gamedir`.
