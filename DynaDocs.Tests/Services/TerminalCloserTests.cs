namespace DynaDocs.Tests.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;
using DynaDocs.Services;
using Xunit;

[Collection("ProcessUtils")]
public class TerminalCloserTests : IDisposable
{
    private readonly RecordingProcessStarter _recorder = new();

    public TerminalCloserTests()
    {
        TerminalCloser.ProcessStarterOverride = _recorder;
        ProcessUtils.PowerShellResolverOverride = () => "pwsh.exe";
    }

    public void Dispose()
    {
        TerminalCloser.ProcessStarterOverride = null;
        ProcessUtils.PowerShellResolverOverride = null;
    }

    [Fact]
    public void SpawnDelayedKill_Windows_UsesResolvedPowerShell()
    {
        ProcessUtils.PowerShellResolverOverride = () => "pwsh.exe";

        TerminalCloser.SpawnDelayedKill(12345);

        Assert.Single(_recorder.Started);
        var psi = _recorder.Started[0];

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal("pwsh.exe", psi.FileName);
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
    public void SpawnDelayedKill_Windows_CorrectArguments()
    {
        TerminalCloser.SpawnDelayedKill(9999);

        Assert.Single(_recorder.Started);
        var psi = _recorder.Started[0];

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Contains("-NoProfile", psi.Arguments);
            Assert.Contains("-WindowStyle Hidden", psi.Arguments);
            Assert.Contains("Start-Sleep 3", psi.Arguments);
            Assert.Contains("Stop-Process -Id 9999 -Force", psi.Arguments);
        }
        else
        {
            Assert.Contains("sleep 3", psi.Arguments);
            Assert.Contains("kill -TERM 9999", psi.Arguments);
        }
    }

    [Fact]
    public void SpawnDelayedKill_Unix_UsesBash()
    {
        TerminalCloser.SpawnDelayedKill(12345);

        Assert.Single(_recorder.Started);
        var psi = _recorder.Started[0];

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal("bash", psi.FileName);
            Assert.Contains("kill -TERM", psi.Arguments);
        }
    }

    [Fact]
    public void SpawnDelayedKill_ProcessStartFails_PrintsMessage()
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

        Assert.Contains("Auto-close failed", stdout.ToString());
        Assert.Contains("Close this terminal manually", stdout.ToString());
    }

    [Fact]
    public void ScheduleClaudeTermination_NoClaudeAncestor_PrintsMessage()
    {
        // In test context, there's no "claude" process in the ancestor chain
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
            // No claude ancestor found — should show clear error
            Assert.Contains("Auto-close failed", output);
            Assert.Contains("Claude process", output);
        }
        else
        {
            // If by some chance a "claude" ancestor exists in test env, verify it targets that PID
            Assert.Single(_recorder.Started);
        }
    }

    [Fact]
    public void SpawnDelayedKill_Windows_FallsBackToPowershell()
    {
        ProcessUtils.PowerShellResolverOverride = () => "powershell.exe";

        TerminalCloser.SpawnDelayedKill(12345);

        Assert.Single(_recorder.Started);
        var psi = _recorder.Started[0];

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal("powershell.exe", psi.FileName);
        }
    }
}
