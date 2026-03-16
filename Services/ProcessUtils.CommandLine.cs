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

    internal static List<int> FindByWmic(string pattern)
    {
        var escaped = pattern.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");
        var output = RunProcess("wmic",
            $"process where \"CommandLine like '%{escaped}%'\" get ProcessId /format:csv");
        return output != null ? ParseWmicCsvOutput(output) : [];
    }

    internal static List<int> ParseWmicCsvOutput(string output)
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

    internal static List<int> FindByPowerShell(string pattern)
    {
        try
        {
            var shell = ResolvePowerShell();
            var psEscaped = EscapeForPowerShellLike(pattern);
            var output = RunProcess(shell,
                $"-NoProfile -Command \"Get-CimInstance Win32_Process | Where-Object {{ $_.CommandLine -like '*{psEscaped}*' }} | Select-Object -ExpandProperty ProcessId\"",
                10000);
            return output != null ? ParseNewlineSeparatedPids(output) : [];
        }
        catch { return []; }
    }

    /// <summary>
    /// Parses newline-separated PID output (used by PowerShell and ps commands).
    /// </summary>
    internal static List<int> ParseNewlineSeparatedPids(string output)
    {
        var pids = new List<int>();
        foreach (var line in output.Split('\n'))
        {
            if (int.TryParse(line.Trim(), out var pid) && pid > 0)
                pids.Add(pid);
        }
        return pids;
    }

    /// <summary>
    /// Parses `ps -eo pid,args` output, returning PIDs whose args contain the pattern.
    /// </summary>
    internal static List<int> ParsePsEoPidArgs(string output, string pattern)
    {
        var pids = new List<int>();
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.Contains(pattern, StringComparison.OrdinalIgnoreCase)) continue;
            var spaceIdx = trimmed.IndexOf(' ');
            if (spaceIdx > 0 && int.TryParse(trimmed[..spaceIdx], out var pid) && pid > 0)
                pids.Add(pid);
        }
        return pids;
    }

    internal static List<int> FindProcessesByCommandLineLinux(string pattern)
    {
        var pids = new List<int>();
        try
        {
            foreach (var dir in Directory.GetDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(dir), out var pid)) continue;
                try
                {
                    var cmdline = File.ReadAllText(Path.Combine(dir, "cmdline")).Replace('\0', ' ');
                    if (cmdline.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        pids.Add(pid);
                }
                catch { }
            }
        }
        catch { }
        return pids;
    }

    internal static List<int> FindProcessesByCommandLineMac(string pattern)
    {
        var output = RunProcess("ps", "-eo pid,args");
        return output != null ? ParsePsEoPidArgs(output, pattern) : [];
    }
}
