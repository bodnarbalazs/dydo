namespace DynaDocs.Sync.Notion;

using DynaDocs.Services;
using DynaDocs.Sync.Model;
using DynaDocs.Sync.Notion.Provisioning;
using DynaDocs.Utils;

/// <summary>
/// Runs <c>dydo notion reset</c> (backlog: notion-reset-command): wipe the tracked Notion databases then
/// recreate them fresh from the sync model, so a manually-messed board matches the model again — something
/// <c>dydo notion sync</c> (a forward, create-only reconcile) cannot do. The wipe ARCHIVES the tracked
/// databases by their recorded ids and only THEN clears provision state; deleting state first would orphan the
/// old databases (their ids lost) and the reprovision would duplicate them. After the wipe the normal spine
/// provision path re-mints every database and re-pushes every repo doc. <c>--dry-run</c> prints the archive +
/// recreate plan and writes nothing; a real run confirms destructively before touching Notion. Gating mirrors
/// <see cref="NotionSyncService"/>: missing token/project/parent report cleanly and exit success.
/// </summary>
public static class NotionReset
{
    public static int Execute(
        string? token,
        IConfigService config,
        Func<string, INotionClient> clientFactory,
        bool dryRun,
        Func<bool> confirm,
        TextWriter output,
        TextWriter error,
        string? parentPageOverride = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            error.WriteLine(
                $"notion reset: not configured. Set {NotionTokenResolver.TokenEnvVar} to a Notion integration token to enable reset.");
            return ExitCodes.Success;
        }

        if (config.GetProjectRoot() == null)
        {
            error.WriteLine("notion reset: no dydo.json found; run inside a dydo project.");
            return ExitCodes.Success;
        }

        var configuredParentPageId = config.LoadConfig()?.Notion?.ParentPageId;
        var parentPageId = !string.IsNullOrWhiteSpace(parentPageOverride)
            ? parentPageOverride
            : NotionParentResolver.Resolve(configuredParentPageId);
        if (string.IsNullOrWhiteSpace(parentPageId))
        {
            error.WriteLine(
                $"notion reset: no parent page configured. Set notion.parentPageId in dydo.json or {NotionParentResolver.ParentPageEnvVar} to the page the spine databases live under.");
            return ExitCodes.Success;
        }

        var dydoRoot = config.GetDydoRoot();

        if (dryRun)
        {
            // Preview through the SAME decision point (issue 0257 CRITICAL 2), which reads the legacy files a real
            // run would migrate first — so the archive count reflects the real plan, not a not-yet-created scoped file.
            var preview = NotionSpineState.Resolve(dydoRoot, configuredParentPageId, parentPageOverride, dryRun: true, output);
            var previewTracked = NotionProvisioner.LoadTracked(preview.ProvisionPath);
            output.WriteLine($"notion reset --dry-run: would archive {previewTracked.Count} tracked database(s), then recreate from the sync model. Nothing was changed.");
            foreach (var record in previewTracked)
                output.WriteLine($"  archive    {record.ObjectType,-9} database {record.DatabaseId}");
            foreach (var type in SyncModelLoader.Load(dydoRoot).InDependencyOrder())
                output.WriteLine($"  recreate   {type.Type,-9} \"{type.NotionTitle}\"");
            return ExitCodes.Success;
        }

        if (!confirm())
        {
            output.WriteLine("notion reset: aborted, nothing changed.");
            return ExitCodes.Success;
        }

        try
        {
            var client = clientFactory(token);

            // Resolve — and migrate legacy state — only AFTER the destructive confirm, so a declined reset renames
            // nothing and the "nothing changed" message above stays honest. Reset then operates only on the parent it
            // resolved, so a scratch --parent-page never touches the configured board's tracked databases or snapshots.
            var state = NotionSpineState.Resolve(dydoRoot, configuredParentPageId, parentPageOverride, dryRun: false, output);
            var tracked = NotionProvisioner.LoadTracked(state.ProvisionPath);

            foreach (var record in tracked)
            {
                client.ArchiveDatabase(record.DatabaseId);
                output.WriteLine($"  archived   {record.ObjectType,-9} database {record.DatabaseId}");
            }

            // Clear state AFTER archiving, never before: an archived-but-still-recorded database would be reused
            // by the reprovision (its validity probe 200s, since Notion returns a trashed database rather than
            // 404ing it), so the tracked ids must be dropped for the spine to mint fresh ones. Deleting the file
            // is the clear — Load treats a missing file as empty state. Only THIS parent's scoped provision file is
            // removed, so every other parent's state survives.
            if (File.Exists(state.ProvisionPath))
                File.Delete(state.ProvisionPath);

            // Recreate: the normal spine path mints a fresh database per type (Lookup now returns null) and
            // re-pushes every repo doc as a create. The mint path also deletes each type's stale base snapshot,
            // so the empty new databases are reconciled against an empty base — no spurious mass-delete, so the
            // fuse is untrippable here today. Still map any trip to a tool error the same way NotionSyncService
            // does, so a future path that could trip a reset never exits Success silently.
            var result = NotionSpineSync.Run(client, state, dryRun: false, output);
            return result.FuseTripped ? ExitCodes.ToolError : ExitCodes.Success;
        }
        catch (NotionApiException ex)
        {
            error.WriteLine($"notion reset: Notion API error — {ex.Message}");
            return ExitCodes.ToolError;
        }
    }
}
