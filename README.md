# SwyfInstaller

Console installer + basic WinForms UI for
[SWYF Custom AI](https://github.com/manifest-dex/swyf-custom-ai-mod).

- Downloads the latest `SWYF-Custom-AI-*-win-x64.zip` GitHub release.
- Verifies the download against the published checksum **and** the GitHub
  asset digest before touching the game (mismatch = abort).
- Update removes old package files but always keeps `settings.json`,
  session, `member-keys/` and `CustomAI/backup/`.
- `verify` re-checks installed files for changes; `uninstall` runs the
  package's own uninstaller and cleans up leftovers.
- Game folder auto-detection via Steam libraries (or set manually).

See [SwyfInstaller/README.md](SwyfInstaller/README.md) for full usage.

## Build

Requires .NET 10 SDK, Windows x64.

```powershell
dotnet build SwyfInstaller.sln -c Release
dotnet SwyfInstaller/bin/Release/net10.0-windows/SwyfInstaller.dll selftest
```

Single-file executables:

```powershell
dotnet publish SwyfInstaller/SwyfInstaller.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o SwyfInstaller/publish
dotnet publish SwyfInstaller.Gui/SwyfInstaller.Gui.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o SwyfInstaller.Gui/publish
Copy-Item SwyfInstaller/publish/SwyfInstaller.exe SwyfInstaller.Gui/publish/
```

## Layout

```
SwyfInstaller/        console tool (download / verify / install / update / uninstall)
SwyfInstaller.Gui/    WinForms front-end driving the console tool
```
