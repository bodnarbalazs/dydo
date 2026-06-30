namespace DynaDocs.Commands;

using System.CommandLine;
using System.Net.Http;
using DynaDocs.Services;
using DynaDocs.Sync;
using DynaDocs.Sync.Notion;

/// <summary>
/// The <c>dydo notion sync</c> command (Decision 025 §5). Resolves the integration token and parent
/// page, then provisions and reconciles every object type in the project's sync model through the real
/// <see cref="NotionSyncAdapter"/>. When the token or parent page is missing it reports cleanly and
/// exits without any network call. <c>--dry-run</c> previews the reconcile plan.
/// </summary>
public static class NotionCommand
{
    public const string TokenEnvVar = NotionTokenResolver.TokenEnvVar;

    public static Command Create()
    {
        var command = new Command("notion", "Sync dydo docs with a Notion workspace");
        command.Subcommands.Add(CreateSyncCommand());
        return command;
    }

    private static Command CreateSyncCommand()
    {
        var command = new Command("sync", "Reconcile dydo docs against the configured Notion workspace");
        var dryRun = new Option<bool>("--dry-run")
        {
            Description = "Compute and print the reconcile plan without applying any change.",
        };
        command.Options.Add(dryRun);
        command.SetAction(parse => RunSync(parse.GetValue(dryRun)));
        return command;
    }

    private static int RunSync(bool dryRun) =>
        NotionSyncService.Execute(
            NotionTokenResolver.Resolve(),
            new ConfigService(),
            CreateClient,
            dryRun,
            Console.Out,
            Console.Error);

    /// <summary>The real transport: a fresh <see cref="HttpClient"/> wrapped by <see cref="NotionClient"/>.
    /// The handler is owned by the client for the process lifetime of one sync invocation.</summary>
    private static INotionClient CreateClient(string token) => new NotionClient(new HttpClient(), token);
}
