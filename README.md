# SWYF Custom AI

**Use your own OpenAI-compatible text provider in Scam With Your Friends Playtest.**

Press **F8** or click **AI Settings** in the main or pause menu to configure your provider in a local browser panel. Only the **lobby host** needs the mod. Guests use the game's existing networking and local voice synthesis.

- Set the API Base URL, API key, and model without restarting the game.
- Test the connection and the structured response format expected by callers.
- Optionally override temperature, top-p, and response token limits.
- See custom-provider status in the main menu instead of the original AI sign-in status.

This mod only replaces the text-generation provider. Speech recognition and voice synthesis are handled by the game and are not modified.

This is an unofficial Windows Mono Playtest mod. It checks the game methods and libraries it uses, patches two managed assemblies, and keeps verified originals for each game version. It does not include game binaries.

## How to use

### 1. Install the mod

You need:

- **Windows x64** and a compatible **Scam With Your Friends Playtest** installation. Originally tested on Unity **6000.3.10f1**; later Mono builds are accepted when their hook signatures, call sites, UI identifiers, and required library members pass compatibility checks.
- **[ASP.NET Core Runtime 10, Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)**. The base .NET Runtime alone is not enough for the settings panel.
- An OpenAI-compatible **Chat Completions** endpoint and a model capable of returning the game's structured JSON responses. Keyless local endpoints are supported.

1. Close the game and wait for Steam downloads to finish.
2. In Steam, open **Properties > Installed Files > Browse**.
3. Extract **all contents** of `SWYF-Custom-AI-win-x64.zip` directly into that folder.
4. Double-click **Install Custom AI.cmd**. It automatically uses the folder containing the script; no terminal command or game-path entry is needed.
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
      bridge/
      panel/
      installer/
```

Extracting the ZIP places the mod files; the one-time install step applies the patch. The ZIP does not overwrite game assemblies directly or include settings/backups. **PowerShell 7 is not required for this release workflow.** ASP.NET Core Runtime 10 x64 is still required.

For developers or an unpacked `dist/` folder, the PowerShell installation command also remains available:

```powershell
./install.ps1 -GameDir '<your-game-folder>'
```

Replace the path with your installation folder.

If you have the source repository rather than a built package, follow [Build from source](#build-from-source) first. The repository's `install.ps1` automatically uses its `dist/` package.

The installer backs up the original assemblies under `<game>/CustomAI/backup/`. Custom AI is **disabled on first installation**. Reinstalling preserves existing settings.

### 2. Configure your provider

1. Launch the game and press **F8**, or click **AI Settings** in the main or pause menu.
2. Enter the **API Base URL**, for example `http://127.0.0.1:1234/v1`. The mod appends `/chat/completions`; do not include that suffix yourself.
3. Enter the exact **Model ID** accepted by your provider.
4. Enter an **API key** if required. Leave it blank for a keyless local server. When a key is already saved, leaving the field blank preserves it; **Delete saved API key** removes it.
5. Click **Test Connection**. This uses the form's current values and checks that the response matches the game schema. It does not save the form. Requests may incur provider charges.
6. Enable **Use custom provider** and click **Save Settings**.
7. Host a lobby and start a call. New requests use the saved settings; an in-flight request keeps its existing settings.

The browser panel listens only on `127.0.0.1`, uses a dynamically assigned port, and closes with the game. Open it through F8 rather than bookmarking an old session URL.

### Six provider options to try

These options provide or route an OpenAI-compatible API for the mod. **Free does not mean unlimited, and API compatibility does not guarantee every model will work.** The two cloud options have free usage limits; routers depend on the connected provider's access and billing; the three local model servers avoid per-request cloud charges but use your own hardware and electricity. Check current terms and model licenses before use.

These are setup suggestions, not a list of services all tested end to end in this game. After choosing a model, run **Test Connection**, then try an actual call. Keep responses within the game's **15-second deadline**. Do not append `/chat/completions` to the Base URLs below.

#### 1. OpenRouter — free cloud models

