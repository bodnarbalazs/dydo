namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;

public class InquisitionCommandTests
{
    [Fact]
    public void InquisitionCommand_Create_HasCoverageSubcommand()
    {
        var command = InquisitionCommand.Create();

        Assert.Equal("inquisition", command.Name);
        Assert.Single(command.Subcommands);
        Assert.Equal("coverage", command.Subcommands[0].Name);
    }
}
