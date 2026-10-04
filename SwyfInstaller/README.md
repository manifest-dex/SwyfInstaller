# SwyfInstaller

Console installer for [SWYF Custom AI](https://github.com/manifest-dex/swyf-custom-ai-mod)
(`manifest-dex/swyf-custom-ai-mod`). Requires .NET 10 SDK to build; the built
apps run on plain Windows x64 with no extra setup.

## GUI

`SwyfInstaller.Gui` is a basic WinForms front-end for the console tool:
game-folder box with Browse/Detect, repo box, Install / Update / Verify /
Uninstall buttons, patch-step and full-uninstall options, and a live log.
It shells out to `SwyfInstaller.exe`, so keep both files together:

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
`gamedir`, `selftest`. Common flags: `--gamedir PATH`, `--repo OWNER/REPO`,
`--yes`, `--no-apply`, `--force`, `--full`.

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
