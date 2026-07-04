namespace DynaDocs.Sync.Notion;

using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Gates and runs one <c>dydo notion sync</c> tick (slice brief §6): require a token, a dydo project,
/// and a parent page, then hand off to <see cref="NotionSpineSync"/> to provision and reconcile every
/// object type defined in the project's sync model. Every external dependency — the token, config, and the
/// <see cref="INotionClient"/> factory — is injected, so the whole flow is unit-testable with fakes
/// and no live network; the command supplies the real wiring. Missing token/project/parent report
/// cleanly and exit success (nothing to do); a Notion API failure exits with a tool error.
/// </summary>
public static class NotionSyncService
{
    public static int Execute(
        string? token,
        IConfigService config,
        Func<string, INotionClient> clientFactory,
        bool dryRun,
        TextWriter output,
        TextWriter error,
        bool prune = false)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            error.WriteLine(
                $"notion sync: not configured. Set {NotionTokenResolver.TokenEnvVar} to a Notion integration token to enable sync.");
            return ExitCodes.Success;
        }

        if (config.GetProjectRoot() == null)
        {
            error.WriteLine("notion sync: no dydo.json found; run inside a dydo project.");
            return ExitCodes.Success;
        }

        var parentPageId = NotionParentResolver.Resolve(config.LoadConfig()?.Notion?.ParentPageId);
        if (string.IsNullOrWhiteSpace(parentPageId))
        {
            error.WriteLine(
                $"notion sync: no parent page configured. Set notion.parentPageId in dydo.json or {NotionParentResolver.ParentPageEnvVar} to the page the spine databases live under.");
            return ExitCodes.Success;
        }

        try
        {
            NotionSpineSync.Run(clientFactory(token), config.GetDydoRoot(), parentPageId, dryRun, output, prune);
            return ExitCodes.Success;
        }
        catch (NotionApiException ex)
        {
            // The message carries Notion's HTTP status + error body, never the token.
            error.WriteLine($"notion sync: Notion API error — {ex.Message}");
            return ExitCodes.ToolError;
        }
    }
}
