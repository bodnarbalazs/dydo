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

    // The watchdog is a stub awaiting its Notion-sync repurpose (DR-041): every subcommand
    // prints the awaiting notice, exits 0, and does nothing harmful.
    [Theory]
    [InlineData("start")]
    [InlineData("stop")]
    [InlineData("run")]
    public void Subcommand_PrintsAwaitingNotice_AndExitsZero(string subcommand)
    {
        var (output, exitCode) = RunSubcommand(subcommand);

        Assert.Equal(0, exitCode);
        Assert.Contains("awaiting its Notion-sync repurpose", output);
    }

    private static (string output, int exitCode) RunSubcommand(string subcommand)
    {
        var (exitCode, stdout, _) = ConsoleCapture.All(() =>
        {
            var command = WatchdogCommand.Create();
            return command.Parse(subcommand).Invoke();
        });
        return (stdout, exitCode);
    }
}
