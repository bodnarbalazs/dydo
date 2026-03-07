namespace DynaDocs.Tests.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;
using DynaDocs.Services;
using Xunit;

public class TerminalCloserTests : IDisposable
{
    private readonly RecordingProcessStarter _recorder = new();

    public TerminalCloserTests()
    {
        TerminalCloser.ProcessStarterOverride = _recorder;
    }

    public void Dispose()
    {
        TerminalCloser.ProcessStarterOverride = null;
    }

    [Fact]
    public void SpawnDelayedKill_StartsProcess_WithCorrectPid()
    {
        TerminalCloser.SpawnDelayedKill(12345);

        Assert.Single(_recorder.Started);
        var psi = _recorder.Started[0];

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal("powershell", psi.FileName);
            Assert.Contains("12345", psi.Arguments);
            Assert.Contains("Stop-Process", psi.Arguments);
        }
        else
        {
            Assert.Equal("bash", psi.FileName);
            Assert.Contains("12345", psi.Arguments);
            Assert.Contains("kill", psi.Arguments);
        }
    }

    [Fact]
    public void SpawnDelayedKill_PrintsFallback_WhenProcessStartFails()
    {
        _recorder.FailAll = true;

        var stdout = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(stdout);

        try
        {
            TerminalCloser.SpawnDelayedKill(12345);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Contains("Could not schedule auto-close", stdout.ToString());
    }

    [Fact]
    public void ScheduleClaudeTermination_DoesNotKillRealProcesses()
    {
        // In test context, this should either find an ancestor and use the mock,
        // or not find one and print the fallback. Either way, no real kill happens.
        var stdout = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(stdout);

        try
        {
            TerminalCloser.ScheduleClaudeTermination();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = stdout.ToString();
        // Either the mock was used (ancestor found) or fallback was printed (no ancestor)
        if (_recorder.Started.Count == 0)
        {
            Assert.Contains("Could not detect Claude process", output);
        }
        else
        {
            // Mock intercepted the kill — no real process was harmed
            Assert.Single(_recorder.Started);
        }
    }
}
