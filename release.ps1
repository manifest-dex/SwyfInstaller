# Release script for SwyfInstaller.
# Builds, tests, packages (ZIP + .sha256), tags and publishes a GitHub Release.
# Usage:  powershell -ExecutionPolicy Bypass -File release.ps1 -Tag v1.0.0
#         powershell -ExecutionPolicy Bypass -File release.ps1 -Tag v1.0.0 -BuildOnly
param(
    [string]$Tag = "v1.0.0",
    [switch]$BuildOnly
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$version = $Tag.TrimStart('v')

function Step($msg) { Write-Host ""; Write-Host "=== $msg ===" -ForegroundColor Cyan }
function Exec($cmd) {
    Write-Host "> $cmd"
    $code = 0
    Invoke-Expression $cmd
    $code = $LASTEXITCODE
    if ($code -ne 0) { throw "Command failed with exit code ${code}: $cmd" }
}

Step "Checking working tree"
$dirty = (git -C $root status --porcelain) -join "`n"
if ($dirty.Trim() -ne "") { throw "Working tree is not clean. Commit everything first." }

Step "Building solution"
Exec "dotnet build `"$root\SwyfInstaller.sln`" -c Release --nologo -v q"

Step "Running selftest"
Exec "dotnet `"$root\SwyfInstaller\bin\Release\net10.0-windows\SwyfInstaller.dll`" selftest"

Step "Publishing executables"
Exec "dotnet publish `"$root\SwyfInstaller\SwyfInstaller.csproj`" -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o `"$root\SwyfInstaller\publish`" --nologo -v q"
Exec "dotnet publish `"$root\SwyfInstaller.Gui\SwyfInstaller.Gui.csproj`" -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o `"$root\SwyfInstaller.Gui\publish`" --nologo -v q"
Copy-Item "$root\SwyfInstaller\publish\SwyfInstaller.exe" "$root\SwyfInstaller.Gui\publish\" -Force

Step "Building setup"
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (!(Test-Path $iscc)) { throw "Inno Setup 6 not found at $iscc. Install it first: https://jrsoftware.org/isinfo.php" }
Push-Location $root
& $iscc "/DAppVersion=$version" "installer.iss"
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Inno Setup failed." }
Pop-Location
$setupName = "SwyfInstaller-Setup-v$version-win-x64.exe"
$setupPath = Join-Path $root "artifacts\$setupName"
if (!(Test-Path $setupPath)) { throw "Setup output not found: $setupPath" }
$hash = (Get-FileHash $setupPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $setupName" | Set-Content "$setupPath.sha256" -NoNewline
Write-Host "${setupName}: $hash"
$assets = @($setupName)
$artifacts = Split-Path $setupPath -Parent

if ($BuildOnly) { Write-Host ""; Write-Host "Build-only done: $artifacts"; return }

Step "Tagging $Tag"
$existing = ""
try { $existing = git -C $root rev-parse $Tag 2>$null } catch { }
if ($existing.Trim() -ne "") { throw "Tag $Tag already exists. Delete it or pick a new tag." }
Exec "git -C `"$root`" tag -a $Tag -m `"SwyfInstaller $Tag`""
Exec "git -C `"$root`" push origin $Tag"

Step "Publishing GitHub Release $Tag"
$env:GIT_TERMINAL_PROMPT = "0"
$env:GCM_INTERACTIVE = "never"
$fill = "protocol=https`nhost=github.com`n`n" | git credential fill
$token = ($fill -split "`r?`n" | Where-Object { $_ -like 'password=*' } | Select-Object -First 1) -replace '^password=', ''
if ([string]::IsNullOrWhiteSpace($token)) { throw "No stored GitHub credentials found. Sign in once (e.g. git push) and retry." }
$headers = @{
    Authorization           = "Bearer $token"
    Accept                  = "application/vnd.github+json"
    "X-GitHub-Api-Version"  = "2022-11-28"
}
$notes = @"
Console installer + basic WinForms UI for SWYF Custom AI.

- Downloads the latest mod release and checks it before touching the game.
- Update removes old package files, keeps settings and backups.
- Verify re-checks installed files; uninstall runs the package uninstaller.
- The app now updates itself: it checks this repo on startup and installs
  new releases automatically (checksum-verified).

Run the setup to install (Start Menu + Programs list, no admin needed).
"@
$releaseBody = @{
    tag_name   = $Tag
    name       = "SwyfInstaller $Tag"
    body       = $notes
    draft      = $false
    prerelease = $false
} | ConvertTo-Json
$release = Invoke-RestMethod -Method Post -Uri "https://api.github.com/repos/manifest-dex/SwyfInstaller/releases" `
    -Headers $headers -ContentType "application/json" -Body $releaseBody -TimeoutSec 60
$uploadBase = ($release.upload_url -split '\{')[0]
foreach ($name in $assets + @("$setupName.sha256")) {
    $path = Join-Path $artifacts $name
    Write-Host "Uploading $name ..."
    $uri = $uploadBase + "?name=" + [uri]::EscapeDataString($name)
    Invoke-RestMethod -Method Post -Uri $uri -Headers $headers `
        -ContentType "application/octet-stream" -InFile $path -TimeoutSec 900 | Out-Null
}
Write-Host ""
Write-Host "Released: $($release.html_url)" -ForegroundColor Green
