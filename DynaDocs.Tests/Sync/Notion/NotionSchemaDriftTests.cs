namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Model;
using DynaDocs.Sync.Notion.Dtos;
using DynaDocs.Sync.Notion.Provisioning;

public class NotionSchemaDriftTests : IDisposable
{
    private readonly string _dir;
    private readonly SyncModel _model;

    public NotionSchemaDriftTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dydo-drift-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        _model = SyncModelLoader.Load(Path.Combine(_dir, "dydo"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    /// <summary>Seed a data source's live schema mirroring the model, so only deliberately-added drift is rogue.</summary>
    private static void SeedModelSchema(FakeNotionClient client, string dataSourceId, SyncObjectType type)
    {
        var schema = client.DataSourceSchema(dataSourceId).Properties;
        foreach (var (name, def) in type.Properties)
            schema[name] = def.Type == "select"
                ? new NotionPropertySchema { Select = new NotionSelectSchema { Options = (def.Options ?? []).Select(o => new NotionSelectOption { Name = o }).ToList() } }
                : new NotionPropertySchema();
    }

    [Fact]
    public void Check_RogueProperty_Warns_LeavesUntouched_WhenNotPruning()
    {
        var client = new FakeNotionClient();
        SeedModelSchema(client, "ds1", _model.Object("Campaign"));
        client.DataSourceSchema("ds1").Properties["Owner"] = new NotionPropertySchema(); // rogue, not in model
        var output = new StringWriter();

        NotionSchemaDrift.Check(_model, _model.Object("Campaign"), "ds1", client, prune: false, output);

        Assert.Contains("WARN rogue property \"Owner\"", output.ToString());
        Assert.Empty(client.DataSourceUpdates);                       // left untouched
        Assert.True(client.DataSourceSchema("ds1").Properties.ContainsKey("Owner"));
    }

    [Fact]
    public void Check_RogueProperty_Deleted_WhenPruning()
    {
        var client = new FakeNotionClient();
        SeedModelSchema(client, "ds1", _model.Object("Campaign"));
        client.DataSourceSchema("ds1").Properties["Owner"] = new NotionPropertySchema();
        var output = new StringWriter();

        NotionSchemaDrift.Check(_model, _model.Object("Campaign"), "ds1", client, prune: true, output);

        Assert.Contains("PRUNE rogue property \"Owner\"", output.ToString());
        var (_, request) = Assert.Single(client.DataSourceUpdates);
        Assert.Null(request.Properties["Owner"]);                     // null body = delete
        Assert.False(client.DataSourceSchema("ds1").Properties.ContainsKey("Owner"));
    }

    [Fact]
    public void Check_SyncedReverseRelation_IsNotRogue()
    {
        // Campaign carries a "sprints" reverse column that Sprint's dual-property relation synthesises.
        // It is legitimate schema the model implies, so it must never be flagged as drift.
        var client = new FakeNotionClient();
        SeedModelSchema(client, "ds1", _model.Object("Campaign"));
        client.DataSourceSchema("ds1").Properties["sprints"] = new NotionPropertySchema { Relation = new NotionRelationSchema() };
        var output = new StringWriter();

        NotionSchemaDrift.Check(_model, _model.Object("Campaign"), "ds1", client, prune: true, output);

        Assert.DoesNotContain("sprints", output.ToString());
        Assert.Empty(client.DataSourceUpdates);
    }

    [Fact]
    public void Check_RogueSelectOption_Warns_ValueStillRoundTrips()
    {
        var client = new FakeNotionClient();
        SeedModelSchema(client, "ds1", _model.Object("Campaign"));
        client.DataSourceSchema("ds1").Properties["status"].Select!.Options.Add(new NotionSelectOption { Name = "wip" });
        var output = new StringWriter();

        NotionSchemaDrift.Check(_model, _model.Object("Campaign"), "ds1", client, prune: false, output);

        Assert.Contains("WARN rogue option \"wip\" on \"status\"", output.ToString());
        Assert.Empty(client.DataSourceUpdates);
    }

    [Fact]
    public void Check_RogueSelectOption_Pruned_ResetsToModelOptionsWithColors()
    {
        var client = new FakeNotionClient();
        SeedModelSchema(client, "ds1", _model.Object("Campaign"));
        client.DataSourceSchema("ds1").Properties["status"].Select!.Options.Add(new NotionSelectOption { Name = "wip" });
        var output = new StringWriter();

        NotionSchemaDrift.Check(_model, _model.Object("Campaign"), "ds1", client, prune: true, output);

        Assert.Contains("PRUNE rogue option \"wip\" on \"status\"", output.ToString());
        var (_, request) = Assert.Single(client.DataSourceUpdates);
        var options = request.Properties["status"].Select!.Options;
        Assert.DoesNotContain(options, o => o.Name == "wip");                 // rogue option dropped
        Assert.Equal(["proposed", "active", "done", "abandoned"], options.Select(o => o.Name));
        Assert.Equal("green", options.Single(o => o.Name == "done").Color);  // model colors reapplied
    }

    [Fact]
    public void Check_NoDrift_ReportsNothing_TouchesNothing()
    {
        var client = new FakeNotionClient();
        SeedModelSchema(client, "ds1", _model.Object("Campaign"));
        var output = new StringWriter();

        NotionSchemaDrift.Check(_model, _model.Object("Campaign"), "ds1", client, prune: true, output);

        Assert.Empty(output.ToString());
        Assert.Empty(client.DataSourceUpdates);
    }
}
