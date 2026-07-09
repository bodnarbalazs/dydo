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
        // Seed the child page's native markdown body (DR 035) — the docs mirror reads bodies through the
        // markdown API, not block children.
        if (childBody.Length != 0)
            client.SetPageMarkdown(child, childBody);
        return (client, root, child);
    }

    private DocsPageAdapter AdapterOver(FakeNotionClient client, string root, params string[] managed) =>
        new(client, root, new Dictionary<string, string>(), new Dictionary<string, string>(),
            new HashSet<string>(managed));

    [Fact]
    public void ReadExternalState_WalksTree_ReadingRootAndManagedChildBodies()
    {
        var (client, root, child) = SeedTree("Child body line");
        client.SetPageMarkdown(root, "Root index body");
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
        // The body is written through the native markdown API verbatim — blank lines survive, unlike the
        // retired lossy converter (DR 035).
        Assert.Equal("# Architecture\n\nbody", client.GetPageMarkdown(newId).Markdown);
    }

    [Fact]
    public void Apply_Update_ReplacesBody_ViaMarkdown_WithoutArchivingNestedChildPages()
    {
        // A folder page holds a body AND a nested child page. Replacing its body via the markdown API updates
        // the body and never archives the nested sub-page (structure is repo-owned) — Notion maps the markdown
        // to the page's own blocks, leaving child pages intact.
        var client = new FakeNotionClient();
        var root = client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page("workspace"),
            Properties = new() { ["title"] = new() { Type = "title", Title = NotionRichText.Of("Docs") } },
        }).Id;
        client.SetPageMarkdown(root, "old body");
        var nested = client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page(root),
            Properties = new() { ["title"] = new() { Type = "title", Title = NotionRichText.Of("Sub") } },
        }).Id;
        var adapter = AdapterOver(client, root, root, nested);

        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert { LocalId = ".", ExternalId = root, Fields = [], Body = "new body" });
        adapter.Apply(changes, new Dictionary<string, string>());

        Assert.Equal("new body", client.StoredMarkdown(root));                         // body replaced (no child tags)
        Assert.Contains(client.GetChildPages(root), p => p.Id == nested);              // nested sub-page preserved
        Assert.False(client.IsArchived(nested));
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
        Assert.Equal("edited in repo", client.GetPageMarkdown(child).Markdown);

        // Unchanged tick: no create and no markdown body write.
        client.MarkdownUpdates.Clear();
        var result = Runner().Run([Read()]);
        Assert.All(result.Results, r => Assert.Equal(ReconcileAction.None, r.Action));
        Assert.Empty(client.MarkdownUpdates);
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
        client.SetPageMarkdown(child, "edited in notion");
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
        client.SetPageMarkdown(child, "line one\n\nline two\n\nline THREE notion");

        var result = new SyncRunner(Adapter(), store, (_, _, _) => repoPath).Run([SyncDocFile.Read(repoPath, "guide", repoPath)]);

        Assert.Equal(0, result.ConflictCount);
        var merged = SyncDocFile.Read(repoPath, "guide", repoPath).Body;
        Assert.Contains("line ONE repo", merged);
        Assert.Contains("line THREE notion", merged);
    }

    [Fact]
    public void RunThroughEngine_ResurrectCreate_WritesBodyAtomically_NeverWipesCanonicalFile()
    {
        // Issue 0235 canonical-body WIPE window: the old two-step create (CreatePage then a SEPARATE
        // UpdatePageMarkdown) could throw AFTER the page existed, recording a full-body base against an empty
        // Notion page; the next tick then read that empty page as an external clear and EMPTIED the canonical
        // repo file. The fix carries the body in the create itself (DR 035 §1 create-with-body), so it lands
        // atomically. This test pins that even with the separate body write BROKEN, a resurrect create still
        // persists the full body and the canonical file is never modified.
        var (client, root, child) = SeedTree("canonical body");
        var repoPath = Path.Combine(_dir, "guide.md");
        var repoContent = "---\ntitle: Guide\narea: general\n---\n\ncanonical body";
        File.WriteAllText(repoPath, repoContent);

        var store = new BaseSnapshotStore(Path.Combine(_dir, "snap.json"));
        store.Set(new SyncDoc { LocalId = "guide", ExternalId = child, Fields = [], Body = "canonical body", SourcePath = "" });

        DocsPageAdapter Adapter() => new(
            client, root, new Dictionary<string, string> { ["guide"] = root },
            new Dictionary<string, string> { ["guide"] = "Guide" }, ManagedFrom(store));
        SyncDoc Read() => SyncDocFile.Read(repoPath, "guide", repoPath);
        SyncRunner Runner() => new(Adapter(), store, (_, _, _) => repoPath);

        // The page vanishes from the external listing (eventual consistency / a colleague's stray archive) while
        // the repo doc is still present — the repo-owned-structure resurrect path (CreateToExternal). The separate
        // body write is broken, so a two-step create would throw mid-batch and poison the base.
        client.HiddenFromListing.Add(child);
        client.FailMarkdownUpdate = true;

        Assert.Null(Record.Exception(() => Runner().Run([Read()])));   // atomic create — no separate write to fail on

        var resurrectedId = store.Get("guide")!.ExternalId!;
        Assert.NotEqual(child, resurrectedId);                          // a fresh page was minted
        Assert.Equal("canonical body", client.GetPageMarkdown(resurrectedId).Markdown); // body landed IN the create
        Assert.Empty(client.MarkdownUpdates);                          // never a separate UpdatePageMarkdown
        Assert.Equal(repoContent, File.ReadAllText(repoPath));          // canonical file byte-unchanged

        // The next tick reads the resurrected page (now visible) with the full body: base, repo, and external all
        // agree, so it is a pure no-op — the empty-page-vs-full-base wipe never materialises.
        var result = Runner().Run([Read()]);
        Assert.All(result.Results, r => Assert.Equal(ReconcileAction.None, r.Action));
        Assert.Equal(repoContent, File.ReadAllText(repoPath));
    }

    [Fact]
    public void RunThroughEngine_CreateWithBody_SilentlyIgnored_Throws_DoesNotAdvanceBaseNorWipeCanonical()
    {
        // The self-enforcing read-back guard (DR 035): the create-with-body markdown field is doc-sourced. If live
        // Notion SILENTLY IGNORES it, the page is created empty — and recording the full-body base against that
        // empty page is the exact issue 0235 wipe (next tick reads the empty page as a clear and empties canonical).
        // The guard reads the body straight back after the create and THROWS on the empty echo, before the base
        // advances — so the wipe can never fire.
        var (client, root, child) = SeedTree("canonical body");
        var repoPath = Path.Combine(_dir, "guide.md");
        var repoContent = "---\ntitle: Guide\narea: general\n---\n\ncanonical body";
        File.WriteAllText(repoPath, repoContent);

        var store = new BaseSnapshotStore(Path.Combine(_dir, "snap.json"));
        store.Set(new SyncDoc { LocalId = "guide", ExternalId = child, Fields = [], Body = "canonical body", SourcePath = "" });

        DocsPageAdapter Adapter() => new(
            client, root, new Dictionary<string, string> { ["guide"] = root },
            new Dictionary<string, string> { ["guide"] = "Guide" }, ManagedFrom(store));
        SyncDoc Read() => SyncDocFile.Read(repoPath, "guide", repoPath);
        SyncRunner Runner() => new(Adapter(), store, (_, _, _) => repoPath);

        // The page vanishes from the listing (repo-owned-structure resurrect → create-with-body), and live Notion
        // silently drops the create's markdown field: the resurrected page is created EMPTY.
        client.HiddenFromListing.Add(child);
        client.SilentlyIgnoreCreateMarkdown = true;

        Assert.Throws<NotionApiException>(() => Runner().Run([Read()]));

        // The base still points at the original page with the full body — never advanced to the empty resurrect.
        Assert.Equal(child, store.Get("guide")!.ExternalId);
        Assert.Equal("canonical body", store.Get("guide")!.Body);
        // The canonical file is byte-unchanged — the wipe never fires.
        Assert.Equal(repoContent, File.ReadAllText(repoPath));
    }

    [Fact]
    public void Apply_ArchiveFailsNonAncestor_Propagates_AndIsNotRecordedAsLanded()
    {
        // Issue 0221: a NON-ancestor archive failure (a transient 5xx / rate-limit / auth) must PROPAGATE, never
        // be swallowed — and the failed page must NOT be reported as a landed delete, so the caller keeps its base.
        var (client, root, child) = SeedTree("");
        var adapter = AdapterOver(client, root, root, child);
        client.FailUpdate = true; // UpdatePage throws a 500 — not an archived-ancestor 400

        var changes = new SyncChangeSet();
        changes.Deletes.Add(child);
        var deleted = new List<string>();

        Assert.Throws<NotionApiException>(() => adapter.Apply(changes, new Dictionary<string, string>(), deleted));
        Assert.DoesNotContain(child, deleted);       // never reported as landed
        Assert.False(client.IsArchived(child));       // the live page was not archived
    }

    [Fact]
    public void Apply_ArchiveUnderArchivedAncestor_IsTolerated_ButNotRecordedAsLanded()
    {
        // The ONE tolerated case (issue 0221): the page's ancestor is already archived, so Notion 400s the
        // redundant archive. The adapter swallows it (the page is already trashed under its ancestor) — but it
        // must NOT report the page as landed, so the caller keeps the base entry for the next tick's Retire.
        var client = new FakeNotionClient();
        var root = client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page("workspace"),
            Properties = new() { ["title"] = new() { Type = "title", Title = NotionRichText.Of("Docs") } },
        }).Id;
        var folder = client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page(root),
            Properties = new() { ["title"] = new() { Type = "title", Title = NotionRichText.Of("Folder") } },
        }).Id;
        var child = client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page(folder),
            Properties = new() { ["title"] = new() { Type = "title", Title = NotionRichText.Of("Child") } },
        }).Id;
        client.UpdatePage(folder, new NotionPageUpdateRequest { Archived = true }); // ancestor pre-archived out of band

        var adapter = AdapterOver(client, root, root, folder, child);
        var changes = new SyncChangeSet();
        changes.Deletes.Add(child); // archiving the child now 400s with "archived ancestor"
        var deleted = new List<string>();

        var ex = Record.Exception(() => adapter.Apply(changes, new Dictionary<string, string>(), deleted));

        Assert.Null(ex);                        // tolerated, not propagated
        Assert.DoesNotContain(child, deleted);  // but NOT recorded as landed — base retained for Retire
    }

    [Fact]
    public void RunThroughEngine_ArchiveFailsNonAncestor_SurfacesError_RetainsBaseForRetry()
    {
        // Issue 0221 end-to-end: a repo doc is deleted and its page archive fails non-ancestrally. The failure
        // must surface (so NotionSyncService returns ToolError) AND the base entry must be RETAINED for retry —
        // never silently dropped, which would orphan the still-live Notion page behind a success exit.
        var (client, root, child) = SeedTree("original");
        var repoPath = Path.Combine(_dir, "guide.md");

        var store = new BaseSnapshotStore(Path.Combine(_dir, "snap.json"));
        store.Set(new SyncDoc { LocalId = "guide", ExternalId = child, Fields = [], Body = "original", SourcePath = "" });

        DocsPageAdapter Adapter() => new(
            client, root, new Dictionary<string, string>(),
            new Dictionary<string, string> { ["guide"] = "Guide" }, ManagedFrom(store));
        SyncRunner Runner() => new(Adapter(), store, (_, _, _) => repoPath);

        // The repo doc is gone (never written) → its page archive is queued; UpdatePage fails with a 500.
        client.FailUpdate = true;
        Assert.Throws<NotionApiException>(() => Runner().Run([]));

        var retained = store.Get("guide");
        Assert.NotNull(retained);                    // base entry kept for retry, not dropped
        Assert.Equal(child, retained!.ExternalId);
        Assert.False(client.IsArchived(child));       // the live page was never archived
    }

    [Fact]
    public void Apply_Update_FolderPageWithChildren_IsChildSafe_LeafPageIsDestructive()
    {
        // DR 035 §3 child-safety: replace_content with allow_deleting_content:true can TRASH a page's child pages
        // (makenotion/notion-mcp-server#171). A FOLDER page carries the nested docs as child pages, so its body
        // update must be issued CHILD-SAFE (allow_deleting_content:false); a LEAF page (no children) takes the
        // destructive full overwrite (true). The fake models the bug — a true replace archives the page's children,
        // so a folder update issued destructively would wipe the nested doc.
        var client = new FakeNotionClient();
        var root = client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page("workspace"),
            Properties = new() { ["title"] = new() { Type = "title", Title = NotionRichText.Of("Docs") } },
        }).Id;
        var folder = client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page(root),
            Properties = new() { ["title"] = new() { Type = "title", Title = NotionRichText.Of("Folder") } },
        }).Id;
        var nestedDoc = client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page(folder),
            Properties = new() { ["title"] = new() { Type = "title", Title = NotionRichText.Of("Nested") } },
        }).Id;
        var adapter = AdapterOver(client, root, root, folder, nestedDoc);

        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert { LocalId = "folder", ExternalId = folder, Fields = [], Body = "new folder index" });
        changes.Upserts.Add(new SyncUpsert { LocalId = "folder/nested", ExternalId = nestedDoc, Fields = [], Body = "new leaf body" });
        adapter.Apply(changes, new Dictionary<string, string>());

        // The folder update was issued NON-destructively, so its nested child page survives the structural wipe.
        Assert.Contains((folder, false), client.MarkdownUpdateCalls);
        Assert.Contains(client.GetChildPages(folder), p => p.Id == nestedDoc);
        Assert.False(client.IsArchived(nestedDoc));
        Assert.Equal("new folder index", client.StoredMarkdown(folder)); // body still replaced (child tags not part of body)
        // The leaf page (no children to protect) took the destructive full overwrite.
        Assert.Contains((nestedDoc, true), client.MarkdownUpdateCalls);
    }

    [Fact]
    public void RunThroughEngine_TruncatedExternalRead_ReusesLastSyncedBody_NeverTruncatesCanonicalFile()
    {
        // DR 035 §4 / deferred finding 3 (20k ceiling): a body past Notion's ~20k-block export ceiling reads back
        // TRUNCATED — cut short, hence shorter than the real body. Treating it as external state would look like a
        // Notion-side deletion and merge the cut-short body onto the canonical repo file, truncating it. The adapter
        // reuses the last-synced body so the reconcile is a no-op and the canonical file is left byte-unchanged.
        var (client, root, child) = SeedTree("full body\n\nsecond paragraph");
        var repoPath = Path.Combine(_dir, "guide.md");
        var repoContent = "---\ntitle: Guide\narea: general\n---\n\nfull body\n\nsecond paragraph";
        File.WriteAllText(repoPath, repoContent);

        var store = new BaseSnapshotStore(Path.Combine(_dir, "snap.json"));
        store.Set(new SyncDoc { LocalId = "guide", ExternalId = child, Fields = [], Body = "full body\n\nsecond paragraph", SourcePath = "" });

        var lastBody = new Dictionary<string, string> { [child] = "full body\n\nsecond paragraph" };
        DocsPageAdapter Adapter() => new(
            client, root, new Dictionary<string, string>(),
            new Dictionary<string, string> { ["guide"] = "Guide" }, ManagedFrom(store), null, lastBody);
        SyncDoc Read() => SyncDocFile.Read(repoPath, "guide", repoPath);
        SyncRunner Runner() => new(Adapter(), store, (_, _, _) => repoPath);

        // Notion returns a truncated body (only the first paragraph survived the ceiling).
        client.SetPageMarkdown(child, "full body");
        client.TruncatedReadFor.Add(child);

        var result = Runner().Run([Read()]);

        Assert.All(result.Results, r => Assert.Equal(ReconcileAction.None, r.Action)); // no merge, no write-to-repo
        Assert.Equal(repoContent, File.ReadAllText(repoPath));                          // canonical file byte-unchanged
        Assert.Empty(client.MarkdownUpdates);                                           // nothing pushed either
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