- **Base URL:** `https://openrouter.ai/api/v1`
- **API key:** create an OpenRouter account and API key.
- **Model ID:** select a currently available model with the **`:free`** suffix from the [model catalog](https://openrouter.ai/models). Choose one whose endpoint supports structured outputs.
- **Output mode:** start with **JSON Schema**; use **JSON compatibility mode** only if the model supports JSON mode instead.
- **Free-use limits:** free variants have availability and rate limits that differ from paid variants. Keep the `:free` suffix; selecting the paid variant changes billing. Check your account's current limits rather than assuming unlimited calls.
- **Official docs:** [Free variants](https://openrouter.ai/docs/guides/routing/model-variants/free) · [Limits](https://openrouter.ai/docs/api/reference/limits).

#### 2. Google Gemini API — cloud free tier

- **Base URL:** `https://generativelanguage.googleapis.com/v1beta/openai`
- **API key:** create a Gemini API key in [Google AI Studio](https://aistudio.google.com/).
- **Model ID:** choose a current model listed with a **Free Tier**, preferably a fast Flash model that supports structured output. Copy its exact API model ID; model availability changes.
- **Output mode:** **JSON Schema**, using Google's OpenAI-compatible endpoint above.
- **Free-use limits:** eligibility, quotas, and model availability depend on the current tier and region. Stay within the free tier and check billing before enabling paid usage. Review the free-tier data-use terms on the pricing page.
- **Official docs:** [OpenAI compatibility](https://ai.google.dev/gemini-api/docs/openai) · [Pricing and free tier](https://ai.google.dev/gemini-api/docs/pricing).

#### 3. OmniRoute / 9Router — use a provider router

- **When to choose this:** if you already use OmniRoute or 9Router to manage provider connections, point the mod at its OpenAI-compatible endpoint. You do not need to run a local language model just because the router runs on your PC.
- **Base URL:** copy the OpenAI-compatible API base address shown by your router. Use its configured host, port, and API prefix; do not paste a dashboard URL or the full `/chat/completions` endpoint.
- **API key:** enter the router's client-facing API key if required. Configure upstream provider credentials in the router itself.
- **Model ID:** use the exact model ID or route alias exposed by the router, rather than assuming the upstream model name is accepted unchanged.
- **Output mode:** start with **JSON Schema** and select a route whose upstream model supports structured output. Use **JSON compatibility mode** only if the selected route supports JSON mode instead.
- **Free-use limits:** a router does not make paid inference free. Use a connected provider's eligible free tier or free model if you want to avoid inference charges; upstream quotas, subscriptions, and terms still apply. Check any automatic fallback routes for paid models.
- **Validation:** keep the router running, test the connection from F8, then try a call. Routing and upstream generation must together fit within the 15-second deadline.

#### 4. LM Studio — local model server

- **Base URL:** `http://127.0.0.1:1234/v1` when using the default server port.
- **API key:** leave blank by default; enter your server token if you enable authentication.
- **Setup / Model ID:** install [LM Studio](https://lmstudio.ai/), download and load an instruction/chat model, then start its local server. Copy the loaded model's API identifier from LM Studio.
- **Output mode:** **JSON Schema** with a model that supports structured output.
- **Free-use limits:** local inference has no cloud request bill. RAM/VRAM and inference speed determine usability; a model that runs alongside the game without exceeding the deadline is more useful than a larger, slower one.
- **Official docs:** [OpenAI compatibility](https://lmstudio.ai/docs/developer/openai-compat) · [Structured output](https://lmstudio.ai/docs/developer/openai-compat/structured-output) · [Offline operation](https://lmstudio.ai/docs/app/offline).

#### 5. Ollama — local model server

- **Base URL:** `http://127.0.0.1:11434/v1`
- **API key:** leave blank for the local server; its OpenAI compatibility examples use a placeholder key that local Ollama ignores.
- **Setup / Model ID:** install [Ollama](https://ollama.com/), download a local instruction/chat model, and use its exact installed name from `ollama list`.
- **Output mode:** **JSON Schema** for supported local models. Preload the model before playing so its cold start does not consume the caller deadline.
- **Free-use limits:** this recommendation is for models running **locally**, not Ollama Cloud. Local inference uses your hardware without per-request charges; cloud plans have separate limits and billing.
- **Official docs:** [OpenAI compatibility](https://docs.ollama.com/api/openai-compatibility) · [Preloading and local operation](https://docs.ollama.com/faq) · [Local versus cloud pricing](https://ollama.com/pricing).

#### 6. LocalAI — self-hosted local API

- **Base URL:** `http://127.0.0.1:8080/v1` when deployed locally on port 8080.
- **API key:** use the key configured for your LocalAI server, or leave blank only if that server has authentication disabled.
- **Setup / Model ID:** install [LocalAI](https://localai.io/), load a text-generation model, and use the model name exposed by your server. On Windows, its container deployment is an option if you already use Docker.
- **Output mode:** choose a model/backend supporting structured output and start with **JSON Schema**; test the selected backend before playing.
- **Free-use limits:** LocalAI is open source and self-hosted. Running it on your own hardware avoids a hosted inference bill; rented servers and model licensing may have separate costs. Setup is more involved than the desktop alternatives above.
- **Official sources:** [Project and setup](https://github.com/mudler/LocalAI) · [Default container port configuration](https://github.com/mudler/LocalAI/blob/master/docker-compose.yaml).

### Advanced settings

- **Temperature / Top P:** leave blank to inherit the game's defaults. If a model explicitly rejects an inherited parameter, the mod removes that parameter and retries the same provider within the existing deadline. Explicit overrides are not silently discarded.
- **Maximum response tokens:** leave blank to retain the game's limit. Raising it can increase response time.
- **JSON Schema:** the default; preserves the game's structured output contract.
- **JSON compatibility mode:** uses `json_object` and adds the schema to the model instructions. The game's response validation still applies, so this cannot make every model compatible.

Caller turns retain the game's **15-second deadline**. Slow providers or invalid responses can trigger the game's fallback dialogue. Custom-provider failures never automatically switch to the original paid/game-credit service.

### Multiplayer and voice

AI requests are made by the **host**. Guests do not need the mod or your API key. Installing the mod only on a guest does not change the host's provider.

The mod changes text generation, not speech recognition or synthesis. Each client's existing voice pipeline speaks the generated text. This host-only design follows the inspected game code; a separate unmodded-client end-to-end test has not been completed.

### Connection status

With custom AI enabled, the main menu shows the configured model and one of these states:

- **Not tested yet:** no result is available for this configuration in the current panel session.
- **Connecting:** a request is in progress.
- **Last request succeeded:** the provider returned a usable Chat Completions response. This is not a continuous connectivity guarantee.
- **Connection error:** the request or local panel failed; details appear in the warning/panel.

Steam connectivity warnings still apply. Disabling custom AI restores the original game's AI status display. The mod does not disable Steam or other backend authentication.

## Update, verify, or uninstall

Close the game before installing, updating, or uninstalling, and let Steam finish its downloads. Keep the `CustomAI/backup` folder intact.

For update-aware startup, double-click **Play with Custom AI.cmd** in the game folder. It repairs compatible updates before opening Steam. The equivalent developer command is:

```powershell
./launch.ps1 -GameDir '<your-game-folder>'
```

The launch helpers check compatibility, repair patches removed by a compatible Steam update, then launch the game through Steam. They run only when invoked; they do not install a background watcher. Launching directly from Steam after an update may leave the mod inactive until you run **Install Custom AI.cmd** again.

To remove the patch, double-click **Uninstall Custom AI.cmd** with the game closed. The PowerShell equivalents remain available below.

```powershell
# Verify the installed patch, or check the supported clean game files.
./install.ps1 -Action verify -GameDir '<your-game-folder>'

# Restore the original game assemblies.
./install.ps1 -Action uninstall -GameDir '<your-game-folder>'
```

Compatible updates do not require a new hardcoded hash or a mod rebuild. Backups are stored by original SHA-256 under `CustomAI/backup/<hash>/`, so repairing the mod never replaces the new game with an old backup. Partial updates are supported: files still carrying a recognized patch use their verified backup, while new clean files are checked and backed up separately. Existing version 1/2 installations migrate on reinstall.

Uninstall restores only recognized patched files and preserves clean files already replaced by Steam. Settings and every backup generation remain available.

Compatibility checks cannot guarantee future game behavior. A changed AI contract, removed hook, renamed required UI element, missing library member, or a switch to IL2CPP requires a mod update. The installer stops before changing live assemblies when structural checks fail; it does not guess new hooks. Semantic changes with unchanged signatures still require live testing.

**Keep `<game>/CustomAI/settings.json` private. It contains your API key in plaintext.** Do not upload your installed `CustomAI` folder, backups, game logs, or modified game DLLs. Share the generated release ZIP.

## Troubleshooting

**F8 or the menu button does not open a browser**

Confirm the ASP.NET Core 10 x64 runtime is installed (`dotnet --list-runtimes` should include `Microsoft.AspNetCore.App 10.x`). Reinstall the complete package with the game closed, then restart the game. Check `[CustomAI]` entries in `%USERPROFILE%\AppData\LocalLow\Jatater Worldwide\Scam With Your Friends\Player.log`.

**Callers keep saying "Sorry, what were you saying?"**

This is the game's fallback response. Open F8 and inspect the error, then test the connection. Check the API root, model ID, credentials, structured output support, and response speed. Leave unsupported temperature/top-p overrides blank. Try JSON compatibility mode if your model rejects JSON Schema.

**The panel session is invalid or unreachable**

Open it again from F8. Session tokens and the localhost port change when the panel restarts.

**"Incompatible game update" or "Unrecognized existing mod patch"**

The required game contract changed, or an existing patch cannot be matched to its manifest. The installer leaves the game assemblies untouched. Keep your backups; obtain a compatible mod update, or restore game files using Steam before retrying. File hashes are still checked for backup integrity and concurrent file changes, not used as a release allowlist.

## Build from source

### Prerequisites

- **[.NET 10 SDK, Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)** and PowerShell 7.
- The supported game installation, including its `Scam With Your Friends_Data/Managed` folder.
- NuGet access on the first build, or the required packages already in your cache.

The build restores the existing dependencies **Mono.Cecil 0.11.5** and **Microsoft.NETFramework.ReferenceAssemblies.net471 1.0.3**. Unity, Mirror, Newtonsoft.Json, and UniTask are referenced from your installed game; they are not shipped with the mod.

Replace `<your-game-folder>` in examples with your own installation path. No machine-specific game path is stored in the repository. Pass `-GameDir` explicitly, or set `$env:SWYF_GAME_DIR` in your local PowerShell session. Do not commit personal environment settings. Debug symbols are excluded from the release ZIP, and compiled source paths are mapped to `/_/`.

From the repository root:

```powershell
./build.ps1 -GameDir '<your-game-folder>'
```

The script builds the Unity bridge, publishes the panel and installer, and verifies that every bridge member reference exists in the game's stripped libraries. It creates:

```text
dist/
  bridge/SWYF.CustomAI.Bridge.dll
  panel/                         # ASP.NET Core helper and embedded browser UI
  installer/                     # Version-checked installer and Mono.Cecil
  install.ps1
  launch.ps1                     # Check/repair, then launch through Steam
  README.md
```

The build also creates **`artifacts/SWYF-Custom-AI-win-x64.zip`**, ready to attach to a GitHub Release. It contains the three clickable CMD helpers and the `CustomAI/package/` payload, with no outer ZIP folder. The package is framework-dependent: recipients still need ASP.NET Core Runtime 10. No game DLLs, settings, credentials, or backups belong in that package.

## Run tests

Build the package first. The default suite uses a local fake provider and disposable copies of game assemblies. It does not call your configured AI service. Close the game for installer tests.

```powershell
$env:SWYF_GAME_DIR = '<your-game-folder>'
dotnet run --project tests/Tests.csproj -c Release

# Skip installer tests while the game is running.
dotnet run --project tests/Tests.csproj -c Release -- --provider-only
```

Coverage includes request conversion, JSON compatibility, unsupported sampling parameters, timeouts/cancellation, authentication boundaries, credential redaction, settings persistence, provider status, process shutdown, installation, reinstall, uninstall, simulated compatible/partial updates, and incompatible-hook rejection. Simulated updates validate the patch lifecycle; they do not substitute for testing future game releases.

An optional live check reads your installed settings and sends a request to that provider; it can incur charges:

```powershell
dotnet run --project tests/Tests.csproj -c Release -- --provider-only --live
```

## Publish a GitHub Release from your PC

No Actions runner or background service is needed. Run this on the Windows PC with the game installed:

```powershell
./release.ps1 -Tag v1.0.0
```

The script builds and tests the ZIP locally, creates an annotated tag at the current commit, pushes **only that tag** to `origin`, and publishes a GitHub Release with:

- `SWYF-Custom-AI-v1.0.0-win-x64.zip`
- `SWYF-Custom-AI-v1.0.0-win-x64.zip.sha256`

Set `SWYF_GAME_DIR` in your local shell first, or include `-GameDir '<your-game-folder>'` in the command.

### One-time setup

1. Install Git and [GitHub CLI](https://cli.github.com/), along with the build prerequisites above.
2. Run `gh auth login` and authenticate Git access to your GitHub repository.
3. Configure the repository's `origin` remote. Commit the release scripts and all intended source changes before releasing.

Close the game and panel, and wait for Steam downloads to finish. The working tree must be clean so that the ZIP matches the tagged commit. The script never commits your changes for you and refuses to move a tag that points to another commit.

```powershell
# Build, test, tag, push the tag, and publish.
./release.ps1 -Tag v1.0.0 -GameDir '<your-game-folder>'

# Build and test the versioned ZIP without changing tags or publishing anything.
./release.ps1 -Tag v1.0.0 -BuildOnly

# A prerelease tag creates a prerelease on GitHub.
./release.ps1 -Tag v1.1.0-beta.1
```

New releases start as drafts and are published only after both assets upload successfully. If an upload fails, rerun the same command from the same commit. Existing releases keep their title and notes; matching assets are replaced. An existing draft is published after the upload succeeds. A tag successfully pushed before a later failure remains on GitHub for the retry.

This is an explicit local command: clicking Publish Release on GitHub or pushing a tag by itself cannot start a build on an unconnected PC. The former runner workflow has been removed. There are no GitHub Actions secrets or runner settings to configure. Only the ZIP and checksum are uploaded; game libraries, test backups, settings, and API keys remain local.

## Project layout

```text
src/Bridge/          Unity lifecycle, F8/menu controls, status display, request forwarding
src/Panel/           Localhost server, English settings UI, provider transport
tools/Installer/    Compatibility checks, versioned backups, patcher and uninstaller
tools/verify-bridge.ps1
packaging/          Double-click install, play, and uninstall helpers
tests/              Local integration tests and optional live provider check
build.ps1           Build and package
release.ps1         Build/test locally, push a tag, and publish the GitHub Release
install.ps1         Install, verify, or uninstall
launch.ps1          Repair compatible updates, then launch through Steam
```

The bridge intercepts `KolkataApi.CompleteOpenRouterAsync` while preserving game prompts, history, validation, and fallback behavior. It forwards custom requests to the helper using `UnityWebRequest`. The helper owns provider credentials and outbound HTTP calls.

Browser endpoints are `/api/config`, `/api/status`, and `/api/test`. The bridge uses `/internal/status` and `/internal/chat/completions`. Browser and internal routes use separate per-session tokens; configuration reads never return the API key.

Build output, generated context caches, local settings, and test artifacts are excluded from Git.
