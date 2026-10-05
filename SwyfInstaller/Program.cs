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
}
