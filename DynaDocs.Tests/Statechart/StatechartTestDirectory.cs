namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;
using System.Runtime.ExceptionServices;

// A process it owns is ended before the directory is deleted, so a failed test cannot leave a helper holding
// its scratch open and let the teardown error replace the test's own. The helper is ended by closing its input:
// it answers EOF by closing its scratch handles before it exits, while on Windows a killed helper's handle was
// seen still open after its exit. A helper that does not exit within the bound fails the teardown by its pid
// rather than hanging it; every other helper is still ended and the directory still deleted.
internal sealed class StatechartTestDirectory : IDisposable
{
    private static readonly TimeSpan ExitBound = TimeSpan.FromSeconds(30);
    private readonly List<Process> _owned = [];
    public string Path { get; } = Directory.CreateTempSubdirectory("statechart-test-").FullName;

    public Process Own(Process process)
    {
        _owned.Add(process);
        return process;
    }

    public void Dispose()
    {
        var failures = new List<Exception>();
        foreach (var process in _owned)
        {
            try { End(process); }
            catch (Exception error) { failures.Add(error); }
        }
        try { Directory.Delete(Path, recursive: true); }
        catch (Exception error) { failures.Add(error); }
        if (failures.Count == 1) ExceptionDispatchInfo.Throw(failures[0]);
        if (failures.Count > 1) throw new AggregateException("Statechart test teardown failed.", failures);
    }

    private static void End(Process process)
    {
        using (process)
        {
            process.StandardInput.Close();
            if (!process.WaitForExit(ExitBound))
                throw new TimeoutException(
                    $"Statechart helper {process.Id} did not exit within {ExitBound.TotalSeconds:0} s of EOF; its scratch may still be held.");
        }
    }
}
