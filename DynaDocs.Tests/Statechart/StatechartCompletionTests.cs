namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;
using DynaDocs.Services.Statechart;

public class StatechartCompletionTests
{
    [Fact]
    public void ACompletionPortWithoutDrainEvidenceFailsClosed()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Throws<PlatformNotSupportedException>(() => new WindowsStatechartJob());
            return;
        }

        using var job = new WindowsStatechartJob();
        using var current = Process.GetCurrentProcess();
        using var completion = new WindowsStatechartCompletion(job.DuplicateCompletionPortTo(current));
        Assert.False(completion.WaitForEmpty(TimeSpan.FromMilliseconds(10)));
        Assert.Equal(0u, job.ActiveProcesses());
    }
}
