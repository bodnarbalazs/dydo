namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Services;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Provisioning;
using DynaDocs.Utils;

[Collection("ConsoleOutput")]
public class NotionResetTests : IDisposable
{
    private readonly string _dir;

    public NotionResetTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dydo-notion-reset-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void Execute_ArchivesTracked_ClearsState_Recreates_NoOrphanDuplicates()
    {
        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out var client, out var statePath);
        try
        {
            Directory.SetCurrentDirectory(project);

            // Provision the board once, then capture the tracked ids the reset must wipe.
            Assert.Equal(ExitCodes.Success, Sync(client));
            var originalIds = NotionProvisioner.LoadTracked(statePath).Select(t => t.DatabaseId).ToList();
            Assert.NotEmpty(originalIds);
            var createdBeforeReset = client.CreatedDatabases.Count;

            var code = NotionReset.Execute(
                "tok", new ConfigService(), _ => client, dryRun: false, confirm: () => true,
                new StringWriter(), new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            // Every tracked database was archived (the wipe half).
            Assert.Equal(originalIds.OrderBy(x => x), client.ArchivedDatabases.OrderBy(x => x));
            // The recreate minted a FRESH database per type — new ids reusing none of the archived ones, so the
            // old databases are trashed, never left beside their duplicates (the no-orphan-duplicates invariant).
            var newIds = NotionProvisioner.LoadTracked(statePath).Select(t => t.DatabaseId).ToList();
            Assert.Equal(originalIds.Count, newIds.Count);
            Assert.Empty(newIds.Intersect(originalIds));
            Assert.Equal(createdBeforeReset * 2, client.CreatedDatabases.Count);
            // The repo doc re-materialized as a page in the fresh database.
            var newDataSource = NotionProvisioner.LoadTracked(statePath).Single().DataSourceId;
            Assert.NotEmpty(client.QueryDataSource(newDataSource));
        }
        finally { Directory.SetCurrentDirectory(savedCwd); }
    }

    [Fact]
    public void Execute_DryRun_PrintsPlan_MakesZeroWrites()
    {
        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out var client, out var statePath);
        try
        {
            Directory.SetCurrentDirectory(project);
            Assert.Equal(ExitCodes.Success, Sync(client));
            var stateBefore = File.ReadAllText(statePath);
            var createdBefore = client.CreatedDatabases.Count;

            var output = new StringWriter();
            var code = NotionReset.Execute(
                "tok", new ConfigService(), _ => client, dryRun: true,
                confirm: () => throw new InvalidOperationException("confirm must not run in dry-run"),
                output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            var text = output.ToString();
            Assert.Contains("--dry-run", text);
            Assert.Contains("would archive", text);
            Assert.Contains("Campaign", text);
            // Zero writes: nothing archived, no new database minted, provision state byte-identical.
            Assert.Empty(client.ArchivedDatabases);
            Assert.Equal(createdBefore, client.CreatedDatabases.Count);
            Assert.Equal(stateBefore, File.ReadAllText(statePath));
        }
        finally { Directory.SetCurrentDirectory(savedCwd); }
    }

    [Fact]
    public void Execute_ArchivesTypeDroppedFromModel_RecreatesOnlyCurrentModel()
    {
        // LoadTracked reads provision.json independent of the model, so a type dropped from the model since it
        // was last provisioned is still tracked — reset must archive its now-orphaned database too (the archive
        // loop iterates the raw state file, the recreate loop iterates the current model: two independent lists).
        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out var client, out var statePath);
        try
        {
            Directory.SetCurrentDirectory(project);

            // Provision a TWO-type board (Campaign + Sprint), then drop Sprint from the model.
            WriteModel(project, TwoTypeModel);
            Assert.Equal(ExitCodes.Success, Sync(client));
            var trackedIds = NotionProvisioner.LoadTracked(statePath).Select(t => t.DatabaseId).OrderBy(x => x).ToList();
            Assert.Equal(2, trackedIds.Count);
            WriteModel(project, OneTypeModel);

            var code = NotionReset.Execute(
                "tok", new ConfigService(), _ => client, dryRun: false, confirm: () => true,
                new StringWriter(), new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            // BOTH databases were archived — including the dropped Sprint's, which the current model no longer names.
            Assert.Equal(trackedIds, client.ArchivedDatabases.OrderBy(x => x));
            // Only the surviving type is recreated: the orphan does not linger in state.
            var recreated = NotionProvisioner.LoadTracked(statePath);
            Assert.Equal("Campaign", recreated.Single().ObjectType);
        }
        finally { Directory.SetCurrentDirectory(savedCwd); }
    }

