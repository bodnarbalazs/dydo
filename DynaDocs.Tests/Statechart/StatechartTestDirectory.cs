namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;
using System.Runtime.ExceptionServices;

// Helpers normally answer EOF by closing their scratch handles before exiting. Only a timeout forces a
// process-tree kill and a second bounded wait, so a failed assertion does not abandon a stalled helper.
// A killed Windows helper's handle was seen still open briefly after exit, so a timeout reports its pid
// and the uncertainty about scratch handles. Each helper is ended and directory deletion is attempted
// independently; teardown failures are rethrown afterwards.
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
            if (process.WaitForExit(ExitBound)) return;

            var pid = process.Id;
            var outcome = "was killed";
            Exception? killError = null;
            try { process.Kill(entireProcessTree: true); }
            catch (Exception) when (process.HasExited)
            {
                outcome = "exited before it could be killed";
            }
            catch (Exception error)
            {
                outcome = "could not be killed";
                killError = error;
            }
            if (!process.WaitForExit(ExitBound))
                outcome = "did not exit even after the kill attempt and a second bounded wait";
            throw new TimeoutException(
                $"Statechart helper {pid} {outcome} after not exiting within {ExitBound.TotalSeconds:0} s of EOF; " +
                "its scratch may still have been held when deletion was attempted.", killError);
        }
    }
}
