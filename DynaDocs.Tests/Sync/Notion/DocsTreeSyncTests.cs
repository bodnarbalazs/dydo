namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

public class DocsTreeSyncTests : IDisposable
{
    private readonly string _root;
    private readonly string _dydoRoot;

    public DocsTreeSyncTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dydo-docstree-" + Guid.NewGuid().ToString("N")[..8]);
        _dydoRoot = Path.Combine(_root, "dydo");

        // A docs tree spanning browsable areas, a folder index body, a nested folder, the spine dir that must be
        // excluded (project/campaigns), and a framework dir that must be excluded (_system via the model write,
        // plus agents).
        Seed("understand/_index.md", "---\ntitle: Understand\n---\n\nThe understand area.");
        Seed("understand/architecture.md", "---\ntitle: Architecture\narea: understand\n---\n\n# Arch\n\nbody one.");
        Seed("understand/guard/guard-system.md", "---\ntitle: Guard System\n---\n\nHow the guard works.");
        Seed("guides/coding.md", "---\ntitle: Coding\n---\n\nStandards.");
        Seed("project/decisions/033-mirror.md", "---\ntitle: DR 033\n---\n\nThe mirror decision.");
        Seed("project/campaigns/dydo-2-0.md", "---\ntitle: dydo 2.0\nstatus: active\n---\n\nSpine row, not a doc.");
        Seed("agents/Charlie/brief.md", "---\ntitle: Brief\n---\n\nAgent workspace, excluded.");

