# No Pester, network access, UAC prompts or runtime installation needed.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../packaging/prerequisites.ps1')
$script:passed = 0
function Check([bool]$Condition, [string]$Name) {
    if (!$Condition) { throw "FAIL: $Name" }
    $script:passed++
    Write-Host "PASS: $Name"
}
function Throws([scriptblock]$Action, [string]$Message) {
    try { & $Action; throw 'Expected an exception.' }
    catch { Check ($_.Exception.Message -like "*$Message*") $Message }
}
function New-Metadata {
    $release = [pscustomobject]@{ 'release-version' = '10.0.12' }
    foreach ($component in @('runtime', 'aspnetcore-runtime')) {
        $prefix = if ($component -eq 'runtime') { 'dotnet' } else { 'aspnetcore' }
        $folder = if ($component -eq 'runtime') { 'Runtime' } else { 'aspnetcore/Runtime' }
        $release | Add-Member -NotePropertyName $component -NotePropertyValue ([pscustomobject]@{
            version = '10.0.12'; files = @([pscustomobject]@{
                rid = 'win-x64'; name = "$prefix-runtime-win-x64.exe"
                url = "https://builds.dotnet.microsoft.com/dotnet/$folder/10.0.12/$prefix-runtime-10.0.12-win-x64.exe"
                hash = ('a' * 128)
            })
        })
    }
    [pscustomobject]@{ releases = @($release) }
}

foreach ($component in @('runtime', 'aspnetcore-runtime')) {
    $file = Get-RuntimeDownload (New-Metadata) $component
    Check ($file.Version -eq '10.0.12' -and $file.Name.EndsWith('-win-x64.exe')) "select stable x64 $component"
}
& {
    $metadata = New-Metadata
    $preview = (New-Metadata).releases[0]
    $preview.'release-version' = '10.0.13-preview.1'
    $metadata.releases = @($preview) + $metadata.releases
    Check ((Get-RuntimeDownload $metadata runtime).Version -eq '10.0.12') 'ignore preview releases'
    $metadata.releases = @($preview)
    Throws { Get-RuntimeDownload $metadata runtime } 'stable .NET 10 release'
}
foreach ($badUrl in @('http://builds.dotnet.microsoft.com/dotnet/Runtime/10.0.12/dotnet-runtime-10.0.12-win-x64.exe',
    'https://builds.dotnet.microsoft.com.evil.example/download.exe', 'https://evil.example/download.exe')) {
    $metadata = New-Metadata
    $metadata.releases[0].runtime.files[0].url = $badUrl
    Throws { Get-RuntimeDownload $metadata runtime } 'URL or SHA-512'
}
& {
    $metadata = New-Metadata
    $metadata.releases[0].runtime.files[0].hash = 'bad'
    Throws { Get-RuntimeDownload $metadata runtime } 'URL or SHA-512'
    $metadata.releases[0].runtime.files[0].rid = 'win-x86'
    Throws { Get-RuntimeDownload $metadata runtime } 'one Windows x64 installer'
}

