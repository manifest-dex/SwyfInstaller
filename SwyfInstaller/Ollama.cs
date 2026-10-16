using System.Text.Json;

namespace SwyfInstaller;

// Keyless path: local Ollama, no accounts, no API keys. The mod talks to
// Ollama directly (http://127.0.0.1:11434/v1), the router is not involved.
public static class OllamaLocal
{
    public const string BaseUrl = "http://127.0.0.1:11434/v1";
    public const string TagsUrl = "http://127.0.0.1:11434/api/tags";
    public const string SuggestedModel = "llama3.1";

    private static void Say(IProgress<string> log, string msg)
    {
        Console.WriteLine(msg);
        try { log?.Report(msg); } catch { }
    }

    public static async Task<bool> IsRunningAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var res = await http.GetAsync(TagsUrl, ct);
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public static async Task<List<string>> ListModelsAsync(CancellationToken ct = default)
    {
        var names = new List<string>();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            string json = await http.GetStringAsync(TagsUrl, ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("models", out var models))
                foreach (var m in models.EnumerateArray())
                    if (m.TryGetProperty("name", out var n) && n.GetString() is string name && name.Length > 0)
                        names.Add(name);
        }
        catch { }
        return names;
    }

    public static void EnsureModSettings(string gameDir, string model, IProgress<string> log = null)
    {
        if (!GameDir.IsGameDir(gameDir)) throw new InvalidOperationException("Pick a valid game folder first.");
        string path = Path.Combine(gameDir, "CustomAI", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var settings = new
        {
            enabled = true,
            baseUrl = BaseUrl,
            model,
            apiKey = "",
            temperature = (double?)null,
            topP = (double?)null,
            maxTokens = (int?)null,
            outputMode = "schema",
            useManifestDeX = false,
        };
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
        Say(log, "Mod settings point at local Ollama (keyless): " + BaseUrl + " / " + model + ".");
    }

    public static async Task<int> SetupFlowAsync(string gameDir, IProgress<string> log = null, CancellationToken ct = default)
    {
        if (!GameDir.IsGameDir(gameDir)) throw new InvalidOperationException("Pick a valid game folder first.");
        if (!await IsRunningAsync(ct))
        {
            Say(log, "Ollama is not running on 127.0.0.1:11434.");
            Say(log, "Keyless setup, no account needed:");
            Say(log, "  1. Install from https://ollama.com/download and start Ollama.");
            Say(log, "  2. Pull a fast model that supports structured output:  ollama pull " + SuggestedModel);
            Say(log, "     (bigger option on a strong PC: ollama pull qwen2.5:32b)");
            Say(log, "  3. Preload it once so the first game call beats the 15s deadline:  ollama run " + SuggestedModel + " \"hi\"");
            Say(log, "  4. Press this button again — the mod will point itself at your pulled model.");
            return 1;
        }
        var models = await ListModelsAsync(ct);
        if (models.Count == 0)
        {
            Say(log, "Ollama is running but no models are pulled.");
            Say(log, "Run:  ollama pull " + SuggestedModel + "   (then press this button again).");
            Say(log, "Preload before playing:  ollama run " + SuggestedModel + " \"hi\"");
            return 1;
        }
        string pick = models[0];
        Say(log, "Ollama models: " + string.Join(", ", models));
        EnsureModSettings(gameDir, pick, log);
        Say(log, "Keep Ollama running while you play. Verify with F8 > Test Connection, then Save Settings.");
        return 0;
    }
}
