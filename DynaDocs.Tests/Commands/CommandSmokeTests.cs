namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;

/// <summary>
/// Smoke tests to verify all commands can be instantiated without throwing.
/// This catches issues like invalid Option constructor parameters.
/// </summary>
public class CommandSmokeTests
{
    [Fact]
    public void AllCommands_CanBeInstantiated_WithoutThrowing()
    {
        // This test ensures command construction doesn't throw due to
        // issues like invalid aliases, missing parameters, etc.
        var commands = new Func<System.CommandLine.Command>[]
        {
            CheckCommand.Create,
            CompleteCommand.Create,
            CompletionsCommand.Create,
            FixCommand.Create,
            GraphCommand.Create,
            GapCheckCommand.Create,
            GuardCommand.Create,
            HelpCommand.Create,
            IndexCommand.Create,
            InitCommand.Create,
            ValidateCommand.Create
        };

        foreach (var createCommand in commands)
        {
            var exception = Record.Exception(() => createCommand());
            Assert.Null(exception);
        }
    }

    [Fact]
    public void RootCommand_CanBeBuilt_WithAllSubcommands()
    {
        // Verify the entire CLI can be constructed — mirrors Program.cs registrations
        var exception = Record.Exception(() =>
        {
            var rootCommand = new System.CommandLine.RootCommand("Test")
            {
                CheckCommand.Create(),
                CompleteCommand.Create(),
                CompletionsCommand.Create(),
                FixCommand.Create(),
                GraphCommand.Create(),
                GapCheckCommand.Create(),
                GuardCommand.Create(),
                HelpCommand.Create(),
                IndexCommand.Create(),
                InitCommand.Create(),
                ValidateCommand.Create(),
            };

            // version is the only command created inline in Program.cs
            rootCommand.Subcommands.Add(new System.CommandLine.Command("version", "Test"));

            // Must match Program.cs: 11 Create() commands + 1 inline (version) = 12
            Assert.Equal(12, rootCommand.Subcommands.Count);
        });

        Assert.Null(exception);
    }
}
