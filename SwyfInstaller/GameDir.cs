using System.Diagnostics;
using Microsoft.Win32;

namespace SwyfInstaller;

public static class GameDir
{
    public const string ExeName = "Scam With Your Friends.exe";
    public const string FolderName = "Scam With Your Friends Playtest";
    public const string ProcessName = "Scam With Your Friends";

    public static List<string> FindCandidates()
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string lib in SteamLibraries())
        {
            string dir;
            try { dir = Path.GetFullPath(Path.Combine(lib, "steamapps", "common", FolderName)); }
            catch { continue; }
            string key = dir.ToUpperInvariant();
            if (seen.Contains(key)) continue;
            if (IsGameDir(dir))
            {
                seen.Add(key);
                found.Add(dir);
            }
        }
        return found;
    }

    public static bool IsGameDir(string dir)
    {
        return !string.IsNullOrWhiteSpace(dir) &&
               Directory.Exists(dir) &&
               File.Exists(Path.Combine(dir, ExeName));
    }

    public static bool IsGameRunning()
    {
        try { return Process.GetProcessesByName(ProcessName).Length > 0; }
        catch { return false; }
    }

    public static string Resolve(Store.AppConfig cfg, bool interactive)
    {
        if (IsGameDir(cfg.GameDir)) return cfg.GameDir;

        var candidates = FindCandidates();
        if (candidates.Count == 1)
        {
            Console.WriteLine("Detected game folder: " + candidates[0]);
            cfg.GameDir = candidates[0];
            return cfg.GameDir;
        }
        if (candidates.Count > 1 && interactive)
        {
            Console.WriteLine("Multiple installs found:");
            for (int i = 0; i < candidates.Count; i++)
                Console.WriteLine("  [" + (i + 1) + "] " + candidates[i]);
            Console.Write("Pick one (number), or paste a path: ");
            string answer = (Console.ReadLine() ?? "").Trim().Trim('"');
            if (int.TryParse(answer, out int n) && n >= 1 && n <= candidates.Count)
            {
                cfg.GameDir = candidates[n - 1];
                return cfg.GameDir;
            }
            if (IsGameDir(answer))
            {
                cfg.GameDir = Path.GetFullPath(answer);
                return cfg.GameDir;
            }
            throw new InvalidOperationException("No valid game folder selected.");
        }
        if (!interactive)
            throw new InvalidOperationException("Game folder not found (missing " + ExeName + ").");
        Console.WriteLine("Could not find the game automatically.");
        Console.Write("Paste the game folder path (Steam > Properties > Installed Files > Browse): ");
        string typed = (Console.ReadLine() ?? "").Trim().Trim('"');
        if (IsGameDir(typed))
        {
            cfg.GameDir = Path.GetFullPath(typed);
            return cfg.GameDir;
        }
        throw new InvalidOperationException("That folder does not contain " + ExeName + ".");
    }

    private static List<string> SteamLibraries()
    {
        var libs = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void AddLib(string p)
        {
            try
            {
                string full = Path.GetFullPath(p);
                if (Directory.Exists(full) && seen.Add(full.ToUpperInvariant()))
                    libs.Add(full);
            }
            catch { }
        }
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            object val = key?.GetValue("SteamPath");
            if (val is string steamPath)
            {
                AddLib(steamPath);
                string vdfGuess = "";
                try { vdfGuess = Path.GetFullPath(Path.Combine(steamPath, "steamapps", "libraryfolders.vdf")); }
                catch { }
                if (vdfGuess != "" && File.Exists(vdfGuess))
                {
                    foreach (string line in File.ReadAllLines(vdfGuess))
                    {
                        string t = line.Trim();
                        if (!t.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase)) continue;
                        var parts = t.Split('"', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                            AddLib(parts[^1].Replace(@"\\", @"\"));
                    }
                }
            }
        }
        catch { }
        foreach (string drive in new[] { "C", "D", "E", "F" })
            AddLib(drive + @":\Program Files (x86)\Steam");
        return libs;
    }
}
