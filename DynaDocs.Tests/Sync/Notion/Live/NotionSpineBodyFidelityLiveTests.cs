namespace DynaDocs.Tests.Sync.Notion.Live;

using DynaDocs.Sync;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;
using DynaDocs.Sync.Notion.Provisioning;

/// <summary>Live proof for DR-043.  Every test provisions below its own <see cref="NotionLiveTestBase.ChildPageId"/>;
/// it never reads the configured board parent.</summary>
[Trait("Category", "notion-live")]
public sealed class NotionSpineBodyFidelityLiveTests : NotionLiveTestBase
{
    [NotionLiveFact]
    public void ExistingV2LocalEdit_NativeEcho_NextDeltaIsQuietAndFileBytesAreIdentical()
    {
        var scope = SetUpTrackedFixture();
        try
        {
            var edited = scope.Fixture.Replace("watchdog fixture.", "local watchdog fixture!", StringComparison.Ordinal);
            File.WriteAllText(scope.NotePath, edited);

            var push = NotionSpineDelta.Run(Client, scope.State, census: false, validateProvisioning: false);
            var echo = Client.GetPageMarkdown(scope.PageId);
            Assert.False(echo.Truncated);
            Assert.Contains("local watchdog fixture!", echo.Markdown, StringComparison.OrdinalIgnoreCase);
            var expected = File.ReadAllBytes(scope.NotePath);

            var tick = NotionSpineDelta.Run(Client, scope.State, census: false, validateProvisioning: false);

            Assert.False(push.Quiet);
            Assert.Equal(1, push.Reconciled);
            Assert.True(tick.Quiet);
            Assert.Equal(expected, File.ReadAllBytes(scope.NotePath));
        }
        finally { DeleteScope(scope.Root); }
    }

    [NotionLiveFact]
    public void ExternalNativeMarkdownEdit_ImportsOneSurgicalSpan_ThenDeltaIsQuiet()
    {
        var scope = SetUpTrackedFixture();
        try
        {
            const string original = "watchdog fixture.";
            const string replacement = "remote watchdog fixture.";
            var before = File.ReadAllText(scope.NotePath);
            var position = before.IndexOf(original, StringComparison.Ordinal);
            Assert.True(position >= 0);
            Client.UpdatePageMarkdown(scope.PageId, scope.Body.Replace(original, replacement, StringComparison.Ordinal), allowDeletingContent: true);

            var import = NotionSpineDelta.Run(Client, scope.State, census: false, validateProvisioning: false);
            var imported = File.ReadAllText(scope.NotePath);
            var next = NotionSpineDelta.Run(Client, scope.State, census: false, validateProvisioning: false);

            Assert.Equal(before[..position], imported[..position]);
            Assert.Equal(before[(position + original.Length)..], imported[(position + replacement.Length)..]);
            Assert.Equal(replacement, imported.Substring(position, replacement.Length));
            Assert.Equal(before[..(before.IndexOf("---\n\n", StringComparison.Ordinal) + 5)],
                imported[..(imported.IndexOf("---\n\n", StringComparison.Ordinal) + 5)]);
            Assert.False(import.Quiet);
            Assert.Equal(1, import.Reconciled);
            Assert.True(next.Quiet);
        }
        finally { DeleteScope(scope.Root); }
    }

    [NotionLiveFact]
    public void NotionOriginatedCreate_ImportsPristineFile_ThenDeltaIsQuiet()
    {
        var root = Path.Combine(Path.GetTempPath(), "dydo-live-fidelity-" + Guid.NewGuid().ToString("N"));
        try
        {
            var dydoRoot = CreateProject(root, includeFixture: false);
            var state = NotionSpineState.Resolve(dydoRoot, configuredParentPageId: null, ChildPageId, dryRun: false, TextWriter.Null);
            NotionSpineSync.Run(Client, state, dryRun: false, TextWriter.Null);
            var dataSource = Assert.Single(NotionProvisioner.LoadTracked(state.ProvisionPath));
            var page = Client.CreatePage(new NotionPageCreateRequest
            {
                Parent = NotionParent.DataSource(dataSource.DataSourceId),
                Properties = new() { ["title"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("Native create") } },
                Markdown = "Created natively.\n",
            });

            NotionSpineDelta.Run(Client, state, census: false, validateProvisioning: false);
            var path = Path.Combine(dydoRoot, "project", "notes", "native-create.md");
            var imported = File.ReadAllBytes(path);
            var next = NotionSpineDelta.Run(Client, state, census: false, validateProvisioning: false);

            Assert.Contains("Created natively.", File.ReadAllText(path));
            Assert.True(next.Quiet);
            Assert.Equal(imported, File.ReadAllBytes(path));
            Assert.Equal(page.Id, new BaseSnapshotStore(state.SnapshotPath("Note")).Get("native-create")!.ExternalId);
        }
        finally { DeleteScope(root); }
    }

    private (string Root, NotionSpineState State, string NotePath, string Fixture, string Body, string PageId) SetUpTrackedFixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "dydo-live-fidelity-" + Guid.NewGuid().ToString("N"));
        var dydoRoot = CreateProject(root, includeFixture: true);
        var state = NotionSpineState.Resolve(dydoRoot, configuredParentPageId: null, ChildPageId, dryRun: false, TextWriter.Null);
        NotionSpineSync.Run(Client, state, dryRun: false, TextWriter.Null);
        NotionSpineDelta.Run(Client, state, census: false, validateProvisioning: false);
        var dataSource = Assert.Single(NotionProvisioner.LoadTracked(state.ProvisionPath));
        var page = Assert.Single(Client.QueryDataSource(dataSource.DataSourceId));
        var fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "slice-11-sanitized.md"));
        return (root, state, Path.Combine(dydoRoot, "project", "notes", "slice-11.md"), fixture,
            fixture[(fixture.IndexOf("---\n\n", StringComparison.Ordinal) + 5)..], page.Id);
    }

    private static string CreateProject(string root, bool includeFixture)
    {
        var dydoRoot = Path.Combine(root, "dydo");
        var notes = Path.Combine(dydoRoot, "project", "notes");
        Directory.CreateDirectory(notes);
        Directory.CreateDirectory(Path.Combine(dydoRoot, "_system"));
        File.WriteAllText(Path.Combine(dydoRoot, "_system", "sync-model.json"),
            """{ "objects": [{ "type":"Note", "dir":"project/notes", "notionTitle":"Live fidelity notes", "properties":{"title":{"type":"title"},"status":{"type":"select","options":["active"]}} }] }""");
        if (includeFixture)
            File.WriteAllText(Path.Combine(notes, "slice-11.md"), File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "slice-11-sanitized.md")));
        return dydoRoot;
    }

    private static void DeleteScope(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
