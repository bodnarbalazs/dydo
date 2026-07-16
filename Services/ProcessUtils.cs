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
}
