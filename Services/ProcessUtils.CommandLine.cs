namespace DynaDocs.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;

public static partial class ProcessUtils
{
    /// <summary>
    /// Finds processes whose command line contains the given pattern.
    /// Used by the watchdog to find claude processes for specific agents.
    /// </summary>
    public static List<int> FindProcessesByCommandLine(string pattern)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return FindProcessesByCommandLineWindows(pattern);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return FindProcessesByCommandLineLinux(pattern);
        return FindProcessesByCommandLineMac(pattern);
    }

    private static List<int> FindProcessesByCommandLineWindows(string pattern)
    {
        try
        {
            return FindByWmic(pattern);
        }
        catch
        {
            return FindByPowerShell(pattern);
        }
    }

    private static List<int> FindByWmic(string pattern)
    {
        var escaped = pattern.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");
        var psi = new ProcessStartInfo("wmic",
            $"process where \"CommandLine like '%{escaped}%'\" get ProcessId /format:csv")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        if (proc == null) return [];
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(5000);
        return ParseWmicCsvOutput(output);
    }

    private static List<int> ParseWmicCsvOutput(string output)
    {
        var pids = new List<int>();
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            var parts = trimmed.Split(',');
            if (parts.Length >= 2 && int.TryParse(parts[^1].Trim(), out var pid) && pid > 0)
                pids.Add(pid);
        }
        return pids;
    }

    private static List<int> FindByPowerShell(string pattern)
    {
        var pids = new List<int>();
        try
        {
            var shell = ResolvePowerShell();
            var psEscaped = EscapeForPowerShellLike(pattern);
            var psi = new ProcessStartInfo(shell,
                $"-NoProfile -Command \"Get-CimInstance Win32_Process | Where-Object {{ $_.CommandLine -like '*{psEscaped}*' }} | Select-Object -ExpandProperty ProcessId\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return pids;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10000);

            foreach (var line in output.Split('\n'))
            {
                if (int.TryParse(line.Trim(), out var pid) && pid > 0)
                    pids.Add(pid);
            }
        }
        catch { }
        return pids;
    }

    private static List<int> FindProcessesByCommandLineLinux(string pattern)
    {
        var pids = new List<int>();
        try
        {
            foreach (var dir in Directory.GetDirectories("/proc"))
            {
                var name = Path.GetFileName(dir);
                if (!int.TryParse(name, out var pid)) continue;

                var cmdlinePath = Path.Combine(dir, "cmdline");
                if (!File.Exists(cmdlinePath)) continue;

                try
                {
                    var cmdline = File.ReadAllText(cmdlinePath).Replace('\0', ' ');
                    if (cmdline.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        pids.Add(pid);
                }
                catch { }
            }
        }
        catch { }
        return pids;
    }

    private static List<int> FindProcessesByCommandLineMac(string pattern)
    {
        var pids = new List<int>();
        try
        {
            var psi = new ProcessStartInfo("ps", "-eo pid,args")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return pids;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (!trimmed.Contains(pattern, StringComparison.OrdinalIgnoreCase)) continue;
                var spaceIdx = trimmed.IndexOf(' ');
                if (spaceIdx > 0 && int.TryParse(trimmed[..spaceIdx], out var pid) && pid > 0)
                    pids.Add(pid);
            }
        }
        catch { }
        return pids;
    }
}
