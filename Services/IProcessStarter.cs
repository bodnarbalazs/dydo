namespace DynaDocs.Services;

using System.Diagnostics;

/// <summary>
/// Interface for starting processes. Enables testing without actually launching terminals.
/// </summary>
public interface IProcessStarter
{
    void Start(ProcessStartInfo psi);
}
