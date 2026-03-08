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
    public void ScheduleClaudeTermination_UsesGrandparentPid()
    {
        // In test context, grandparent PID should exist (test runner → shell → dydo).
        // The mock intercepts the kill so no real process is harmed.
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
        if (_recorder.Started.Count == 0)
        {
            // No grandparent found — fallback message should mention parent or CLI
            Assert.True(
                output.Contains("Could not detect parent process") ||
                output.Contains("Could not detect CLI process"),
                $"Unexpected fallback message: {output}");
        }
        else
        {
            // Mock intercepted the kill — verify the PID is the grandparent
            Assert.Single(_recorder.Started);
            var psi = _recorder.Started[0];
            var myPid = Environment.ProcessId;
            var parentPid = ProcessUtils.GetParentPid(myPid);
            Assert.NotNull(parentPid);
            var grandparentPid = ProcessUtils.GetParentPid(parentPid!.Value);
            Assert.NotNull(grandparentPid);
            Assert.Contains(grandparentPid!.Value.ToString(), psi.Arguments);
        }
    }
}
