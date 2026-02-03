namespace DynaDocs.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

public static class ProcessUtils
{
    /// <summary>
    /// Gets the parent process ID of the current process.
    /// </summary>
    public static int GetParentProcessId()
    {
        return GetParentProcessId(Environment.ProcessId);
    }

    /// <summary>
    /// Gets the parent process ID of a given process.
    /// </summary>
    public static int GetParentProcessId(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return GetParentProcessId(process);
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Gets the parent process ID of a given process.
    /// </summary>
    public static int GetParentProcessId(Process process)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetParentProcessIdWindows(process.Id);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return GetParentProcessIdMacOS(process.Id);
        }
        else
        {
            return GetParentProcessIdLinux(process.Id);
        }
    }

    /// <summary>
    /// Walks up the process tree to find the terminal/shell process.
    /// Returns (terminalPid, claudePid) where claudePid is the immediate parent.
    /// </summary>
    public static (int TerminalPid, int ClaudePid) GetProcessAncestors()
    {
        var currentPid = Environment.ProcessId;
        var parentPid = GetParentProcessId(currentPid);
        var grandparentPid = GetParentProcessId(parentPid);

        // Current = dydo, Parent = Claude Code, Grandparent = Terminal
        return (grandparentPid, parentPid);
    }

    /// <summary>
    /// Checks if a process with the given PID is still running.
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

    [SupportedOSPlatform("windows")]
    private static int GetParentProcessIdWindows(int processId)
    {
#if WINDOWS
        try
        {
            // Use WMI via Process class workaround
            // On Windows, we can use the ParentProcessId from performance counters
            using var query = new System.Management.ManagementObjectSearcher(
                $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {processId}");

            foreach (var item in query.Get())
            {
                return Convert.ToInt32(item["ParentProcessId"]);
            }
        }
        catch
        {
            // Fallback: try reading from /proc style if available (WSL)
            // or return -1
        }
#endif
        return -1;
    }

    [SupportedOSPlatform("macos")]
    private static int GetParentProcessIdMacOS(int processId)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/ps",
                    Arguments = $"-o ppid= -p {processId}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (process.ExitCode == 0 && int.TryParse(output, out var ppid))
            {
                return ppid;
            }
        }
        catch
        {
            // Ignore errors
        }

        return -1;
    }

    private static int GetParentProcessIdLinux(int processId)
    {
        try
        {
            // On Linux, read from /proc/{pid}/stat
            var statPath = $"/proc/{processId}/stat";
            if (File.Exists(statPath))
            {
                var stat = File.ReadAllText(statPath);
                // Format: pid (comm) state ppid ...
                // Find the closing paren of comm, then parse ppid
                var lastParen = stat.LastIndexOf(')');
                if (lastParen > 0)
                {
                    var afterComm = stat[(lastParen + 2)..].Split(' ');
                    if (afterComm.Length >= 2)
                    {
                        return int.Parse(afterComm[1]); // ppid is after state
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return -1;
    }
}
