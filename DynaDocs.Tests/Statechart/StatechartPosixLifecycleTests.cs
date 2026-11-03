namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;
using DynaDocs.Services.Statechart;

public class StatechartPosixLifecycleTests
{
    [Fact]
    public async Task RegisteredPosixLifetimeGatesWorkAndCleansOnlyAfterIndependentDrain()
    {
        using var scope = new StatechartTestDirectory();
        if (!OperatingSystem.IsLinux())
        {
            await Assert.ThrowsAsync<PlatformNotSupportedException>(() =>
                PosixStatechartLifetime.StartAsync(new ProcessStartInfo("missing"), scope.Path, scope.Path, scope.Path));
            return; // Platform refusal is not Linux execution evidence.
        }
        await using var lifetime = await PosixStatechartLifetime.StartAsync(StatechartTestBinary.Start(), scope.Path, scope.Path, scope.Path);
        Assert.Throws<InvalidOperationException>(() => lifetime.Gate(new ProcessStartInfo("/missing-before-registration")));
        await lifetime.RegisterAsync();
        File.WriteAllText(Path.Combine(lifetime.Stage, "owned-file"), "checking");
        var stage = lifetime.Stage;
        var temp = lifetime.Temp;
        Assert.True(await lifetime.CompleteAsync());
        Assert.False(Directory.Exists(stage));
        Assert.False(Directory.Exists(temp));
        Assert.All(Directory.GetFiles(scope.Path, "scratch-*.journal"), journal =>
            Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(journal)));
    }

    [Fact]
    public async Task PosixControlEofDrainsOutsideThePublicProcessBeforeRegisteredCleanup()
    {
        using var scope = new StatechartTestDirectory();
        if (!OperatingSystem.IsLinux())
        {
            await Assert.ThrowsAsync<PlatformNotSupportedException>(() =>
                PosixStatechartLifetime.StartAsync(new ProcessStartInfo("missing"), scope.Path, scope.Path, scope.Path));
            return;
        }
        var lifetime = await PosixStatechartLifetime.StartAsync(StatechartTestBinary.Start(), scope.Path, scope.Path, scope.Path);
        await lifetime.RegisterAsync();
        var stage = lifetime.Stage;
        var temp = lifetime.Temp;
        File.WriteAllText(Path.Combine(stage, "owned-file"), "checking");
        await lifetime.DisposeAsync();
        Assert.False(Directory.Exists(stage));
        Assert.False(Directory.Exists(temp));
        Assert.All(Directory.GetFiles(scope.Path, "scratch-*.journal"), journal =>
            Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(journal)));
    }
}
