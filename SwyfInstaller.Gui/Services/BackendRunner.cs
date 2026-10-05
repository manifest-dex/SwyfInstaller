using System.Diagnostics;
using System.IO;

namespace SwyfInstaller.Gui.Services;

internal sealed class BackendRunner
{
    private const string BackendExe = "SwyfInstaller.exe";
    private Process _current;
    private readonly object _gate = new();

    public string BackendPath()
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        string exe = Path.Combine(dir, BackendExe);
        if (!File.Exists(exe))
            throw new FileNotFoundException(BackendExe + " was not found next to this app (" + dir + ").");
        return exe;
    }

    public void Kill()
    {
        lock (_gate)
        {
            try { _current?.Kill(entireProcessTree: true); }
            catch { }
        }
    }

    public async Task<List<string>> RunCaptureAsync(string args, CancellationToken ct = default)
    {
        var lines = new List<string>();
        await RunAsync(args, new Progress<string>(line => lines.Add(line)), null, ct);
        return lines;
    }

    public async Task<int> RunAsync(string args, IProgress<string> log, IProgress<double> progress, CancellationToken ct)
    {
        string backend = BackendPath();
        log?.Report("$ SwyfInstaller.exe " + args);
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo(backend, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(backend) ?? "",
        };
        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            if (e.Data.StartsWith("##PROGRESS ") && double.TryParse(
                    e.Data.Substring("##PROGRESS ".Length),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double v))
                progress?.Report(v);
            else
                log?.Report(e.Data);
        };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) log?.Report(e.Data); };
        lock (_gate) { _current = p; }
        try
        {
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            try
            {
                await p.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                Kill();
                throw;
            }
            return p.ExitCode;
        }
        finally
        {
            lock (_gate) { if (_current == p) _current = null; }
        }
    }
}
