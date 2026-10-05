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

    private static ReleaseInfo TryParseRelease(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tag_name", out var tagEl))
            return null;

        var rel = new ReleaseInfo { Tag = tagEl.GetString() ?? "" };
        if (string.IsNullOrWhiteSpace(rel.Tag))
            return null;

        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

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

        if (rel.ZipUrl == "" || rel.ChecksumUrl == "")
            return null;
        return rel;
    }

    public static ReleaseInfo ParseLatest(string repo, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var rel = TryParseRelease(root);
        if (rel != null)
            return rel;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tag_name", out _))
            throw new InvalidDataException("GitHub API response has no tag_name (repo may have no releases).");
        throw new InvalidDataException("Release has no verifiable SWYF-Custom-AI-*-win-x64.zip asset (.zip + .sha256 required). Aborting for safety.");
    }

    // Newest-first scan: returns the first non-draft release carrying a
    // verifiable mod ZIP, so setup-only releases in the same repo are skipped.
    public static async Task<ReleaseInfo> GetLatestAsync(HttpClient http, string repo, CancellationToken ct)
    {
        for (int page = 1; page <= 5; page++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, ApiBase + repo.Trim('/') + "/releases?per_page=20&page=" + page);
            req.Headers.UserAgent.ParseAdd("SwyfInstaller/1.0");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var res = await http.SendAsync(req, ct);
            string body = await res.Content.ReadAsStringAsync(ct);
            if ((int)res.StatusCode == 404)
                throw new InvalidDataException("No published releases found for " + repo + " (HTTP 404).");
            if (!res.IsSuccessStatusCode)
                throw new HttpRequestException("GitHub API returned HTTP " + (int)res.StatusCode + ": " + body.Trim());
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                break;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True && draft.GetBoolean())
                    continue;
                var rel = TryParseRelease(el);
                if (rel != null)
                    return rel;
            }
        }
        throw new InvalidDataException("No published release with a verifiable SWYF-Custom-AI-*-win-x64.zip asset found for " + repo + ".");
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
