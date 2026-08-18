namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Sync;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

public sealed class NotionDualProjectionDeltaTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dydo-delta-v2-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Slice11Fixture_LocalEdit_LossyNativeEcho_NextDeltaIsQuietAndByteIdentical(string newline)
    {
        var fixture = Slice11Fixture(newline);
        var (client, state, page) = Setup(fixture);
        NotionSpineDelta.Run(client, state, false, false); // establish a warm cursor
        File.WriteAllText(NotePath, fixture.Replace("watchdog fixture", "__watchdog fixture__", StringComparison.Ordinal));
        client.MarkdownReadTransform = markdown => markdown.Replace("__", "**", StringComparison.Ordinal);
        NotionSpineDelta.Run(client, state, false, false);
        var expected = File.ReadAllText(NotePath);

        var result = NotionSpineDelta.Run(client, state, false, false);

        Assert.True(result.Quiet);
        Assert.Equal(expected, File.ReadAllText(NotePath));
        Assert.Contains("__watchdog fixture__", expected);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Slice11Fixture_ExternalOneSpanEdit_ImportsOnceWithExactUntouchedBytes(string newline)
    {
        var fixture = Slice11Fixture(newline);
        var (client, state, page) = Setup(fixture);
        NotionSpineDelta.Run(client, state, false, false);
        const string original = "watchdog fixture";
        const string replacement = "remote watchdog fixture";
        var external = client.StoredMarkdown(page.Id).Replace(original, replacement, StringComparison.Ordinal);
        var writes = client.MarkdownWriteCalls;
        client.SetPageMarkdown(page.Id, external);

        NotionSpineDelta.Run(client, state, false, false);
        var imported = File.ReadAllText(NotePath);
        var next = NotionSpineDelta.Run(client, state, false, false);
        var at = fixture.IndexOf(original, StringComparison.Ordinal);

        Assert.Equal(fixture[..at], imported[..at]);
        Assert.Equal(fixture[(at + original.Length)..], imported[(at + replacement.Length)..]);
        var frontmatterEnd = fixture.IndexOf("The native Markdown watchdog fixture.", StringComparison.Ordinal);
        Assert.Equal(fixture[..frontmatterEnd], imported[..frontmatterEnd]);
        Assert.Equal(writes, client.MarkdownWriteCalls);
        Assert.True(next.Quiet);
    }

    [Fact]
    public void V1MigrationConflict_RetainsCursorAndWritesShadow()
    {
        var (client, state, page) = Setup();
        var snapshot = state.SnapshotPath("Note");
        File.WriteAllText(snapshot, $$"""{"objects":[{"localId":"note","externalId":"{{page.Id}}","fields":[{"key":"title","value":"Note"}],"body":"Body.","bodyVersion":1}]}""");
        File.WriteAllText(NotePath, "---\ntitle: Note\n---\n\nRepo.");
        client.SetPageMarkdown(page.Id, "Remote.");
        var deltaPath = Directory.EnumerateFiles(Path.Combine(_root, "dydo", "_system", ".local", "sync"), "delta.json", SearchOption.AllDirectories).Single();
        var before = File.ReadAllText(deltaPath);

        var result = NotionSpineDelta.Run(client, state, false, false);

        Assert.True(result.Conflicts > 0);
        Assert.Equal(before, File.ReadAllText(deltaPath));
        Assert.True(File.Exists(Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md")));
    }

    [Fact]
    public void V1MigrationConflict_DeltaDiagnosticsNameLocalReasonAndBothPaths()
    {
        var (client, state, page) = Setup();
        File.WriteAllText(state.SnapshotPath("Note"), $$"""{"objects":[{"localId":"note","externalId":"{{page.Id}}","fields":[{"key":"title","value":"Note"}],"body":"Body.","bodyVersion":1}]}""");
        File.WriteAllText(NotePath, "---\ntitle: Note\n---\n\nRepo.");
        client.SetPageMarkdown(page.Id, "Remote.");
        var output = new StringWriter();

        NotionSpineDelta.Run(client, state, false, false, diagnostics: output);

        var diagnostic = output.ToString();
        Assert.Contains("note", diagnostic);
        Assert.Contains("migration has two-sided body edits", diagnostic);
        Assert.Contains(NotePath, diagnostic);
        Assert.Contains(Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md"), diagnostic);
    }

    [Fact]
    public void WatchdogCommandTick_ForwardsConflictDiagnosticsToItsOutput()
    {
        var (client, state, page) = Setup();
        File.WriteAllText(Path.Combine(_root, "dydo.json"), """{"version":1,"notion":{"parentPageId":"parent"}}""");
        File.WriteAllText(state.SnapshotPath("Note"), $$"""{"objects":[{"localId":"note","externalId":"{{page.Id}}","fields":[{"key":"title","value":"Note"}],"body":"Body.","bodyVersion":1}]}""");
        File.WriteAllText(NotePath, "---\ntitle: Note\n---\n\nRepo.");
        client.SetPageMarkdown(page.Id, "Remote.");
        var output = new StringWriter();
        var cwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_root);
            WatchdogCommand.RunNotionDeltaTick("token", new ConfigService(), client, false, false, output);
        }
        finally { Directory.SetCurrentDirectory(cwd); }

        var diagnostic = output.ToString();
        Assert.Contains("note", diagnostic);
        Assert.Contains("migration has two-sided body edits", diagnostic);
        Assert.Contains(NotePath, diagnostic);
        Assert.Contains(Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md"), diagnostic);
    }

    [Fact]
    public void V1Migration_QuietBoundaryReadsOnlyItsRecordedExternalPage()
    {
        var (client, state, page) = Setup();
        File.WriteAllText(state.SnapshotPath("Note"), $$"""{"objects":[{"localId":"note","externalId":"{{page.Id}}","fields":[{"key":"title","value":"Note"}],"body":"Body.","bodyVersion":1}]}""");
        var queries = client.QueryDataSourceCalls;
        var reads = client.MarkdownReadCalls;
        var afterBoundaryWindow = DateTimeOffset.Parse(page.LastEditedTime!).UtcDateTime.AddMinutes(3);

        var result = NotionSpineDelta.Run(client, state, false, false, nowUtc: afterBoundaryWindow);

        Assert.True(result.Quiet);
        Assert.Equal(queries + 1, client.QueryDataSourceCalls);
        Assert.Equal(reads + 1, client.MarkdownReadCalls);
        Assert.True(new BaseSnapshotStore(state.SnapshotPath("Note")).IsV2("note"));
    }

    [Fact]
    public void V1Migration_ExternalOnlyImport_SavesPostWriteMtimeAndNextTickIsQuiet()
    {
        var (client, state, page) = Setup();
        File.WriteAllText(state.SnapshotPath("Note"), $$"""{"objects":[{"localId":"note","externalId":"{{page.Id}}","fields":[{"key":"title","value":"Note"}],"body":"Body.","bodyVersion":1}]}""");
        client.SetPageMarkdown(page.Id, "Remote.");

        NotionSpineDelta.Run(client, state, false, false);

        var deltaPath = Directory.EnumerateFiles(Path.Combine(_root, "dydo", "_system", ".local", "sync"), "delta.json", SearchOption.AllDirectories).Single();
        var afterImport = new NotionDeltaState(deltaPath);
        var savedMtime = Assert.Single(afterImport.Files).Value;
        Assert.Equal(File.GetLastWriteTimeUtc(NotePath).Ticks, savedMtime);
        var stateAfterImport = File.ReadAllText(deltaPath);
        var writes = client.MarkdownWriteCalls;

        var next = NotionSpineDelta.Run(client, state, false, false);

        Assert.True(next.Quiet);
        Assert.Equal(writes, client.MarkdownWriteCalls);
        Assert.Equal(stateAfterImport, File.ReadAllText(deltaPath));
    }

    [Fact]
    public void PendingCreateBinding_RetainsDeltaStateThenPushesPostCrashPropertyWithoutBodyWrite()
    {
        var (client, state, _) = Setup();
        NotionSpineDelta.Run(client, state, false, false); // warm state before the simulated process loss
        var newPath = Path.Combine(Path.GetDirectoryName(NotePath)!, "new.md");
        File.WriteAllText(newPath, "---\ntitle: New\nstatus: active\n---\n\nCreated.");
        client.ThrowFirstMarkdownReadAfterBodyCreate = true;
        Assert.Throws<NotionApiException>(() => NotionSpineSync.Run(client, state, false, TextWriter.Null));
        File.WriteAllText(newPath, "---\ntitle: Changed after crash\nstatus: active\n---\n\nCreated.");
        var deltaPath = Directory.EnumerateFiles(Path.Combine(_root, "dydo", "_system", ".local", "sync"), "delta.json", SearchOption.AllDirectories).Single();
        var before = File.ReadAllText(deltaPath);
        var writes = client.MarkdownWriteCalls;

        NotionSpineDelta.Run(client, state, false, false); // exact write-id binds, but must not consume the local edit

        Assert.Equal(before, File.ReadAllText(deltaPath));
        Assert.Equal(writes, client.MarkdownWriteCalls);
        NotionSpineDelta.Run(client, state, false, false);

        Assert.Equal(writes, client.MarkdownWriteCalls); // field-only replay: no native body replace
        var created = client.QueryDataSource("ds-1").Single(page => page.Id != "page-1");
        Assert.Equal("Changed after crash", NotionRichText.Flatten(created.Properties["title"].Title));
    }

    [Fact]
    public void MarkerFreeResolution_ReadsTargetOncePlusItsRequiredWriteReceipt()
    {
        var (client, state, page) = Setup();
        File.WriteAllText(NotePath, "---\ntitle: Note\n---\n\nRepo.");
        client.SetPageMarkdown(page.Id, "Remote.");
        NotionSpineSync.Run(client, state, false, TextWriter.Null);
        var shadow = Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md");
        File.WriteAllText(shadow, "---\ntitle: Note\n---\n\nResolved.");
        var reads = client.MarkdownReadCalls;

        NotionSpineDelta.Run(client, state, false, false);

        // The normal delta hit supplies the observation; the only second read is the mandatory mutation receipt.
        Assert.Equal(reads + 2, client.MarkdownReadCalls);
    }

    [Fact]
    public void MarkerFreeResolution_NonHitTargetReadsOnlyObservationAndMandatoryReceipt()
    {
        var (client, state, page) = Setup();
        NotionSpineDelta.Run(client, state, false, false); // seed the cursor before the full-sync conflict
        File.WriteAllText(NotePath, "---\ntitle: Note\n---\n\nRepo.");
        client.SetPageMarkdown(page.Id, "Remote.");
        NotionSpineSync.Run(client, state, false, TextWriter.Null);
        var shadow = Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md");
        File.WriteAllText(shadow, "---\ntitle: Note\n---\n\nResolved.");
        var reads = client.MarkdownReadCalls;

        NotionSpineDelta.Run(client, state, false, false);

        Assert.Equal(reads + 2, client.MarkdownReadCalls);
        Assert.Equal("Resolved.", client.StoredMarkdown(page.Id));
    }

    private string NotePath => Path.Combine(_root, "dydo", "project", "notes", "note.md");

    private static string Slice11Fixture(string newline) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "slice-11-sanitized.md"))
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace("\n", newline, StringComparison.Ordinal);

    private (FakeNotionClient Client, NotionSpineState State, NotionPage Page) Setup(string body = "---\ntitle: Note\n---\n\nBody.")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(NotePath)!);
        File.WriteAllText(NotePath, body);
        var model = Path.Combine(_root, "dydo", "_system", "sync-model.json");
        Directory.CreateDirectory(Path.GetDirectoryName(model)!);
        File.WriteAllText(model, """{ "objects": [{ "type":"Note", "dir":"project/notes", "notionTitle":"Notes", "properties":{"title":{"type":"title"},"status":{"type":"select","options":["active"]}} }] }""");
        var state = NotionSpineState.Resolve(Path.Combine(_root, "dydo"), "parent", null, false, TextWriter.Null);
        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, state, false, TextWriter.Null);
        return (client, state, Assert.Single(client.QueryDataSource("ds-1")));
    }
}
