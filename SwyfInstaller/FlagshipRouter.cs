using System.Diagnostics;
using System.Text.Json;

namespace SwyfInstaller;

public static class FlagshipRouter
{
    public const string RepoUrl = "https://github.com/theRizwan/FlagshipRouter.git";
    public const string DefaultBaseUrl = "http://127.0.0.1:20128/v1";
    public const string DefaultModel = "groq/openai/gpt-oss-120b";
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
        // .cmd/.bat/.ps1 must go through the shell; direct CreateProcess on them fails with exit 1 and no output.
        bool viaShell = file.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);
        string realFile = viaShell ? (OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh") : file;
        string realArgs = viaShell ? (OperatingSystem.IsWindows() ? "/c \"" + file + " " + args + "\"" : "-c \"" + file + " " + args + "\"") : args;
        Say(log, "> " + (viaShell ? realFile + " " + realArgs : file + " " + args));
        var psi = new ProcessStartInfo(realFile, realArgs)
        {
            WorkingDirectory = workDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi);
        if (p == null) throw new InvalidOperationException("Could not start " + file);
        var stdout = new System.Text.StringBuilder();
        var stderr = new System.Text.StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data != null) { stdout.AppendLine(e.Data); try { log?.Report("  " + e.Data); } catch { } } };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) { stderr.AppendLine(e.Data); try { log?.Report("  " + e.Data); } catch { } } };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        try { await p.WaitForExitAsync(ct); }
        catch
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            throw;
        }
        if (p.ExitCode != 0)
        {
            string tail = stderr.ToString();
            if (tail.Length == 0) tail = stdout.ToString();
            if (tail.Length > 2000) tail = "...\n" + tail.Substring(tail.Length - 2000);
            Say(log, file + " " + args + " failed (exit " + p.ExitCode + ").");
            if (tail.Trim().Length > 0) Say(log, tail.Trim());
        }
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
        await RunAsync("node", "--version", dir, log, false, ct);
        await RunAsync(npm, "--version", dir, log, false, ct);
        int code = await RunAsync(npm, "install --no-fund --loglevel error", dir, log, false, ct);
        if (code != 0) throw new InvalidOperationException("npm install failed (exit " + code + "). Full output is in Details below — check Node 20.9+/22, free disk space, and antivirus quarantine, then retry.");
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
                max_tokens = 256,
            }), System.Text.Encoding.UTF8, "application/json");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(20));
        using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        string body = await res.Content.ReadAsStringAsync(cts.Token);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(FriendlyRouterError((int)res.StatusCode, body));
        return "Router OK (" + body.Length + " bytes).";
    }

    public static string FriendlyRouterError(int status, string body)
    {
        string snippet = body.Length > 300 ? body.Substring(0, 300) : body;
        if (body.Contains("No active credentials for provider:"))
        {
            string provider = "that provider";
            string marker = "No active credentials for provider:";
            int at = body.IndexOf(marker, StringComparison.Ordinal);
            if (at >= 0)
            {
                string rest = body.Substring(at + marker.Length).TrimStart(' ', '"');
                int end = rest.IndexOfAny(new[] { '"', '}', ',', '\n', '\r' });
                string name = (end >= 0 ? rest.Substring(0, end) : rest).Trim().Trim('"');
                if (name.Length > 0) provider = name.Length > 40 ? name.Substring(0, 40) : name;
            }
            return "Router HTTP " + status + ": no credentials for provider '" + provider + "'. Press 'Save Groq key' in this installer and paste the key"
                + " (Groq free: https://console.groq.com/keys), or open " + DashboardUrl
                + " > Models, click Connect on a '" + provider + "' model (direct: " + DashboardUrl + "/providers/" + provider + "), then press Test router.";
        }
        if (body.Contains("FreeTierError"))
            return "Router HTTP " + status + ": OpenCode's free tier only works inside OpenCode itself, not via the router. "
                + "Open " + DashboardUrl + " > Models and pick a Ready model from a connected provider (Groq free recommended), "
                + "put it in F8 > Model ID, Save Settings, then Test again.";
        if (body.Contains("Model is unavailable"))
            return "Router HTTP " + status + ": that model is not served upstream right now. Open " + DashboardUrl
                + " > Models, pick a model marked Ready, put it in F8 > Model ID, Save Settings, then Test again.";
        if (status == 401 || status == 403)
            return "Router HTTP " + status + ": bad or missing router key. Create one in " + DashboardUrl
                + " > Endpoint & Key, paste it into F8 > API key, Save Settings, then Test again. (" + snippet + ")";
        return "Router returned HTTP " + status + ": " + snippet;
    }

    public sealed class RouterSession : IDisposable
    {
        public HttpClient Http { get; }
        private readonly HttpClientHandler handler;
        public RouterSession()
        {
            handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer(), UseCookies = true };
            Http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        }
        public void Dispose() { Http.Dispose(); handler.Dispose(); }
    }

    public static async Task<RouterSession> LoginRouterAsync(string dashboardPassword, IProgress<string> log = null, CancellationToken ct = default)
    {
        var session = new RouterSession();
        try
        {
            using var res = await session.Http.PostAsync("http://127.0.0.1:20128/api/auth/login",
                new StringContent(JsonSerializer.Serialize(new { password = dashboardPassword ?? "" }),
                    System.Text.Encoding.UTF8, "application/json"), ct);
            string body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode || !body.Contains("\"success\":true"))
                throw new InvalidOperationException("Dashboard login failed. If you changed the dashboard password from 123456, open "
                    + DashboardUrl + " > Models, click Connect on a Groq model, and paste the key there instead.");
            Say(log, "Dashboard login OK.");
            return session;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            session.Dispose();
            throw new InvalidOperationException("Could not reach the dashboard login: " + ex.Message);
        }
    }

    public static async Task ValidateGroqKeyAsync(RouterSession session, string groqKey, IProgress<string> log = null, CancellationToken ct = default)
    {
        using var res = await session.Http.PostAsync("http://127.0.0.1:20128/api/providers/validate",
            new StringContent(JsonSerializer.Serialize(new { provider = "groq", apiKey = groqKey }),
                System.Text.Encoding.UTF8, "application/json"), ct);
        string body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode || !body.Contains("\"valid\":true"))
            throw new InvalidOperationException("Groq rejected the key (" + (int)res.StatusCode + "). Check it at https://console.groq.com/keys and retry. (" + (body.Length > 200 ? body.Substring(0, 200) : body) + ")");
        Say(log, "Groq key validated.");
    }

    public static async Task EnsureGroqConnectionAsync(RouterSession session, string groqKey, IProgress<string> log = null, CancellationToken ct = default)
    {
        using var res = await session.Http.PostAsync("http://127.0.0.1:20128/api/providers",
            new StringContent(JsonSerializer.Serialize(new
            {
                provider = "groq",
                name = "Groq",
                apiKey = groqKey,
                priority = 1,
                testStatus = "active",
                allowOverwrite = true,
            }), System.Text.Encoding.UTF8, "application/json"), ct);
        string body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException("Could not save the Groq connection (HTTP " + (int)res.StatusCode + "). Open "
                + DashboardUrl + "/providers/groq and paste the key there instead. (" + (body.Length > 200 ? body.Substring(0, 200) : body) + ")");
        Say(log, "Groq connection saved in FlagshipRouter.");
    }

    public static async Task<int> SaveGroqKeyFlowAsync(string gameDir, string groqKey, string dashboardPassword, string routerKey, IProgress<string> log = null, CancellationToken ct = default)
    {
        if (!GameDir.IsGameDir(gameDir)) throw new InvalidOperationException("Pick a valid game folder first.");
        if (string.IsNullOrWhiteSpace(groqKey)) throw new InvalidOperationException("Paste a Groq key first.");
        if (!await IsRunningAsync(ct))
            throw new InvalidOperationException("FlagshipRouter is not running. Press Setup FlagshipRouter first.");
        using var session = await LoginRouterAsync(dashboardPassword ?? "123456", log, ct);
        await ValidateGroqKeyAsync(session, groqKey.Trim(), log, ct);
        await EnsureGroqConnectionAsync(session, groqKey.Trim(), log, ct);
        EnsureModSettings(gameDir, routerKey, log);
        string key = routerKey;
        if (string.IsNullOrEmpty(key))
        {
            try
            {
                string path = Path.Combine(gameDir, "CustomAI", "settings.json");
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("apiKey", out var k) && k.ValueKind == JsonValueKind.String)
                    key = k.GetString() ?? "";
            }
            catch { }
        }
        Say(log, await TestRouterAsync(string.IsNullOrEmpty(key) ? null : key, ct));
        Say(log, "Done: Groq connected, mod pointed at " + DefaultModel + ". F8 > Test Connection, then Save Settings.");
        return 0;
    }

    public static async Task DisableRouterLoginAsync(RouterSession session, IProgress<string> log = null, CancellationToken ct = default)
    {
        using var res = await session.Http.SendAsync(new HttpRequestMessage(new HttpMethod("PATCH"), "http://127.0.0.1:20128/api/settings")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { requireLogin = false }), System.Text.Encoding.UTF8, "application/json"),
        }, ct);
        string body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode || !body.Contains("\"requireLogin\":false"))
            throw new InvalidOperationException("Could not disable the dashboard login (HTTP " + (int)res.StatusCode + "). Change it in the dashboard under Settings instead.");
        Say(log, "Dashboard login disabled — the router UI now opens without a password (local only). Re-enable it under Settings any time.");
    }

    public static async Task<int> RemoveRouterPasswordFlowAsync(string dashboardPassword, IProgress<string> log = null, CancellationToken ct = default)
    {
        if (!await IsRunningAsync(ct))
            throw new InvalidOperationException("FlagshipRouter is not running. Press Setup FlagshipRouter first.");
        using var session = await LoginRouterAsync(dashboardPassword ?? "123456", log, ct);
        await DisableRouterLoginAsync(session, log, ct);
        return 0;
    }

    public static void ApplyRecommendedModSettings(string gameDir, IProgress<string> log = null)
    {
        if (!GameDir.IsGameDir(gameDir)) throw new InvalidOperationException("Pick a valid game folder first.");
        string path = Path.Combine(gameDir, "CustomAI", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string key = "";
        bool useMdx = false;
        double? temp = null, topP = null;
        int? maxTokens = null;
        string outputMode = "schema";
        bool hadFile = File.Exists(path);
        try
        {
            if (hadFile)
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;
                if (root.TryGetProperty("apiKey", out var k) && k.ValueKind == JsonValueKind.String) key = k.GetString() ?? "";
                if (root.TryGetProperty("useManifestDeX", out var m) && m.ValueKind is JsonValueKind.True or JsonValueKind.False) useMdx = m.GetBoolean();
                if (root.TryGetProperty("temperature", out var t) && t.ValueKind == JsonValueKind.Number) temp = t.GetDouble();
                if (root.TryGetProperty("topP", out var p) && p.ValueKind == JsonValueKind.Number) topP = p.GetDouble();
                if (root.TryGetProperty("maxTokens", out var mt) && mt.ValueKind == JsonValueKind.Number) maxTokens = mt.GetInt32();
                if (root.TryGetProperty("outputMode", out var o) && o.ValueKind == JsonValueKind.String && (o.GetString() is "schema" or "json")) outputMode = o.GetString()!;
            }
        }
        catch { }
        if (useMdx)
        {
            Say(log, "Mod stays on ManifestDeX AI — private provider fields untouched.");
            return;
        }
        var settings = new
        {
            enabled = true,
            baseUrl = DefaultBaseUrl,
            model = DefaultModel,
            apiKey = key,
            temperature = temp,
            topP,
            maxTokens,
            outputMode,
            useManifestDeX = false,
        };
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tempPath, path, true);
        Say(log, (hadFile ? "Mod settings updated to recommended defaults (" : "Mod settings created with recommended defaults (") + DefaultBaseUrl + " / " + DefaultModel + ", key " + (key.Length > 0 ? "kept" : "none") + "). F8 > Test Connection, then Save Settings.");
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
        Say(log, "Next: connect a provider — Groq free is fastest for the game's 15s deadline: create a key at https://console.groq.com/keys, "
            + "open " + DashboardUrl + " > Models, click Connect on a Groq model (or " + DashboardUrl + "/providers/groq directly), paste it,"
            + " then open Models and confirm a Groq model is Ready. Then press Test router.");
        return 0;
    }
}
