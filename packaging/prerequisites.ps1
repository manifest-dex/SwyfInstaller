# Runs with Windows PowerShell 5.1, before either .NET application can start.
param([string]$PackageDir = $PSScriptRoot)

function Test-ManagedRuntime {
    param([string]$Executable)
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $process.StartInfo.FileName = $Executable
    $process.StartInfo.Arguments = '--check-runtime'
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.CreateNoWindow = $true
    $process.StartInfo.RedirectStandardOutput = $true
    $process.StartInfo.RedirectStandardError = $true
    # Missing frameworks must return an exit code, not display the host's download dialog.
    $process.StartInfo.EnvironmentVariables['DOTNET_DISABLE_GUI_ERRORS'] = '1'
    try {
        [void]$process.Start()
        $output = $process.StandardOutput.ReadToEndAsync()
        $errors = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(15000)) { $process.Kill(); $process.WaitForExit(); return $false }
        return $process.ExitCode -eq 0 -and $output.GetAwaiter().GetResult().Trim() -eq 'SWYF_RUNTIME_OK'
    } catch { return $false }
    finally { $process.Dispose() }
}

function Get-RuntimeMetadata {
    # No SDK or third-party download manager is needed.
    Invoke-RestMethod -Uri 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json' -UseBasicParsing -TimeoutSec 30 -MaximumRedirection 0
}

function Get-RuntimeDownload {
    param($Metadata, [ValidateSet('runtime', 'aspnetcore-runtime')][string]$Component)
    $release = $Metadata.releases | Where-Object { $_.'release-version' -match '^10\.0\.\d+$' } |
        Sort-Object { [version]$_.'release-version' } -Descending | Select-Object -First 1
    $version = $release.$Component.version
    if ($version -notmatch '^10\.0\.\d+$') { throw 'Microsoft runtime metadata did not contain a stable .NET 10 release.' }
    $prefix = if ($Component -eq 'runtime') { 'dotnet' } else { 'aspnetcore' }
    $folder = if ($Component -eq 'runtime') { 'Runtime' } else { 'aspnetcore/Runtime' }
    $files = @($release.$Component.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -eq "$prefix-runtime-win-x64.exe" })
    if ($files.Count -ne 1) { throw 'Microsoft runtime metadata did not contain one Windows x64 installer.' }
    $file = $files[0]
    $expectedUrl = "https://builds.dotnet.microsoft.com/dotnet/$folder/$version/$prefix-runtime-$version-win-x64.exe"
    if ($file.url -cne $expectedUrl -or $file.hash -notmatch '^[0-9a-fA-F]{128}$') {
        throw 'Microsoft runtime download URL or SHA-512 checksum was invalid.'
    }
    [pscustomobject]@{ Url = $file.url; Hash = $file.hash; Name = "$prefix-runtime-$version-win-x64.exe"; Version = $version }
}

function Save-RuntimeInstaller {
    param($Download, [string]$Destination)
    Invoke-WebRequest -Uri $Download.Url -OutFile $Destination -UseBasicParsing -TimeoutSec 600 -MaximumRedirection 0
}

function Assert-RuntimeInstaller {
    param([string]$Path, [string]$ExpectedHash)
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA512).Hash -ine $ExpectedHash) {
        throw 'The runtime download failed its SHA-512 check. No installer was run. Please retry.'
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or !$signature.SignerCertificate -or
        $signature.SignerCertificate.Subject -notmatch '(?:^|,\s*)O=Microsoft Corporation(?:,|$)') {
        throw 'The runtime download does not have a valid Microsoft signature. No installer was run.'
    }
}

function Start-RuntimeInstaller {
    param([string]$Path)
    Write-Host 'Approve the Windows administrator prompt to install the Microsoft runtime.'
    try {
        $process = Start-Process -FilePath $Path -ArgumentList '/install', '/passive', '/norestart' -Verb RunAs -Wait -PassThru -ErrorAction Stop
        return $process.ExitCode
    } catch {
        # PowerShell may wrap the native UAC cancellation exception.
        $exception = $_.Exception
        while ($exception) {
            if ($exception -is [System.ComponentModel.Win32Exception] -and $exception.NativeErrorCode -eq 1223) {
                throw 'Runtime installation was canceled. Run Install Custom AI.cmd again and approve the Windows prompt.'
            }
            $exception = $exception.InnerException
        }
        throw
    }
}

