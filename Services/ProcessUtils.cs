namespace DynaDocs.Services;

using System.Diagnostics;

/// <summary>
/// Utility methods for process-related operations.
/// Used for lock file staleness detection.
/// </summary>
public static partial class ProcessUtils
{
    /// <summary>
    /// When set, IsProcessRunning uses this instead of probing the system.
    /// </summary>
    public static Func<int, bool>? IsProcessRunningOverride { get; set; }

    /// <summary>
    /// Checks if a process with the given PID is still running.
    /// Used for detecting stale lock files.
    /// </summary>
    public static bool IsProcessRunning(int processId)
    {
        if (IsProcessRunningOverride != null) return IsProcessRunningOverride(processId);
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
    /// When set, GetProcessName uses this instead of probing the system.
    /// </summary>
    public static Func<int, string?>? GetProcessNameOverride { get; set; }

    /// <summary>
    /// Gets the process name for a given PID.
    /// </summary>
    public static string? GetProcessName(int processId)
    {
        if (GetProcessNameOverride != null) return GetProcessNameOverride(processId);
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
            if (p == null) return "powershell.exe";
            p.WaitForExit(2000);
            if (p.ExitCode == 0) return "pwsh.exe";
        }
        catch { }
        return "powershell.exe";
    }

    /// <summary>
    /// Escapes a string for safe interpolation into a single-quoted PowerShell -like pattern.
    /// Inside single-quoted strings, only '' (two single quotes) represents a literal single quote.
    /// </summary>
    public static string EscapeForPowerShellLike(string value)
    {
        return value.Replace("'", "''");
    }

    /// <summary>
    /// Launches a process and returns its stdout. Returns null if the process fails to start.
    /// </summary>
    internal static string? RunProcess(string command, string args, int timeoutMs = 5000)
    {
        try
        {
            var psi = new ProcessStartInfo(command, args)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi)!;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(timeoutMs);
            return output;
        }
        catch { return null; }
    }

    /// <summary>
    /// Launches a process and drains both stdout and stderr concurrently, returning
    /// (exitCode, stdout, stderr). Concurrent draining avoids the pipe-buffer deadlock
    /// that occurs when one stream fills (~64 KB on Windows) while the parent reads only
    /// the other. On timeout or start failure the exit code is -1 and the drained strings
    /// are empty; callers that need to preserve a different timeout-sentinel translate it
    /// at the call site (e.g. WorktreeCommand.RunProcessSilent maps -1 back to 1).
    /// </summary>
    /// <param name="environment">Entries are merged into the child's environment; null values remove a variable. Parent env is inherited unchanged for keys not listed.</param>
    /// <param name="redirectStdin">When true, redirects stdin and closes it immediately to signal EOF — required for git invocations that must not block on a credential prompt.</param>
    internal static (int ExitCode, string Stdout, string Stderr) RunProcessCapture(
        string fileName,
        string arguments,
        string? workingDir = null,
        int timeoutMs = 5000,
        IReadOnlyDictionary<string, string?>? environment = null,
        bool redirectStdin = false)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = redirectStdin,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            if (workingDir != null) psi.WorkingDirectory = workingDir;
            if (environment != null)
            {
                foreach (var kv in environment)
                {
                    if (kv.Value == null) psi.Environment.Remove(kv.Key);
                    else psi.Environment[kv.Key] = kv.Value;
                }
            }

            using var process = Process.Start(psi);
            if (process == null) return (-1, string.Empty, string.Empty);

            if (redirectStdin)
            {
                try { process.StandardInput.Close(); } catch { /* best-effort */ }
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                try { Task.WaitAll([stdoutTask, stderrTask], 500); } catch { /* best-effort */ }
                return (-1, string.Empty, string.Empty);
            }

            return (process.ExitCode, stdoutTask.GetAwaiter().GetResult(), stderrTask.GetAwaiter().GetResult());
        }
        catch
        {
            return (-1, string.Empty, string.Empty);
        }
    }
}
