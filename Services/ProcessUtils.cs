namespace DynaDocs.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Utility methods for process-related operations.
/// Used for lock file staleness detection.
/// </summary>
public static class ProcessUtils
{
    /// <summary>
    /// Checks if a process with the given PID is still running.
    /// Used for detecting stale lock files.
    /// </summary>
    public static bool IsProcessRunning(int processId)
    {
        if (processId <= 0) return false;

        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the process name for a given PID.
    /// </summary>
    public static string? GetProcessName(int processId)
    {
        if (processId <= 0) return null;

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the parent process ID for a given PID.
    /// Uses .NET's Process class on Windows; parses /proc on Linux; ps on macOS.
    /// </summary>
    public static int? GetParentPid(int pid)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var process = Process.GetProcessById(pid);
                return GetParentPidWindows(process);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var statusPath = $"/proc/{pid}/status";
                if (!File.Exists(statusPath)) return null;
                foreach (var line in File.ReadLines(statusPath))
                {
                    if (line.StartsWith("PPid:") && int.TryParse(line[5..].Trim(), out var ppid))
                        return ppid;
                }
                return null;
            }
            else
            {
                // macOS
                var psi = new ProcessStartInfo("ps", $"-o ppid= -p {pid}")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return null;
                var output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit();
                return int.TryParse(output, out var result) ? result : null;
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Walks up the process tree from the current process, returning the first ancestor
    /// whose process name contains the given string (case-insensitive).
    /// </summary>
    public static int? FindAncestorProcess(string nameContains, int maxDepth = 10)
    {
        var pid = Environment.ProcessId;

        for (var i = 0; i < maxDepth; i++)
        {
            var parentPid = GetParentPid(pid);
            if (parentPid == null || parentPid <= 1) return null;

            var name = GetProcessName(parentPid.Value);
            if (name != null && name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
                return parentPid.Value;

            pid = parentPid.Value;
        }

        return null;
    }

    /// <summary>
    /// When set, ResolvePowerShell uses this instead of probing the system.
    /// </summary>
    public static Func<string>? PowerShellResolverOverride { get; set; }

    /// <summary>
    /// Resolves which PowerShell executable to use.
    /// Prefers pwsh (PowerShell 7+), falls back to powershell (Windows PowerShell 5.1).
    /// </summary>
    public static string ResolvePowerShell()
    {
        if (PowerShellResolverOverride != null) return PowerShellResolverOverride();

        try
        {
            using var p = Process.Start(new ProcessStartInfo("pwsh", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            p?.WaitForExit(2000);
            if (p is { ExitCode: 0 }) return "pwsh.exe";
        }
        catch { }
        return "powershell.exe";
    }

    private static int? GetParentPidWindows(Process process)
    {
        var pbi = new PROCESS_BASIC_INFORMATION();
        int status = NtQueryInformationProcess(
            process.Handle, 0, ref pbi,
            Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out _);
        return status == 0 ? (int)pbi.InheritedFromUniqueProcessId : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public nint ExitStatus;
        public nint PebBaseAddress;
        public nint AffinityMask;
        public nint BasePriority;
        public nint UniqueProcessId;
        public nint InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        nint processHandle, int processInformationClass,
        ref PROCESS_BASIC_INFORMATION processInformation,
        int processInformationLength, out int returnLength);

    /// <summary>
    /// Escapes a string for safe interpolation into a single-quoted PowerShell -like pattern.
    /// Inside single-quoted strings, only '' (two single quotes) represents a literal single quote.
    /// </summary>
    public static string EscapeForPowerShellLike(string value)
    {
        return value.Replace("'", "''");
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
        var pids = new List<int>();
        try
        {
            // Escape WQL string metacharacters
            var escaped = pattern.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");
            var psi = new ProcessStartInfo("wmic", $"process where \"CommandLine like '%{escaped}%'\" get ProcessId /format:csv")
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
                if (string.IsNullOrEmpty(trimmed)) continue;
                // CSV format: Node,ProcessId
                var parts = trimmed.Split(',');
                if (parts.Length >= 2 && int.TryParse(parts[^1].Trim(), out var pid) && pid > 0)
                    pids.Add(pid);
            }
        }
        catch
        {
            // wmic not available — try PowerShell fallback
            try
            {
                var shell = ResolvePowerShell();
                var psEscaped = EscapeForPowerShellLike(pattern);
                var psi = new ProcessStartInfo(shell, $"-NoProfile -Command \"Get-CimInstance Win32_Process | Where-Object {{ $_.CommandLine -like '*{psEscaped}*' }} | Select-Object -ExpandProperty ProcessId\"")
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
        }
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
