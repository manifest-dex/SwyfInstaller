using System.Text.Json;

namespace SwyfInstaller;

public sealed class ReleaseInfo
{
    public string Tag { get; set; } = "";
    public string ZipName { get; set; } = "";
    public string ZipUrl { get; set; } = "";
    public long ZipSize { get; set; }
    public string ZipDigest { get; set; } = "";
    public string ChecksumName { get; set; } = "";
    public string ChecksumUrl { get; set; } = "";
}

internal static class Github
{
    private const string ApiBase = "https://api.github.com/repos/";

    public static ReleaseInfo ParseLatest(string repo, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tag_name", out var tagEl))
            throw new InvalidDataException("GitHub API response has no tag_name (repo may have no releases).");

        var rel = new ReleaseInfo { Tag = tagEl.GetString() ?? "" };
        if (string.IsNullOrWhiteSpace(rel.Tag))
            throw new InvalidDataException("GitHub API response has an empty tag.");

        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Release " + rel.Tag + " has no downloadable assets.");

        foreach (var a in assets.EnumerateArray())
        {
            string name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            string dl = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
            if (name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
            {
                if (rel.ChecksumUrl == "" && dl != "")
                {
                    rel.ChecksumName = name;
                    rel.ChecksumUrl = dl;
                }
                continue;
            }
            if (name.StartsWith("SWYF-Custom-AI-", StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase) && dl != "")
            {
                rel.ZipName = name;
                rel.ZipUrl = dl;
                if (a.TryGetProperty("size", out var s) && s.TryGetInt64(out long size)) rel.ZipSize = size;
                if (a.TryGetProperty("digest", out var d))
                {
                    string digest = d.GetString() ?? "";
                    if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                        rel.ZipDigest = digest.Substring(7).ToLowerInvariant();
                }
            }
        }

        if (rel.ZipUrl == "")
            throw new InvalidDataException("Release " + rel.Tag + " has no SWYF-Custom-AI-*-win-x64.zip asset.");
        if (rel.ChecksumUrl == "")
            throw new InvalidDataException("Release " + rel.Tag + " has no checksum file, so the download cannot be verified. Aborting for safety.");
        return rel;
    }

    public static async Task<ReleaseInfo> GetLatestAsync(HttpClient http, string repo, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, ApiBase + repo.Trim('/') + "/releases/latest");
        req.Headers.UserAgent.ParseAdd("SwyfInstaller/1.0");
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var res = await http.SendAsync(req, ct);
        string body = await res.Content.ReadAsStringAsync(ct);
        if ((int)res.StatusCode == 404)
            throw new InvalidDataException("No published releases found for " + repo + " (HTTP 404).");
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException("GitHub API returned HTTP " + (int)res.StatusCode + ": " + body.Trim());
        return ParseLatest(repo, body);
    }

    public static HttpClient CreateClient(TimeSpan timeout)
    {
        var http = new HttpClient { Timeout = timeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SwyfInstaller/1.0");
        return http;
    }

    public static async Task DownloadAsync(HttpClient http, string url, string destPath, long expectedSize, IProgress<double> progress, CancellationToken ct)
    {
        using var res = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException("Download returned HTTP " + (int)res.StatusCode + " for " + url);
        long? total = res.Content.Headers.ContentLength;
        if (total == null || total <= 0) total = expectedSize > 0 ? expectedSize : null;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destPath)) ?? ".");
        using var net = await res.Content.ReadAsStreamAsync(ct);
        using var file = File.Create(destPath);
        byte[] buffer = new byte[81920];
        long done = 0;
        int lastPct = -1;
        while (true)
        {
            int read = await net.ReadAsync(buffer, ct);
            if (read == 0) break;
            await file.WriteAsync(buffer.AsMemory(0, read), ct);
            done += read;
            if (total > 0)
            {
                int pct = (int)(done * 100 / total.Value);
                if (pct != lastPct && pct % 5 == 0)
                {
                    lastPct = pct;
                    try { progress?.Report(pct); } catch { }
                }
            }
        }
    }
}
