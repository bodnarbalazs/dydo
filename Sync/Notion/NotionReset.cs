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

        var parentPageId = !string.IsNullOrWhiteSpace(parentPageOverride)
            ? parentPageOverride
            : NotionParentResolver.Resolve(config.LoadConfig()?.Notion?.ParentPageId);
        if (string.IsNullOrWhiteSpace(parentPageId))
        {
            error.WriteLine(
                $"notion reset: no parent page configured. Set notion.parentPageId in dydo.json or {NotionParentResolver.ParentPageEnvVar} to the page the spine databases live under.");
            return ExitCodes.Success;
        }

        var dydoRoot = config.GetDydoRoot();
        var statePath = NotionProvisioner.PathFor(dydoRoot);
        var tracked = NotionProvisioner.LoadTracked(statePath);

        if (dryRun)
        {
            output.WriteLine($"notion reset --dry-run: would archive {tracked.Count} tracked database(s), then recreate from the sync model. Nothing was changed.");
            foreach (var record in tracked)
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

            foreach (var record in tracked)
            {
                client.ArchiveDatabase(record.DatabaseId);
                output.WriteLine($"  archived   {record.ObjectType,-9} database {record.DatabaseId}");
            }

            // Clear state AFTER archiving, never before: an archived-but-still-recorded database would be reused
            // by the reprovision (its validity probe 200s, since Notion returns a trashed database rather than
            // 404ing it), so the tracked ids must be dropped for the spine to mint fresh ones. Deleting the file
            // is the clear — Load treats a missing file as empty state.
            if (File.Exists(statePath))
                File.Delete(statePath);

            // Recreate: the normal spine path mints a fresh database per type (Lookup now returns null) and
            // re-pushes every repo doc as a create. The mint path also deletes each type's stale base snapshot,
            // so the empty new databases are reconciled against an empty base — no spurious mass-delete.
            NotionSpineSync.Run(client, dydoRoot, parentPageId, dryRun: false, output);
            return ExitCodes.Success;
        }
        catch (NotionApiException ex)
        {
            error.WriteLine($"notion reset: Notion API error — {ex.Message}");
            return ExitCodes.ToolError;
        }
    }
}