# Signature and hash checks must both pass before elevation can be reached.
& {
    function Get-FileHash { [pscustomobject]@{ Hash = ('b' * 128) } }
    function Get-AuthenticodeSignature { throw 'Signature should not be read after a hash mismatch.' }
    Throws { Assert-RuntimeInstaller 'unused' ('a' * 128) } 'SHA-512 check'
}
foreach ($signatureCase in @(
    @{ Status = 'NotSigned'; Subject = 'CN=Microsoft Corporation, O=Microsoft Corporation, C=US' },
    @{ Status = 'Valid'; Subject = 'CN=Not Microsoft, O=Other, C=US' },
    @{ Status = 'Valid'; Subject = 'CN=Microsoft Corporation, O=Microsoft Corporation Impostor, C=US' })) {
    & {
        function Get-FileHash { [pscustomobject]@{ Hash = ('a' * 128) } }
        function Get-AuthenticodeSignature { [pscustomobject]@{ Status = $signatureCase.Status; SignerCertificate = [pscustomobject]@{ Subject = $signatureCase.Subject } } }
        Throws { Assert-RuntimeInstaller 'unused' ('a' * 128) } 'valid Microsoft signature'
    }
}
& {
    function Get-FileHash { [pscustomobject]@{ Hash = ('a' * 128) } }
    function Get-AuthenticodeSignature { [pscustomobject]@{ Status = 'Valid'; SignerCertificate = [pscustomobject]@{ Subject = 'CN=Microsoft Corporation, O=Microsoft Corporation, C=US' } } }
    Assert-RuntimeInstaller 'unused' ('A' * 128)
    Check $true 'accept valid hash and Microsoft signature'
}
& {
    function Start-Process { throw (New-Object System.ComponentModel.Win32Exception 1223) }
    Throws { Start-RuntimeInstaller 'unused' } 'installation was canceled'
}
foreach ($code in @(0, 3010, 1641, 1603)) {
    & {
        function Save-RuntimeInstaller { }
        function Assert-RuntimeInstaller { }
        function Start-RuntimeInstaller { return $code }
        if ($code -eq 1603) { Throws { Install-RuntimeComponent (New-Metadata) runtime $PSScriptRoot } 'exit code 1603' }
        else { Check ((Install-RuntimeComponent (New-Metadata) runtime $PSScriptRoot) -eq $(if ($code -eq 0) { 0 } else { 3010 })) "handle Microsoft installer exit $code" }
    }
}

$fixture = Join-Path $PSScriptRoot ('../artifacts/prerequisite-tests/' + [guid]::NewGuid().ToString('N') + '/game & spaces/CustomAI/package')
[void](New-Item -ItemType Directory -Force -Path $fixture)
foreach ($component in @('installer', 'panel')) {
    $folder = Join-Path $fixture $component
    [void](New-Item -ItemType Directory -Path $folder)
    $name = if ($component -eq 'installer') { 'SWYF.CustomAI.Installer' } else { 'SWYF.CustomAI.Panel' }
    foreach ($extension in @('.exe', '.dll', '.runtimeconfig.json')) { Set-Content -LiteralPath (Join-Path $folder ($name + $extension)) -Value 'fixture; never executed' }
}

# Orchestration tests replace only external effects. Nothing can download or elevate.
foreach ($scenario in @('ready', 'missing-aspnet', 'missing-both', 'offline', 'download-failure', 'bad-signature', 'cancel', 'reboot', 'still-broken', 'incomplete')) {
    & {
        $state = @{ Runtime = $scenario -notin @('missing-both'); AspNet = $scenario -eq 'ready'; Metadata = 0; Installed = @(); Fetched = @() }
        function Test-ManagedRuntime([string]$Executable) {
            if ($Executable.EndsWith('Installer.exe')) { return $state.Runtime }
            return $state.AspNet
        }
        function Get-RuntimeMetadata {
            $state.Metadata++
            if ($scenario -eq 'offline') { throw 'Offline fixture' }
            return New-Metadata
        }
        function Save-RuntimeInstaller($Download, [string]$Destination) {
            $state.Fetched += $Destination
            Set-Content -LiteralPath $Destination -Value 'fixture'
            if ($scenario -eq 'download-failure') { throw 'Interrupted download fixture' }
        }
        function Assert-RuntimeInstaller {
            if ($scenario -eq 'bad-signature') { throw 'Invalid signature fixture' }
        }
        function Start-RuntimeInstaller([string]$Path) {
            $state.Installed += [IO.Path]::GetFileName($Path)
            if ($scenario -eq 'cancel') { throw 'UAC canceled fixture' }
            if ($scenario -eq 'reboot') { return 3010 }
            if ($Path.Contains('aspnetcore')) { $state.AspNet = $scenario -ne 'still-broken' } else { $state.Runtime = $true }
            return 0
        }
        $package = if ($scenario -eq 'incomplete') { Join-Path $fixture 'missing' } else { $fixture }
        $result = Invoke-Prerequisites $package
        $expected = if ($scenario -in @('ready', 'missing-aspnet', 'missing-both')) { 0 } elseif ($scenario -eq 'reboot') { 3010 } else { 1 }
        Check ($result -eq $expected) "setup result: $scenario"
        if ($scenario -in @('ready', 'incomplete')) { Check ($state.Metadata -eq 0) "$scenario needs no network" }
        if ($scenario -in @('ready', 'incomplete', 'offline', 'download-failure', 'bad-signature')) { Check ($state.Installed.Count -eq 0) "$scenario runs no runtime installer" }
        if ($scenario -eq 'missing-aspnet') { Check ($state.Installed.Count -eq 1 -and $state.Installed[0].StartsWith('aspnetcore')) 'base runtime alone triggers ASP.NET installation' }
        if ($scenario -eq 'missing-both') { Check ($state.Installed.Count -eq 2 -and $state.Installed[0].StartsWith('dotnet') -and $state.Installed[1].StartsWith('aspnetcore')) 'install missing base runtime before ASP.NET' }
        foreach ($path in $state.Fetched) { Check (!(Test-Path -LiteralPath $path)) "$scenario cleans temporary download" }
    }
}

