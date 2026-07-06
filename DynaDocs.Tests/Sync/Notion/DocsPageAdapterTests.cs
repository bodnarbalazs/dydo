namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

public class DocsPageAdapterTests : IDisposable
{
    private readonly string _dir;

    public DocsPageAdapterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dydo-docs-adapter-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private static NotionBlock ChildPageBlock(string id, string title) => new()
    {
        Type = "child_page", Id = id, ChildPage = new NotionChildPageBody { Title = title },
    };

    /// <summary>Seed the fake with a "Docs" root page holding one managed child page, and return
    /// (client, rootId, childId). Uses CreatePage so the fake tracks parent/title for GetChildPages.</summary>
    private static (FakeNotionClient Client, string Root, string Child) SeedTree(string childBody)
    {
        var client = new FakeNotionClient();
        var root = client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page("workspace"),
            Properties = new() { ["title"] = new() { Type = "title", Title = NotionRichText.Of("Docs") } },
        }).Id;
        var child = client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page(root),
            Properties = new() { ["title"] = new() { Type = "title", Title = NotionRichText.Of("Guide") } },
        }).Id;
        // Append (not CreatePage children) so the body blocks receive ids, as real Notion assigns them —
        // a body replace must be able to delete the old blocks by id.
        if (childBody.Length != 0)
            client.AppendBlockChildren(child, new NotionAppendChildrenRequest { Children = NotionBlockConverter.ToBlocks(childBody) });
        return (client, root, child);
    }

    private DocsPageAdapter AdapterOver(FakeNotionClient client, string root, params string[] managed) =>
        new(client, root, new Dictionary<string, string>(), new Dictionary<string, string>(),
            new HashSet<string>(managed));

    [Fact]
    public void ReadExternalState_WalksTree_ReadingRootAndManagedChildBodies()
    {
        var (client, root, child) = SeedTree("Child body line");
        client.SetBlockChildren(root, NotionBlockConverter.ToBlocks("Root index body"));
        var adapter = AdapterOver(client, root, root, child);

        var records = adapter.ReadExternalState();

        Assert.Equal(2, records.Count);
        Assert.Equal("Root index body", records.Single(r => r.ExternalId == root).Body);
        Assert.Equal("Child body line", records.Single(r => r.ExternalId == child).Body);
        Assert.All(records, r => Assert.Empty(r.Fields)); // frontmatter never surfaces as page fields
    }

    [Fact]
    public void ReadExternalState_IgnoresUnmanagedStrayPages_NeverAdopted()
    {
        var (client, root, child) = SeedTree("");
        // A colleague creates a stray page under the Docs root; it is not in the managed set.
        var stray = client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page(root),
            Properties = new() { ["title"] = new() { Type = "title", Title = NotionRichText.Of("Colleague note") } },
        }).Id;

        var records = AdapterOver(client, root, root, child).ReadExternalState();

        Assert.DoesNotContain(records, r => r.ExternalId == stray);
        Assert.Contains(records, r => r.ExternalId == child);
    }

    [Fact]
    public void Apply_Create_PostsChildPage_UnderMappedParent_WithTitleAndBody()
    {
        var (client, root, _) = SeedTree("");
        var adapter = new DocsPageAdapter(
            client, root,
            new Dictionary<string, string> { ["understand/architecture"] = root },
            new Dictionary<string, string> { ["understand/architecture"] = "Architecture" },
            new HashSet<string> { root });

        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert
        {
            LocalId = "understand/architecture", ExternalId = null,
            Fields = [], Body = "# Architecture\n\nbody",
        });
        var assigned = new Dictionary<string, string>();
        adapter.Apply(changes, assigned);

        var newId = assigned["understand/architecture"];
        var created = client.GetChildPages(root).Single(p => p.Id == newId);
        Assert.Equal("Architecture", created.Title);
        // The blank line collapses through the intentionally lossy converter (one block per non-blank line).
        Assert.Equal("# Architecture\nbody", NotionBlockConverter.FromBlocks(client.GetBlockChildren(newId)));
    }

    [Fact]
    public void Apply_Update_ReplacesBody_WithoutArchivingNestedChildPages()
    {
        // A folder page holds body blocks AND a nested child_page. Replacing its body must delete the body
        // blocks but never the child_page (that would archive the sub-page — structure is repo-owned).
        var client = new FakeNotionClient();
        var root = client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page("workspace"),
            Properties = new() { ["title"] = new() { Type = "title", Title = NotionRichText.Of("Docs") } },
        }).Id;
        client.SetBlockChildren(root, [
            new NotionBlock { Type = "paragraph", Id = "body-1", Paragraph = new NotionBlockBody { RichText = NotionRichText.Of("old body") } },
            ChildPageBlock("nested-page", "Sub"),
        ]);
        var adapter = AdapterOver(client, root, root);

        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert { LocalId = ".", ExternalId = root, Fields = [], Body = "new body" });
        adapter.Apply(changes, new Dictionary<string, string>());

        Assert.Contains("body-1", client.DeletedBlocks);        // stale body block removed
        Assert.DoesNotContain("nested-page", client.DeletedBlocks); // nested sub-page preserved
        Assert.Equal("new body", NotionBlockConverter.FromBlocks(client.GetBlockChildren(root)));
    }

    [Fact]
    public void Apply_Delete_ArchivesPage()
    {
        var (client, root, child) = SeedTree("");
        var adapter = AdapterOver(client, root, root, child);

        var changes = new SyncChangeSet();
        changes.Deletes.Add(child);
        adapter.Apply(changes, new Dictionary<string, string>());

        Assert.Empty(client.GetChildPages(root)); // archived child no longer enumerated
    }

    [Fact]
    public void RunThroughEngine_RepoEditApplies_ToNotion_ThenUnchangedTickIssuesNoWrite()
    {
        // One-sided repo edit applies to Notion; a subsequent unchanged tick is a pure no-op (no API writes).
        var (client, root, child) = SeedTree("original");
        var repoPath = Path.Combine(_dir, "guide.md");
        File.WriteAllText(repoPath, "---\ntitle: Guide\narea: general\n---\n\noriginal");

        var store = new BaseSnapshotStore(Path.Combine(_dir, "snap.json"));
        // Seed the base so the child page is a managed UPDATE target, not a fresh create.
        store.Set(new SyncDoc { LocalId = "guide", ExternalId = child, Fields = [], Body = "original", SourcePath = "" });

        DocsPageAdapter Adapter() => new(
            client, root, new Dictionary<string, string>(),
            new Dictionary<string, string> { ["guide"] = "Guide" },
            ManagedFrom(store));
        SyncDoc Read() => SyncDocFile.Read(repoPath, "guide", repoPath);
        SyncRunner Runner() => new(Adapter(), store, (_, _, _) => repoPath);

        // Repo edits the body.
        File.WriteAllText(repoPath, "---\ntitle: Guide\narea: general\n---\n\nedited in repo");
        Runner().Run([Read()]);
        Assert.Equal("edited in repo", NotionBlockConverter.FromBlocks(client.GetBlockChildren(child)));

        // Unchanged tick: no create, append, or delete.
        client.AppendedTo.Clear();
        client.DeletedBlocks.Clear();
        var result = Runner().Run([Read()]);
        Assert.All(result.Results, r => Assert.Equal(ReconcileAction.None, r.Action));
        Assert.Empty(client.AppendedTo);
        Assert.Empty(client.DeletedBlocks);
    }

    [Fact]
    public void RunThroughEngine_NotionBodyEdit_MergesBack_PreservingFrontmatter()
    {
        // One-sided Notion body edit merges back to the repo file, and the repo frontmatter is preserved
        // (a plain page cannot round-trip it, so the invisible-field overlay must restore it, not blank it).
        var (client, root, child) = SeedTree("original");
        var repoPath = Path.Combine(_dir, "guide.md");
        File.WriteAllText(repoPath, "---\ntitle: Guide\narea: general\ntype: reference\n---\n\noriginal");

        var store = new BaseSnapshotStore(Path.Combine(_dir, "snap.json"));
        store.Set(new SyncDoc { LocalId = "guide", ExternalId = child, Fields = [], Body = "original", SourcePath = "" });

        DocsPageAdapter Adapter() => new(
            client, root, new Dictionary<string, string>(),
            new Dictionary<string, string> { ["guide"] = "Guide" }, ManagedFrom(store));
        SyncRunner Runner() => new(Adapter(), store, (_, _, _) => repoPath);

        // A colleague edits the page body in Notion.
        client.SetBlockChildren(child, NotionBlockConverter.ToBlocks("edited in notion"));
        Runner().Run([SyncDocFile.Read(repoPath, "guide", repoPath)]);

        var merged = SyncDocFile.Read(repoPath, "guide", repoPath);
        Assert.Equal("edited in notion", merged.Body);
        Assert.Equal("general", merged.GetField("area"));   // frontmatter survived the write-back
        Assert.Equal("reference", merged.GetField("type"));
    }

    [Fact]
    public void RunThroughEngine_TwoSidedNonOverlappingBodyEdits_Merge_NoConflict()
    {
        var (client, root, child) = SeedTree("line one\n\nline two\n\nline three");
        var repoPath = Path.Combine(_dir, "guide.md");
        File.WriteAllText(repoPath, "---\ntitle: Guide\n---\n\nline one\n\nline two\n\nline three");

        var store = new BaseSnapshotStore(Path.Combine(_dir, "snap.json"));
        store.Set(new SyncDoc { LocalId = "guide", ExternalId = child, Fields = [], Body = "line one\n\nline two\n\nline three", SourcePath = "" });

        DocsPageAdapter Adapter() => new(
            client, root, new Dictionary<string, string>(),
            new Dictionary<string, string> { ["guide"] = "Guide" }, ManagedFrom(store));

        // Repo changes the first line; Notion changes the last line — disjoint.
        File.WriteAllText(repoPath, "---\ntitle: Guide\n---\n\nline ONE repo\n\nline two\n\nline three");
        client.SetBlockChildren(child, NotionBlockConverter.ToBlocks("line one\n\nline two\n\nline THREE notion"));

        var result = new SyncRunner(Adapter(), store, (_, _, _) => repoPath).Run([SyncDocFile.Read(repoPath, "guide", repoPath)]);

        Assert.Equal(0, result.ConflictCount);
        var merged = SyncDocFile.Read(repoPath, "guide", repoPath).Body;
        Assert.Contains("line ONE repo", merged);
        Assert.Contains("line THREE notion", merged);
    }

    private static HashSet<string> ManagedFrom(BaseSnapshotStore store)
    {
        var ids = new HashSet<string>();
        foreach (var localId in store.LocalIds)
        {
            var externalId = store.Get(localId)?.ExternalId;
            if (externalId != null) ids.Add(externalId);
        }
        return ids;
    }
}
