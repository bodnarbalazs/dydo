namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>DR 035 §4/§5: a genuine two-sided conflict is diverted to the shadow tree — NEVER written as
/// conflict markers into a canonical repo file (the root cause of issue 0235) — and a human's resolved shadow
/// file is promoted on the next sync.</summary>
public class DocsShadowConflictTests : IDisposable
{
    private readonly string _root;
    private readonly string _dydoRoot;

    private const string SpineModel = """
        { "objects": [ { "type": "Campaign", "dir": "project/campaigns", "notionTitle": "Campaigns",
          "properties": { "title": { "type": "title" } } } ] }
        """;

    public DocsShadowConflictTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dydo-shadow-" + Guid.NewGuid().ToString("N")[..8]);
        _dydoRoot = Path.Combine(_root, "dydo");
        Directory.CreateDirectory(_dydoRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    // ---- SyncRunner-level: the shadow-routing invariant --------------------------------------------------

    [Fact]
    public void GenuineTwoSidedEdit_DivertsToShadow_LeavesCanonicalUntouched_AndDoesNotAdvanceBaseOrPush()
    {
        var (client, root, child) = SeedTree("line one\nline two\nline three");
        var repoPath = Path.Combine(_dydoRoot, "guide.md");
        File.WriteAllText(repoPath, "---\ntitle: Guide\n---\n\nline one\nline two\nline three");

        var store = new BaseSnapshotStore(Path.Combine(_dydoRoot, "snap.json"));
        store.Set(new SyncDoc { LocalId = "guide", ExternalId = child, Fields = [], Body = "line one\nline two\nline three", SourcePath = "" });

        // Repo and Notion both edit the SAME line, differently — a real overlap the 3-way merge cannot resolve.
        var repoEdit = "---\ntitle: Guide\n---\n\nline one\nline TWO repo\nline three";
        File.WriteAllText(repoPath, repoEdit);
        client.SetPageMarkdown(child, "line one\nline TWO notion\nline three");

        var shadowPath = Path.Combine(_dydoRoot, "_system", "notion_sync", "guide.md");
        var adapter = new DocsPageAdapter(
            client, root, new Dictionary<string, string>(),
            new Dictionary<string, string> { ["guide"] = "Guide" }, ManagedFrom(store));
        var runner = new SyncRunner(adapter, store, (_, _, _) => repoPath, _ => shadowPath);

        client.MarkdownUpdates.Clear();
        var result = runner.Run([SyncDocFile.Read(repoPath, "guide", repoPath)]);

        // Diverted to the shadow tree, WITH the conflict markers.
        Assert.Equal(["guide"], result.ShadowedLocalIds);
        Assert.True(File.Exists(shadowPath));
        Assert.Contains("<<<<<<< repo", File.ReadAllText(shadowPath));

        // The canonical file is untouched — still the repo's own edit, and NEVER carrying conflict markers.
        Assert.Equal(repoEdit, File.ReadAllText(repoPath));
        Assert.DoesNotContain("<<<<<<< repo", File.ReadAllText(repoPath));

        // The base did not advance and nothing was pushed — so the two-sided edit is re-detected next tick.
        Assert.Equal("line one\nline two\nline three", store.Get("guide")!.Body);
        Assert.Empty(client.MarkdownUpdates);
    }

    [Fact]
    public void NoShadowResolver_KeepsHistoricalBehavior_WritesMergedBodyToCanonical()
    {
        // The spine passes no shadow resolver: the conflicted body (markers and all) is written to the canonical
        // file as before — proving the shadow diversion is opt-in to the docs mirror and does not change the spine.
        var (client, root, child) = SeedTree("line one\nline two\nline three");
        var repoPath = Path.Combine(_dydoRoot, "guide.md");
        File.WriteAllText(repoPath, "---\ntitle: Guide\n---\n\nline one\nline TWO repo\nline three");

        var store = new BaseSnapshotStore(Path.Combine(_dydoRoot, "snap.json"));
        store.Set(new SyncDoc { LocalId = "guide", ExternalId = child, Fields = [], Body = "line one\nline two\nline three", SourcePath = "" });
        client.SetPageMarkdown(child, "line one\nline TWO notion\nline three");

        var adapter = new DocsPageAdapter(
            client, root, new Dictionary<string, string>(),
            new Dictionary<string, string> { ["guide"] = "Guide" }, ManagedFrom(store));
        var result = new SyncRunner(adapter, store, (_, _, _) => repoPath).Run([SyncDocFile.Read(repoPath, "guide", repoPath)]);

        Assert.Empty(result.ShadowedLocalIds);
        Assert.Contains("<<<<<<< repo", File.ReadAllText(repoPath)); // markers land on the canonical file (spine behavior)
    }

    // ---- Safety-rail sentinel guard ----------------------------------------------------------------------

    [Fact]
    public void ContainsConflictMarkers_MatchesEitherEndpointSentinel_NotIncidentalMarkdown()
    {
        Assert.True(ThreeWayTextMerge.ContainsConflictMarkers("a\n<<<<<<< repo\nx\n=======\ny\n>>>>>>> external\nb"));
        Assert.True(ThreeWayTextMerge.ContainsConflictMarkers("only <<<<<<< repo remains"));
        Assert.True(ThreeWayTextMerge.ContainsConflictMarkers("only >>>>>>> external remains"));
        // Ordinary prose and Markdown rules are not conflict markers.
        Assert.False(ThreeWayTextMerge.ContainsConflictMarkers("compare a >>> b and c <<< d"));
        Assert.False(ThreeWayTextMerge.ContainsConflictMarkers("Heading\n=======\n\n---"));
    }

    [Fact]
    public void InProgressShadow_StillCarryingMarkers_IsNotClobberedByNextTick()
    {
        // A human is mid-resolution: the shadow already exists and still carries markers, but holds their partial
        // edits. The next tick re-detects the same two-sided edit — it must NOT overwrite the shadow (discarding
        // the human's work); it leaves the file as-is while keeping the conflict active (shadowed, base un-advanced).
        var (client, root, child) = SeedTree("line one\nline two\nline three");
        var repoPath = Path.Combine(_dydoRoot, "guide.md");

        var store = new BaseSnapshotStore(Path.Combine(_dydoRoot, "snap.json"));
        store.Set(new SyncDoc { LocalId = "guide", ExternalId = child, Fields = [], Body = "line one\nline two\nline three", SourcePath = "" });

        File.WriteAllText(repoPath, "---\ntitle: Guide\n---\n\nline one\nline TWO repo\nline three");
        client.SetPageMarkdown(child, "line one\nline TWO notion\nline three");

        var shadowPath = Path.Combine(_dydoRoot, "_system", "notion_sync", "guide.md");
        Directory.CreateDirectory(Path.GetDirectoryName(shadowPath)!);
        var humanPartial = "HUMAN MID EDIT\n<<<<<<< repo\nline TWO repo\n=======\nline TWO notion\n>>>>>>> external\ntail";
        File.WriteAllText(shadowPath, humanPartial);

        var adapter = new DocsPageAdapter(
            client, root, new Dictionary<string, string>(),
            new Dictionary<string, string> { ["guide"] = "Guide" }, ManagedFrom(store));
        var result = new SyncRunner(adapter, store, (_, _, _) => repoPath, _ => shadowPath)
            .Run([SyncDocFile.Read(repoPath, "guide", repoPath)]);

        Assert.Equal(["guide"], result.ShadowedLocalIds);          // conflict still active
        Assert.Equal(humanPartial, File.ReadAllText(shadowPath));   // human's in-progress edit untouched
    }

    [Theory]
    [InlineData("<<<<<<< repo\nline TWO repo\n=======\nline TWO notion")]
    [InlineData("line TWO repo\n=======\nline TWO notion\n>>>>>>> external")]
    public void PartiallyResolvedShadow_WithEitherEndpointMarker_IsNotPromoted(string partialShadow)
    {
        var (client, root, child) = SeedTree("line one\nline two\nline three");
        var repoPath = Path.Combine(_dydoRoot, "guide.md");
        var repoEdit = "---\ntitle: Guide\n---\n\nline one\nline TWO repo\nline three";
        File.WriteAllText(repoPath, repoEdit);

        var store = new BaseSnapshotStore(Path.Combine(_dydoRoot, "snap.json"));
        store.Set(new SyncDoc { LocalId = "guide", ExternalId = child, Fields = [], Body = "line one\nline two\nline three", SourcePath = "" });
        client.SetPageMarkdown(child, "line one\nline TWO notion\nline three");

        var shadowPath = Path.Combine(_dydoRoot, "_system", "notion_sync", "guide.md");
        Directory.CreateDirectory(Path.GetDirectoryName(shadowPath)!);
        File.WriteAllText(shadowPath, partialShadow);

        var adapter = new DocsPageAdapter(
            client, root, new Dictionary<string, string>(),
            new Dictionary<string, string> { ["guide"] = "Guide" }, ManagedFrom(store));
        new SyncRunner(adapter, store, (_, _, _) => repoPath, _ => shadowPath)
            .Run([SyncDocFile.Read(repoPath, "guide", repoPath)]);

        Assert.Equal(partialShadow, File.ReadAllText(shadowPath));
        Assert.Equal(repoEdit, File.ReadAllText(repoPath));
    }

    // ---- DocsTreeSync-level: promotion of a resolved shadow file -----------------------------------------

    [Fact]
    public void ResolvedShadowFile_IsPromotedToCanonical_OnNextSync_AndDeleted()
    {
        Seed("understand/architecture.md", "---\ntitle: Architecture\n---\n\n# Arch\n\nbody one.");
        WriteModel(SpineModel);

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        // Force a genuine two-sided conflict on the same line, so the tick diverts it to the shadow tree.
        var arch = PageIdFor(client, "Architecture");
        File.WriteAllText(DocPath("understand/architecture.md"), "---\ntitle: Architecture\n---\n\n# Arch\n\nbody REPO.");
        client.SetPageMarkdown(arch, "# Arch\n\nbody NOTION.");
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var shadowPath = Path.Combine(_dydoRoot, "_system", "notion_sync", "understand", "architecture.md");
        Assert.True(File.Exists(shadowPath));
        Assert.Contains("<<<<<<< repo", File.ReadAllText(shadowPath));
        Assert.DoesNotContain("<<<<<<< repo", File.ReadAllText(DocPath("understand/architecture.md")));

        // The human resolves the shadow file (removes the markers, picks a final body).
        File.WriteAllText(shadowPath, "---\ntitle: Architecture\n---\n\n# Arch\n\nbody RESOLVED.");

        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        // Promoted onto the canonical file and the shadow removed.
        Assert.Contains("body RESOLVED.", File.ReadAllText(DocPath("understand/architecture.md")));
        Assert.False(File.Exists(shadowPath));
    }

    [Theory]
    [InlineData("<<<<<<< repo", ">>>>>>> external")]
    [InlineData(">>>>>>> external", "<<<<<<< repo")]
    public void PartiallyResolvedShadow_MissingEitherEndpoint_IsNotPromotedOrPushed(
        string removedMarker, string remainingMarker)
    {
        Seed("understand/architecture.md", "---\ntitle: Architecture\n---\n\n# Arch\n\nbody one.");
        WriteModel(SpineModel);

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var arch = PageIdFor(client, "Architecture");
        var canonicalPath = DocPath("understand/architecture.md");
        File.WriteAllText(canonicalPath, "---\ntitle: Architecture\n---\n\n# Arch\n\nbody REPO.");
        client.SetPageMarkdown(arch, "# Arch\n\nbody NOTION.");
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var shadowPath = Path.Combine(_dydoRoot, "_system", "notion_sync", "understand", "architecture.md");
        var partialShadow = File.ReadAllText(shadowPath).Replace(removedMarker, string.Empty, StringComparison.Ordinal);
        File.WriteAllText(shadowPath, partialShadow);
        var canonicalBeforePromotion = File.ReadAllText(canonicalPath);

        client.MarkdownUpdates.Clear();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        Assert.Equal(canonicalBeforePromotion, File.ReadAllText(canonicalPath));
        Assert.DoesNotContain("<<<<<<< ", File.ReadAllText(canonicalPath));
        Assert.DoesNotContain("=======", File.ReadAllText(canonicalPath));
        Assert.DoesNotContain(">>>>>>> ", File.ReadAllText(canonicalPath));
        Assert.Equal(partialShadow, File.ReadAllText(shadowPath));
        Assert.Contains(remainingMarker, File.ReadAllText(shadowPath));
        Assert.Empty(client.MarkdownUpdates);
    }

    [Fact]
    public void ResolvedShadow_CarryingChildPageTags_IsPromotedCLEAN_TagsStripped()
    {
        // Invariant (DR 035 §3 / issue 0235): EVERY write to a canonical file goes through CleanForPersist. Shadow
        // promotion is a canonical write, so a human who resolves a shadow while keeping a hunk that still carries the
        // Notion child-page `<page url>` structure tags must NOT land that soup on disk — it is stripped on promotion,
        // exactly as the read side strips it. This closes the one canonical-write ingress the read-side strip left open.
        Seed("understand/architecture.md", "---\ntitle: Architecture\n---\n\n# Arch\n\nbody one.");
        WriteModel(SpineModel);

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        // Force a genuine two-sided conflict so the tick diverts to a shadow.
        var arch = PageIdFor(client, "Architecture");
        File.WriteAllText(DocPath("understand/architecture.md"), "---\ntitle: Architecture\n---\n\n# Arch\n\nbody REPO.");
        client.SetPageMarkdown(arch, "# Arch\n\nbody NOTION.");
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var shadowPath = Path.Combine(_dydoRoot, "_system", "notion_sync", "understand", "architecture.md");
        Assert.True(File.Exists(shadowPath));

        // The human resolves the shadow but keeps the child-page tag soup in the chosen hunk.
        File.WriteAllText(shadowPath,
            "---\ntitle: Architecture\n---\n\n# Arch\n\nbody RESOLVED.\n<page url=\"https://app.notion.com/p/abc123\">nested</page>");

        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        // Promoted, shadow removed — and the child-page tag was STRIPPED, prose preserved.
        var canonical = File.ReadAllText(DocPath("understand/architecture.md"));
        Assert.Contains("body RESOLVED.", canonical);
        Assert.DoesNotContain("<page url=", canonical);
        Assert.DoesNotContain("nested</page>", canonical);
        Assert.False(File.Exists(shadowPath));
    }

    [Fact]
    public void ResolvedRootIndexShadow_IsPromotedToRootIndex_NotJunkDotDotMd_AndNextSyncDoesNotWedge()
    {
        // The root index (dydo/_index.md) is the "Docs" page body; its reserved "." local id shadows to _root.md.
        // A resolved root shadow must promote onto the REAL _index.md — never the junk "dydo/..md" that
        // RootLocalId + ".md" would build. That junk file would also re-ingest with stem "." on the next tick,
        // colliding the root node's "." local id and wedging the sync with "two repo files share local id '.'".
        Seed("_index.md", "---\ntitle: Docs\n---\n\n# Docs\n\nroot body one.");
        WriteModel(SpineModel);

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var rootPage = client.GetChildPages("workspace").Single(p => p.Title == "Docs").Id;

        // Genuine two-sided conflict on the root index body — same line edited differently on both sides.
        File.WriteAllText(DocPath("_index.md"), "---\ntitle: Docs\n---\n\n# Docs\n\nroot body REPO.");
        client.SetPageMarkdown(rootPage, "# Docs\n\nroot body NOTION.");
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var shadowPath = Path.Combine(_dydoRoot, "_system", "notion_sync", "_root.md");
        Assert.True(File.Exists(shadowPath));
        Assert.Contains("<<<<<<< repo", File.ReadAllText(shadowPath));
        Assert.DoesNotContain("<<<<<<< repo", File.ReadAllText(DocPath("_index.md")));

        // The human resolves the root shadow.
        File.WriteAllText(shadowPath, "---\ntitle: Docs\n---\n\n# Docs\n\nroot body RESOLVED.");

        // The next sync must NOT throw — the junk "..md" would collide the root's "." local id in IndexByLocalId.
        var ex = Record.Exception(() => DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter()));
        Assert.Null(ex);

        // Promoted onto the real root index, never the junk path, and the shadow removed.
        Assert.Contains("root body RESOLVED.", File.ReadAllText(DocPath("_index.md")));
        Assert.False(File.Exists(shadowPath));
        Assert.False(File.Exists(Path.Combine(_dydoRoot, "..md")));
    }

