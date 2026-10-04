using System.Diagnostics;
using System.IO.Compression;

namespace SwyfInstaller;

internal static class Ops
{
    private static readonly string[] PackageCmds =
    {
        "Install Custom AI.cmd",
        "Play with Custom AI.cmd",
        "Uninstall Custom AI.cmd",
    };

    private static readonly string[] PackagePaths =
    {
        "Install Custom AI.cmd",
        "Play with Custom AI.cmd",
        "Uninstall Custom AI.cmd",
        "CustomAI/README.md",
        "CustomAI/package",
    };

    public static void WarnIfGameRunning()
    {
        if (GameDir.IsGameRunning())
            Console.WriteLine("WARNING: the game appears to be running. Close it (and the F8 panel) before continuing.");
    }

    public static async Task<ReleaseInfo> FetchLatestAsync(string repo, CancellationToken ct)
    {
        using var http = Github.CreateClient(TimeSpan.FromSeconds(30));
        return await Github.GetLatestAsync(http, repo, ct);
    }

    public static async Task<string> DownloadAndVerifyAsync(ReleaseInfo rel, string workDir, CancellationToken ct)
    {
        Directory.CreateDirectory(workDir);
        string zipPath = Path.Combine(workDir, rel.ZipName);
        string shaPath = Path.Combine(workDir, rel.ChecksumName);

        using var http = Github.CreateClient(TimeSpan.FromMinutes(10));
        Console.WriteLine("Downloading " + rel.ZipName + " ...");
        await Github.DownloadAsync(http, rel.ZipUrl, zipPath, rel.ZipSize, ct);
        Console.WriteLine("Downloading " + rel.ChecksumName + " ...");
        await Github.DownloadAsync(http, rel.ChecksumUrl, shaPath, 0, ct);

        string expected = Store.ParseChecksumFile(await File.ReadAllTextAsync(shaPath, ct));
        string actual = Store.Sha256File(zipPath);
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Download check failed: the file does not match the published checksum. Deleted nothing, aborting.");
        if (rel.ZipDigest != "" && !string.Equals(rel.ZipDigest, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Download check failed: the file does not match GitHub's checksum. Aborting.");
        Console.WriteLine("Download verified.");
        return zipPath;
    }

    public static List<string> ExtractZip(string zipPath, string gameDir)
    {
        var relPaths = new List<string>();
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/")) continue;
            string rel = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            if (rel.Contains("..")) throw new InvalidDataException("ZIP contains unsafe path: " + entry.FullName);
            string dest = Path.Combine(gameDir, rel);
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(dest);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? gameDir);
            entry.ExtractToFile(dest, overwrite: true);
            relPaths.Add(rel);
        }
        return relPaths;
    }

    public static Store.Manifest RecordManifest(string repo, string tag, string gameDir, List<string> relPaths)
    {
        var m = new Store.Manifest { Tag = tag, Repo = repo };
        foreach (string rel in relPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            string full = Path.Combine(gameDir, rel);
            if (File.Exists(full))
                m.Files[rel] = Store.Sha256File(full);
        }
        Store.SaveManifest(m);
        return m;
    }

