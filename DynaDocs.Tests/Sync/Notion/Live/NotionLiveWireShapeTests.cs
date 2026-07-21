namespace DynaDocs.Tests.Sync.Notion.Live;

using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// LIVE (ns-9, ns-5): the recovery DTOs the create/view re-query paths depend on actually deserialize non-null
/// from real Notion responses. The fake hand-builds these shapes, so a field Notion names differently (or nests
/// elsewhere) would pass the fake yet leave the ambiguous-create recovery unable to match a lost create. This
/// creates one database and asserts the three live shapes the recovery reads: <c>RetrieveDatabase.parent.page_id</c>,
/// <c>ListViews[].name</c>, and a <c>SearchDataSources</c> hit's <c>name</c> + <c>parent.database_id</c>.
/// </summary>
[Trait("Category", "notion-live")]
public sealed class NotionLiveWireShapeTests : NotionLiveTestBase
{
    [NotionLiveFact]
    public void RecoveryDtos_DeserializeNonNull_FromLiveResponses()
    {
        var title = ScratchName();
        var db = Client.CreateDatabase(new NotionDatabaseCreateRequest
        {
            Parent = new NotionDatabaseParent { PageId = ChildPageId },
            Title = NotionRichText.Of(title),
            InitialDataSource = new NotionInitialDataSource
            {
                Properties = new Dictionary<string, NotionPropertySchema> { ["title"] = new() { Title = new NotionEmptyConfig() } },
            },
        });

        // RetrieveDatabase exposes the owning page (the parent-page adoption guard reads it).
        var retrieved = Client.RetrieveDatabase(db.Id);
        Assert.NotNull(retrieved.Parent);
        Assert.False(string.IsNullOrEmpty(retrieved.Parent!.PageId));

        // ListViews exposes each view's name (the CreateView recovery matches by name).
        var views = Client.ListViews(db.Id);
        Assert.NotEmpty(views);
        Assert.All(views, v => Assert.False(string.IsNullOrEmpty(v.Name)));

        // A SearchDataSources hit exposes its name and its owning database id (the CreateDatabase recovery adopts it).
        // Notion search is eventually consistent, so poll for the just-created data source to be indexed (up to 3
        // attempts, 2s apart) before falling back to any hit. An empty workspace with search still cold could
        // legitimately return zero hits — the assert message says so.
        NotionSearchResult? hit = null;
        for (var attempt = 0; attempt < 3 && hit == null; attempt++)
        {
            if (attempt > 0)
                Thread.Sleep(TimeSpan.FromSeconds(2));
            var hits = Client.SearchDataSources();
            hit = hits.FirstOrDefault(h => h.Name == title) ?? hits.FirstOrDefault();
        }
        Assert.True(hit != null,
            "SearchDataSources returned no data sources after 3 attempts — the just-created database should be "
            + "indexed by now; a completely empty workspace with a cold search index is the only benign cause.");
        Assert.False(string.IsNullOrEmpty(hit!.Name));
        Assert.NotNull(hit.Parent);
        Assert.False(string.IsNullOrEmpty(hit.Parent!.DatabaseId));
    }
}