        // Pin a model whose Campaign dir is project/campaigns, so the mirror derives that exclusion from the model.
        WriteModel("""
            {
              "objects": [
                { "type": "Campaign", "dir": "project/campaigns", "notionTitle": "Campaigns",
                  "properties": { "title": { "type": "title" }, "status": { "type": "select", "options": ["active"] } } }
              ]
            }
            """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private void Seed(string relPath, string content)
    {
        var full = Path.Combine(_dydoRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private void WriteModel(string json)
    {
        var path = Path.Combine(_dydoRoot, "_system", "sync-model.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private static string Root(FakeNotionClient client) =>
        client.GetChildPages("workspace").Single(p => p.Title == "Docs").Id;

    private static string Child(FakeNotionClient client, string parentId, string title) =>
        client.GetChildPages(parentId).Single(p => p.Title == title).Id;

    private string DocPath(string rel) => Path.Combine(_dydoRoot, rel.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void Run_ProvisionsExpectedHierarchy_ExcludingSpineAndFrameworkDirs()
    {
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var root = Root(client);
        var topTitles = client.GetChildPages(root).Select(p => p.Title).OrderBy(x => x, StringComparer.Ordinal).ToList();
        // understand/guides/project mirrored; campaigns (spine), _system + agents (framework) excluded.
        Assert.Equal(["Understand", "guides", "project"], topTitles);

        // Doc pages nest under their folder page.
        var understand = Child(client, root, "Understand");
        Assert.Contains(client.GetChildPages(understand), p => p.Title == "Architecture");
        // A nested subfolder resolves its parent chain.
        var guard = Child(client, understand, "guard");
        Assert.Contains(client.GetChildPages(guard), p => p.Title == "Guard System");

        // The excluded spine dir minted no page anywhere under project.
        var project = Child(client, root, "project");
        Assert.DoesNotContain(client.GetChildPages(project), p => p.Title == "Campaigns" || p.Title == "campaigns");
        Assert.Contains(client.GetChildPages(project), p => p.Title == "decisions");
    }

    [Fact]
    public void Run_IndexFile_BecomesFolderPageBody()
    {
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var understand = Child(client, Root(client), "Understand");
        Assert.Equal("The understand area.", client.GetPageMarkdown(understand).Markdown);
    }

    [Fact]
    public void Run_DocPageBody_MirrorsMarkdown()
    {
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var architecture = Child(client, Child(client, Root(client), "Understand"), "Architecture");
        // The body round-trips verbatim through the native markdown API — blank lines are preserved (DR 035),
        // unlike the retired lossy converter that collapsed them.
        Assert.Equal("# Arch\n\nbody one.", client.GetPageMarkdown(architecture).Markdown);
    }

    [Fact]
    public void Run_FreshSync_WritesEveryBodyAtCreate_IssuesNoPatch()
    {
        // DR 035 §2: a full fresh sync writes every page body atomically at page-create (POST create-with-body),
        // so it issues NO PATCH — dodging the destructive replace_content entirely on fresh runs, and writing each
        // folder body before that folder has any child pages (inherently child-safe).
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        Assert.Empty(client.MarkdownUpdates); // no body was ever PATCHed — every body rode its create

        // The bodies still landed, via the atomic create.
        var understand = Child(client, Root(client), "Understand");
        Assert.Equal("The understand area.", client.GetPageMarkdown(understand).Markdown);
        var architecture = Child(client, understand, "Architecture");
        Assert.Equal("# Arch\n\nbody one.", client.GetPageMarkdown(architecture).Markdown);
    }

    [Fact]
    public void Run_FreshSync_SilentCreateWithBodyIgnore_NeverWipesCanonical_SelfHealsViaChildSafePatch()
    {
        // DR 035 review finding 1: making create-with-body the COMMON fresh-sync path reintroduces the issue 0235
        // wipe class with full-tree blast radius. The create's markdown field is doc-sourced and create-with-body is
        // unconfirmed against a page_id parent — if live Notion SILENTLY IGNORES it, every page is created EMPTY. The
        // structure phase must NOT record the full body against those empty pages: the next-tick body phase would
        // then read external "" vs base=body as a Notion-side clear (repo == base, so external wins) and WIPE every
        // canonical repo doc. The read-back guard records an empty base instead, so the body phase pushes the repo
        // body via a CHILD-SAFE PATCH (graceful degradation) rather than wiping.
        var client = new FakeNotionClient { SilentlyIgnoreCreateMarkdown = true };
        var ex = Record.Exception(() =>
            DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter()));

        Assert.Null(ex); // graceful — a systematic create-with-body ignore never aborts the fresh sync

        // The canonical repo docs are UNTOUCHED — the wipe never fired.
        Assert.Contains("body one.", File.ReadAllText(DocPath("understand/architecture.md")));
        Assert.Contains("The understand area.", File.ReadAllText(DocPath("understand/_index.md")));

        // The bodies still landed in Notion — via the body-phase PATCH fallback, not the ignored create.
        Assert.NotEmpty(client.MarkdownUpdates);
        var understand = Child(client, Root(client), "Understand");
        var architecture = Child(client, understand, "Architecture");
        Assert.Equal("The understand area.", client.GetPageMarkdown(understand).Markdown);
        Assert.Equal("# Arch\n\nbody one.", client.GetPageMarkdown(architecture).Markdown);

        // The folder-body PATCH was child-safe: the nested doc/sub-folder pages survived (allow_deleting_content:false).
        var guard = Child(client, understand, "guard");
        Assert.False(client.IsArchived(architecture));
        Assert.False(client.IsArchived(guard));
        Assert.All(client.MarkdownUpdateCalls.Where(c => c.PageId == understand),
            c => Assert.False(c.AllowDeletingContent)); // a page WITH children is never a destructive overwrite

        // And a second tick with the same ignore is a clean no-op — no re-wipe, no re-push (base now matches Notion).
        client.MarkdownUpdates.Clear();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());
        Assert.Empty(client.MarkdownUpdates);
        Assert.Contains("body one.", File.ReadAllText(DocPath("understand/architecture.md")));
    }

    [Fact]
    public void Run_FolderIndexEdit_UpdatesFolderBody_PreservingNestedDocPages()
    {
        // DR 035 §3 end-to-end: editing a folder's _index.md drives a folder-body update on a page that HAS child
        // pages (the nested docs and sub-folders). The update must be child-safe — the nested pages survive, never
        // trashed by a destructive replace_content (makenotion/notion-mcp-server#171).
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var understand = Child(client, Root(client), "Understand");
        var architecture = Child(client, understand, "Architecture");
        var guard = Child(client, understand, "guard");

        File.WriteAllText(DocPath("understand/_index.md"), "---\ntitle: Understand\n---\n\nThe understand area, revised.");
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        Assert.Equal("The understand area, revised.", client.GetPageMarkdown(understand).Markdown); // folder body updated
        Assert.False(client.IsArchived(architecture));                                              // nested doc preserved
        Assert.False(client.IsArchived(guard));                                                     // nested folder preserved
        Assert.Contains(client.GetChildPages(understand), p => p.Id == architecture);
    }

    [Fact]
    public void Run_RemovedDoc_ArchivesItsPage()
    {
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var understand = Child(client, Root(client), "Understand");
        Assert.Contains(client.GetChildPages(understand), p => p.Title == "Architecture");

        // The doc is removed from the repo; the next tick archives its page.
        File.Delete(DocPath("understand/architecture.md"));
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        Assert.DoesNotContain(client.GetChildPages(understand), p => p.Title == "Architecture");
    }

    [Fact]
    public void Run_ReRun_IsIdempotent_NoDuplicatePages()
    {
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var root = Root(client);
        var understand = Child(client, root, "Understand");
        var topCountBefore = client.GetChildPages(root).Count;
        var understandCountBefore = client.GetChildPages(understand).Count;

        // A second identical tick creates nothing new and re-pushes no body.
        client.MarkdownUpdates.Clear();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        Assert.Single(client.GetChildPages("workspace")); // still exactly one "Docs" root
        Assert.Equal(topCountBefore, client.GetChildPages(root).Count);
        Assert.Equal(understandCountBefore, client.GetChildPages(understand).Count);
        Assert.Empty(client.MarkdownUpdates); // no body re-pushed
    }

    [Fact]
    public void Run_NotionSideBodyEdit_OnDocPage_MergesBackToRepoFile()
    {
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var architecture = Child(client, Child(client, Root(client), "Understand"), "Architecture");
        client.SetPageMarkdown(architecture, "# Arch\n\nedited in notion.");

        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var file = File.ReadAllText(DocPath("understand/architecture.md"));
        Assert.Contains("edited in notion.", file);
        Assert.Contains("title: Architecture", file); // frontmatter preserved
    }

    [Fact]
    public void Run_DryRun_CreatesNoPages()
    {
        var client = new FakeNotionClient();
        var output = new StringWriter();

        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: true, output);

        Assert.Contains("--dry-run", output.ToString());
        Assert.Empty(client.GetChildPages("workspace")); // no "Docs" root minted
        Assert.False(File.Exists(BaseSnapshotStore.PathFor(_dydoRoot, DocsTreeSync.SnapshotAdapterName("workspace"))));
    }

    [Fact]
    public void Run_DryRun_WritesObservablePlan_PerformsZeroClientWrites()
    {
        var client = new FakeNotionClient();
        var output = new StringWriter();

        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: true, output);

        var text = output.ToString();
        // A non-empty plan: the root "Docs" page, then a create line per folder/doc naming its repo path and title.
        Assert.Contains("--dry-run", text);
        Assert.Contains("\"Docs\" (root page)", text);
        Assert.Contains("create", text);
        Assert.Contains("understand/architecture", text);
        Assert.Contains("(Architecture)", text);

        // Zero writes: dry-run only reads. No pages minted, no body markdown pushed, no snapshot saved.
        Assert.Empty(client.GetChildPages("workspace"));
        Assert.Empty(client.MarkdownUpdates);
        Assert.False(File.Exists(BaseSnapshotStore.PathFor(_dydoRoot, DocsTreeSync.SnapshotAdapterName("workspace"))));
    }

    [Fact]
    public void Run_EmptyScaffoldingFolder_MintsNoPage()
    {
        // A folder with no docs and no index is pruned — it never mints a barren Notion page.
        Directory.CreateDirectory(Path.Combine(_dydoRoot, "empty-dir"));

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        Assert.DoesNotContain(client.GetChildPages(Root(client)), p => p.Title == "empty-dir");
    }

    [Fact]
    public void Run_NavOrderFrontmatter_OrdersFolderSiblingsAheadOfAlphabetical()
    {
        // 'zzz' sorts last alphabetically but a nav-order override lifts it ahead of 'guides'.
        Seed("zzz/_index.md", "---\ntitle: Zzz\nnav-order: 1\n---\n\nFirst by nav-order.");

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var topTitles = client.GetChildPages(Root(client)).Select(p => p.Title).ToList();
        Assert.True(topTitles.IndexOf("Zzz") < topTitles.IndexOf("guides"),
            "nav-order:1 folder must be created before the alphabetically-earlier 'guides'");
    }

    [Fact]
    public void Run_NavOrderFrontmatter_OrdersFileSiblingsAheadOfAlphabetical()
    {
        // 'zzz-note.md' sorts last alphabetically within guides/ but nav-order:1 lifts its page ahead of 'coding.md'.
        // (nav-order applies to BOTH folders and files — decision on finding 4.)
        Seed("guides/zzz-note.md", "---\ntitle: Zzz Note\nnav-order: 1\n---\n\nFirst by nav-order.");

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var guides = Child(client, Root(client), "guides");
        var titles = client.GetChildPages(guides).Select(p => p.Title).ToList();
        Assert.True(titles.IndexOf("Zzz Note") < titles.IndexOf("Coding"),
            "nav-order:1 file must be created before the alphabetically-earlier 'Coding'");
    }

    [Fact]
    public void Run_DeeperFolder_NameCollidesWithSpineLeaf_IsNotExcluded()
    {
        // 'releases' is a spine dir (top-level). A DEEPER folder that merely shares the bare name 'releases'
        // must still be mirrored — only the full-path spine dir is excluded (finding 3).
        WriteModel("""
            {
              "objects": [
                { "type": "Release", "dir": "releases", "notionTitle": "Releases",
                  "properties": { "title": { "type": "title" } } }
              ]
            }
            """);
        Seed("releases/v1.md", "---\ntitle: v1\n---\n\nSpine release row, excluded.");
        Seed("understand/releases/notes.md", "---\ntitle: Release Notes\n---\n\nA browsable doc.");

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var root = Root(client);
        // The top-level spine 'releases' dir minted no page.
        Assert.DoesNotContain(client.GetChildPages(root), p => p.Title is "Releases" or "releases");
        // The deeper understand/releases folder IS mirrored despite the bare-name collision.
        var understand = Child(client, root, "Understand");
        var releases = Child(client, understand, "releases");
        Assert.Contains(client.GetChildPages(releases), p => p.Title == "Release Notes");
    }

    [Fact]
    public void Run_OffLimitsFiles_AreNeverMirrored()
    {
        // The guard marks the root index and files-off-limits.md off-limits (a hard §5 security invariant): a
        // mirrored page would be externally editable in Notion and merge back through the engine's file I/O,
        // bypassing the PreToolUse guard. The mirror must honor the SAME off-limits source the guard uses.
        File.WriteAllText(Path.Combine(_root, "dydo.json"), "{\"version\":1}");
        Seed("index.md", "---\ntitle: Docs Root\n---\n\nRoot index body — off-limits, must not upload.");
        Seed("understand/secret.md", "---\ntitle: Secret\n---\n\ntop secret, must not upload.");
        File.WriteAllText(Path.Combine(_dydoRoot, "files-off-limits.md"),
            "# Off-limits\n\n```\ndydo/index.md\ndydo/files-off-limits.md\ndydo/understand/secret.md\n```\n");

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var root = Root(client);
        // The off-limits root index was never uploaded — the "Docs" page is a bare container.
        Assert.Equal("", client.GetPageMarkdown(root).Markdown);
        // files-off-limits.md never became a root child page.
        Assert.DoesNotContain(client.GetChildPages(root), p => p.Title is "files-off-limits" or "Off-limits");
        // The off-limits secret doc minted no page under understand.
        var understand = Child(client, root, "Understand");
        Assert.DoesNotContain(client.GetChildPages(understand), p => p.Title == "Secret");
    }

    [Fact]
    public void Run_CreatePageFailsMidStructuralPhase_ReRunCreatesNoDuplicates()
    {
        // A CreatePage failure part-way through the structural phase (a 429/500 under the throttle, or a kill):
        // the pages minted before the crash must be persisted so the re-run reuses them and never duplicates.
        var client = new FakeNotionClient { FailCreateAfter = 3 };
        Assert.Throws<NotionApiException>(() =>
            DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter()));

        client.FailCreateAfter = null;
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        // Exactly one "Docs" root under the parent, and no folder/doc page appears twice under any parent.
        Assert.Single(client.GetChildPages("workspace"));
        AssertNoDuplicateChildTitles(client, Root(client));
    }

    [Fact]
    public void Run_DeletesAncestorAndDescendantInOneTick_ArchivesDescendantBeforeAncestor()
    {
        // A folder page is an ANCESTOR of its doc pages; Notion rejects archiving a page under an already-archived
        // ancestor (the fake now enforces this). Deleting a whole folder queues both its page AND its child doc's
        // page for archive in one tick — archiving must go descendant-first, or the child's archive 400s and (via
        // the per-archive skip guard) the child is left un-archived.
        Seed("understand/deep/_index.md", "---\ntitle: Deep\n---\n\nDeep index.");
        Seed("understand/deep/note.md", "---\ntitle: Note\n---\n\nA note.");

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var understand = Child(client, Root(client), "Understand");
        var deep = Child(client, understand, "Deep");
        var note = Child(client, deep, "Note");

        // Delete the whole folder so its page and its child's page both archive this tick.
        Directory.Delete(Path.Combine(_dydoRoot, "understand", "deep"), recursive: true);
        var ex = Record.Exception(() =>
            DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter()));

        Assert.Null(ex);
        // Descendant-first ordering archived BOTH: an ancestor-first order would 400 on the child and leave it live.
        Assert.True(client.IsArchived(note), "the descendant doc page must be archived");
        Assert.True(client.IsArchived(deep), "the ancestor folder page must be archived");
    }

