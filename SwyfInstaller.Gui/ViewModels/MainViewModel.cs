using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwyfInstaller.Gui.Services;

namespace SwyfInstaller.Gui.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const string DefaultRepo = "manifest-dex/swyf-custom-ai-mod";
    private const string ExeName = "Scam With Your Friends.exe";

    private readonly BackendRunner _backend = new();
    private readonly SelfUpdater _updater = new();
    private CancellationTokenSource _runCts;

    [ObservableProperty] private string _gameDir = "";
    [ObservableProperty] private string _repo = DefaultRepo;
    [ObservableProperty] private string _installedTag = "";
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isProgressIndeterminate;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _runPatchStep = true;
    [ObservableProperty] private bool _forceReinstall;
    [ObservableProperty] private bool _fullUninstall;
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private string _updateButtonText = "Update app";
    private SelfUpdateInfo _pendingUpdate;

    public string InstalledLabel => InstalledTag == "" ? "Mod status unknown" : "Installed mod: " + InstalledTag;
    public string AppVersion => "v" + SelfUpdater.CurrentVersion;

    partial void OnInstalledTagChanged(string value) => OnPropertyChanged(nameof(InstalledLabel));

    public async Task InitializeAsync()
    {
        LoadSavedState();
        await CheckForUpdatesAsync();
    }

    private string AppDataDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SwyfInstaller");

    private void LoadSavedState()
    {
        try
        {
            string cfg = Path.Combine(AppDataDir(), "config.json");
            if (!File.Exists(cfg)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(cfg));
            if (doc.RootElement.TryGetProperty("GameDir", out var g) && g.GetString() is string gd && gd != "")
                GameDir = gd;
            if (doc.RootElement.TryGetProperty("Repo", out var r) && r.GetString() is string rp && rp != "")
                Repo = rp;
            if (doc.RootElement.TryGetProperty("InstalledTag", out var t) && t.GetString() is string tag)
                InstalledTag = tag;
        }
        catch { }
    }

    private void AppendLog(string line)
    {
        LogText += line + "\n";
        if (LogText.Length > 40000)
            LogText = LogText.Substring(LogText.Length - 40000);
    }

    [RelayCommand]
    private void BrowseGameDir()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select the game folder (contains " + ExeName + ")"
        };
        if (dlg.ShowDialog() != true) return;
        if (!File.Exists(Path.Combine(dlg.FolderName, ExeName)))
        {
            MessageBox.Show("That folder does not contain " + ExeName + ".", "Not a game folder",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        GameDir = dlg.FolderName;
    }

    [RelayCommand]
    private async Task DetectGameDirAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = "Detecting game folder…";
        try
        {
            var lines = await _backend.RunCaptureAsync("detect");
            string first = lines.FirstOrDefault(l => l.Trim() != "");
            if (first == "")
            {
                MessageBox.Show("No game folder detected. Use Browse instead.", "Not found",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            GameDir = first.Trim();
            AppendLog("Detected: " + GameDir);
            StatusMessage = "Ready";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Detect failed", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Ready";
        }
        finally { IsBusy = false; }
    }

    private string GameArgs(string command)
    {
        string args = command + " --yes --window --progress";
        if (GameDir.Trim() != "") args += " --gamedir \"" + GameDir.Trim() + "\"";
        if (Repo.Trim() != "") args += " --repo " + Repo.Trim();
        return args;
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (IsBusy) return;
        if (MessageBox.Show("Download and install the latest release?", "Confirm install",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        string args = GameArgs("install");
        if (!RunPatchStep) args += " --no-apply";
        await RunModAsync(args, "Installing…");
    }

    [RelayCommand]
    private async Task UpdateModAsync()
    {
        if (IsBusy) return;
        if (MessageBox.Show("Remove old files and update to the latest release?", "Confirm update",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        string args = GameArgs("update");
        if (!RunPatchStep) args += " --no-apply";
        if (ForceReinstall) args += " --force";
        await RunModAsync(args, "Updating…");
    }

    [RelayCommand]
    private async Task VerifyAsync()
    {
        if (IsBusy) return;
        await RunModAsync(GameArgs("verify"), "Verifying…");
    }

    [RelayCommand]
    private async Task UninstallAsync()
    {
        if (IsBusy) return;
        string extra = FullUninstall ? " INCLUDING settings and backups" : "";
        if (MessageBox.Show("Uninstall the mod" + extra + "?", "Confirm uninstall",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        string args = GameArgs("uninstall");
        if (FullUninstall) args += " --full";
        await RunModAsync(args, "Uninstalling…");
    }

    [RelayCommand]
    private void CancelRun()
    {
        try { _runCts?.Cancel(); } catch { }
        _backend.Kill();
    }

    private async Task RunModAsync(string args, string phase)
    {
        if (IsBusy) return;
        IsBusy = true;
        _runCts = new CancellationTokenSource();
        IsProgressIndeterminate = true;
        Progress = 0;
        StatusMessage = phase;
        var log = new Progress<string>(line => AppendLog(line));
        var bar = new Progress<double>(v =>
        {
            if (v < 0) IsProgressIndeterminate = true;
            else { IsProgressIndeterminate = false; Progress = v / 100.0; }
        });
        try
        {
            int code = await _backend.RunAsync(args, log, bar, _runCts.Token);
            StatusMessage = "Exit code " + code;
            LoadSavedState();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled";
            AppendLog("Cancelled.");
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed";
            AppendLog("ERROR: " + ex.Message);
            MessageBox.Show(ex.Message, "Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        await CheckForUpdatesAsync(manual: true);
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (IsBusy && !manual) return;
        if (manual) StatusMessage = "Checking for app updates…";
        try
        {
            var info = await _updater.CheckForUpdatesAsync();
            if (info == null)
            {
                if (manual)
                {
                    StatusMessage = "App is up to date (v" + SelfUpdater.CurrentVersion + ")";
                    AppendLog("App is up to date (v" + SelfUpdater.CurrentVersion + ").");
                }
                return;
            }
            _pendingUpdate = info;
            UpdateButtonText = "Update app to v" + info.Version;
            UpdateAvailable = true;
            StatusMessage = "App update available: v" + info.Version;
            AppendLog("App update available: v" + info.Version + " — " + info.Name);
            if (manual && !string.IsNullOrWhiteSpace(info.Notes))
                AppendLog(info.Notes.Length > 800 ? info.Notes.Substring(0, 800) + "…" : info.Notes);
        }
        catch (Exception ex)
        {
            if (manual)
            {
                StatusMessage = "Update check failed";
                AppendLog("Update check failed: " + ex.Message);
                MessageBox.Show("Could not check for updates: " + ex.Message, "Update check",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    [RelayCommand]
    private async Task DownloadAndInstallUpdateAsync()
    {
        var info = _pendingUpdate;
        if (info == null || IsBusy) return;
        if (MessageBox.Show("Download and install app v" + info.Version + " now? The app will restart.",
                "Confirm update", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        IsBusy = true;
        IsProgressIndeterminate = false;
        Progress = 0;
        try
        {
            AppendLog("Downloading app v" + info.Version + " …");
            var prog = new Progress<double>(f =>
            {
                Progress = f;
                StatusMessage = "Downloading update… " + (int)(f * 100) + "%";
            });
            string dir = await _updater.DownloadAsync(info, prog, CancellationToken.None);
            AppendLog("Download verified. Restarting to apply…");
            SelfUpdater.InstallAndRestart(dir, info.SetupName);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            StatusMessage = "Update failed";
            AppendLog("Update failed: " + ex.Message);
            MessageBox.Show("Update failed: " + ex.Message, "Update",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
        }
    }
}