    [Fact]
    public void PromotionRead_ThrowsWhenPageArchived_DoesNotWedge_ReconcileProceeds()
    {
        // The promotion aligns the base to the current external body — but if the page was archived/trashed in
        // Notion while the conflict sat unresolved, that read 404s. The guard skips the alignment instead of
        // throwing at the same point every tick: the sync must NOT wedge, and the reconcile proceeds (resurrecting
        // from the repo-owned structure). The resolution still lands on the canonical file and the shadow clears.
        Seed("understand/architecture.md", "---\ntitle: Architecture\n---\n\n# Arch\n\nbody one.");
        WriteModel(SpineModel);

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var arch = PageIdFor(client, "Architecture");
        File.WriteAllText(DocPath("understand/architecture.md"), "---\ntitle: Architecture\n---\n\n# Arch\n\nbody REPO.");
        client.SetPageMarkdown(arch, "# Arch\n\nbody NOTION.");
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var shadowPath = Path.Combine(_dydoRoot, "_system", "notion_sync", "understand", "architecture.md");
        Assert.True(File.Exists(shadowPath));

        // The human resolves the shadow; meanwhile the page is archived/trashed and its body read now 404s.
        File.WriteAllText(shadowPath, "---\ntitle: Architecture\n---\n\n# Arch\n\nbody RESOLVED.");
        client.UpdatePage(arch, new NotionPageUpdateRequest { Archived = true });
        client.FailMarkdownReadFor.Add(arch);

        var ex = Record.Exception(() => DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter()));
        Assert.Null(ex); // the unreadable page must not wedge the whole sync

