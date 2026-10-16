using System.Diagnostics;
using System.Text.Json;

namespace SwyfInstaller;

public static class FlagshipRouter
{
    public const string RepoUrl = "https://github.com/theRizwan/FlagshipRouter.git";
    public const string DefaultBaseUrl = "http://127.0.0.1:20128/v1";
    public const string DefaultModel = "gpt-6-astra";
    public const int DefaultPort = 20128;
    public const string HealthUrl = "http://127.0.0.1:20128/api/health";
    public const string DashboardUrl = "http://localhost:20128/dashboard";
    public const string ModelsUrl = "http://localhost:20128/v1/models";

    public static string InstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlagshipRouter");

    public static async Task<bool> IsRunningAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var res = await http.GetAsync(HealthUrl, ct);
            if (!res.IsSuccessStatusCode) return false;
            var body = await res.Content.ReadAsStringAsync(ct);
            return body.Contains("\"ok\"");
        }
        catch { return false; }
    }

    public static async Task<bool> WaitForReadyAsync(TimeSpan timeout, IProgress<string> log = null, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (await IsRunningAsync(ct)) return true;
            await Task.Delay(1500, ct);
        }
        return await IsRunningAsync(ct);
    }

    private static void Say(IProgress<string> log, string msg)
    {
        Console.WriteLine(msg);
        try { log?.Report(msg); } catch { }
    }

    private static async Task<int> RunAsync(string file, string args, string workDir, IProgress<string> log, bool visibleWindow, CancellationToken ct)
    {
        Say(log, "> " + file + " " + args);
        var psi = new ProcessStartInfo(file, args)
        {
            WorkingDirectory = workDir,
            UseShellExecute = visibleWindow,
            CreateNoWindow = !visibleWindow,
        };
        using var p = Process.Start(psi);
        if (p == null) throw new InvalidOperationException("Could not start " + file);
        if (visibleWindow)
        {
            await p.WaitForExitAsync(ct);
            return p.ExitCode;
        }
        await p.WaitForExitAsync(ct);
        return p.ExitCode;
    }

    public static async Task CheckPrereqsAsync(IProgress<string> log = null, CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo("node", "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) throw new InvalidOperationException("Could not start node.");
            string version = (await p.StandardOutput.ReadToEndAsync(ct)).Trim().TrimStart('v');
            await p.WaitForExitAsync(ct);
            if (p.ExitCode != 0) throw new InvalidOperationException("node is not installed.");
            var parts = version.Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out var mj) ? mj : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out var mn) ? mn : 0;
            if (major < 20 || (major == 20 && minor < 9))
                throw new InvalidOperationException("Node.js " + version + " found, but FlagshipRouter needs Node 20.9+ (22 recommended).");
            Say(log, "Node.js v" + version + " OK.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Node.js 20.9+ is required for FlagshipRouter. Install it from https://nodejs.org then retry. (" + ex.Message + ")");
        }
        try
        {
            var psi = new ProcessStartInfo("git", "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) throw new InvalidOperationException("Could not start git.");
            await p.WaitForExitAsync(ct);
            if (p.ExitCode != 0) throw new InvalidOperationException("git is not installed.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("git is required to fetch FlagshipRouter. Install it from https://git-scm.com then retry. (" + ex.Message + ")");
        }
    }

    public static async Task EnsureSourceAsync(IProgress<string> log = null, CancellationToken ct = default)
    {
        string dir = InstallDir;
        if (!Directory.Exists(Path.Combine(dir, ".git")) || !File.Exists(Path.Combine(dir, "package.json")))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dir)!);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            Say(log, "Cloning FlagshipRouter to " + dir + " ...");
            int code = await RunAsync("git", "clone --depth 1 " + RepoUrl + " \"" + dir + "\"", Path.GetTempPath(), log, false, ct);
            if (code != 0) throw new InvalidOperationException("git clone failed (exit " + code + ").");
            return;
        }
        Say(log, "FlagshipRouter source found at " + dir + ", updating ...");
        try { await RunAsync("git", "pull --ff-only", dir, log, false, ct); }
        catch (Exception ex) { Say(log, "git pull skipped: " + ex.Message); }
    }

    public static async Task EnsureDepsAsync(IProgress<string> log = null, CancellationToken ct = default)
    {
        string dir = InstallDir;
        if (File.Exists(Path.Combine(dir, "node_modules", ".package-lock.json")) ||
            Directory.Exists(Path.Combine(dir, "node_modules")))
        {
            Say(log, "FlagshipRouter dependencies look present, skipping npm install.");
            return;
        }
        string npm = OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
        Say(log, "Installing FlagshipRouter dependencies (first run takes a few minutes) ...");
        int code = await RunAsync(npm, "install", dir, log, false, ct);
        if (code != 0) throw new InvalidOperationException("npm install failed (exit " + code + ").");
    }

    public static void StartDetached(IProgress<string> log = null)
    {
        string dir = InstallDir;
        string npm = OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
        Say(log, "Starting FlagshipRouter on port " + DefaultPort + " (dashboard opens in your browser) ...");
        var psi = new ProcessStartInfo(npm, "run launch -- --host 127.0.0.1")
        {
            WorkingDirectory = dir,
            UseShellExecute = true,
        };
        Process.Start(psi);
        Say(log, "FlagshipRouter terminal opened. Keep it running, then press Test router here.");
        Say(log, "Dashboard: " + DashboardUrl + " (default password 123456, change under Settings).");
    }

    public static void EnsureModSettings(string gameDir, string apiKey = null, IProgress<string> log = null)
    {
        if (!GameDir.IsGameDir(gameDir)) throw new InvalidOperationException("Game folder is not set.");
        string path = Path.Combine(gameDir, "CustomAI", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string existingKey = "";
        bool hadFile = File.Exists(path);
        try
        {
            if (hadFile)
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("apiKey", out var k) && k.ValueKind == JsonValueKind.String)
                    existingKey = k.GetString() ?? "";
            }
        }
        catch { }
        string key = apiKey ?? existingKey ?? "";
        var settings = new
        {
            enabled = true,
            baseUrl = DefaultBaseUrl,
            model = DefaultModel,
            apiKey = key,
            temperature = (double?)null,
            topP = (double?)null,
            maxTokens = (int?)null,
            outputMode = "schema",
            useManifestDeX = false,
        };
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
        Say(log, hadFile ? "Mod settings updated to FlagshipRouter (key preserved: " + (key.Length > 0 ? "yes" : "none") + ")."
                         : "Mod settings created for FlagshipRouter (auto-enabled).");
    }

    public static async Task<string> TestRouterAsync(string apiKey = null, CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var req = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:20128/v1/chat/completions");
        if (!string.IsNullOrEmpty(apiKey)) req.Headers.Authorization = new("Bearer", apiKey);
        req.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                model = DefaultModel,
                messages = new[] { new { role = "user", content = "Reply with one short greeting." } },
                max_tokens = 32,
            }), System.Text.Encoding.UTF8, "application/json");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(20));
        using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        string body = await res.Content.ReadAsStringAsync(cts.Token);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException("Router returned HTTP " + (int)res.StatusCode + ": " + (body.Length > 300 ? body.Substring(0, 300) : body));
        return "Router OK (" + body.Length + " bytes).";
    }

    public static async Task<int> SetupFlowAsync(string gameDir, bool startServer, IProgress<string> log = null, IProgress<double> progress = null, CancellationToken ct = default)
    {
        if (!GameDir.IsGameDir(gameDir)) throw new InvalidOperationException("Pick a valid game folder first.");
        await CheckPrereqsAsync(log, ct);
        await EnsureSourceAsync(log, ct);
        await EnsureDepsAsync(log, ct);
        if (await IsRunningAsync(ct))
        {
            Say(log, "FlagshipRouter is already running on port " + DefaultPort + ".");
        }
        else if (startServer)
        {
            StartDetached(log);
            Say(log, "Waiting for the router to answer " + HealthUrl + " ...");
            if (!await WaitForReadyAsync(TimeSpan.FromMinutes(3), log, ct))
                throw new InvalidOperationException("Router did not start in time. Check the FlagshipRouter terminal, then press Test router.");
        }
        else
        {
            Say(log, "Source ready at " + InstallDir + ". Start it with: npm run launch -- --host 127.0.0.1");
        }
        EnsureModSettings(gameDir, null, log);
        Say(log, "Next: open " + DashboardUrl + " > Endpoint & Key (create a key if required) > Models (pick a Ready model). Then press Test router.");
        return 0;
    }
}
