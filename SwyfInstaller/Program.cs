namespace SwyfInstaller;

internal static class Program
{
    private const string Usage = """
        SwyfInstaller 1.0.0 - console installer for SWYF Custom AI
          https://github.com/manifest-dex/swyf-custom-ai-mod

        Usage:
          SwyfInstaller [command] [options]

        Commands:
          (none)       interactive menu
          install      download latest release, verify download, extract, patch
          update       remove old package files, download latest, verify, re-patch
          verify       check installed files for changes or missing files
          uninstall    run the package uninstaller, remove leftover package files
          latest       show the latest published release and its assets
          gamedir      detect or set the game folder
          detect       print detected game folders, one per line (no prompts)
          selftest     run built-in checks (no network, no game needed)
          help         show this text

        Options:
          --gamedir PATH   game folder (contains "Scam With Your Friends.exe")
          --repo OWNER/REPO override source repo (default manifest-dex/swyf-custom-ai-mod)
          --yes            answer yes to confirmations
          --no-apply       skip running Install Custom AI.cmd after extract
          --force          reinstall even when already on the latest tag
          --full           uninstall: also delete settings, session and backups
          --window         run package scripts in their own window (for GUIs)
          --progress       emit ##PROGRESS lines for machine-readable progress
        """;

    public static async Task<int> Main(string[] args)
    {
        var opts = ParseArgs(args);
        if (opts.TryGetValue("help", out _) || opts.TryGetValue("h", out _) || args.Contains("help"))
        {
            Console.WriteLine(Usage);
            return 0;
        }

        string command = args.Length > 0 && !args[0].StartsWith("-") ? args[0].ToLowerInvariant() : "";
        if (command == "selftest") return SelfTest.Run();

        var cfg = Store.LoadConfig();
        if (opts.TryGetValue("repo", out string repo) && !string.IsNullOrWhiteSpace(repo)) cfg.Repo = repo.Trim();

        string workDir = Path.Combine(Path.GetTempPath(), "SwyfInstaller");
        bool autoYes = opts.ContainsKey("yes");
        bool applyPatch = !opts.ContainsKey("no-apply");
        bool force = opts.ContainsKey("force");
        bool full = opts.ContainsKey("full");
        bool window = opts.ContainsKey("window");
        bool progressLines = opts.ContainsKey("progress");
        opts.TryGetValue("gamedir", out string cliDir);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            switch (command)
            {
                case "":
                    return await MenuAsync(cfg, workDir, cts.Token);
                case "install":
                    GameDir.Resolve(cfg, cliDir ?? "", interactive: false);
                    Store.SaveConfig(cfg);
                    return await Ops.InstallFlowAsync(cfg, workDir, autoYes, applyPatch, window, progressLines, cts.Token);
                case "update":
                    GameDir.Resolve(cfg, cliDir ?? "", interactive: false);
                    Store.SaveConfig(cfg);
                    return await Ops.UpdateFlowAsync(cfg, workDir, autoYes, applyPatch, force, window, progressLines, cts.Token);
                case "verify":
                    GameDir.Resolve(cfg, cliDir ?? "", interactive: false);
                    Store.SaveConfig(cfg);
                    return Ops.VerifyFlow(cfg);
                case "uninstall":
                    GameDir.Resolve(cfg, cliDir ?? "", interactive: false);
                    Store.SaveConfig(cfg);
                    return Ops.UninstallFlow(cfg, autoYes, full, window);
                case "latest":
                    return await LatestAsync(cfg.Repo, cts.Token);
                case "detect":
                    foreach (string d in GameDir.FindCandidates())
                        Console.WriteLine(d);
                    return 0;
                case "gamedir":
                    GameDir.Resolve(cfg, cliDir ?? "", interactive: true);
                    Store.SaveConfig(cfg);
                    Console.WriteLine("Game folder: " + cfg.GameDir);
                    return 0;
                default:
                    Console.WriteLine("Unknown command: " + command);
                    Console.WriteLine(Usage);
                    return 1;
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
            return 1;
        }
    }

    private static async Task<int> LatestAsync(string repo, CancellationToken ct)
    {
        try
        {
            var rel = await Ops.FetchLatestAsync(repo, ct);
            Console.WriteLine("Latest: " + rel.Tag);
            Console.WriteLine("  " + rel.ZipName + " (" + rel.ZipSize / 1024 + " KB)");
            Console.WriteLine("  " + rel.ChecksumName);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
            return 1;
        }
    }

    private static async Task<int> MenuAsync(Store.AppConfig cfg, string workDir, CancellationToken ct)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== SWYF Custom AI installer ===");
            Console.WriteLine("Repo:      " + cfg.Repo);
            Console.WriteLine("Game:      " + (cfg.GameDir == "" ? "(not set)" : cfg.GameDir));
            Console.WriteLine("Installed: " + (cfg.InstalledTag == "" ? "(unknown)" : cfg.InstalledTag));
            Console.WriteLine();
            Console.WriteLine("  1) Install");
            Console.WriteLine("  2) Update (remove old, download latest)");
            Console.WriteLine("  3) Verify installed files");
            Console.WriteLine("  4) Uninstall");
            Console.WriteLine("  5) Set game folder");
            Console.WriteLine("  0) Exit");
            Console.Write("Choice: ");
            string choice = (Console.ReadLine() ?? "").Trim();
            try
            {
                switch (choice)
                {
                    case "1":
                        GameDir.Resolve(cfg, "", interactive: true);
                        Store.SaveConfig(cfg);
                        await Ops.InstallFlowAsync(cfg, workDir, autoYes: false, applyPatch: true, visibleWindow: false, progressLines: false, ct);
                        break;
                    case "2":
                        GameDir.Resolve(cfg, "", interactive: true);
                        Store.SaveConfig(cfg);
                        await Ops.UpdateFlowAsync(cfg, workDir, autoYes: false, applyPatch: true, force: false, visibleWindow: false, progressLines: false, ct);
                        break;
                    case "3":
                        GameDir.Resolve(cfg, "", interactive: true);
                        Store.SaveConfig(cfg);
                        Ops.VerifyFlow(cfg);
                        break;
                    case "4":
                        GameDir.Resolve(cfg, "", interactive: true);
                        Store.SaveConfig(cfg);
                        Ops.UninstallFlow(cfg, autoYes: false, full: false, visibleWindow: false);
                        break;
                    case "5":
                        cfg.GameDir = "";
                        GameDir.Resolve(cfg, "", interactive: true);
                        Store.SaveConfig(cfg);
                        break;
                    case "0":
                    case "q":
                    case "exit":
                        return 0;
                    default:
                        Console.WriteLine("Pick 0-5.");
                        break;
                }
            }
            catch (OperationCanceledException) { Console.WriteLine("Cancelled."); }
            catch (Exception ex) { Console.WriteLine("ERROR: " + ex.Message); }
        }
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (!a.StartsWith("-")) continue;
            string key = a.TrimStart('-');
            int eq = key.IndexOf('=');
            if (eq >= 0)
            {
                opts[key.Substring(0, eq)] = key.Substring(eq + 1);
            }
            else if (i + 1 < args.Length && !args[i + 1].StartsWith("-") &&
                     (key == "gamedir" || key == "repo"))
            {
                opts[key] = args[++i];
            }
            else
            {
                opts[key] = "1";
            }
        }
        return opts;
    }
}
