namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Utils;

/// <summary>
/// The <c>dydo notion</c> command shell (Decision 025 §5). The sync engine is Notion-agnostic
/// and lives behind <c>ISyncAdapter</c>; the real Notion REST adapter and live calls are a later
/// slice. Until a token is configured, <c>notion sync</c> reports the not-configured state and
/// exits cleanly rather than attempting a network call.
/// </summary>
public static class NotionCommand
{
    public const string TokenEnvVar = "DYDO_NOTION_TOKEN";

    public static Command Create()
    {
        var command = new Command("notion", "Sync dydo docs with a Notion workspace");
        command.Subcommands.Add(CreateSyncCommand());
        return command;
    }

    private static Command CreateSyncCommand()
    {
        var command = new Command("sync", "Reconcile dydo docs against the configured Notion workspace");
        command.SetAction(_ => RunSync());
        return command;
    }

    private static int RunSync()
    {
        var token = Environment.GetEnvironmentVariable(TokenEnvVar);
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine(
                $"notion sync: not configured. Set {TokenEnvVar} to a Notion integration token to enable sync.");
            return ExitCodes.Success;
        }

        // A token is present but the live Notion adapter ships in a later slice; there is no
        // adapter to run yet, so report that rather than pretend to sync.
        Console.Error.WriteLine(
            "notion sync: the Notion adapter is not available in this build. Sync engine is ready; "
            + "live Notion connectivity ships in a later slice.");
        return ExitCodes.Success;
    }
}
