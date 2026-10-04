using System.IO.Compression;

namespace SwyfInstaller;

internal static class SelfTest
{
    private const string CannedRelease = """
        {"tag_name":"v9.9.9","assets":[
        {"name":"SWYF-Custom-AI-v9.9.9-win-x64.zip","browser_download_url":"https://example.com/a.zip","size":123,"digest":"sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"},
        {"name":"SWYF-Custom-AI-v9.9.9-win-x64.zip.sha256","browser_download_url":"https://example.com/a.zip.sha256"}]}
        """;

    public static int Run()
    {
        int failures = 0;
        void Check(bool ok, string name)
        {
            Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
            if (!ok) failures++;
        }

        string root = Path.Combine(Path.GetTempPath(), "SwyfInstallerSelfTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            string gameDir = Path.Combine(root, "game");
            string payload = Path.Combine(root, "payload");
            Directory.CreateDirectory(gameDir);
            Directory.CreateDirectory(Path.Combine(payload, "CustomAI", "package"));
            Directory.CreateDirectory(Path.Combine(gameDir, "CustomAI"));
            File.WriteAllText(Path.Combine(gameDir, GameDir.ExeName), "fake-game");
            File.WriteAllText(Path.Combine(gameDir, "CustomAI", "settings.json"), "{}");
            File.WriteAllText(Path.Combine(gameDir, "CustomAI", "manifestdex-session.dat"), "secret");
            Directory.CreateDirectory(Path.Combine(gameDir, "CustomAI", "backup", "abc"));
            File.WriteAllText(Path.Combine(gameDir, "CustomAI", "backup", "abc", "orig.dll"), "orig");
            File.WriteAllText(Path.Combine(gameDir, "Install Custom AI.cmd"), "old");
            File.WriteAllText(Path.Combine(payload, "CustomAI", "package", "new.dll"), "new-bytes");

            string h1 = Store.Sha256Bytes(new byte[] { 1, 2, 3 });
            Check(h1.Length == 64 && Store.IsHex64(h1), "sha256 helper produces 64 hex chars");
            Check(!Store.IsHex64("xyz"), "IsHex64 rejects garbage");
            Check(Store.ParseChecksumFile(h1 + "  file.zip\n") == h1, "checksum parse 'hash  filename'");
            Check(Store.ParseChecksumFile("  " + h1.ToUpperInvariant() + "\n") == h1, "checksum parse bare hash");

            string zipPath = Path.Combine(root, "pkg.zip");
            ZipFile.CreateFromDirectory(payload, zipPath);
            var files = Ops.ExtractZip(zipPath, gameDir);
            Check(files.Count == 1 && File.Exists(Path.Combine(gameDir, "CustomAI", "package", "new.dll")), "zip extract");
            var manifest = Ops.RecordManifest("o/r", "v9.9.9", gameDir, files);
            Check(manifest.Files.Count == 1, "manifest recorded");
            Check(Ops.VerifyAgainstManifest(gameDir, manifest) == 0, "verify clean install passes");

            File.WriteAllText(Path.Combine(gameDir, "CustomAI", "package", "new.dll"), "tampered");
            Check(Ops.VerifyAgainstManifest(gameDir, manifest) == 2, "verify detects tampered file");
            File.Delete(Path.Combine(gameDir, "CustomAI", "package", "new.dll"));
            Check(Ops.VerifyAgainstManifest(gameDir, manifest) == 2, "verify detects missing file");

            Ops.RemovePackageFiles(gameDir);
            Check(!File.Exists(Path.Combine(gameDir, "Install Custom AI.cmd")), "update removes old cmd");
            Check(File.Exists(Path.Combine(gameDir, "CustomAI", "settings.json")), "update keeps settings.json");
            Check(File.Exists(Path.Combine(gameDir, "CustomAI", "manifestdex-session.dat")), "update keeps session");
            Check(File.Exists(Path.Combine(gameDir, "CustomAI", "backup", "abc", "orig.dll")), "update keeps backups");

            var rel = Github.ParseLatest("o/r", CannedRelease);
            Check(rel.Tag == "v9.9.9" && rel.ZipName.EndsWith(".zip") && rel.ChecksumUrl.EndsWith(".sha256"), "release asset parsing");
            Check(rel.ZipDigest.Length == 64, "release digest parsing");

            bool threw = false;
            try { Github.ParseLatest("o/r", "{}"); } catch { threw = true; }
            Check(threw, "release parse rejects empty payload");

            threw = false;
            try { Store.ParseChecksumFile("not a hash\n"); } catch { threw = true; }
            Check(threw, "checksum parse rejects garbage");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }

        Console.WriteLine(failures == 0 ? "SELFTEST: ALL PASS" : "SELFTEST: " + failures + " FAILURES");
        return failures == 0 ? 0 : 1;
    }
}
