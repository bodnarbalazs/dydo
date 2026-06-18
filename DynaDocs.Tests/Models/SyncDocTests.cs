namespace DynaDocs.Tests.Models;

using DynaDocs.Models;

public class SyncDocTests
{
    private static SyncDoc Doc(params (string Key, string Value)[] fields) => new()
    {
        LocalId = "t",
        Fields = fields.Select(f => new SyncField { Key = f.Key, Value = f.Value }).ToList(),
        Body = "body",
        SourcePath = "tasks/t.md",
    };

    [Fact]
    public void GetField_PresentKey_ReturnsValue()
    {
        Assert.Equal("open", Doc(("status", "open")).GetField("status"));
    }

    [Fact]
    public void GetField_IsCaseInsensitive()
    {
        Assert.Equal("open", Doc(("Status", "open")).GetField("status"));
    }

    [Fact]
    public void GetField_MissingKey_ReturnsNull()
    {
        Assert.Null(Doc(("status", "open")).GetField("priority"));
    }

    [Fact]
    public void GetField_NoFields_ReturnsNull()
    {
        Assert.Null(Doc().GetField("status"));
    }
}
