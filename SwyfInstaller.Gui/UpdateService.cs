using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace SwyfInstaller.Gui;

internal sealed record SelfUpdateInfo(
    string Version,
    string Name,
    string Notes,
    string GuiName,
    string GuiUrl,
    long GuiSize,
    string GuiDigest,
    string BackName,
    string BackUrl,
    long BackSize,
    string BackDigest);

internal sealed class SelfUpdater
{
    public const string Owner = "manifest-dex";
    public const string Repo = "SwyfInstaller";

    private static readonly HttpClient _api = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly HttpClient _dl = new() { Timeout = Timeout.InfiniteTimeSpan };

    static SelfUpdater()
    {
        _api.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SwyfInstallerGui", "1.0"));
        _api.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _dl.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SwyfInstallerGui", "1.0"));
    }

    public static Version CurrentVersion
    {
        get
        {
            var v = Assembly.GetEntryAssembly()?.GetName().Version;
            return Normalize(v ?? new Version(1, 0, 0));
        }
    }

    internal static Version Normalize(Version v) =>
        new(Math.Max(v.Major, 0), Math.Max(v.Minor, 0), Math.Max(v.Build, 0));

    internal static bool TryParseTag(string tag, out Version version)
    {
        version = new Version(1, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;
        string t = tag.Trim().TrimStart('v', 'V').Split('-', '+')[0];
        if (Version.TryParse(t, out var v))
        {
            version = Normalize(v);
            return true;
        }
        return false;
    }

    public async Task<SelfUpdateInfo> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        using var res = await _api.GetAsync(
            $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest", ct);
        if (!res.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
        if (!root.TryGetProperty("tag_name", out var tagEl)) return null;
        if (!TryParseTag(tagEl.GetString() ?? "", out var latest)) return null;
        if (latest <= CurrentVersion) return null;
        if (!root.TryGetProperty("assets", out var assets)) return null;

        string guiName = "", guiUrl = "", guiDigest = "";
        long guiSize = 0;
        string backName = "", backUrl = "", backDigest = "";
        long backSize = 0;
        foreach (var a in assets.EnumerateArray())
        {
            string name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            string url = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
            if (url == "" || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
            long size = a.TryGetProperty("size", out var s) && s.TryGetInt64(out var v) ? v : 0;
            string digest = "";
            if (a.TryGetProperty("digest", out var d))
            {
                string raw = d.GetString() ?? "";
                if (raw.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    digest = raw.Substring(7).ToLowerInvariant();
            }
            if (name.StartsWith("SwyfInstallerGui-", StringComparison.OrdinalIgnoreCase))
            {
                guiName = name;
                guiUrl = url;
                guiSize = size;
                guiDigest = digest;
            }
            else if (name.StartsWith("SwyfInstaller-", StringComparison.OrdinalIgnoreCase))
            {
                backName = name;
                backUrl = url;
                backSize = size;
                backDigest = digest;
            }
        }
        if (guiUrl == "" || backUrl == "") return null;

        string relName = root.TryGetProperty("name", out var rn) ? rn.GetString() ?? "" : "";
        string notes = root.TryGetProperty("body", out var rb) ? rb.GetString() ?? "" : "";
        return new SelfUpdateInfo(latest.ToString(), relName, notes,
            guiName, guiUrl, guiSize, guiDigest, backName, backUrl, backSize, backDigest);
    }

    private static async Task DownloadFileAsync(string url, string dest, long size, double fromFrac, double toFrac,
        IProgress<double> progress, CancellationToken ct)
    {
        using var res = await _dl.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();
        long total = res.Content.Headers.ContentLength ?? size;
        await using var net = await res.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(dest);
        byte[] buf = new byte[81920];
        long read = 0;
        int n;
        while ((n = await net.ReadAsync(buf, ct)) > 0)
        {
            await file.WriteAsync(buf.AsMemory(0, n), ct);
            read += n;
            if (total > 0 && progress != null)
                progress.Report(fromFrac + (toFrac - fromFrac) * read / total);
        }
    }

    private static string ParseChecksum(string text)
    {
        foreach (string rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            string token = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0].Trim('*', ' ', '\t');
            if (token.Length == 64)
            {
                bool hex = true;
                foreach (char c in token)
                {
                    if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) { hex = false; break; }
                }
                if (hex) return token.ToLowerInvariant();
            }
        }
        throw new InvalidDataException("No SHA-256 hash found in checksum file.");
    }

    private static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static async Task<string> DownloadTextAsync(string url, CancellationToken ct)
    {
        using var res = await _dl.GetAsync(url, ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync(ct);
    }

    public async Task<string> DownloadAsync(SelfUpdateInfo update, IProgress<double> progress, CancellationToken ct = default)
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SwyfInstallerGui", "updates");
        Directory.CreateDirectory(dir);
        foreach (string f in Directory.GetFiles(dir)) File.Delete(f);

        string guiPath = Path.Combine(dir, update.GuiName);
        string backPath = Path.Combine(dir, update.BackName);
        await DownloadFileAsync(update.GuiUrl, guiPath, update.GuiSize, 0.0, 0.45, progress, ct);
        await DownloadFileAsync(update.BackUrl, backPath, update.BackSize, 0.45, 0.9, progress, ct);

        string guiSum = ParseChecksum(await DownloadTextAsync(update.GuiUrl + ".sha256", ct));
        string backSum = ParseChecksum(await DownloadTextAsync(update.BackUrl + ".sha256", ct));
        if (!string.Equals(Sha256File(guiPath), guiSum, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Download check failed for " + update.GuiName + ". Deleted nothing.");
        if (!string.Equals(Sha256File(backPath), backSum, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Download check failed for " + update.BackName + ". Deleted nothing.");
        if (update.GuiDigest != "" && !string.Equals(Sha256File(guiPath), update.GuiDigest, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Download does not match the release checksum for " + update.GuiName + ".");
        if (update.BackDigest != "" && !string.Equals(Sha256File(backPath), update.BackDigest, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Download does not match the release checksum for " + update.BackName + ".");
        progress?.Report(1.0);
        return dir;
    }

    public static void InstallAndRestart(string dir, string guiName, string backName)
    {
        string appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string installedGui = Path.GetFileName(Environment.ProcessPath ?? "");
        if (installedGui == "") installedGui = "SwyfInstallerGui.exe";
        const string installedBack = "SwyfInstaller.exe";
        int pid = Environment.ProcessId;
        string script = Path.Combine(dir, "update.cmd");

        string bat =
            "@echo off\r\n" +
            $"set \"UPD={dir}\"\r\n" +
            $"set \"APPDIR={appDir}\"\r\n" +
            $":wait\r\ntasklist /FI \"PID eq {pid}\" 2>NUL | find \"{pid}\" >NUL\r\n" +
            "if %errorlevel%==0 ( timeout /t 1 /nobreak >NUL & goto wait )\r\n" +
            $"copy /y \"%UPD%\\{guiName}\" \"%APPDIR%\\{installedGui}\" >NUL\r\n" +
            $"copy /y \"%UPD%\\{backName}\" \"%APPDIR%\\{installedBack}\" >NUL\r\n" +
            $"start \"\" \"%APPDIR%\\{installedGui}\"\r\n" +
            "rd /s /q \"%UPD%\"\r\n" +
            "(goto) 2>nul & del \"%~f0\"\r\n";
        File.WriteAllText(script, bat);

        Process.Start(new ProcessStartInfo
        {
            FileName = script,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = appDir,
        });
    }
}
