namespace DynaDocs.Tests.Statechart;

using System.ComponentModel;
using System.Diagnostics;
using DynaDocs.Services.Statechart;

public class StatechartJobTests
{
    [Theory]
    [InlineData(0, 87)]
    [InlineData(4, 5)]
    public void NativeAttachmentFailuresPreserveTheirWin32Error(int pid, int expected)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Throws<PlatformNotSupportedException>(() => new WindowsStatechartJob());
            return;
        }

        using var job = new WindowsStatechartJob();
        var error = Assert.Throws<Win32Exception>(() => job.Attach(pid));
        Assert.Equal(expected, error.NativeErrorCode);
        Assert.Equal(0u, job.ActiveProcesses());
    }

    [Fact]
    public void ANewJobOwnsNoUnrelatedProcess()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Throws<PlatformNotSupportedException>(() => new WindowsStatechartJob());
            return;
        }

        using var job = new WindowsStatechartJob();
        Assert.False(job.Contains(Environment.ProcessId));
        Assert.Equal(0u, job.ActiveProcesses());
    }

    // Sealing terminates every member with the job's exit code, then nothing more can join.
    [SkippableFact]
    public async Task ASealedJobDrainsItsMemberAndAcceptsNoOther()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Job Objects are Windows-only.");
        using var job = new WindowsStatechartJob();
        using var member = Process.Start(Waiting())!;
        job.Attach(member.Id);
        Assert.True(job.Contains(member.Id));
        Assert.NotEqual(0u, job.ActiveProcesses());

        Assert.True(await job.SealAndDrainAsync(TimeSpan.FromSeconds(30)));
        await member.WaitForExitAsync();
        Assert.Equal(2, member.ExitCode);
        Assert.Equal(0u, job.ActiveProcesses());
        Assert.Throws<InvalidOperationException>(() => job.Attach(Environment.ProcessId));
        Assert.Equal(87, Assert.Throws<Win32Exception>(() => job.Contains(0)).NativeErrorCode);
    }

    // A live handle keeps an exited process openable, but the kernel refuses to assign it to the job.
    [SkippableFact]
    public async Task AnExitedProcessIsRefusedRatherThanOwned()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Job Objects are Windows-only.");
        using var exited = Process.Start(Waiting())!;
        exited.StandardInput.Close();
        await exited.WaitForExitAsync();
        using var job = new WindowsStatechartJob();

        Assert.Throws<Win32Exception>(() => job.Attach(exited.Id));
        Assert.False(job.Contains(exited.Id));
        Assert.Equal(0u, job.ActiveProcesses());
    }

    // A command interpreter that lives until its input closes.
    private static ProcessStartInfo Waiting() => new("cmd.exe")
    {
        UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true
    };
}
