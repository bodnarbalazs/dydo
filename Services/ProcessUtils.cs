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
}
