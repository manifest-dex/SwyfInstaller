# SWYF Custom AI — Installer + Mod

Use your own OpenAI-compatible AI provider in **Scam With Your Friends Playtest**.
This repo holds everything: the easy installer app and the mod itself
(`custom-ai/`). Only the **lobby host** needs the mod — guests join normally.

Press **F8** or click **AI Settings** in the main or pause menu to configure
your provider in a local browser panel. Guests use the game's existing
networking and local voice synthesis.

- Set the API Base URL, API key, and model without restarting the game.
- Test the connection and the structured response format expected by callers.
- See the saved game provider while testing a draft. **Test Connection** does
  not save; **Save Settings** applies the selection to the game.
- Optionally override temperature, top-p, and response token limits.
- See custom-provider status in the main menu instead of the original AI
  sign-in status.

This mod replaces the text-generation provider. Speech recognition and voice
synthesis are handled by the game and are not modified.

This is an unofficial Windows Mono Playtest mod. It checks the game methods
and libraries it uses, patches two managed assemblies, and keeps verified
originals for each game version. It does not include game binaries.

## Get started in 3 steps (recommended)

1. **Install this app** — download `SwyfInstaller-Setup-v*-win-x64.exe` from
   [Releases](https://github.com/manifest-dex/SwyfInstaller/releases) and run it
   (per-user, no admin needed). Or build from source below.
2. **Find your game** — the app detects your Steam libraries on startup and
   fills in the game folder itself. If it can't, press **Detect** or **Browse**
   to the folder Steam opens via
   right-click game → Properties → Installed Files → Browse. Close the game first.
3. **Press Install, then play** — press **Install**, wait for "Done", start the
   game, host a lobby, and press **F8** (or AI Settings) to enter your provider
   details (API base URL, model ID, optional key) and **Save Settings**.

Guests don't need anything. New calls use your saved settings.

## What the app buttons do

| Button | What happens | Your data |
|---|---|---|
| Install | Downloads the latest verified mod and applies it | Kept |
| Update | Removes old mod files, installs the latest | Settings, sign-in, backups kept |
| Uninstall | Removes the mod, restores original game files | Settings and backups kept |

Downloads are SHA-256 verified against the published checksum **and**
GitHub's asset digest before anything touches your game — a mismatch aborts.
The app checks for its own updates silently on startup. The patch step always
runs after download. A copyable details log lives under Details.

## Manual install (without the app)

You need:

- **Windows x64** and a compatible **Scam With Your Friends Playtest**
  installation. Originally tested on Unity **6000.3.10f1**; later Mono builds
  are accepted when their hook signatures, call sites, UI identifiers, and
  required library members pass compatibility checks.
- **.NET Runtime 10 and ASP.NET Core Runtime 10, Windows x64**. The installer
  checks both and downloads missing runtimes from Microsoft. Internet access
  and approval of the Windows administrator prompt are needed only if a
  runtime is missing. For offline setup,
  [install both runtimes manually](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
  first. The base .NET Runtime alone is not enough for the settings panel.
- An OpenAI-compatible **Chat Completions** endpoint and a model capable of
  returning the game's structured JSON responses. Keyless local endpoints are
  supported.

1. Close the game and wait for Steam downloads to finish.
2. In Steam, open **Properties > Installed Files > Browse**.
3. Extract **all contents** of `SWYF-Custom-AI-win-x64.zip` (from the
   `custom-ai-v*` releases on this repo) directly into that folder.
4. Double-click **Install Custom AI.cmd**. It checks the required runtimes
   before modifying the game. If a runtime is missing, wait for the Microsoft
   download and approve the Windows administrator prompt. If a restart is
   requested, restart Windows and run the script again.
5. Start the game and press **F8** to configure your provider.

The resulting layout should look like this:

```text
Scam With Your Friends Playtest/
  Scam With Your Friends.exe       # Already installed by Steam
  Install Custom AI.cmd
  Play with Custom AI.cmd
  Uninstall Custom AI.cmd
  CustomAI/
    README.md
    package/
      prerequisites.ps1
      bridge/
      panel/
      installer/
```

Extracting the ZIP places the mod files; the one-time install step applies
the patch. The ZIP does not overwrite game assemblies directly or include
settings/backups. **PowerShell 7 is not required:** runtime setup uses the
Windows PowerShell included with Windows. Downloads are checked against
Microsoft's SHA-512 checksum and Authenticode signature before installation.
Computers with working runtimes need no download or administrator prompt.

If **F8 / AI Settings does nothing**, close the game and run
**Install Custom AI.cmd** again. It checks whether the actual settings panel
and mod installer can load their runtimes, including when .NET is installed
outside PATH. A failed download, canceled administrator prompt or unusable
runtime stops setup with an explanation. On managed PCs where PowerShell or
runtime installation is blocked, use the manual runtime link above or contact
your administrator.

The installer backs up the original assemblies under
`<game>/CustomAI/backup/`. Custom AI is **disabled on first installation**.
Reinstalling preserves your provider settings.

## Configure your provider

### ManifestDeX AI for members

The F8 panel also offers **Use ManifestDeX AI**, a community-hosted option at
`https://swyf-ai.manifestdex.com`. Click **Sign in with ManifestDeX**, approve
the requested account, XP/rank and membership permissions in your browser,
then select the option and save. Sign-in returns through the SWYF site; no
code entry is required. The service must be available and configured by its
administrator.

While selected, private provider fields are locked and their saved values are
preserved. If the service is disabled, requests fail and the option becomes
unavailable for new selections. You can uncheck an existing selection to
return to your private provider. Errors do not silently switch to the
original game service.

After sign-in the panel shows your name/avatar, Free/Pro membership, rank,
remaining personal request/token allowances and reset dates. **Refresh
account** immediately checks upgrades; regular checks refresh within 30
seconds. Global budgets are never shown. Profile changes preserve usage. Old
device-code sessions require a one-time OAuth reconnection.

The service stores IP address, member identity, game conversations, generated
replies and technical request details for 14 days, accessible to its
administrator. The OAuth permission page requests consent. The game receives
only a separate 30-day SWYF token protected locally with Windows DPAPI. Keep
`manifestdex-session.dat` and `member-keys` private. Revoke connections from
[Connected applications](https://manifestdex.com/account/connections).
Support hosting at [manifestdex.com/donate](https://manifestdex.com/donate).

### Your own provider

1. Launch the game and press **F8**, or click **AI Settings** in the main or
   pause menu.
2. Enter the **API Base URL**, for example `http://127.0.0.1:1234/v1`. The mod
   appends `/chat/completions`; do not include that suffix yourself.
3. Enter the exact **Model ID** accepted by your provider.
4. Enter an **API key** if required. Leave it blank for a keyless local
   server. When a key is already saved, leaving the field blank preserves it;
   **Delete saved API key** removes it.
5. Click **Test Connection**. This uses the form's current values and checks
   that the response matches the game schema. It does not save the form.
   Requests may incur provider charges.
6. Enable **Use custom provider** and click **Save Settings**.
7. Host a lobby and start a call. New requests use the saved settings; an
   in-flight request keeps its existing settings.

The browser panel listens only on `127.0.0.1`, uses a dynamically assigned
port, and closes with the game. Open it through F8 rather than bookmarking an
old session URL.

### Six provider options to try

These options provide or route an OpenAI-compatible API for the mod. **Free
does not mean unlimited, and API compatibility does not guarantee every model
will work.** The two cloud options have free usage limits; routers depend on
the connected provider's access and billing; the three local model servers
avoid per-request cloud charges but use your own hardware and electricity.
Check current terms and model licenses before use.

These are setup suggestions, not a list of services all tested end to end in
this game. After choosing a model, run **Test Connection**, then try an
actual call. Keep responses within the game's **15-second deadline**. Do not
append `/chat/completions` to the Base URLs below.

#### 1. OpenRouter — free cloud models

- **Base URL:** `https://openrouter.ai/api/v1`
- **API key:** create an OpenRouter account and API key.
- **Model ID:** select a currently available model with the **`:free`** suffix
  from the [model catalog](https://openrouter.ai/models). Choose one whose
  endpoint supports structured outputs.
- **Output mode:** start with **JSON Schema**; use **JSON compatibility mode**
  only if the model supports JSON mode instead.
- **Free-use limits:** free variants have availability and rate limits that
  differ from paid variants. Keep the `:free` suffix; selecting the paid
  variant changes billing. Check your account's current limits rather than
  assuming unlimited calls.
- **Official docs:** [Free variants](https://openrouter.ai/docs/guides/routing/model-variants/free) · [Limits](https://openrouter.ai/docs/api/reference/limits).

#### 2. Google Gemini API — cloud free tier

- **Base URL:** `https://generativelanguage.googleapis.com/v1beta/openai`
- **API key:** create a Gemini API key in [Google AI Studio](https://aistudio.google.com/).
- **Model ID:** choose a current model listed with a **Free Tier**, preferably
  a fast Flash model that supports structured output. Copy its exact API model
  ID; model availability changes.
- **Output mode:** **JSON Schema**, using Google's OpenAI-compatible endpoint above.
- **Free-use limits:** eligibility, quotas, and model availability depend on
  the current tier and region. Stay within the free tier and check billing
  before enabling paid usage. Review the free-tier data-use terms on the
  pricing page.
- **Official docs:** [OpenAI compatibility](https://ai.google.dev/gemini-api/docs/openai) · [Pricing and free tier](https://ai.google.dev/gemini-api/docs/pricing).

#### 3. OmniRoute / 9Router — use a provider router

- **When to choose this:** if you already use OmniRoute or 9Router to manage
  provider connections, point the mod at its OpenAI-compatible endpoint. You
  do not need to run a local language model just because the router runs on
  your PC.
- **Base URL:** copy the OpenAI-compatible API base address shown by your
  router. Use its configured host, port, and API prefix; do not paste a
  dashboard URL or the full `/chat/completions` endpoint.
- **API key:** enter the router's client-facing API key if required.
  Configure upstream provider credentials in the router itself.
- **Model ID:** use the exact model ID or route alias exposed by the router,
  rather than assuming the upstream model name is accepted unchanged.
- **Output mode:** start with **JSON Schema** and select a route whose
  upstream model supports structured output. Use **JSON compatibility mode**
  only if the selected route supports JSON mode instead.
- **Free-use limits:** a router does not make paid inference free. Use a
  connected provider's eligible free tier or free model if you want to avoid
  inference charges; upstream quotas, subscriptions, and terms still apply.
  Check any automatic fallback routes for paid models.
- **Validation:** keep the router running, test the connection from F8, then
  try a call. Routing and upstream generation must together fit within the
  15-second deadline.

#### 4. LM Studio — local model server

- **Base URL:** `http://127.0.0.1:1234/v1` when using the default server port.
- **API key:** leave blank by default; enter your server token if you enable authentication.
- **Setup / Model ID:** install [LM Studio](https://lmstudio.ai/), download
  and load an instruction/chat model, then start its local server. Copy the
  loaded model's API identifier from LM Studio.
- **Output mode:** **JSON Schema** with a model that supports structured output.
- **Free-use limits:** local inference has no cloud request bill. RAM/VRAM and
  inference speed determine usability; a model that runs alongside the game
  without exceeding the deadline is more useful than a larger, slower one.
- **Official docs:** [OpenAI compatibility](https://lmstudio.ai/docs/developer/openai-compat) · [Structured output](https://lmstudio.ai/docs/developer/openai-compat/structured-output) · [Offline operation](https://lmstudio.ai/docs/app/offline).

#### 5. Ollama — local model server

- **Base URL:** `http://127.0.0.1:11434/v1`
- **API key:** leave blank for the local server; its OpenAI compatibility
  examples use a placeholder key that local Ollama ignores.
- **Setup / Model ID:** install [Ollama](https://ollama.com/), download a
  local instruction/chat model, and use its exact installed name from
  `ollama list`.
- **Output mode:** **JSON Schema** for supported local models. Preload the
  model before playing so its cold start does not consume the caller deadline.
- **Free-use limits:** this recommendation is for models running **locally**,
  not Ollama Cloud. Local inference uses your hardware without per-request
  charges; cloud plans have separate limits and billing.
- **Official docs:** [OpenAI compatibility](https://docs.ollama.com/api/openai-compatibility) · [Preloading and local operation](https://docs.ollama.com/faq) · [Local versus cloud pricing](https://ollama.com/pricing).

#### 6. LocalAI — self-hosted local API

- **Base URL:** `http://127.0.0.1:8080/v1` when deployed locally on port 8080.
- **API key:** use the key configured for your LocalAI server, or leave blank
  only if that server has authentication disabled.
- **Setup / Model ID:** install [LocalAI](https://localai.io/), load a
  text-generation model, and use the model name exposed by your server. On
  Windows, its container deployment is an option if you already use Docker.
- **Output mode:** choose a model/backend supporting structured output and
  start with **JSON Schema**; test the selected backend before playing.
- **Free-use limits:** LocalAI is open source and self-hosted. Running it on
  your own hardware avoids a hosted inference bill; rented servers and model
  licensing may have separate costs. Setup is more involved than the desktop
  alternatives above.
- **Official sources:** [Project and setup](https://github.com/mudler/LocalAI) · [Default container port configuration](https://github.com/mudler/LocalAI/blob/master/docker-compose.yaml).

### Advanced settings

- **Temperature / Top P:** leave blank to inherit the game's defaults. If a
  model explicitly rejects an inherited parameter, the mod removes that
  parameter and retries the same provider within the existing deadline.
  Explicit overrides are not silently discarded.
- **Maximum response tokens:** leave blank to retain the game's limit.
  Raising it can increase response time.
- **JSON Schema:** the default; preserves the game's structured output contract.
- **JSON compatibility mode:** uses `json_object` and adds the schema to the
  model instructions. The game's response validation still applies, so this
  cannot make every model compatible.

Caller turns retain the game's **15-second deadline**. Slow providers or
invalid responses can trigger the game's fallback dialogue.
Custom-provider failures never automatically switch to the original
paid/game-credit service.

### Multiplayer and voice

AI requests are made by the **host**. Guests do not need the mod or your API
key. Installing the mod only on a guest does not change the host's provider.

The mod changes text generation, not speech recognition or synthesis. Each
client's existing voice pipeline speaks the generated text. This host-only
design follows the inspected game code; a separate unmodded-client end-to-end
test has not been completed.

### Connection status

With custom AI enabled, the main menu shows the configured model and one of
these states:

- **Not tested yet:** no result is available for this configuration in the
  current panel session.
- **Connecting:** a request is in progress.
- **Last request succeeded:** the provider returned a usable Chat Completions
  response. This is not a continuous connectivity guarantee.
- **Connection error:** the request or local panel failed; details appear in
  the warning/panel.

Steam connectivity warnings still apply. Disabling custom AI restores the
original game's AI provider and status display.

## Update, verify, or uninstall

Close the game before installing, updating, or uninstalling, and let Steam
finish its downloads. Keep the `CustomAI/backup` folder intact.

The installer app scans this repo's releases (newest first) for the latest
verified mod ZIP, skipping setup-only releases. Its Update button removes old
package files only (`Install/Play/Uninstall Custom AI.cmd`,
`CustomAI/README.md`, `CustomAI/package/`) — `settings.json`, session,
`member-keys/` and `CustomAI/backup/` are always kept — then extracts the new
package, records a per-file SHA-256 manifest, and re-applies the patch.

For update-aware startup without the app, double-click
**Play with Custom AI.cmd** in the game folder. It repairs compatible updates
before opening Steam. The equivalent developer command (from `custom-ai/`) is:

```powershell
./custom-ai/launch.ps1 -GameDir '<your-game-folder>'
```

The launch helpers check compatibility, repair patches removed by a
compatible Steam update, then launch the game through Steam. They run only
when invoked; they do not install a background watcher. Launching directly
from Steam after an update may leave the mod inactive until you update again.

To remove the patch, double-click **Uninstall Custom AI.cmd** with the game
closed (or use the app's Uninstall button). The PowerShell equivalents
(from `custom-ai/`) remain available below.

```powershell
# Verify the installed patch, or check the supported clean game files.
./custom-ai/install.ps1 -Action verify -GameDir '<your-game-folder>'

# Restore the original game assemblies.
./custom-ai/install.ps1 -Action uninstall -GameDir '<your-game-folder>'
```

Compatible updates do not require a new hardcoded hash or a mod rebuild.
Backups are stored by original SHA-256 under `CustomAI/backup/<hash>/`, so
repairing the mod never replaces the new game with an old backup. Partial
updates are supported: files still carrying a recognized patch use their
verified backup, while new clean files are checked and backed up separately.

Uninstall restores only recognized patched files and preserves clean files
already replaced by Steam. Settings and every backup generation remain
available.

Compatibility checks cannot guarantee future game behavior. A changed AI
contract, removed hook, renamed required UI element, missing library member,
or a switch to IL2CPP requires a mod update. The installer stops before
changing live assemblies when structural checks fail; it does not guess new
hooks. Semantic changes with unchanged signatures still require live testing.

**Keep `<game>/CustomAI/settings.json` private. It contains your API key in
plaintext.** Do not upload your installed `CustomAI` folder, backups, game
logs, or modified game DLLs. Share the generated release ZIP.

## Troubleshooting

- **F8 does nothing** — close the game, press **Update** in the installer app
  (or run **Install Custom AI.cmd** again for a manual install). Steam
  updates can remove the patch; updating repairs it. Confirm the ASP.NET Core
  10 x64 runtime is installed (`dotnet --list-runtimes` should include
  `Microsoft.AspNetCore.App 10.x`). Check `[CustomAI]` entries in
  `%USERPROFILE%\AppData\LocalLow\Jatater Worldwide\Scam With Your Friends\Player.log`.
- **"Sorry, what were you saying?"** — the game's fallback response. Open F8
  and inspect the error, then test the connection. Check the API root, model
  ID, credentials, structured output support, and response speed. Leave
  unsupported temperature/top-p overrides blank. Try JSON compatibility mode
  if your model rejects JSON Schema.
- **Panel session invalid or unreachable** — open it again from F8. Session
  tokens and the localhost port change when the panel restarts.
- **"Incompatible game update" / "Unrecognized existing mod patch"** — the
  required game contract changed, or an existing patch cannot be matched to
  its manifest. Keep your backups; obtain a compatible mod update, or restore
  game files using Steam before retrying.
- **Something failed in the app** — open Details → **Copy log** and include it
  when asking for help.

## Build from source

### Installer app

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

`release.ps1 -Tag` automates build, test, setup, tag and publish. The console
(`SwyfInstaller/`) is a parameter-free interactive menu fallback; the GUI
calls the backend directly.

### Mod package

Requires **[.NET 10 SDK, Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)**
and PowerShell 7, plus the supported game installation (including its
`Scam With Your Friends_Data/Managed` folder) and NuGet access on the first
build (or the required packages already in your cache).

The build restores **Mono.Cecil 0.11.5** and
**Microsoft.NETFramework.ReferenceAssemblies.net471 1.0.3**. Unity, Mirror,
Newtonsoft.Json, and UniTask are referenced from your installed game; they are
not shipped with the mod.

Replace `<your-game-folder>` with your installation path. Pass `-GameDir`
explicitly, or set `$env:SWYF_GAME_DIR` in your local PowerShell session. Do
not commit personal environment settings. Debug symbols are excluded from the
release ZIP, and compiled source paths are mapped to `/_/`.

From `custom-ai/`:

```powershell
./custom-ai/build.ps1 -GameDir '<your-game-folder>'
```

The script builds the Unity bridge, publishes the panel and installer, and
verifies that every bridge member reference exists in the game's stripped
libraries. It creates `dist/` (`bridge/`, `panel/`, `installer/`,
`install.ps1`, `launch.ps1`, `README.md`) and
**`custom-ai/artifacts/SWYF-Custom-AI-win-x64.zip`**, ready to attach to a
GitHub Release alongside its `.sha256`. The package is framework-dependent:
**Install Custom AI.cmd** installs missing .NET 10 and ASP.NET Core 10 x64
runtimes before patching. No game DLLs, settings, credentials, or backups
belong in that package. For an unpacked `dist/` folder:

```powershell
./custom-ai/install.ps1 -GameDir '<your-game-folder>'
```

## Run tests

Installer backend check (no network, no game needed):

```powershell
dotnet SwyfInstaller/bin/Release/net10.0-windows/SwyfInstaller.dll selftest
```

Mod tests need a built package first. The default suite uses a local fake
provider and disposable copies of game assemblies. It does not call your
configured AI service. Close the game for installer tests.

```powershell
$env:SWYF_GAME_DIR = '<your-game-folder>'
dotnet run --project custom-ai/tests/Tests.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File custom-ai/tests/prerequisites.test.ps1
node custom-ai/tests/signin-ui.test.mjs

# Skip installer tests while the game is running.
dotnet run --project custom-ai/tests/Tests.csproj -c Release -- --provider-only
```

Coverage includes request conversion, JSON compatibility, unsupported
sampling parameters, timeouts/cancellation, authentication boundaries,
credential redaction, settings persistence, provider status, process
shutdown, installation, reinstall, uninstall, simulated compatible/partial
updates, and incompatible-hook rejection. Simulated updates validate the
patch lifecycle; they do not substitute for testing future game releases.

Prerequisite tests use Windows PowerShell 5.1 and simulate downloads,
administrator cancellation, installer failures and restart requirements. They
also load the real packaged applications against both installed runtimes and
an isolated base-only .NET runtime. No runtime installer is executed and no
installed runtime is changed by these tests.

An optional live check reads your installed settings and sends a request to
that provider; it can incur charges:

```powershell
dotnet run --project custom-ai/tests/Tests.csproj -c Release -- --provider-only --live
```

## Releases

- **Installer setups** ship from the root `release.ps1 -Tag vX.Y.Z`: build,
  selftest, single-file publish, Inno Setup exe (+ `.sha256`), tag, push tag,
  publish GitHub Release.
- **Mod packages** (`SWYF-Custom-AI-*-win-x64.zip` + `.sha256`) ship as
  `custom-ai-v*` releases on this same repo (mirrored from the retired
  standalone repo; see [custom-ai/RELEASING.md](custom-ai/RELEASING.md) for the
  original procedure). Only the ZIP and checksum are uploaded; game
  libraries, test backups, settings, and API keys remain local.

## Layout

```text
SwyfInstaller/        backend (download / verify / install / update / uninstall)
SwyfInstaller.Gui/    basic installer app calling the backend directly
SwyfInstaller.sln     installer solution
custom-ai/            mod source: src/Bridge, src/Panel, tools/Installer,
                      tests, packaging, build.ps1 / install.ps1 / launch.ps1
custom-ai/README.md   full mod doc, also shipped as CustomAI/README.md in the ZIP
installer.iss         Inno Setup script (per-user setup exe)
release.ps1           build, test, setup, tag and publish an installer release
```

How the mod works: the bridge intercepts
`KolkataApi.CompleteOpenRouterAsync` while preserving game prompts, history,
validation, and fallback behavior. It forwards custom requests to the helper
using `UnityWebRequest`. The helper owns provider credentials and outbound
HTTP calls. Browser endpoints are `/api/config`, `/api/status`, and
`/api/test`. The bridge uses `/internal/status` and
`/internal/chat/completions`. Browser and internal routes use separate
per-session tokens; configuration reads never return the API key.

Build output, generated context caches, local settings, and test artifacts
are excluded from Git.
