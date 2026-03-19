namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class InboxItemParserTests : IDisposable
{
    private readonly string _testDir;

    public InboxItemParserTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-inbox-parser-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private string CreateInboxFile(string filename, string content)
    {
        var inboxDir = Path.Combine(_testDir, "inbox");
        Directory.CreateDirectory(inboxDir);
        var path = Path.Combine(inboxDir, filename);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ParseInboxItem_WithFromRole_ParsesCorrectly()
    {
        var path = CreateInboxFile("abc123-my-task.md",
            "---\nid: abc123\nfrom: Brian\nfrom_role: code-writer\nrole: reviewer\ntask: my-task\nreceived: 2026-03-19T10:00:00Z\n---\n\n## Brief\n\nReview this code.");

        var item = InboxItemParser.ParseInboxItem(path);

        Assert.NotNull(item);
        Assert.Equal("code-writer", item!.FromRole);
        Assert.Equal("Brian", item.From);
        Assert.Equal("reviewer", item.Role);
    }

    [Fact]
    public void ParseInboxItem_WithoutFromRole_ReturnsNullFromRole()
    {
        var path = CreateInboxFile("abc123-my-task.md",
            "---\nid: abc123\nfrom: Brian\nrole: reviewer\ntask: my-task\nreceived: 2026-03-19T10:00:00Z\n---\n\n## Brief\n\nReview this code.");

        var item = InboxItemParser.ParseInboxItem(path);

        Assert.NotNull(item);
        Assert.Null(item!.FromRole);
    }

    [Fact]
    public void GetInboxItems_ParsesFromRole()
    {
        CreateInboxFile("abc123-my-task.md",
            "---\nid: abc123\nfrom: Brian\nfrom_role: orchestrator\nrole: code-writer\ntask: my-task\nreceived: 2026-03-19T10:00:00Z\n---\n\n## Brief\n\nImplement feature.");

        var items = InboxItemParser.GetInboxItems(_testDir);

        Assert.Single(items);
        Assert.Equal("orchestrator", items[0].FromRole);
    }
}
