namespace DynaDocs.Services;

/// <summary>
/// Interface for detecting terminal applications. Supports both installation checks and
/// runtime detection (e.g., via TERM_PROGRAM environment variable).
/// </summary>
public interface ITerminalDetector
{
    bool IsAvailable(string appName);

    /// <summary>
    /// Detects the terminal application this process is running in.
    /// Returns "iTerm" for iTerm2, "Terminal" for Terminal.app, or null if unknown.
    /// </summary>
    string? GetRunningTerminal();
}
