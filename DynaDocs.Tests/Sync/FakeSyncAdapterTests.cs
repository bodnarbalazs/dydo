namespace DynaDocs.Tests.Sync;

using DynaDocs.Models;
using DynaDocs.Sync;

public class FakeSyncAdapterTests
{
    private static List<SyncField> F(params (string, string)[] fields) =>
        fields.Select(f => new SyncField { Key = f.Item1, Value = f.Item2 }).ToList();

    [Fact]
    public void Seed_ThenRead_ReturnsRecord()
    {
        var a = new FakeSyncAdapter();
        a.Seed("ext-1", F(("status", "open")), "body");

        var state = a.ReadExternalState();
        Assert.Single(state);
        Assert.Equal("ext-1", state[0].ExternalId);
        Assert.Equal("body", state[0].Body);
    }

    [Fact]
    public void Apply_CreateUpsert_AssignsIdKeyedByLocalId()
    {
        var a = new FakeSyncAdapter();
        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert { LocalId = "t", ExternalId = null, Fields = F(("status", "open")), Body = "b" });

        var assigned = new Dictionary<string, string>();
        a.Apply(changes, assigned);

        Assert.True(assigned.ContainsKey("t"));
        Assert.Single(a.ReadExternalState());
    }

    [Fact]
    public void Apply_UpdateUpsert_KeepsIdNoNewAssignment()
    {
        var a = new FakeSyncAdapter();
        a.Seed("ext-1", F(("status", "open")), "b");

        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert { LocalId = "t", ExternalId = "ext-1", Fields = F(("status", "done")), Body = "b" });
        var assigned = new Dictionary<string, string>();
        a.Apply(changes, assigned);

        Assert.Empty(assigned);
        Assert.Equal("done", a.ReadExternalState()[0].Fields.First(f => f.Key == "status").Value);
    }

    [Fact]
    public void Apply_Delete_RemovesRecord()
    {
        var a = new FakeSyncAdapter();
        a.Seed("ext-1", F(("status", "open")), "b");

        var changes = new SyncChangeSet();
        changes.Deletes.Add("ext-1");
        a.Apply(changes, new Dictionary<string, string>());

        Assert.Empty(a.ReadExternalState());
    }

    [Fact]
    public void Edit_ReplacesExistingRecord()
    {
        var a = new FakeSyncAdapter();
        a.Seed("ext-1", F(("status", "open")), "b");
        a.Edit("ext-1", F(("status", "done")), "b2");

        var rec = a.ReadExternalState()[0];
        Assert.Equal("done", rec.Fields.First(f => f.Key == "status").Value);
        Assert.Equal("b2", rec.Body);
    }
}
