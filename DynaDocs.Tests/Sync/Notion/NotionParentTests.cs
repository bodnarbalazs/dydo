namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Notion.Dtos;

public class NotionParentTests
{
    [Fact]
    public void DataSource_Factory_SetsDataSourceParent_LeavesPageIdNull()
    {
        var parent = NotionParent.DataSource("ds-1");

        Assert.Equal("data_source_id", parent.Type);
        Assert.Equal("ds-1", parent.DataSourceId);
        Assert.Null(parent.PageId); // the other id stays null so only one is serialized (WhenWritingNull)
    }

    [Fact]
    public void Page_Factory_SetsPageParent_LeavesDataSourceIdNull()
    {
        var parent = NotionParent.Page("page-1");

        Assert.Equal("page_id", parent.Type);
        Assert.Equal("page-1", parent.PageId);
        Assert.Null(parent.DataSourceId);
    }
}