    [Fact]
    public void Execute_ConfirmDeclined_Aborts_MakesZeroWrites()
    {
        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out var client, out _);
        try
        {
            Directory.SetCurrentDirectory(project);
            Assert.Equal(ExitCodes.Success, Sync(client));
            var createdBefore = client.CreatedDatabases.Count;

            var output = new StringWriter();
            var code = NotionReset.Execute(
                "tok", new ConfigService(), _ => client, dryRun: false, confirm: () => false,
                output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("aborted", output.ToString());
            Assert.Empty(client.ArchivedDatabases);
            Assert.Equal(createdBefore, client.CreatedDatabases.Count);
        }
        finally { Directory.SetCurrentDirectory(savedCwd); }
    }

    [Fact]
    public void Execute_NotionApiError_ReportsAndExitsToolError()
    {
        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out var client, out _);
        try
        {
            Directory.SetCurrentDirectory(project);
            Assert.Equal(ExitCodes.Success, Sync(client));
            client.FailArchiveDatabase = true;

            var err = new StringWriter();
            var code = NotionReset.Execute(
                "tok", new ConfigService(), _ => client, dryRun: false, confirm: () => true,
                new StringWriter(), err);

            Assert.Equal(ExitCodes.ToolError, code);
            Assert.Contains("Notion API error", err.ToString());
        }
        finally { Directory.SetCurrentDirectory(savedCwd); }
    }

    [Fact]
    public void Execute_NoToken_ReportsNotConfigured_ExitsSuccess()
    {
        var err = new StringWriter();
        var code = NotionReset.Execute(
            token: null, new ConfigService(), _ => new FakeNotionClient(), dryRun: false, confirm: () => true,
            new StringWriter(), err);

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("not configured", err.ToString());
    }

    [Fact]
    public void Execute_NoProject_ReportsMissingConfig_ExitsSuccess()
    {
        var savedCwd = Directory.GetCurrentDirectory();
        var bare = Path.Combine(_dir, "bare");
        Directory.CreateDirectory(bare);
        var err = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(bare);
            var code = NotionReset.Execute(
                "tok", new ConfigService(), _ => new FakeNotionClient(), dryRun: false, confirm: () => true,
                new StringWriter(), err);

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("no dydo.json", err.ToString());
        }
        finally { Directory.SetCurrentDirectory(savedCwd); }
    }

    private static int Sync(FakeNotionClient client) =>
        NotionSyncService.Execute("tok", new ConfigService(), _ => client, dryRun: false, new StringWriter(), new StringWriter());

    private static void WriteModel(string project, string json) =>
        File.WriteAllText(Path.Combine(project, "dydo", "_system", "sync-model.json"), json);

    private const string TwoTypeModel = """
        {
          "objects": [
            { "type": "Campaign", "dir": "project/campaigns", "notionTitle": "Campaigns",
              "properties": { "title": { "type": "title" }, "status": { "type": "select", "options": ["active", "done"] } } },
            { "type": "Sprint", "dir": "project/sprints", "notionTitle": "Sprints",
              "properties": { "title": { "type": "title" }, "campaign": { "type": "relation", "to": "Campaign" } } }
          ]
        }
        """;

    private const string OneTypeModel = """
        {
          "objects": [
            { "type": "Campaign", "dir": "project/campaigns", "notionTitle": "Campaigns",
              "properties": { "title": { "type": "title" }, "status": { "type": "select", "options": ["active", "done"] } } }
          ]
        }
        """;

    /// <summary>Build a minimal dydo project pinned to a single Campaign type (deterministic ds ids) with one
    /// campaign doc and a configured parent page, plus a fake client. Returns the project root and the
    /// provision-state path.</summary>
    private string SetUpProject(out FakeNotionClient client, out string statePath)
    {
        var project = Path.Combine(_dir, "proj-" + Guid.NewGuid().ToString("N")[..6]);
        var dydoRoot = Path.Combine(project, "dydo");
        var campaigns = Path.Combine(dydoRoot, "project", "campaigns");
        Directory.CreateDirectory(campaigns);
        File.WriteAllText(Path.Combine(project, "dydo.json"), "{\"version\":1,\"notion\":{\"parentPageId\":\"page-root\"}}");

        var modelPath = Path.Combine(dydoRoot, "_system", "sync-model.json");
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        File.WriteAllText(modelPath, """
            {
              "objects": [
                { "type": "Campaign", "dir": "project/campaigns", "notionTitle": "Campaigns",
                  "properties": { "title": { "type": "title" }, "status": { "type": "select", "options": ["active", "done"] } } }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(campaigns, "c1.md"), "---\ntitle: C1\nstatus: active\n---\n\nBody.");

        statePath = NotionProvisioner.PathFor(dydoRoot);
        client = new FakeNotionClient();
        return project;
    }
}
