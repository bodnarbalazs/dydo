namespace DynaDocs.Tests.Sync.Notion.Live;

using DynaDocs.Sync.Model;
using DynaDocs.Sync.Notion.Dtos;
using DynaDocs.Sync.Notion.Provisioning;

/// <summary>
/// LIVE (ns-9, issue 0278): the <c>FutureFeature</c> type provisions with its select options intact and a row
/// created with a title actually renders that title. 0278 escaped because the fake reports whatever schema it is
/// handed; only a live provision confirms the status options land on the real data source and that a page's title
/// reads back non-empty.
/// </summary>
[Trait("Category", "notion-live")]
public sealed class NotionLiveFutureFeatureTests : NotionLiveTestBase
{
    [NotionLiveFact]
    public void FutureFeature_TitleRenders_StatusOptionsPresent()
    {
        var dydoRoot = Path.Combine(Path.GetTempPath(), "dydo-live-ff-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var type = SyncModelLoader.Load(dydoRoot).Object("FutureFeature");
            var statePath = Path.Combine(dydoRoot, "provision.json");
            var provisioner = new NotionProvisioner(Client, statePath, TextWriter.Null);
            var record = provisioner.Create(type, ChildPageId, new Dictionary<string, string>());

            // The live data source carries a title-typed property and the four declared status options.
            var dataSource = Client.RetrieveDataSource(record.DataSourceId);
            var titleProp = dataSource.Properties.First(p => p.Value.Title != null).Key;
            var statusOptions = dataSource.Properties["status"].Select!.Options.Select(o => o.Name).ToList();
            Assert.Equal(new[] { "raw", "shaping", "promoted", "dropped" }.OrderBy(x => x), statusOptions.OrderBy(x => x));

            // A created row's title renders (0278's class): read it back non-empty.
            Client.CreatePage(new NotionPageCreateRequest
            {
                Parent = NotionParent.DataSource(record.DataSourceId),
                Properties = new()
                {
                    [titleProp] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("A bright idea") },
                    ["status"] = new NotionPropertyValue { Type = "select", Select = new NotionSelectOption { Name = "shaping" } },
                },
            });

            var page = Assert.Single(Client.QueryDataSource(record.DataSourceId));
            var title = NotionRichText.Flatten(page.Properties.Values.First(v => v.Type == "title").Title);
            Assert.Equal("A bright idea", title);
        }
        finally
        {
            if (Directory.Exists(dydoRoot)) Directory.Delete(dydoRoot, recursive: true);
        }
    }
}
