# Releasing v1.1.0

This is a manual local release. No commit or tag deploys SWYF or publishes a GitHub release automatically. The production service and ManifestDeX OAuth must be deployed and verified separately before announcing free hosted AI availability.

## Prepare and verify

On the Windows PC with a compatible game installation, install .NET SDK 10, Git, Node.js and GitHub CLI. Close the game and companion. Version is in `Directory.Build.props`; release notes are `releases/v1.1.0.md`. Commit all intended changes first. The script refuses a dirty tree, mismatched version/tag, missing notes, or local-test source.

```powershell
cd 'C:\Users\berke\source\repos\SWYF Custom AI Mod'
./release.ps1 -Tag v1.1.0 -BuildOnly -GameDir 'D:\SteamLibrary\steamapps\common\Scam With Your Friends Playtest'
```

This builds the production bridge/panel/installer, verifies bridge references against your game, runs the provider/installer tests on disposable copies, and checks the sign-in UI. It creates:

- `artifacts/SWYF-Custom-AI-v1.1.0-win-x64.zip`
- `artifacts/SWYF-Custom-AI-v1.1.0-win-x64.zip.sha256`

`-BuildOnly` creates no tag, performs no upload, and installs nothing into your game. The installed temporary local-test panel is not used as build input. Do not zip your game or installed `CustomAI` folder manually: it contains private settings and account sessions.

## Tag and publish when ready

`origin` is `https://github.com/manifest-dex/swyf-custom-ai-mod.git`. Check it before publishing. The local annotated `v1.1.0` tag is prepared at the release commit; do not force-move it. To include that commit in the remote main branch, push main yourself first:

```powershell
git status --short
git show v1.1.0 --no-patch
gh auth login
git push origin main

# Build/test again, push only the v1.1.0 tag, and upload a draft with curated notes.
./release.ps1 -Tag v1.1.0 -Draft -GameDir 'D:\SteamLibrary\steamapps\common\Scam With Your Friends Playtest'

# After reviewing notes/assets and verifying production OAuth and a real game call:
gh release edit v1.1.0 --repo manifest-dex/swyf-custom-ai-mod --draft=false
```

Omit `-Draft` to let `release.ps1` publish after upload. It will never move a tag pointing at another commit. An upload failure leaves the newly created release as a draft; rerun from the same commit to retry. Existing notes/titles are preserved. Only ZIP/checksum assets are uploaded. A GitHub UI action cannot build the package on your PC.

For a later release, update `Version`/assembly versions, add the matching notes file, commit, and use its new tag. A tag containing a prerelease suffix creates a GitHub prerelease. Never reuse v1.1.0 for changed source after publication.

## Switching this test installation to production

Once the service is live, close the game and panel, extract the production ZIP and run its installer. It replaces the temporary localhost companion with the production build. Press F8 and connect through ManifestDeX again; the local-test DPAPI session is intentionally not reused. Private provider settings survive. Confirm **Save Settings** after selecting the hosted option. Do not enable the hosted option for members until the server's production OAuth/provider checks pass.
