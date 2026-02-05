namespace DynaDocs.Services;

using System.Diagnostics;

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
}
