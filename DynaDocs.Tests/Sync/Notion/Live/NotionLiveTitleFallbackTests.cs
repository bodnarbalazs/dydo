namespace DynaDocs.Tests.Sync.Notion.Live;

using DynaDocs.Models;
using DynaDocs.Sync;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;
using DynaDocs.Utils;

/// <summary>
/// LIVE (ns-9, issue 0290): a spine doc with NO <c>title:</c> frontmatter still creates a row whose board
/// title is a real, prettified string — never Notion's blank "New page". The fake stores whatever property
/// map it is handed, so it cannot show that the title-typed property must actually be populated for a title to
/// render. Here the adapter's write-side fallback runs against a live data source and the created page is read
/// back to confirm the title surfaced.
/// </summary>
[Trait("Category", "notion-live")]
public sealed class NotionLiveTitleFallbackTests : NotionLiveTestBase
{
    [NotionLiveFact]
    public void Create_WithoutTitleField_RendersPrettifiedLocalId()
    {
        var dataSourceId = CreateTitleAndStatusDatabase();
        var schema = new Dictionary<string, string> { ["title"] = "title", ["status"] = "select" };
        var adapter = new NotionSyncAdapter(Client, dataSourceId, schema);

        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert
        {
            LocalId = "swarm-0119",
            ExternalId = null,
            Fields = new List<SyncField>(), // deliberately no title/name — the fallback must fill it
            Body = "",
        });
        adapter.Apply(changes, new Dictionary<string, string>());

        var page = Assert.Single(Client.QueryDataSource(dataSourceId));
        var title = NotionRichText.Flatten(page.Properties.Values.First(v => v.Type == "title").Title);
        Assert.NotEmpty(title);
        Assert.Equal(TitlePrettifier.Prettify("swarm-0119"), title);
    }

    private string CreateTitleAndStatusDatabase()
    {
        var db = Client.CreateDatabase(new NotionDatabaseCreateRequest
        {
            Parent = new NotionDatabaseParent { PageId = ChildPageId },
            Title = NotionRichText.Of(ScratchName()),
            InitialDataSource = new NotionInitialDataSource
            {
                Properties = new Dictionary<string, NotionPropertySchema>
                {
                    ["title"] = new() { Title = new NotionEmptyConfig() },
                    ["status"] = new() { Select = new NotionSelectSchema { Options = [new() { Name = "a" }, new() { Name = "b" }] } },
                },
            },
        });
        return db.DataSources[0].Id;
    }
}
