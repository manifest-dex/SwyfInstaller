using System.Diagnostics;
using System.Text.Json;

namespace SwyfInstaller.Gui;

internal sealed class MainForm : Form
{
    private const string BackendExe = "SwyfInstaller.exe";
    private const string DefaultRepo = "manifest-dex/swyf-custom-ai-mod";

    private readonly TextBox _gameBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _repoBox = new() { Dock = DockStyle.Fill, Text = DefaultRepo };
    private readonly RichTextBox _log = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        BackColor = System.Drawing.Color.FromArgb(13, 17, 23),
        ForeColor = System.Drawing.Color.FromArgb(240, 246, 252),
        Font = new System.Drawing.Font("Consolas", 9f),
        HideSelection = false,
    };
    private readonly CheckBox _patchBox = new() { Text = "Run patch step", Checked = true, AutoSize = true };
    private readonly CheckBox _fullBox = new() { Text = "Full uninstall", AutoSize = true };
    private readonly ToolStripStatusLabel _status = new() { Text = "Ready" };
    private readonly FlowLayoutPanel _buttons = new() { Dock = DockStyle.Fill, AutoSize = true };
    private bool _running;

    public MainForm()
    {
        Text = "SWYF Custom AI Installer";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new System.Drawing.Size(640, 480);
        Size = new System.Drawing.Size(780, 580);

        var top = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(10, 10, 10, 0) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        top.Controls.Add(new Label { Text = "Game folder", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        top.Controls.Add(_gameBox, 1, 0);
        var gameBtns = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var browse = new Button { Text = "Browse…", AutoSize = true };
        browse.Click += (_, _) => BrowseGame();
        var detect = new Button { Text = "Detect", AutoSize = true };
        detect.Click += async (_, _) => await DetectGame();
        gameBtns.Controls.Add(browse);
        gameBtns.Controls.Add(detect);
        top.Controls.Add(gameBtns, 2, 0);

        top.Controls.Add(new Label { Text = "Repo", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        top.Controls.Add(_repoBox, 1, 1);
        var latest = new Button { Text = "Check latest", AutoSize = true };
        latest.Click += async (_, _) => await RunBackendAsync("latest", null, false);
        top.Controls.Add(latest, 2, 1);

        var mid = new Panel { Dock = DockStyle.Top, Height = 44 };
        _buttons.Controls.Add(MakeButton("Install", async () => await RunBackendAsync(BuildArgs("install"), "Download and install the latest release?", false)));
        _buttons.Controls.Add(MakeButton("Update", async () => await RunBackendAsync(BuildArgs("update"), "Remove old files and update to the latest release?", false)));
        _buttons.Controls.Add(MakeButton("Verify", async () => await RunBackendAsync(BuildArgs("verify"), null, false)));
        _buttons.Controls.Add(MakeButton("Uninstall", async () => await RunBackendAsync(BuildArgs("uninstall") + (_fullBox.Checked ? " --full" : ""), "Uninstall the mod" + (_fullBox.Checked ? " INCLUDING settings and backups" : "") + "?", false)));
        _buttons.Controls.Add(_patchBox);
        _buttons.Controls.Add(_fullBox);
        _buttons.Padding = new Padding(10, 6, 10, 0);
        mid.Controls.Add(_buttons);

        var status = new StatusStrip();
        status.Items.Add(_status);

        Controls.Add(_log);
        Controls.Add(mid);
        Controls.Add(top);
        Controls.Add(status);

        LoadSavedState();
    }

    private Button MakeButton(string text, Func<Task> onClick)
    {
        var b = new Button { Text = text, AutoSize = true };
        b.Click += async (_, _) => await onClick();
        return b;
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
                _gameBox.Text = gd;
            if (doc.RootElement.TryGetProperty("Repo", out var r) && r.GetString() is string rp && rp != "")
                _repoBox.Text = rp;
            if (doc.RootElement.TryGetProperty("InstalledTag", out var t) && t.GetString() is string tag && tag != "")
                _status.Text = "Installed: " + tag;
        }
        catch { }
    }

    private string BackendPath()
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        string exe = Path.Combine(dir, BackendExe);
        if (File.Exists(exe)) return exe;
        throw new FileNotFoundException(BackendExe + " was not found next to this app (" + dir + ").");
    }

    private string BaseArgs(string command)
    {
        string args = command + " --yes";
        if (_gameBox.Text.Trim() != "") args += " --gamedir \"" + _gameBox.Text.Trim() + "\"";
        if (_repoBox.Text.Trim() != "") args += " --repo " + _repoBox.Text.Trim();
        return args;
    }

    private string BuildArgs(string command)
    {
        string args = BaseArgs(command);
        if ((command == "install" || command == "update") && !_patchBox.Checked) args += " --no-apply";
        return args;
    }

    private void BrowseGame()
    {
        using var dlg = new FolderBrowserDialog { Description = "Select the game folder (contains Scam With Your Friends.exe)" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        string exe = Path.Combine(dlg.SelectedPath, "Scam With Your Friends.exe");
        if (!File.Exists(exe))
        {
            MessageBox.Show(this, "That folder does not contain Scam With Your Friends.exe.", "Not a game folder",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _gameBox.Text = dlg.SelectedPath;
    }

    private async Task DetectGame()
    {
        try
        {
            string backend = BackendPath();
            var lines = await RunCaptureAsync(backend, "detect");
            string first = lines.FirstOrDefault(l => l.Trim() != "") ?? "";
            if (first == "")
            {
                MessageBox.Show(this, "No game folder detected. Use Browse… instead.", "Not found",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _gameBox.Text = first.Trim();
            Log("Detected: " + first.Trim());
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Detect failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task<List<string>> RunCaptureAsync(string exe, string args)
    {
        var lines = new List<string>();
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
        };
        p.Start();
        string output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();
        lines.AddRange(output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        return lines;
    }

    private async Task RunBackendAsync(string args, string confirm, bool _)
    {
        if (_running) return;
        if (confirm != null && MessageBox.Show(this, confirm, "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        string backend;
        try { backend = BackendPath(); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Backend missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        _running = true;
        SetButtons(false);
        Log("$ SwyfInstaller.exe " + args);
        _status.Text = "Running…";
        try
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo(backend, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(backend) ?? "",
            };
            p.OutputDataReceived += (_, e) => { if (e.Data != null) Log(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) Log(e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            await p.WaitForExitAsync();
            _status.Text = "Exit code " + p.ExitCode;
            Log("Exit code " + p.ExitCode);
            LoadSavedState();
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            _status.Text = "Failed";
        }
        finally
        {
            _running = false;
            SetButtons(true);
        }
    }

    private void SetButtons(bool enabled)
    {
        foreach (Control c in _buttons.Controls)
            if (c is Button) c.Enabled = enabled;
    }

    private void Log(string line)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action<string>(Log), line); } catch { }
            return;
        }
        _log.AppendText(line + Environment.NewLine);
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }
}
