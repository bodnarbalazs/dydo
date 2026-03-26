namespace DynaDocs.Tests.Integration;

using System.Diagnostics;
using DynaDocs.Services;

/// <summary>
/// Process starter that does nothing. Prevents tests from actually killing processes.
/// </summary>
internal class NoOpProcessStarter : IProcessStarter
{
    public int Start(ProcessStartInfo psi) => Environment.ProcessId;
}