# Real apphost check on the build machine, with network explicitly forbidden.
$dist = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../dist'))
if (Test-Path -LiteralPath (Join-Path $dist 'panel/SWYF.CustomAI.Panel.exe')) {
    & {
        function Get-RuntimeMetadata { throw 'Already installed runtimes must not require network access.' }
        Check ((Invoke-Prerequisites $dist) -eq 0) 'real packaged applications load without network, configuration or a game process'
    }
    & {
        $previousRoot = $env:DOTNET_ROOT_X64
        try {
            $env:DOTNET_ROOT_X64 = $fixture
            Check (!(Test-ManagedRuntime (Join-Path $dist 'panel/SWYF.CustomAI.Panel.exe'))) 'unusable runtime override is detected without a GUI prompt'
            # Reproduce the reported machine state using an isolated copy: working
            # .NET 10 x64, but no ASP.NET shared framework. No installed files change.
            $dotnetRoot = Split-Path (Get-Command dotnet.exe -ErrorAction Stop).Source
            $core = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'shared/Microsoft.NETCore.App') -Directory |
                Where-Object { $_.Name -match '^10\.0\.\d+$' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
            $hostFxr = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'host/fxr') -Directory |
                Where-Object { $_.Name -match '^10\.0\.\d+$' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
            if (!$core -or !$hostFxr) { throw 'Tests require an installed .NET 10 x64 SDK/runtime.' }
            $isolated = Join-Path $fixture 'isolated-dotnet'
            [void](New-Item -ItemType Directory -Force -Path (Join-Path $isolated 'shared/Microsoft.NETCore.App'), (Join-Path $isolated 'host/fxr'))
            Copy-Item -LiteralPath $core.FullName -Destination (Join-Path $isolated 'shared/Microsoft.NETCore.App') -Recurse
            Copy-Item -LiteralPath $hostFxr.FullName -Destination (Join-Path $isolated 'host/fxr') -Recurse
            $env:DOTNET_ROOT_X64 = $isolated
            Check (Test-ManagedRuntime (Join-Path $dist 'installer/SWYF.CustomAI.Installer.exe')) 'real base-only .NET 10 runtime loads the installer'
            Check (!(Test-ManagedRuntime (Join-Path $dist 'panel/SWYF.CustomAI.Panel.exe'))) 'real base-only .NET 10 runtime cannot load the ASP.NET panel'
        } finally { $env:DOTNET_ROOT_X64 = $previousRoot }
    }
}
Write-Host "$script:passed prerequisite checks passed."
