namespace DynaDocs.Tests.Sync;

using DynaDocs.Models;
using DynaDocs.Sync;
using DynaDocs.Sync.Model;

/// <summary>
/// Status-driven repo folder routing (slice brief §3): a doc's canonical path is derived from its status,
/// so folder placement is presentation and frontmatter status is canonical.
/// </summary>
public class RepoFolderLayoutTests
{
    private static List<SyncField> F(params (string, string)[] fields) =>
        fields.Select(f => new SyncField { Key = f.Item1, Value = f.Item2 }).ToList();

    [Fact]
    public void RoutesMappedStatusIntoSubfolder_UnmappedToRoot()
    {
        var layout = new RepoFolderLayout("root", "status", new Dictionary<string, string> { ["closed"] = "closed" });

        Assert.Equal(Path.Combine("root", "closed", "bug.md"), layout.PathFor("bug", F(("status", "closed"))));
        Assert.Equal(Path.Combine("root", "bug.md"), layout.PathFor("bug", F(("status", "open"))));
        Assert.Equal(Path.Combine("root", "bug.md"), layout.PathFor("bug", F(("title", "x")))); // no status -> root
    }

    [Fact]
    public void OptionValueMatch_IsCaseInsensitive()
    {
        // The status field name is matched case-insensitively; its option VALUE must be too, so a frontmatter
        // 'status: Resolved' routes the same as 'resolved' (finding 3).
        var layout = new RepoFolderLayout("root", "status", new Dictionary<string, string> { ["resolved"] = "resolved" });

        Assert.Equal(Path.Combine("root", "resolved", "i.md"), layout.PathFor("i", F(("status", "Resolved"))));
    }

    [Fact]
    public void UnmappedStatus_WithCurrentPath_KeepsExistingPath()
    {
        // An unmapped status never moves an existing doc: it keeps its current path. Only a doc with no
        // current path lands at the root (finding 1).
        var layout = new RepoFolderLayout("root", "status", new Dictionary<string, string> { ["resolved"] = "resolved" });

        var existing = Path.Combine("root", "archive", "i.md");
        Assert.Equal(existing, layout.PathFor("i", F(("status", "open")), existing));
        Assert.Equal(Path.Combine("root", "i.md"), layout.PathFor("i", F(("status", "open")), null));
    }

    [Fact]
    public void NoRouting_KeepsEveryDocFlat()
    {
        var layout = new RepoFolderLayout("root", null, null);

        Assert.Equal(Path.Combine("root", "bug.md"), layout.PathFor("bug", F(("status", "closed"))));
    }

    [Fact]
    public void For_BuildsRoutingFromModelTypesFoldersMap()
    {
        var type = new SyncObjectType
        {
            Type = "Issue",
            Dir = "project/issues",
            Properties = new Dictionary<string, SyncPropertyDef>
            {
                ["title"] = new() { Type = "title" },
                ["status"] = new() { Type = "select", Options = ["open", "closed"], Folders = new() { ["closed"] = "closed" } },
            },
        };

        var layout = RepoFolderLayout.For(type, "project/issues");

        Assert.Equal(Path.Combine("project/issues", "closed", "i.md"), layout.PathFor("i", F(("status", "closed"))));
        Assert.Equal(Path.Combine("project/issues", "i.md"), layout.PathFor("i", F(("status", "open"))));
    }
}
