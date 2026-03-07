namespace DynaDocs.Services;

/// <summary>
/// Interface for detecting installed terminal applications. Enables testing without filesystem checks.
/// </summary>
public interface ITerminalDetector
{
    bool IsAvailable(string appName);
}
