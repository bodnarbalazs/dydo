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
}