        Assert.Contains("body RESOLVED.", File.ReadAllText(DocPath("understand/architecture.md")));
        Assert.False(File.Exists(shadowPath));
    }

    [Fact]
    public void UnresolvedShadowFile_StillCarryingMarkers_IsNotPromoted()
    {
        Seed("understand/architecture.md", "---\ntitle: Architecture\n---\n\n# Arch\n\nbody one.");
        WriteModel(SpineModel);

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var arch = PageIdFor(client, "Architecture");
        File.WriteAllText(DocPath("understand/architecture.md"), "---\ntitle: Architecture\n---\n\n# Arch\n\nbody REPO.");
        client.SetPageMarkdown(arch, "# Arch\n\nbody NOTION.");
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var shadowPath = Path.Combine(_dydoRoot, "_system", "notion_sync", "understand", "architecture.md");
        Assert.True(File.Exists(shadowPath));

        // The human has NOT finished — the shadow still carries markers. It must not be promoted onto canonical.
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());
        Assert.True(File.Exists(shadowPath));
        Assert.DoesNotContain("body NOTION.", File.ReadAllText(DocPath("understand/architecture.md")));
    }

    [Fact]
    public void SpineShadowSibling_SurvivesDocsMirrorPromotePass_Untouched()
    {
        // Regression (ns-4 finding 1): the PM spine's conflict shadows live at _system/notion_sync_spine/ — a
        // SIBLING of this docs mirror's _system/notion_sync/ shadow root, never nested inside it. The docs mirror's
        // PromoteResolvedShadows enumerates notion_sync/** recursively, so a nested (resolved) spine shadow met by a
        // docs-only run would be promoted to a junk canonical path and DELETED, silently losing the human's
        // resolution. With the sibling namespace the docs promote pass must leave the spine shadow untouched.
        Seed("understand/architecture.md", "---\ntitle: Architecture\n---\n\n# Arch\n\nbody.");
        WriteModel(SpineModel);

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        // A resolved spine shadow (no markers) sits in the sibling tree while the docs mirror runs its promote pass.
        var spineShadow = Path.Combine(_dydoRoot, "_system", "notion_sync_spine", "Sprint", "notion-sync.md");
        Directory.CreateDirectory(Path.GetDirectoryName(spineShadow)!);
        var resolved = "---\ntitle: Notion Sync\n---\n\nSync work RESOLVED.";
        File.WriteAllText(spineShadow, resolved);

        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        // Untouched: content intact, not promoted to any junk canonical, not deleted.
        Assert.True(File.Exists(spineShadow));
        Assert.Equal(resolved, File.ReadAllText(spineShadow));
    }

    // ---- helpers -----------------------------------------------------------------------------------------

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
        if (childBody.Length != 0)
            client.SetPageMarkdown(child, childBody);
        return (client, root, child);
    }

    private static HashSet<string> ManagedFrom(BaseSnapshotStore store)
    {
        var ids = new HashSet<string>();
        foreach (var localId in store.LocalIds)
            if (store.Get(localId)?.ExternalId is { } externalId)
                ids.Add(externalId);
        return ids;
    }

    private void Seed(string rel, string content)
    {
        var full = DocPath(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private void WriteModel(string json)
    {
        var path = Path.Combine(_dydoRoot, "_system", "sync-model.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private string DocPath(string rel) => Path.Combine(_dydoRoot, rel.Replace('/', Path.DirectorySeparatorChar));

    private string PageIdFor(FakeNotionClient client, string title)
    {
        string? Find(string parent)
        {
            foreach (var child in client.GetChildPages(parent))
            {
                if (child.Title == title) return child.Id;
                if (Find(child.Id) is { } found) return found;
            }
            return null;
        }
        var root = client.GetChildPages("workspace").Single(p => p.Title == "Docs").Id;
        return Find(root) ?? throw new InvalidOperationException($"no page titled {title}");
    }
}
