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
        var commands = new Func<System.CommandLine.Command>[]
        {
            AdoptCommand.Create,
            CheckCommand.Create,
            CleanCommand.Create,
            CompleteCommand.Create,
            CompletionsCommand.Create,
            FixCommand.Create,
            GraphCommand.Create,
            GapCheckCommand.Create,
            GuardCommand.Create,
            HelpCommand.Create,
            IndexCommand.Create,
            InitCommand.Create,
            MapCommand.Create,
            ModeCommand.Create,
            PrReadyCommand.Create,
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
        // Verify the entire CLI can be constructed - mirrors Program.cs registrations
        var exception = Record.Exception(() =>
        {
            var rootCommand = new System.CommandLine.RootCommand("Test")
            {
                AdoptCommand.Create(),
                CheckCommand.Create(),
                CleanCommand.Create(),
                CompleteCommand.Create(),
                CompletionsCommand.Create(),
                FixCommand.Create(),
                GraphCommand.Create(),
                GapCheckCommand.Create(),
                GuardCommand.Create(),
                HelpCommand.Create(),
                IndexCommand.Create(),
                InitCommand.Create(),
                MapCommand.Create(),
                ModeCommand.Create(),
                PrReadyCommand.Create(),
                ValidateCommand.Create(),
            };

            // version is the only command created inline in Program.cs
            rootCommand.Subcommands.Add(new System.CommandLine.Command("version", "Test"));

            // Must match Program.cs: 16 Create() commands + 1 inline (version) = 17
            Assert.Equal(17, rootCommand.Subcommands.Count);
        });

        Assert.Null(exception);
    }
}
