namespace DynaDocs.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;

public static partial class ProcessUtils
{
    /// <summary>
    /// When set, GetProcessCommandLine uses this instead of probing the system.
    /// </summary>
    public static Func<int, string?>? GetProcessCommandLineOverride { get; set; }

    /// <summary>
    /// Reads the full command line of a single process, or null when unreadable.
    /// Windows: wmic, falling back to a Get-CimInstance query; Linux: /proc/PID/cmdline;
    /// macOS: ps. Backs the #0250/F1 node-ancestor disambiguation — a Windows `node`
    /// ancestor is only an agent host when its command line names the vendor CLI; the
    /// npm-installed dydo launcher shim and unrelated node scripts are transparent.
    /// </summary>
    public static string? GetProcessCommandLine(int pid)
    {
        if (GetProcessCommandLineOverride != null) return GetProcessCommandLineOverride(pid);
        if (pid <= 0) return null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GetProcessCommandLineWindows(pid);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return GetProcessCommandLineLinux(pid);
        return GetProcessCommandLineMac(pid);
    }

    private static string? GetProcessCommandLineWindows(int pid) =>
        GetCommandLineByWmic(pid) ?? GetCommandLineByPowerShell(pid);

    internal static string? GetCommandLineByWmic(int pid)
    {
        try
        {
            return ParseWmicCommandLineList(
                RunProcess("wmic", $"process where \"ProcessId={pid}\" get CommandLine /format:list"));
        }
        catch { return null; }
    }

    internal static string? GetCommandLineByPowerShell(int pid)
    {
        try
        {
            var shell = ResolvePowerShell();
            var value = RunProcess(shell,
                $"-NoProfile -Command \"(Get-CimInstance Win32_Process -Filter 'ProcessId={pid}').CommandLine\"",
                10000)?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch { return null; }
    }

    /// <summary>
    /// Extracts the value from wmic `get CommandLine /format:list` output (a `CommandLine=…` line).
    /// </summary>
    internal static string? ParseWmicCommandLineList(string? output)
    {
        if (string.IsNullOrEmpty(output)) return null;
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("CommandLine=", StringComparison.Ordinal))
            {
                var value = trimmed["CommandLine=".Length..].Trim();
                return value.Length > 0 ? value : null;
            }
        }
        return null;
    }

    internal static string? GetProcessCommandLineLinux(int pid)
    {
        try
        {
            var path = $"/proc/{pid}/cmdline";
            if (!File.Exists(path)) return null;
            var raw = File.ReadAllText(path).Replace('\0', ' ').Trim();
            return raw.Length > 0 ? raw : null;
        }
        catch { return null; }
    }

    internal static string? GetProcessCommandLineMac(int pid)
    {
        var value = RunProcess("ps", $"-o command= -p {pid}")?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

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
