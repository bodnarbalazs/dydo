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
        bool spineOnly = false,
        bool allowMassDelete = false)
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
        var configuredParentPageId = config.LoadConfig()?.Notion?.ParentPageId;
        var parentPageId = !string.IsNullOrWhiteSpace(parentPageOverride)
            ? parentPageOverride
            : NotionParentResolver.Resolve(configuredParentPageId);
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
            var fuseTripped = false;
            if (runSpine)
            {
                // The one decision point resolves every parent-scoped spine state path AND migrates legacy
                // project-scoped files, identically for sync and reset (issue 0257).
                var state = NotionSpineState.Resolve(
                    config.GetDydoRoot(), configuredParentPageId, parentPageOverride, dryRun, output);
                fuseTripped = NotionSpineSync.Run(client, state, dryRun, output, prune, allowMassDelete).FuseTripped;
            }
            if (runDocs)
                DocsTreeSync.Run(client, config.GetDydoRoot(), parentPageId, dryRun, output);
            // A tripped mass-delete fuse (slice ns-2) is a tool error the operator must see and act on — every
            // other type still reconciled, so this is reported only after the whole run, mapping the trip to a
            // non-zero exit rather than a silent success.
            return fuseTripped ? ExitCodes.ToolError : ExitCodes.Success;
        }
        catch (NotionApiException ex)
        {
            // The message carries Notion's HTTP status + error body, never the token.
            error.WriteLine($"notion sync: Notion API error — {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    /// <summary>Validate the sync daemon's config exactly as <see cref="Execute"/> does (ns-13), returning a
    /// human-readable reason the daemon must REFUSE TO START — a missing token, no dydo project, or no configured
    /// parent page — or null when everything the daemon needs is present. The daemon dies on these startup config
    /// errors (never a silent idle loop) but survives every later API/sync error.</summary>
    public static string? DaemonConfigError(string? token, IConfigService config)
    {
        if (string.IsNullOrWhiteSpace(token))
            return $"not configured — set {NotionTokenResolver.TokenEnvVar} to a Notion integration token.";
        if (config.GetProjectRoot() == null)
            return "no dydo.json found; run the daemon inside a dydo project.";
        if (string.IsNullOrWhiteSpace(NotionParentResolver.Resolve(config.LoadConfig()?.Notion?.ParentPageId)))
            return "no parent page configured — set notion.parentPageId in dydo.json or "
                + $"{NotionParentResolver.ParentPageEnvVar}.";
        return null;
    }

    /// <summary>Run one cheap daemon tick against the CONFIGURED board, spine-only (ns-13). Resolves the same
    /// parent-scoped state the manual sync uses, then delegates to <see cref="NotionSpineDelta"/>: a filtered remote
    /// query + local mtime scan feed only the changed-id union to the reconcile engine. Assumes the config was
    /// validated at startup (<see cref="DaemonConfigError"/>). A <see cref="NotionApiException"/> propagates so the
    /// daemon can log it and retry next tick — it never dies on a sync error.</summary>
    public static NotionDeltaTickResult DeltaTick(
        string token, IConfigService config, Func<string, INotionClient> clientFactory,
        bool census, bool validateProvisioning, bool allowMassDelete = false)
    {
        var client = clientFactory(token);
        var configuredParentPageId = config.LoadConfig()?.Notion?.ParentPageId;
        var state = NotionSpineState.Resolve(
            config.GetDydoRoot(), configuredParentPageId, parentPageOverride: null, dryRun: false, TextWriter.Null);
        return NotionSpineDelta.Run(client, state, census, validateProvisioning, allowMassDelete);
    }
}
