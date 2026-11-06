namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;

// A process it owns is ended before the directory is deleted, so a failed test cannot leave a helper holding
// its scratch open and let the teardown error replace the test's own. The helper is ended by closing its input:
// it answers EOF by closing its scratch handles before it exits, while on Windows a killed helper's handle was
// seen still open after its exit.
internal sealed class StatechartTestDirectory : IDisposable
{
    private readonly List<Process> _owned = [];
    public string Path { get; } = Directory.CreateTempSubdirectory("statechart-test-").FullName;

    public Process Own(Process process)
    {
        _owned.Add(process);
        return process;
    }

    public void Dispose()
    {
        foreach (var process in _owned)
        {
            process.StandardInput.Close();
            process.WaitForExit();
            process.Dispose();
        }
        Directory.Delete(Path, recursive: true);
    }
}
