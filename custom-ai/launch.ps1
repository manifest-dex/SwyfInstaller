param([string]$GameDir = $env:SWYF_GAME_DIR)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameDir) -and (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Scam With Your Friends.exe'))) { $GameDir = $PSScriptRoot }
if ([string]::IsNullOrWhiteSpace($GameDir)) { throw 'Pass -GameDir or set SWYF_GAME_DIR to your game installation folder.' }
if (Get-Process -Name 'Scam With Your Friends' -ErrorAction SilentlyContinue) {
    throw 'Close the game before launching with update repair.'
}
# Wait for Steam downloads to finish first: Steam can otherwise replace the patch after this check.
$appId = (Get-Content -Raw -LiteralPath (Join-Path $GameDir 'steam_appid.txt')).Trim()
if ($appId -notmatch '^\d+$') { throw 'The game steam_appid.txt does not contain a valid Steam app ID.' }
& (Join-Path $PSScriptRoot 'install.ps1') -GameDir $GameDir
if ($LASTEXITCODE) { throw 'Compatibility checks failed. The game was not launched.' }
Start-Process -FilePath "steam://rungameid/$appId" -WindowStyle Hidden