    [Fact]
    public void Run_RepoDocPresentButExternalMissing_ReCreatesPage_NeverArchivesNorDeletesRepo()
    {
        // DR 033 §2: structure is repo-owned. A page missing from the tree walk while its repo doc is present is
        // Notion list eventual-consistency, NOT a deletion — the page must be re-created, never archived, and the
        // repo file never deleted. (Before the fix this crashed a fresh run by archiving a page that should exist.)
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var understand = Child(client, Root(client), "Understand");
        var arch = Child(client, understand, "Architecture");

        client.HiddenFromListing.Add(arch); // the page exists but the walk misses it this tick
        var ex = Record.Exception(() =>
            DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter()));

        Assert.Null(ex);
        Assert.False(client.IsArchived(arch)); // never archived — the invariant
        Assert.True(File.Exists(DocPath("understand/architecture.md"))); // repo file never deleted
        // Re-created rather than lost: an Architecture page is present under Understand once the walk sees it again.
        client.HiddenFromListing.Clear();
        Assert.Contains(client.GetChildPages(understand), p => p.Title == "Architecture");
    }

    [Fact]
    public void Run_DryRun_AfterRealRun_KnownDocs_LabeledUpdate()
    {
        // A dry-run after a real run: docs already in the snapshot are labeled "update", not "create".
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var output = new StringWriter();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: true, output);

        var text = output.ToString();
        Assert.Contains("update", text);
        Assert.Contains("understand/architecture", text);
    }

    [Fact]
    public void Run_DryRun_RemovedDocStillInNotion_LabeledArchive()
    {
        // Accurate archive prediction (finding 5a): a removed doc whose page STILL exists in Notion is labeled
        // "archive" — the real run would archive it. The dry-run itself archives nothing (reads only).
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        File.Delete(DocPath("understand/architecture.md"));

        var output = new StringWriter();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: true, output);

        var text = output.ToString();
        Assert.Contains("archive", text);
        Assert.Contains("understand/architecture", text);
        // No real archive happened: the page is still live under Understand.
        var understand = Child(client, Root(client), "Understand");
        Assert.Contains(client.GetChildPages(understand), p => p.Title == "Architecture");
    }

    [Fact]
    public void Run_DryRun_RemovedDocWithDriftedNotionBody_LabeledResurrect_NotArchive()
    {
        // Finding 2 (issue 0221): a removed doc whose Notion page body DRIFTED from the base is not archived by
        // the real run — the delete-vs-external-edit conflict RESURRECTS the repo file from the surviving edit.
        // The dry-run must predict "resurrect", not "archive", so the preview matches the real outcome.
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var understand = Child(client, Root(client), "Understand");
        var arch = Child(client, understand, "Architecture");
        // A colleague edits the page body in Notion since the last sync, then the repo doc is removed.
        client.SetPageMarkdown(arch, "# Arch\n\nedited in notion since base.");
        File.Delete(DocPath("understand/architecture.md"));

        var output = new StringWriter();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: true, output);

        var text = output.ToString();
        Assert.Contains("resurrect", text);
        Assert.Contains("understand/architecture", text);
        Assert.DoesNotContain("archive", text); // must NOT over-predict an archive the real run would not perform
    }

    [Fact]
    public void Run_DryRun_RemovedDocPageAlreadyGone_LabeledRetire_NotArchive()
    {
        // The accurate-plan payoff (finding 5a): a removed doc whose page is ALSO already gone from Notion is
        // labeled "retire", not "archive" — the old local-only prediction would have wrongly cried "archive".
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var understand = Child(client, Root(client), "Understand");
        var arch = Child(client, understand, "Architecture");
        client.UpdatePage(arch, new NotionPageUpdateRequest { Archived = true }); // colleague trashed it out-of-band
        File.Delete(DocPath("understand/architecture.md"));                        // and the repo doc is removed

        var output = new StringWriter();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: true, output);

        var text = output.ToString();
        Assert.Contains("retire", text);
        Assert.Contains("understand/architecture", text);
    }

    [Fact]
    public void Run_DifferentParentPages_UseSeparateSnapshotFiles()
    {
        // The notion-docs snapshot is scoped by parent page (finding 4): alternating --parent-page targets must
        // never share one snapshot, or a scratch smoke's external ids leak into the real workspace run.
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "scratch-a", dryRun: false, new StringWriter());
        DocsTreeSync.Run(client, _dydoRoot, "scratch-b", dryRun: false, new StringWriter());

        var pathA = BaseSnapshotStore.PathFor(_dydoRoot, DocsTreeSync.SnapshotAdapterName("scratch-a"));
        var pathB = BaseSnapshotStore.PathFor(_dydoRoot, DocsTreeSync.SnapshotAdapterName("scratch-b"));
        Assert.NotEqual(pathA, pathB);
        Assert.True(File.Exists(pathA));
        Assert.True(File.Exists(pathB));
    }

    private static void AssertNoDuplicateChildTitles(FakeNotionClient client, string parentId)
    {
        var children = client.GetChildPages(parentId).ToList();
        var titles = children.Select(c => c.Title).ToList();
        Assert.Equal(titles.Count, titles.Distinct().Count());
        foreach (var child in children)
            AssertNoDuplicateChildTitles(client, child.Id);
    }
}
