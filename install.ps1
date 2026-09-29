param(
    [ValidateSet('install','uninstall','verify')][string]$Action = 'install',
    [string]$GameDir = $env:SWYF_GAME_DIR
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameDir) -and (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Scam With Your Friends.exe'))) { $GameDir = $PSScriptRoot }
if ([string]::IsNullOrWhiteSpace($GameDir)) { throw 'Pass -GameDir or set SWYF_GAME_DIR to your game installation folder.' }
$package = if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'installer')) { $PSScriptRoot } else { Join-Path $PSScriptRoot 'dist' }
$installer = Join-Path $package 'installer/SWYF.CustomAI.Installer.exe'
if (!(Test-Path -LiteralPath $installer)) { throw 'Run build.ps1 first.' }
& $installer $Action $GameDir $package
if ($LASTEXITCODE) { throw 'The installation operation failed.' }
