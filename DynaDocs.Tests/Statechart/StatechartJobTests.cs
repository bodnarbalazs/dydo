namespace DynaDocs.Tests.Statechart;

using System.ComponentModel;
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
}
