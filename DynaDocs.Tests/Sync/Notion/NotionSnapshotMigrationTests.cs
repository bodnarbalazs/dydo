namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;
using DynaDocs.Sync.Projection;

public sealed class NotionSnapshotMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dydo-v1-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void EquivalentPair_AdoptsV2WithoutMarkdownWrite()
    {
        var (client, state, page) = Setup();
        WriteV1(state, page.Id, "Body.");
        var writes = client.MarkdownWriteCalls;

        NotionSpineSync.Run(client, state, dryRun: false, TextWriter.Null);

        Assert.True(new BaseSnapshotStore(state.SnapshotPath("Note")).IsV2("note"));
        Assert.Equal(writes, client.MarkdownWriteCalls);
    }

    [Fact]
    public void RepoOnlyEdit_MigratesThenPushesProjectedBody()
    {
        var (client, state, page) = Setup();
        WriteV1(state, page.Id, "Body.");
        File.WriteAllText(NotePath, "---\ntitle: Note\n---\n\nRepo.");

        NotionSpineSync.Run(client, state, dryRun: false, TextWriter.Null);

        Assert.Equal("Repo.", client.StoredMarkdown(page.Id));
        Assert.True(new BaseSnapshotStore(state.SnapshotPath("Note")).IsV2("note"));
    }

    [Fact]
    public void ExternalOnlyEdit_MigratesThenImportsOnce()
    {
        var (client, state, page) = Setup();
        WriteV1(state, page.Id, "Body.");
        client.SetPageMarkdown(page.Id, "Remote.");

        NotionSpineSync.Run(client, state, dryRun: false, TextWriter.Null);

        Assert.Contains("Remote.", File.ReadAllText(NotePath));
        Assert.True(new BaseSnapshotStore(state.SnapshotPath("Note")).IsV2("note"));
    }

    [Fact]
    public void TwoSidedEdit_ShowsMigrationShadowAndRetainsV1()
    {
        var (client, state, page) = Setup();
        WriteV1(state, page.Id, "Body.");
        File.WriteAllText(NotePath, "---\ntitle: Note\n---\n\nRepo.");
        client.SetPageMarkdown(page.Id, "Remote.");

        NotionSpineSync.Run(client, state, dryRun: false, TextWriter.Null);

        Assert.False(new BaseSnapshotStore(state.SnapshotPath("Note")).IsV2("note"));
        Assert.True(File.Exists(Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md")));
    }

    [Fact]
    public void TruncatedRead_ShowsMigrationShadowAndRetainsV1()
    {
        var (client, state, page) = Setup();
        WriteV1(state, page.Id, "Body.");
        client.TruncatedReadFor.Add(page.Id);

        NotionSpineSync.Run(client, state, dryRun: false, TextWriter.Null);

        Assert.False(new BaseSnapshotStore(state.SnapshotPath("Note")).IsV2("note"));
        Assert.True(File.Exists(Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md")));
    }

    [Fact]
    public void MarkerFreeV1MigrationShadow_StagesResolutionAndCompletesProjectedAdoption()
    {
        var (client, state, page) = Setup();
        WriteV1(state, page.Id, "Body.");
        File.WriteAllText(NotePath, "---\ntitle: Note\n---\n\nRepo.");
        client.SetPageMarkdown(page.Id, "Remote.");
        NotionSpineSync.Run(client, state, dryRun: false, TextWriter.Null);
        var shadow = Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md");
        Assert.True(File.Exists(shadow));

        // The metadata comment and merge sentinels are deliberately gone: a normal human-edited markdown file is
        // still resolvable from its retained v1 snapshot rather than requiring a private marker protocol.
        File.WriteAllText(shadow, "---\ntitle: Note\n---\n\nResolved.");
        NotionSpineSync.Run(client, state, dryRun: false, TextWriter.Null);

        Assert.Equal("Resolved.", client.StoredMarkdown(page.Id));
        Assert.True(new BaseSnapshotStore(state.SnapshotPath("Note")).IsV2("note"));
        Assert.False(File.Exists(shadow));
    }

    [Fact]
    public void Classification_IsPureUntilTheCallerAppliesItsPlan()
    {
        var (client, state, page) = Setup();
        WriteV1(state, page.Id, "Body.");
        File.WriteAllText(NotePath, "---\ntitle: Note\n---\n\nRepo.");
        client.SetPageMarkdown(page.Id, "Remote.");
        var snapshot = state.SnapshotPath("Note");
        var before = File.ReadAllText(snapshot);
        var shadowDir = Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note");
        var adapter = new NotionSyncAdapter(client, "ds-1", new Dictionary<string, string> { ["title"] = "title" });
        var repo = SyncDocFile.Read(NotePath, "note", NotePath);

        var plan = NotionSnapshotMigration.Classify(new BaseSnapshotStore(snapshot), [repo], adapter.ReadExternalState(),
            adapter, shadowDir, Path.GetDirectoryName(NotePath)!, TextWriter.Null);

        Assert.Equal(before, File.ReadAllText(snapshot));
        Assert.False(File.Exists(Path.Combine(shadowDir, "note.md")));
        NotionSnapshotMigration.ApplyShadows(plan, shadowDir);
        Assert.True(File.Exists(Path.Combine(shadowDir, "note.md")));
    }

    [Fact]
    public void MissingCanonicalV1_WithUniqueExternalMatch_WritesCompleteObservedShadow()
    {
        var (client, state, page) = Setup();
        WriteV1(state, page.Id, "Body.");
        client.SetPageMarkdown(page.Id, "Observed remote body.");
        File.Delete(NotePath);

        NotionSpineSync.Run(client, state, dryRun: false, TextWriter.Null);

        var shadow = Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md");
        Assert.True(File.Exists(shadow));
        Assert.Contains("Observed remote body.", File.ReadAllText(shadow));
        Assert.False(new BaseSnapshotStore(state.SnapshotPath("Note")).IsV2("note"));
    }

    public static IEnumerable<object[]> MigrationParityCases()
    {
        yield return ["Body.", "Body."];
        yield return ["Repo.", "Body."];
        yield return ["Body.", "Remote."];
        yield return ["Repo.", "Remote."];
    }

    [Theory]
    [MemberData(nameof(MigrationParityCases))]
    public void FullAndDelta_V1MigrationCases_ProduceSameObservableSemantics(string repoBody, string remoteBody)
    {
        var full = RunMigrationCase(repoBody, remoteBody, delta: false);
        Directory.Delete(_root, true);
        var delta = RunMigrationCase(repoBody, remoteBody, delta: true);

        Assert.Equal(full.V2, delta.V2);
        Assert.Equal(full.Base, delta.Base);
        Assert.Equal(full.Pending, delta.Pending);
        Assert.Equal(full.Outcome, delta.Outcome);
        Assert.Equal(full.ExternalBody, delta.ExternalBody);
        Assert.Equal(full.MarkdownWrites, delta.MarkdownWrites);
        Assert.True(full.MarkdownReads > 0);
        Assert.True(delta.MarkdownReads > 0);
        Assert.Equal(full.Canonical, delta.Canonical);
        Assert.Equal(full.Shadow, delta.Shadow);
        Assert.Contains("cursor", full.DeltaState);
        Assert.Contains("cursor", delta.DeltaState);
    }

    private (bool V2, DualBodyBase? Base, string? Pending, string Outcome, string ExternalBody, int MarkdownWrites, int MarkdownReads, string? Canonical, string? Shadow, string DeltaState)
        RunMigrationCase(string repoBody, string remoteBody, bool delta)
    {
        var (client, state, page) = Setup();
        WriteV1(state, page.Id, "Body.");
        File.WriteAllText(NotePath, $"---\ntitle: Note\n---\n\n{repoBody}");
        client.SetPageMarkdown(page.Id, remoteBody);
        var canonicalBefore = File.ReadAllText(NotePath);
        var writes = client.MarkdownWriteCalls;
        var reads = client.MarkdownReadCalls;
        if (delta)
            NotionSpineDelta.Run(client, state, false, false);
        else
            NotionSpineSync.Run(client, state, false, TextWriter.Null);
        var shadow = Path.Combine(_root, "dydo", "_system", "notion_sync_spine", "Note", "note.md");
        var store = new BaseSnapshotStore(state.SnapshotPath("Note"));
        var pending = store.GetPendingBodyWrite("note");
        var canonical = File.Exists(NotePath) ? File.ReadAllText(NotePath) : null;
        var outcome = File.Exists(shadow) ? "conflict"
            : client.MarkdownWriteCalls > writes ? "push"
            : canonical != canonicalBefore ? "import"
            : "adopt";
        var deltaPath = Directory.EnumerateFiles(Path.Combine(_root, "dydo", "_system", ".local", "sync"), "delta.json", SearchOption.AllDirectories).Single();
        return (store.IsV2("note"), store.GetDualBodyBase("note"), pending is null ? null :
                $"{pending.Kind}|{pending.ExternalId}|{pending.PriorLocalBody}|{pending.PriorExternalBody}|{pending.IntendedLocalBody}", outcome,
            client.StoredMarkdown(page.Id), client.MarkdownWriteCalls, client.MarkdownReadCalls, canonical,
            File.Exists(shadow) ? File.ReadAllText(shadow) : null, File.ReadAllText(deltaPath));
    }


    private string NotePath => Path.Combine(_root, "dydo", "project", "notes", "note.md");

    private (FakeNotionClient Client, NotionSpineState State, NotionPage Page) Setup()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(NotePath)!);
        File.WriteAllText(NotePath, "---\ntitle: Note\n---\n\nBody.");
        var model = Path.Combine(_root, "dydo", "_system", "sync-model.json");
        Directory.CreateDirectory(Path.GetDirectoryName(model)!);
        File.WriteAllText(model, """{ "objects": [{ "type":"Note", "dir":"project/notes", "notionTitle":"Notes", "properties":{"title":{"type":"title"}} }] }""");
        var state = NotionSpineState.Resolve(Path.Combine(_root, "dydo"), "parent", null, dryRun: false, TextWriter.Null);
        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, state, dryRun: false, TextWriter.Null);
        return (client, state, Assert.Single(client.QueryDataSource("ds-1")));
    }

    private static void WriteV1(NotionSpineState state, string externalId, string body)
    {
        var path = state.SnapshotPath("Note");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""{"objects":[{"localId":"note","externalId":"{{externalId}}","fields":[{"key":"title","value":"Note"}],"body":"{{body}}","bodyVersion":1}]}""");
    }
}
