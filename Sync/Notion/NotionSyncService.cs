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
        bool prune = false,
        string? parentPageOverride = null,
        bool docs = false,
        bool docsOnly = false,
        bool spineOnly = false)
    {
        // Belt-and-suspenders: the CLI already rejects these combinations, but Execute is also called directly
        // (tests, any future caller), so guard the incoherent scope here too rather than silently favouring one.
        if (docsOnly && spineOnly)
        {
            error.WriteLine("notion sync: --docs-only and --spine-only are mutually exclusive.");
            return ExitCodes.ValidationErrors;
        }

        // --docs adds the mirror, --spine-only skips it — reject rather than silently dropping --docs (issue 0221).
        if (docs && spineOnly)
        {
            error.WriteLine("notion sync: --docs and --spine-only are mutually exclusive.");
            return ExitCodes.ValidationErrors;
        }

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

        // An explicit --parent-page wins over config/env (NotionParentResolver): a smoke can target a SCRATCH
        // page without touching the configured workspace. Absent it, resolution is unchanged.
        var parentPageId = !string.IsNullOrWhiteSpace(parentPageOverride)
            ? parentPageOverride
            : NotionParentResolver.Resolve(config.LoadConfig()?.Notion?.ParentPageId);
        if (string.IsNullOrWhiteSpace(parentPageId))
        {
            error.WriteLine(
                $"notion sync: no parent page configured. Set notion.parentPageId in dydo.json or {NotionParentResolver.ParentPageEnvVar} to the page the spine databases live under.");
            return ExitCodes.Success;
        }

        try
        {
            var client = clientFactory(token);
            // Two Notion surfaces under the same parent page (DR 033): the queryable PM spine of databases, then
            // the browsable docs mirror of nested pages. The docs mirror is OPT-IN (release safety): the default
            // run is the spine ONLY. --docs adds the mirror (spine + docs); --docs-only runs the mirror alone;
            // --spine-only is the explicit spine-only (== default). A docs smoke must never touch the live PM board.
            var runSpine = !docsOnly;
            var runDocs = !spineOnly && (docs || docsOnly);
            if (runSpine)
                NotionSpineSync.Run(client, config.GetDydoRoot(), parentPageId, dryRun, output, prune);
            if (runDocs)
                DocsTreeSync.Run(client, config.GetDydoRoot(), parentPageId, dryRun, output);
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
