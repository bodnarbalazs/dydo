namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;

public class CleanCommandTests
{
    [Fact]
    public void Create_ReturnsCommand()
    {
        var command = CleanCommand.Create();

        Assert.NotNull(command);
        Assert.Equal("clean", command.Name);
    }

    [Fact]
    public void Create_HasExpectedOptions()
    {
        var command = CleanCommand.Create();

        Assert.Equal(3, command.Options.Count);
    }

    [Fact]
    public void Create_HasAgentArgument()
    {
        var command = CleanCommand.Create();

        Assert.Single(command.Arguments);
        Assert.Equal("agent", command.Arguments[0].Name);
    }
}