function Install-RuntimeComponent {
    param($Metadata, [string]$Component, [string]$DownloadDirectory)
    $download = Get-RuntimeDownload $Metadata $Component
    $path = Join-Path $DownloadDirectory $download.Name
    Write-Host "Downloading Microsoft $Component $($download.Version) (Windows x64)..."
    Save-RuntimeInstaller $download $path
    Assert-RuntimeInstaller $path $download.Hash
    $code = Start-RuntimeInstaller $path
    if ($code -in @(3010, 1641)) { return 3010 }
    if ($code -ne 0) { throw "Microsoft runtime installation failed (exit code $code)." }
    return 0
}

function Invoke-Prerequisites {
    param([string]$Package)
    $downloadDirectory = $null
    $oldSecurityProtocol = [Net.ServicePointManager]::SecurityProtocol
    $oldProgressPreference = $ProgressPreference
    try {
        $ErrorActionPreference = 'Stop'
        $ProgressPreference = 'SilentlyContinue'
        [Net.ServicePointManager]::SecurityProtocol = $oldSecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        if (![Environment]::Is64BitOperatingSystem) { throw 'This mod requires 64-bit Windows.' }
        $installer = Join-Path $Package 'installer/SWYF.CustomAI.Installer.exe'
        $panel = Join-Path $Package 'panel/SWYF.CustomAI.Panel.exe'
        foreach ($executable in @($installer, $panel)) {
            foreach ($file in @($executable, [IO.Path]::ChangeExtension($executable, '.dll'), [IO.Path]::ChangeExtension($executable, '.runtimeconfig.json'))) {
                if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw 'The mod package is incomplete. Extract the entire release ZIP into the game folder again.' }
            }
        }
        Write-Host 'Checking .NET 10 and ASP.NET Core 10 (Windows x64)...'
        # Probe the shipped x64 apphosts instead of PATH/registry heuristics. This also
        # catches DOTNET_ROOT overrides, x86-only installs and an SDK missing ASP.NET.
        $hasRuntime = Test-ManagedRuntime $installer
        $hasAspNet = Test-ManagedRuntime $panel
        if ($hasRuntime -and $hasAspNet) { Write-Host 'Runtime checks passed.'; return 0 }
        Write-Host 'A required runtime is missing or cannot be loaded. Downloading from Microsoft.'
        $metadata = Get-RuntimeMetadata
        $downloadDirectory = Join-Path ([IO.Path]::GetTempPath()) ('SWYF-runtime-' + [guid]::NewGuid().ToString('N'))
        [void](New-Item -ItemType Directory -Path $downloadDirectory)
        if (!$hasRuntime) {
            if ((Install-RuntimeComponent $metadata 'runtime' $downloadDirectory) -eq 3010) {
                Write-Host 'Windows needs a restart. Restart your PC, then run Install Custom AI.cmd again.'
                return 3010
            }
            if (!(Test-ManagedRuntime $installer)) { throw '.NET is installed but the mod cannot load it. Restart Windows and retry; check any custom DOTNET_ROOT settings.' }
        }
        if (!(Test-ManagedRuntime $panel)) {
            if ((Install-RuntimeComponent $metadata 'aspnetcore-runtime' $downloadDirectory) -eq 3010) {
                Write-Host 'Windows needs a restart. Restart your PC, then run Install Custom AI.cmd again.'
                return 3010
            }
        }
        if (!(Test-ManagedRuntime $installer) -or !(Test-ManagedRuntime $panel)) {
            throw 'The settings panel still cannot load its runtime. Restart Windows and retry; check any custom DOTNET_ROOT settings.'
        }
        Write-Host 'Runtime checks passed. The settings panel can start.'
        return 0
    } catch {
        Write-Host "Prerequisite check failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host 'The mod was not installed. Check your connection, then run Install Custom AI.cmd again.'
        Write-Host 'For manual/offline setup, install .NET Runtime 10 AND ASP.NET Core Runtime 10, Windows x64:'
        Write-Host 'https://dotnet.microsoft.com/en-us/download/dotnet/10.0'
        return 1
    } finally {
        [Net.ServicePointManager]::SecurityProtocol = $oldSecurityProtocol
        $ProgressPreference = $oldProgressPreference
        if ($downloadDirectory) {
            # Delete only the two files we downloaded; never recursively remove a computed path.
            Get-ChildItem -LiteralPath $downloadDirectory -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match '^(dotnet|aspnetcore)-runtime-10\.0\.\d+-win-x64\.exe$' } |
                Remove-Item -Force -ErrorAction SilentlyContinue
            if (!(Get-ChildItem -LiteralPath $downloadDirectory -Force -ErrorAction SilentlyContinue)) {
                Remove-Item -LiteralPath $downloadDirectory -ErrorAction SilentlyContinue
            }
        }
    }
}

# Dot-sourcing exposes the functions for offline regression tests, without running setup.
if ($MyInvocation.InvocationName -ne '.') { exit (Invoke-Prerequisites $PackageDir) }
