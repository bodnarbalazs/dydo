namespace DynaDocs.Tests.Sync.Notion.Live;

using DynaDocs.Sync.Model;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Provisioning;

/// <summary>
/// LIVE (ns-9): the whole spine provisions against real Notion — all seven default object types mint their
/// databases AND every inlined attention formula/rollup is accepted. The fake treats a formula as an opaque
/// string, so a rejected expression (the 0290-era "Type error with formula" that a formula-referencing-a-formula
/// triggers) is invisible to CI; here <see cref="NotionSpineSync.Run"/> throws a <c>NotionApiException</c> if any
/// schema push is rejected, so a green run proves the live schema is accepted end to end.
/// </summary>
[Trait("Category", "notion-live")]
public sealed class NotionLiveSpineProvisioningTests : NotionLiveTestBase
{
    [NotionLiveFact]
    public void SpineProvisions_AllSevenTypes_FormulasAccepted()
    {
        var dydoRoot = Path.Combine(Path.GetTempPath(), "dydo-live-spine-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            // Auto-seeds the built-in seven-type model; provision + reconcile (no docs) under the scratch child page.
            var expectedTypes = SyncModelLoader.Load(dydoRoot).Objects.Count;
            var state = NotionSpineState.Resolve(dydoRoot, null, ChildPageId, dryRun: false, TextWriter.Null);

            NotionSpineSync.Run(Client, state, dryRun: false, TextWriter.Null);

            var tracked = NotionProvisioner.LoadTracked(state.ProvisionPath);
            Assert.Equal(expectedTypes, tracked.Count);
            Assert.All(tracked, t =>
            {
                Assert.NotEmpty(t.DatabaseId);
                Assert.NotEmpty(t.DataSourceId);
                // Post-pass completed for every type ⇒ its rollups + attention/health formulas were accepted live.
                Assert.True(t.PostPassDone, $"{t.ObjectType} post-pass (rollups/formulas) did not complete");
            });
        }
        finally
        {
            if (Directory.Exists(dydoRoot)) Directory.Delete(dydoRoot, recursive: true);
        }
    }
}
