namespace SwyfInstaller;

// Console fallback for SWYF Custom AI.
// Takes no parameters: run it and pick from the menu.
// (The installer app UI calls Ops directly, not through this console.)
// The only argument honored is "selftest", used by build/release scripts.
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "selftest", StringComparison.OrdinalIgnoreCase))
            return SelfTest.Run();

        var cfg = Store.LoadConfig();
        string workDir = Path.Combine(Path.GetTempPath(), "SwyfInstaller");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            return await MenuAsync(cfg, workDir, cts.Token);
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

    private static async Task<int> MenuAsync(Store.AppConfig cfg, string workDir, CancellationToken ct)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== SWYF Custom AI installer ===");
            Console.WriteLine("Tip: the installer app (SwyfInstallerGui.exe) is easier than this menu.");
            Console.WriteLine("Game:      " + (cfg.GameDir == "" ? "(not set)" : cfg.GameDir));
            Console.WriteLine("Installed: " + (cfg.InstalledTag == "" ? "(unknown)" : cfg.InstalledTag));
            Console.WriteLine();
            Console.WriteLine("  1) Install latest");
            Console.WriteLine("  2) Update (remove old, download latest)");
            Console.WriteLine("  3) Verify installed files");
            Console.WriteLine("  4) Uninstall");
            Console.WriteLine("  5) Set game folder");
            Console.WriteLine("  6) Setup FlagshipRouter (clone, install, start, point mod at it)");
            Console.WriteLine("  7) Setup keyless Ollama (local, no accounts or keys)");
            Console.WriteLine("  8) Save Groq key (validate, connect router, point mod)");
            Console.WriteLine("  9) Apply recommended mod settings (keep key)");
            Console.WriteLine("  10) Remove router dashboard password (local only)");
            Console.WriteLine("  0) Exit");
            Console.Write("Choice: ");
            string choice = (Console.ReadLine() ?? "").Trim();
            try
            {
                switch (choice)
                {
                    case "1":
                        GameDir.Resolve(cfg, interactive: true);
                        Store.SaveConfig(cfg);
                        await Ops.InstallFlowAsync(cfg, workDir, autoYes: false, applyPatch: true, visibleWindow: false, log: null, progress: null, ct);
                        break;
                    case "2":
                        GameDir.Resolve(cfg, interactive: true);
                        Store.SaveConfig(cfg);
                        await Ops.UpdateFlowAsync(cfg, workDir, autoYes: false, applyPatch: true, force: false, visibleWindow: false, log: null, progress: null, ct);
                        break;
                    case "3":
                        GameDir.Resolve(cfg, interactive: true);
                        Store.SaveConfig(cfg);
                        Ops.VerifyFlow(cfg);
                        break;
                    case "4":
                        GameDir.Resolve(cfg, interactive: true);
                        Store.SaveConfig(cfg);
                        Ops.UninstallFlow(cfg, autoYes: false, full: false, visibleWindow: false);
                        break;
                    case "5":
                        cfg.GameDir = "";
                        GameDir.Resolve(cfg, interactive: true);
                        Store.SaveConfig(cfg);
                        break;
                    case "6":
                        GameDir.Resolve(cfg, interactive: true);
                        Store.SaveConfig(cfg);
                        await FlagshipRouter.SetupFlowAsync(cfg.GameDir, startServer: true, null, null, ct);
                        break;
                    case "7":
                        GameDir.Resolve(cfg, interactive: true);
                        Store.SaveConfig(cfg);
                        await OllamaLocal.SetupFlowAsync(cfg.GameDir, null, ct);
                        break;
                    case "8":
                        GameDir.Resolve(cfg, interactive: true);
                        Store.SaveConfig(cfg);
                        Console.Write("Groq key (gsk_...): ");
                        string gkey = (Console.ReadLine() ?? "").Trim();
                        Console.Write("Dashboard password [123456]: ");
                        string dpass = (Console.ReadLine() ?? "").Trim();
                        if (dpass == "") dpass = "123456";
                        string rkey = "";
                        try
                        {
                            string sp = Path.Combine(cfg.GameDir, "CustomAI", "settings.json");
                            using var sdoc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(sp));
                            if (sdoc.RootElement.TryGetProperty("apiKey", out var sk) && sk.ValueKind == System.Text.Json.JsonValueKind.String)
                                rkey = sk.GetString() ?? "";
                        }
                        catch { }
                        await FlagshipRouter.SaveGroqKeyFlowAsync(cfg.GameDir, gkey, dpass, rkey, null, ct);
                        break;
                    case "9":
                        GameDir.Resolve(cfg, interactive: true);
                        Store.SaveConfig(cfg);
                        FlagshipRouter.ApplyRecommendedModSettings(cfg.GameDir);
                        break;
                    case "10":
                        await FlagshipRouter.RemoveRouterPasswordFlowAsync(await PromptDashboardPasswordAsync(), null, ct);
                        break;
                    case "0":
                    case "q":
                    case "exit":
                        return 0;
                    default:
                        Console.WriteLine("Pick 0-10.");
                        break;
                }
            }
            catch (OperationCanceledException) { Console.WriteLine("Cancelled."); }
            catch (Exception ex) { Console.WriteLine("ERROR: " + ex.Message); }
        }
    }

    private static Task<string> PromptDashboardPasswordAsync()
    {
        Console.Write("Current dashboard password [123456]: ");
        string pass = (Console.ReadLine() ?? "").Trim();
        return Task.FromResult(pass == "" ? "123456" : pass);
    }
}
