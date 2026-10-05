# SWYF Custom AI — Installer + Mod

Use your own OpenAI-compatible AI provider in **Scam With Your Friends Playtest**.
This repo holds everything: the easy installer app and the mod itself
(`custom-ai/`). Only the **lobby host** needs the mod — guests join normally.

## Get started in 3 steps

1. **Install this app** — download `SwyfInstaller-Setup-v*-win-x64.exe` from
   [Releases](https://github.com/manifest-dex/SwyfInstaller/releases) and run it
   (per-user, no admin needed). Or build from source below.
2. **Find your game** — the app detects your Steam libraries on startup and
   fills in the game folder itself. If it can't, press **Detect** or **Browse**
   to the folder Steam opens via
   right-click game → Properties → Installed Files → Browse.
3. **Press Install, then play** — press **Install**, wait for "Done", start the
   game, host a lobby, and press **F8** (or AI Settings) to enter your provider
   details (API base URL, model ID, optional key) and **Save Settings**.

Guests don't need anything. New calls use your saved settings.

## What the buttons do

| Button | What happens | Your data |
|---|---|---|
| Install | Downloads the latest verified mod and applies it | Kept |
| Update | Removes old mod files, installs the latest | Settings, sign-in, backups kept |
| Uninstall | Removes the mod, restores original game files | Settings and backups kept |

Downloads are SHA-256 verified against the published checksum **and**
GitHub's asset digest before anything touches your game — a mismatch aborts.

## After installing — F8 panel basics

- Base URL example (local server): `http://127.0.0.1:1234/v1` — do not append
  `/chat/completions` yourself.
- Leave the key blank for keyless local servers. A blank key field keeps a
  saved key; delete it explicitly to remove it.
- **Test Connection** checks the form without saving. **Save Settings** applies it.
- Calls have a 15-second deadline; slow models trigger the game's fallback line.
- Menu shows model + status: Not tested yet / Connecting / Last request
  succeeded / Connection error. Details: [custom-ai/README.md](custom-ai/README.md).

## Troubleshooting

- **F8 does nothing** — close the game, press **Update mod**, start the game again.
  Steam updates can remove the patch; Update repairs it.
- **"Sorry, what were you saying?"** — the game's fallback: check base URL, model
  ID, key, structured-output support and speed in the F8 panel.
- **Game folder error** — the app finds Steam via the registry and all library
  folders automatically; if that fails, pick the folder containing
  `Scam With Your Friends.exe`.
- **Something failed** — open Details → **Copy log** and include it when
  asking for help. Keep `CustomAI/backup/`; never share `settings.json`.

See [SwyfInstaller/README.md](SwyfInstaller/README.md) for console usage and
[custom-ai/README.md](custom-ai/README.md) for full mod docs.

## Build from source

Requires .NET 10 SDK, Windows x64.

```powershell
dotnet build SwyfInstaller.sln -c Release
dotnet SwyfInstaller/bin/Release/net10.0-windows/SwyfInstaller.dll selftest
```

Single-file executables (these exact names are also the release assets,
which the in-app updater downloads and checksum-verifies):

```powershell
dotnet publish SwyfInstaller/SwyfInstaller.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o SwyfInstaller/publish
dotnet publish SwyfInstaller.Gui/SwyfInstaller.Gui.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o SwyfInstaller.Gui/publish
Copy-Item SwyfInstaller/publish/SwyfInstaller.exe SwyfInstaller.Gui/publish/
```

Setup installer (per-user, Start Menu + Programs list, no admin) with
[Inno Setup 6](https://jrsoftware.org/isinfo.php):

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DAppVersion=1.1.4 installer.iss
```

`release.ps1 -Tag` automates build, test, setup, tag and publish.

## Layout

```
SwyfInstaller/        backend (download / verify / install / update / uninstall)
SwyfInstaller.Gui/    basic installer app calling the backend directly
custom-ai/            mod source (Bridge/Panel/Installer/tests, build.ps1)
installer.iss         Inno Setup script (per-user setup exe)
release.ps1           build, test, setup, tag and publish a release
```

Mod releases (`SWYF-Custom-AI-*-win-x64.zip` + `.sha256`) are published from
this repo. Build the mod ZIP with:

```powershell
./custom-ai/build.ps1 -GameDir '<your-game-folder>'
```
