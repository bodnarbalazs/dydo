namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;

[Collection("Integration")]
public class WatchdogCommandTests
{
    [Fact]
    public void Create_ReturnsCommandWithSubcommands()
    {
        var command = WatchdogCommand.Create();

        Assert.Equal("watchdog", command.Name);
        Assert.Equal(3, command.Subcommands.Count);
        Assert.Contains(command.Subcommands, c => c.Name == "start");
        Assert.Contains(command.Subcommands, c => c.Name == "stop");
        Assert.Contains(command.Subcommands, c => c.Name == "run");
    }

    [Fact]
    public void Create_RunSubcommandIsHidden()
    {
        var command = WatchdogCommand.Create();
        var runCmd = command.Subcommands.First(c => c.Name == "run");

        Assert.True(runCmd.Hidden);
    }

    [Fact]
    public void StartAndRun_ExposeIntervalAndCensusOptions()
    {
        var command = WatchdogCommand.Create();
        foreach (var name in new[] { "start", "run" })
        {
            var sub = command.Subcommands.First(c => c.Name == name);
            Assert.Contains(sub.Options, o => o.Name == "--interval");
            Assert.Contains(sub.Options, o => o.Name == "--census-interval");
        }
    }
}
