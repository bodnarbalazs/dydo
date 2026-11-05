namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;

public class StatechartScratchTests
{
    [Fact]
    public async Task AuthoritativeDrainDeletesOnlyRegisteredScratch()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var scope = new StatechartTestDirectory();
        var foreign = Directory.CreateDirectory(Path.Combine(scope.Path, "foreign"));
        File.WriteAllText(Path.Combine(foreign.FullName, "sentinel"), "keep");
        using var job = new WindowsStatechartJob();
        using var scratch = StatechartScratch.Reserve(scope.Path, scope.Path);
        scratch.Register();
        File.WriteAllText(Path.Combine(scratch.Path, "owned"), "remove");

        Assert.True(await job.SealAndDrainAsync(TimeSpan.FromSeconds(5)));
        Assert.True(scratch.CleanupAfterDrain());
        Assert.False(Directory.Exists(scratch.Path));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(foreign.FullName, "sentinel")));
        Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(scratch.Journal));
        Assert.Throws<InvalidOperationException>(() => job.Attach(Environment.ProcessId));
    }

    [Fact]
    public void UncertainDrainRetainsRegisteredScratchAndDurableReason()
    {
        using var scope = new StatechartTestDirectory();
        using var scratch = StatechartScratch.Reserve(scope.Path, scope.Path);
        scratch.Register();
        scratch.Retain("owner-eof: drain uncertain");
        Assert.True(Directory.Exists(scratch.Path));
        Assert.Contains("drain uncertain", File.ReadAllText(scratch.Journal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacedOrUnregisteredScratchIsRetained(bool replace)
    {
        using var scope = new StatechartTestDirectory();
        using var scratch = StatechartScratch.Reserve(scope.Path, scope.Path);
        if (replace)
        {
            Directory.Move(scratch.Path, scratch.Path + "-original");
            Directory.CreateDirectory(scratch.Path);
            File.WriteAllText(System.IO.Path.Combine(scratch.Path, "sentinel"), "foreign");
            Assert.Throws<IOException>(scratch.Register);
        }
        Assert.False(scratch.CleanupAfterDrain());
        Assert.True(Directory.Exists(scratch.Path));
        Assert.Contains("retained: unregistered: no deletion authority", File.ReadAllText(scratch.Journal));
    }
}
