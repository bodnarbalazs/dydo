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
            AgentCommand.Create,
            AuditCommand.Create,
            CheckCommand.Create,
            CleanCommand.Create,
            DispatchCommand.Create,
            FixCommand.Create,
            GraphCommand.Create,
            GuardCommand.Create,
            InboxCommand.Create,
            IndexCommand.Create,
            InitCommand.Create,
            ReviewCommand.Create,
            TaskCommand.Create,
            WhoamiCommand.Create,
            WorkspaceCommand.Create
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
        // Verify the entire CLI can be constructed
        var exception = Record.Exception(() =>
        {
            var rootCommand = new System.CommandLine.RootCommand("Test")
            {
                AgentCommand.Create(),
                AuditCommand.Create(),
                CheckCommand.Create(),
                CleanCommand.Create(),
                DispatchCommand.Create(),
                FixCommand.Create(),
                GraphCommand.Create(),
                GuardCommand.Create(),
                InboxCommand.Create(),
                IndexCommand.Create(),
                InitCommand.Create(),
                ReviewCommand.Create(),
                TaskCommand.Create(),
                WhoamiCommand.Create(),
                WorkspaceCommand.Create()
            };

            // Verify subcommands are present
            Assert.True(rootCommand.Subcommands.Count >= 15);
        });

        Assert.Null(exception);
    }
}
