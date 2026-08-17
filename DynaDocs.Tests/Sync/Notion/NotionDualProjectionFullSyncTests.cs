namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

public sealed class NotionDualProjectionFullSyncTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dydo-full-v2-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void Slice11Fixture_LocalEdit_ThenLossyNativeEcho_LeavesNextFullTickByteIdentical()
    {
        var fixture = Slice11Fixture();
        var (client, state, page) = Setup(fixture);
        File.WriteAllText(NotePath, fixture.Replace("watchdog fixture", "__watchdog fixture__", StringComparison.Ordinal));
        client.MarkdownReadTransform = markdown => markdown.Replace("__", "**", StringComparison.Ordinal);
        NotionSpineSync.Run(client, state, false, TextWriter.Null);
        var expected = File.ReadAllText(NotePath);
        var writes = client.MarkdownWriteCalls;

        NotionSpineSync.Run(client, state, false, TextWriter.Null);

        Assert.Equal(expected, File.ReadAllText(NotePath));
        Assert.Equal(writes, client.MarkdownWriteCalls);
        Assert.Contains("__watchdog fixture__", expected);
    }

    [Fact]
    public void Slice11Fixture_GenuineExternalEdit_ImportsOnceWithFrontmatterAndBodyExact()
    {
        var fixture = Slice11Fixture();
        var (client, state, page) = Setup(fixture);
        client.SetPageMarkdown(page.Id, "External edit.");

        NotionSpineSync.Run(client, state, false, TextWriter.Null);
        var expected = File.ReadAllText(NotePath);
        NotionSpineSync.Run(client, state, false, TextWriter.Null);

        Assert.Equal(expected, File.ReadAllText(NotePath));
        Assert.Equal("---\ntitle: Slice 11\nstatus: active\n---\n\nExternal edit.\n", expected);
    }

    [Fact]
    public void Slice11Fixture_ExternalOneSpanMutation_PreservesEveryOtherCanonicalByte()
    {
        var fixture = Slice11Fixture();
        var (client, state, page) = Setup(fixture);
        var external = client.StoredMarkdown(page.Id).Replace("watchdog fixture", "remote watchdog fixture", StringComparison.Ordinal);
        var writes = client.MarkdownWriteCalls;

        client.SetPageMarkdown(page.Id, external);
        NotionSpineSync.Run(client, state, false, TextWriter.Null);

        var expected = fixture.Replace("watchdog fixture", "remote watchdog fixture", StringComparison.Ordinal);
        Assert.Equal(expected, File.ReadAllText(NotePath));
        Assert.Equal(writes, client.MarkdownWriteCalls);
        Assert.True(NotionSpineDelta.Run(client, state, false, false).Quiet);
    }

    [Fact]
    public void ResolutionChoosingOriginalBase_IsStillJournaledAndReceipted()
    {
        var (client, state, page) = Setup("Body.");
        File.WriteAllText(NotePath, "---\ntitle: Note\nstatus: active\n---\n\nRepo.");
        client.SetPageMarkdown(page.Id, "Remote.");
        NotionSpineSync.Run(client, state, false, TextWriter.Null);
        var shadow = Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md");
        File.WriteAllText(shadow, "---\ntitle: Note\nstatus: active\n---\n\nBody.");
        var writes = client.MarkdownWriteCalls;

        NotionSpineSync.Run(client, state, false, TextWriter.Null);

        var store = new BaseSnapshotStore(state.SnapshotPath("Note"));
        Assert.Equal(writes + 1, client.MarkdownWriteCalls);
        Assert.Equal("Body.", client.StoredMarkdown(page.Id));
        Assert.False(File.Exists(shadow));
        Assert.Null(store.GetPendingBodyWrite("note"));
        Assert.Null(store.GetResolutionCleanupReceipt("note"));
    }

    [Fact]
    public void ResolvedShadow_IsRetainedUntilReceiptThenRemoved()
    {
        var (client, state, page) = Setup("Body.");
        File.WriteAllText(NotePath, "---\ntitle: Note\nstatus: active\n---\n\nRepo.");
        client.SetPageMarkdown(page.Id, "Remote.");
        NotionSpineSync.Run(client, state, false, TextWriter.Null);
        var shadow = Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md");
        File.WriteAllText(shadow, "---\ntitle: Note\nstatus: active\n---\n\nResolved.");
        client.FailMarkdownUpdate = true;

        Assert.Throws<NotionApiException>(() => NotionSpineSync.Run(client, state, false, TextWriter.Null));
        Assert.True(File.Exists(shadow));
        client.FailMarkdownUpdate = false;
        NotionSpineSync.Run(client, state, false, TextWriter.Null);

        Assert.False(File.Exists(shadow));
        Assert.Equal("Resolved.", client.StoredMarkdown(page.Id));
    }

    [Fact]
    public void TruncatedPendingUpdate_IsUnhandledWithoutClearingIntentOrAdvancingBase()
    {
        var (client, state, page) = Setup("Body.");
        File.WriteAllText(NotePath, "---\ntitle: Note\nstatus: active\n---\n\nChanged.");
        client.FailMarkdownUpdate = true;
        Assert.Throws<NotionApiException>(() => NotionSpineSync.Run(client, state, false, TextWriter.Null));
        var before = new BaseSnapshotStore(state.SnapshotPath("Note")).GetDualBodyBase("note")!;
        client.FailMarkdownUpdate = false;
        client.TruncatedReadFor.Add(page.Id);

        NotionSpineSync.Run(client, state, false, TextWriter.Null);

        var after = new BaseSnapshotStore(state.SnapshotPath("Note"));
        Assert.NotNull(after.GetPendingBodyWrite("note"));
        Assert.Equal(before, after.GetDualBodyBase("note"));
        Assert.True(File.Exists(Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md")));
    }

    [Fact]
    public void TruncatedPendingCreate_IsUnhandledWithoutAdoptingTheExactWriteId()
    {
        var (client, state, _) = Setup("Body.");
        var newPath = Path.Combine(Path.GetDirectoryName(NotePath)!, "new.md");
        File.WriteAllText(newPath, "---\ntitle: New\nstatus: active\n---\n\nNew body.");
        client.ThrowFirstMarkdownReadAfterBodyCreate = true;
        Assert.Throws<NotionApiException>(() => NotionSpineSync.Run(client, state, false, TextWriter.Null));
        var created = client.QueryDataSource("ds-1").Single(page => page.Id != "page-1");
        client.TruncatedReadFor.Add(created.Id);

        NotionSpineSync.Run(client, state, false, TextWriter.Null);

        var store = new BaseSnapshotStore(state.SnapshotPath("Note"));
        Assert.NotNull(store.GetPendingBodyWrite("new"));
        Assert.Null(store.Get("new")!.ExternalId);
        Assert.True(File.Exists(Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "new.md")));
    }

    [Fact]
    public void TruncatedPendingResolution_RetainsIntentBaseAndHumanShadow()
    {
        var (client, state, page) = Setup("Body.");
        File.WriteAllText(NotePath, "---\ntitle: Note\nstatus: active\n---\n\nRepo.");
        client.SetPageMarkdown(page.Id, "Remote.");
        NotionSpineSync.Run(client, state, false, TextWriter.Null);
        var shadow = Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md");
        File.WriteAllText(shadow, "---\ntitle: Note\nstatus: active\n---\n\nResolved.");
        client.FailMarkdownUpdate = true;
        Assert.Throws<NotionApiException>(() => NotionSpineSync.Run(client, state, false, TextWriter.Null));
        var before = new BaseSnapshotStore(state.SnapshotPath("Note")).GetDualBodyBase("note")!;
        client.FailMarkdownUpdate = false;
        client.TruncatedReadFor.Add(page.Id);

        NotionSpineSync.Run(client, state, false, TextWriter.Null);

        var after = new BaseSnapshotStore(state.SnapshotPath("Note"));
        Assert.NotNull(after.GetPendingBodyWrite("note"));
        Assert.Equal(before, after.GetDualBodyBase("note"));
        Assert.True(File.Exists(shadow));
    }

    [Fact]
    public void Resolution_WithUnresolvableRelation_NormalizesBaseThenPushesWhenTargetExists()
    {
        var (client, state, page) = SetupSelfRelation();
        File.WriteAllText(NotePath, "---\ntitle: Note\nblocked-by: later\n---\n\nRepo.");
        client.SetPageMarkdown(page.Id, "Remote.");
        NotionSpineSync.Run(client, state, false, TextWriter.Null);
        var shadow = Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md");
        File.WriteAllText(shadow, "---\ntitle: Note\nblocked-by: later\n---\n\nResolved.");

        NotionSpineSync.Run(client, state, false, TextWriter.Null);

        var afterResolution = new BaseSnapshotStore(state.SnapshotPath("Note")).Get("note")!;
        Assert.DoesNotContain(afterResolution.Fields, field => field.Key == "blocked-by");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(NotePath)!, "later.md"), "---\ntitle: Later\n---\n\nLater.");
        NotionSpineSync.Run(client, state, false, TextWriter.Null);
        NotionSpineSync.Run(client, state, false, TextWriter.Null);

        var later = client.QueryDataSource("ds-1").Single(candidate => candidate.Id != page.Id);
        Assert.Contains(client.QueryDataSource("ds-1").Single(candidate => candidate.Id == page.Id)
            .Properties["blocked-by"].Relation!, relation => relation.Id == later.Id);
    }

    [Theory]
    [InlineData("after-intent-save")]
    [InlineData("after-canonical-replace")]
    public void ResolutionPromotionCrashBoundaries_RestartConvergesWithoutLosingShadow(string boundary)
    {
        var (client, state, page) = Setup("Body.");
        File.WriteAllText(NotePath, "---\ntitle: Note\nstatus: active\n---\n\nRepo.");
        client.SetPageMarkdown(page.Id, "Remote.");
        NotionSpineSync.Run(client, state, false, TextWriter.Null);
        var shadow = Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md");
        File.WriteAllText(shadow, "---\ntitle: Note\nstatus: active\n---\n\nResolved.");
        NotionSpineSync.ResolutionPromotionFailpoint = point =>
        {
            if (point == boundary) throw new InvalidOperationException("simulated crash");
        };
        try
        {
            Assert.Throws<InvalidOperationException>(() => NotionSpineSync.Run(client, state, false, TextWriter.Null));
        }
        finally { NotionSpineSync.ResolutionPromotionFailpoint = null; }
        Assert.True(File.Exists(shadow));
        Assert.NotNull(new BaseSnapshotStore(state.SnapshotPath("Note")).GetPendingBodyWrite("note"));

        NotionSpineSync.Run(client, state, false, TextWriter.Null);

        Assert.False(File.Exists(shadow));
        Assert.Equal("Resolved.", client.StoredMarkdown(page.Id));
    }

    [Fact]
    public void ResolutionReceiptCrashBeforeShadowDelete_RestartConverges()
    {
        var (client, state, page) = Setup("Body.");
        File.WriteAllText(NotePath, "---\ntitle: Note\nstatus: active\n---\n\nRepo.");
        client.SetPageMarkdown(page.Id, "Remote.");
        NotionSpineSync.Run(client, state, false, TextWriter.Null);
        var shadow = Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md");
        File.WriteAllText(shadow, "---\ntitle: Note\nstatus: active\n---\n\nResolved.");
        SyncRunner.ResolutionReceiptFailpoint = point =>
        {
            if (point == "after-receipt-save") throw new InvalidOperationException("simulated crash");
        };
        try { Assert.Throws<InvalidOperationException>(() => NotionSpineSync.Run(client, state, false, TextWriter.Null)); }
        finally { SyncRunner.ResolutionReceiptFailpoint = null; }
        Assert.True(new BaseSnapshotStore(state.SnapshotPath("Note")).IsV2("note"));
        Assert.Null(new BaseSnapshotStore(state.SnapshotPath("Note")).GetPendingBodyWrite("note"));
        Assert.NotNull(new BaseSnapshotStore(state.SnapshotPath("Note")).GetResolutionCleanupReceipt("note"));
        Assert.True(File.Exists(shadow));
        client.SetPageMarkdown(page.Id, "Concurrent external.");
        var writes = client.MarkdownWriteCalls;

        NotionSpineSync.Run(client, state, false, TextWriter.Null);
        Assert.False(File.Exists(shadow));
        Assert.Equal(writes, client.MarkdownWriteCalls);
        Assert.Contains("Concurrent external.", File.ReadAllText(NotePath));
        Assert.Null(new BaseSnapshotStore(state.SnapshotPath("Note")).GetResolutionCleanupReceipt("note"));
    }

    private string NotePath => Path.Combine(_root, "dydo", "project", "notes", "note.md");

    private static string Slice11Fixture() => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "slice-11-sanitized.md"));

    private (FakeNotionClient Client, NotionSpineState State, NotionPage Page) Setup(string body)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(NotePath)!);
        File.WriteAllText(NotePath, body.StartsWith("---\n", StringComparison.Ordinal)
            ? body
            : $"---\ntitle: Note\nstatus: active\n---\n\n{body}");
        var model = Path.Combine(_root, "dydo", "_system", "sync-model.json");
        Directory.CreateDirectory(Path.GetDirectoryName(model)!);
        File.WriteAllText(model, """{ "objects": [{ "type":"Note", "dir":"project/notes", "notionTitle":"Notes", "properties":{"title":{"type":"title"},"status":{"type":"select","options":["active"]}} }] }""");
        var state = NotionSpineState.Resolve(Path.Combine(_root, "dydo"), "parent", null, false, TextWriter.Null);
        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, state, false, TextWriter.Null);
        return (client, state, Assert.Single(client.QueryDataSource("ds-1")));
    }

    private (FakeNotionClient Client, NotionSpineState State, NotionPage Page) SetupSelfRelation()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(NotePath)!);
        File.WriteAllText(NotePath, "---\ntitle: Note\n---\n\nBody.");
        var model = Path.Combine(_root, "dydo", "_system", "sync-model.json");
        Directory.CreateDirectory(Path.GetDirectoryName(model)!);
        File.WriteAllText(model, """{ "objects": [{ "type":"Note", "dir":"project/notes", "notionTitle":"Notes", "properties":{"title":{"type":"title"},"blocked-by":{"type":"relation","to":"Note"}} }] }""");
        var state = NotionSpineState.Resolve(Path.Combine(_root, "dydo"), "parent", null, false, TextWriter.Null);
        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, state, false, TextWriter.Null);
        return (client, state, Assert.Single(client.QueryDataSource("ds-1")));
    }
}