    public static int VerifyAgainstManifest(string gameDir, Store.Manifest m)
    {
        int bad = 0;
        if (m.Files.Count == 0)
        {
            Console.WriteLine("Nothing recorded yet. Run install or update first.");
            return 1;
        }
        foreach (var kv in m.Files.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            string full = Path.Combine(gameDir, kv.Key);
            if (!File.Exists(full))
            {
                Console.WriteLine("MISSING  " + kv.Key);
                bad++;
            }
            else if (!string.Equals(Store.Sha256File(full), kv.Value, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("CHANGED  " + kv.Key);
                bad++;
            }
        }
        if (bad == 0)
            Console.WriteLine("OK: all " + m.Files.Count + " files intact (" + m.Tag + ").");
        else
            Console.WriteLine(bad + " problem(s) out of " + m.Files.Count + " files (" + m.Tag + "). Reinstall or update to repair.");
        return bad == 0 ? 0 : 2;
    }

    public static void RemovePackageFiles(string gameDir)
    {
        foreach (string rel in PackagePaths)
        {
            string full = Path.Combine(gameDir, rel);
            try
            {
                if (File.Exists(full)) File.Delete(full);
                else if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not remove " + rel + ": " + ex.Message);
            }
        }
    }

    public static int RunPackageScript(string gameDir, string cmdFile)
    {
        string full = Path.Combine(gameDir, cmdFile);
        if (!File.Exists(full))
        {
            Console.WriteLine("Script not found: " + cmdFile);
            return 1;
        }
        Console.WriteLine("Running " + cmdFile + " ...");
        var psi = new ProcessStartInfo("cmd.exe", "/c \"" + cmdFile + "\"")
        {
            WorkingDirectory = gameDir,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi);
        if (p == null) return 1;
        p.WaitForExit();
        Console.WriteLine("Exit code: " + p.ExitCode);
        return p.ExitCode;
    }

    public static bool Confirm(string question, bool autoYes)
    {
        if (autoYes) return true;
        Console.Write(question + " [y/N] ");
        string ans = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
        return ans == "y" || ans == "yes";
    }

    public static async Task<int> InstallFlowAsync(Store.AppConfig cfg, string workDir, bool autoYes, bool applyPatch, CancellationToken ct)
    {
        WarnIfGameRunning();
        Console.WriteLine("Checking latest release of " + cfg.Repo + " ...");
        ReleaseInfo rel = await FetchLatestAsync(cfg.Repo, ct);
        Console.WriteLine("Latest: " + rel.Tag + " (" + rel.ZipName + ")");
        string zip = await DownloadAndVerifyAsync(rel, workDir, ct);
        Console.WriteLine("Extracting into game folder ...");
        var files = ExtractZip(zip, cfg.GameDir);
        RecordManifest(cfg.Repo, rel.Tag, cfg.GameDir, files);
        cfg.InstalledTag = rel.Tag;
        Store.SaveConfig(cfg);
        Console.WriteLine("Installed " + files.Count + " files (" + rel.Tag + "). Settings and backups untouched.");
        if (applyPatch)
        {
            if (!Confirm("Run Install Custom AI.cmd now to apply the patch?", autoYes)) return 0;
            return RunPackageScript(cfg.GameDir, "Install Custom AI.cmd");
        }
        Console.WriteLine("Skipped patch step (--no-apply). Run Install Custom AI.cmd yourself before playing.");
        return 0;
    }

    public static async Task<int> UpdateFlowAsync(Store.AppConfig cfg, string workDir, bool autoYes, bool applyPatch, bool force, CancellationToken ct)
    {
        WarnIfGameRunning();
        Console.WriteLine("Checking latest release of " + cfg.Repo + " ...");
        ReleaseInfo rel = await FetchLatestAsync(cfg.Repo, ct);
        Console.WriteLine("Latest: " + rel.Tag + ". Installed: " + (cfg.InstalledTag == "" ? "(unknown)" : cfg.InstalledTag));
        if (!force && cfg.InstalledTag != "" && string.Equals(cfg.InstalledTag, rel.Tag, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Already up to date. Use --force to reinstall the same version.");
            return 0;
        }
        if (!Confirm("Update to " + rel.Tag + "? Old package files are removed first (settings and backups are kept).", autoYes)) return 0;
        string zip = await DownloadAndVerifyAsync(rel, workDir, ct);
        Console.WriteLine("Removing old package files (settings and backups are kept) ...");
        RemovePackageFiles(cfg.GameDir);
        Console.WriteLine("Extracting " + rel.Tag + " ...");
        var files = ExtractZip(zip, cfg.GameDir);
        RecordManifest(cfg.Repo, rel.Tag, cfg.GameDir, files);
        cfg.InstalledTag = rel.Tag;
        Store.SaveConfig(cfg);
        Console.WriteLine("Updated to " + rel.Tag + " (" + files.Count + " files).");
        if (applyPatch)
        {
            if (!Confirm("Run Install Custom AI.cmd now to re-apply the patch?", autoYes)) return 0;
            return RunPackageScript(cfg.GameDir, "Install Custom AI.cmd");
        }
        Console.WriteLine("Skipped patch step (--no-apply). Run Install Custom AI.cmd yourself before playing.");
        return 0;
    }

    public static int VerifyFlow(Store.AppConfig cfg)
    {
        if (!GameDir.IsGameDir(cfg.GameDir))
        {
            Console.WriteLine("Game folder is not set. Run with --gamedir or pick Settings in the menu.");
            return 1;
        }
        return VerifyAgainstManifest(cfg.GameDir, Store.LoadManifest());
    }

    public static int UninstallFlow(Store.AppConfig cfg, bool autoYes, bool full)
    {
        WarnIfGameRunning();
        int code = RunPackageScript(cfg.GameDir, "Uninstall Custom AI.cmd");
        if (code != 0 && !Confirm("Uninstaller exited with code " + code + ". Remove leftover package files anyway?", autoYes)) return code;
        if (full)
        {
            if (!Confirm("Full removal: delete the whole CustomAI folder INCLUDING settings, session and backups?", autoYes)) return 0;
            try { Directory.Delete(Path.Combine(cfg.GameDir, "CustomAI"), recursive: true); }
            catch (Exception ex) { Console.WriteLine("Could not remove CustomAI folder: " + ex.Message); return 1; }
        }
        else
        {
            RemovePackageFiles(cfg.GameDir);
        }
        foreach (string cmd in PackageCmds)
        {
            try
            {
                string f = Path.Combine(cfg.GameDir, cmd);
                if (File.Exists(f)) File.Delete(f);
            }
            catch { }
        }
        cfg.InstalledTag = "";
        Store.SaveConfig(cfg);
        try { if (File.Exists(Store.ManifestPath)) File.Delete(Store.ManifestPath); } catch { }
        Console.WriteLine(full ? "Full uninstall done." : "Uninstall done. Settings and backups were kept.");
        return 0;
    }
}
