// @test-tier: 2
namespace DynaDocs.Tests.Sync;

using DynaDocs.Models;
using DynaDocs.Sync;

public class SyncDocFileTests
{
    [Fact]
    public void Parse_PreservesFrontmatterOrder()
    {
        const string content = "---\nstatus: open\npriority: high\nassignee: kim\n---\n\n# Title\n\nBody text.";

        var doc = SyncDocFile.Parse(content, "my-task", "tasks/my-task.md");

        Assert.Equal(["status", "priority", "assignee"], doc.Fields.Select(f => f.Key));
        Assert.Equal("kim", doc.GetField("assignee"));
        Assert.Equal("my-task", doc.LocalId);
    }

    [Fact]
    public void Parse_KeepsBodyIntact()
    {
        const string content = "---\nstatus: open\n---\n\n# Heading\n\n- item one\n- item two\n";

        var doc = SyncDocFile.Parse(content, "t", "tasks/t.md");

        Assert.Equal("# Heading\n\n- item one\n- item two\n", doc.Body);
    }

    [Fact]
    public void Parse_ValueWithColon_Preserved()
    {
        const string content = "---\nlink: https://example.com/x\n---\n\nbody";
        var doc = SyncDocFile.Parse(content, "t", "tasks/t.md");
        Assert.Equal("https://example.com/x", doc.GetField("link"));
    }

    [Fact]
    public void Parse_NoFrontmatter_AllBody()
    {
        var doc = SyncDocFile.Parse("# Just a body\n", "t", "tasks/t.md");
        Assert.Empty(doc.Fields);
        Assert.Equal("# Just a body\n", doc.Body);
    }

    [Fact]
    public void Render_RoundTrips_OrderAndBody()
    {
        const string content = "---\nstatus: open\npriority: high\n---\n\n# Title\n\nLine.";
        var doc = SyncDocFile.Parse(content, "t", "tasks/t.md");

        var rendered = SyncDocFile.Render(doc);
        var reparsed = SyncDocFile.Parse(rendered, "t", "tasks/t.md");

        Assert.Equal(doc.Fields.Select(f => (f.Key, f.Value)), reparsed.Fields.Select(f => (f.Key, f.Value)));
        Assert.Equal(doc.Body, reparsed.Body);
    }

    [Fact]
    public void WriteThenRead_RoundTripsFromDisk()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dydo-syncdoc-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(dir, "t.md");
            var doc = new SyncDoc
            {
                LocalId = "t",
                Fields = [new SyncField { Key = "status", Value = "open" }],
                Body = "# T\n\nbody",
                SourcePath = "tasks/t.md",
            };

            SyncDocFile.Write(path, doc);
            var read = SyncDocFile.Read(path, "t", "tasks/t.md");

            Assert.Equal("open", read.GetField("status"));
            Assert.Equal("# T\n\nbody", read.Body);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Parse_OpenDelimiterButNoClose_AllBody()
    {
        // A leading '---' with no closing delimiter is not valid frontmatter: keep the whole
        // content as body rather than swallowing it.
        var doc = SyncDocFile.Parse("---\nstatus: open\nstill body, no close", "t", "tasks/t.md");
        Assert.Empty(doc.Fields);
        Assert.Equal("---\nstatus: open\nstill body, no close", doc.Body);
    }

    [Fact]
    public void Parse_CarriageReturns_Normalized()
    {
        const string content = "---\r\nstatus: open\r\n---\r\n\r\nbody line";
        var doc = SyncDocFile.Parse(content, "t", "tasks/t.md");
        Assert.Equal("open", doc.GetField("status"));
        Assert.Equal("body line", doc.Body);
    }
}
