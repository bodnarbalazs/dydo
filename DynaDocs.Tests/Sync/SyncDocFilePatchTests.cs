// @test-tier: 2
namespace DynaDocs.Tests.Sync;

using System.Text;
using DynaDocs.Models;
using DynaDocs.Sync;

public sealed class SyncDocFilePatchTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dydo-patch-" + Guid.NewGuid().ToString("N"));

    public SyncDocFilePatchTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void PatchExisting_BodyOnly_PreservesEveryFrontmatterByte()
    {
        var path = Write("---\r\n# comment\r\nstatus: open\r\n---\r\n\r\nold body");
        var current = SyncDocFile.Read(path, "t", path);
        var desired = Doc("done", "new body");

        SyncDocFile.PatchExisting(path, current, desired, patchFields: false, patchBody: true);

        Assert.Equal("---\r\n# comment\r\nstatus: open\r\n---\r\n\r\nnew body", File.ReadAllText(path));
    }

    [Fact]
    public void PatchExisting_FieldOnly_PreservesBodyBytes()
    {
        var path = Write("---\nstatus: open\ncomment: keep\n---\n\nbody\n\n---\nkeep");
        var current = SyncDocFile.Read(path, "t", path);
        var desired = Doc("done", current.Body, ("comment", "keep"));

        SyncDocFile.PatchExisting(path, current, desired, patchFields: true, patchBody: false);

        Assert.Equal("---\nstatus: done\ncomment: keep\n---\n\nbody\n\n---\nkeep", File.ReadAllText(path));
    }

    [Fact]
    public void PatchExisting_AddedField_InsertsBeforeCloser()
    {
        var path = Write("---\nstatus: open\n---\n\nbody");
        var current = SyncDocFile.Read(path, "t", path);
        var desired = Doc("open", "body", ("priority", "high"));

        SyncDocFile.PatchExisting(path, current, desired, patchFields: true, patchBody: false);

        Assert.Equal("---\nstatus: open\npriority: high\n---\n\nbody", File.ReadAllText(path));
    }

    [Fact]
    public void PatchExisting_RemovedField_DeletesOnlyMatchingLine()
    {
        var path = Write("---\nstatus: open\npriority: high\n---\n\nbody");
        var current = SyncDocFile.Read(path, "t", path);
        var desired = Doc("open", "body");

        SyncDocFile.PatchExisting(path, current, desired, patchFields: true, patchBody: false);

        Assert.Equal("---\nstatus: open\n---\n\nbody", File.ReadAllText(path));
    }

    [Fact]
    public void PatchExisting_CombinedPatch_PerformsOneComposedResult()
    {
        var path = Write("---\nstatus: open\n---\n\nold");
        var current = SyncDocFile.Read(path, "t", path);
        var desired = Doc("done", "new");

        SyncDocFile.PatchExisting(path, current, desired, patchFields: true, patchBody: true);

        Assert.Equal("---\nstatus: done\n---\n\nnew", File.ReadAllText(path));
    }

    [Fact]
    public void PatchExisting_BomCrLfInsertion_ChangesOnlyTheInsertedFieldBytes()
    {
        var original = "---\r\n# keep\r\nstatus: open\r\n---\r\n\r\nbody\r\n";
        var path = WriteUtf8Bom(original);
        var current = SyncDocFile.Read(path, "t", path);
        var desired = Doc("open", current.Body, ("priority", "high"));

        SyncDocFile.PatchExisting(path, current, desired, patchFields: true, patchBody: false);

        var expected = "---\r\n# keep\r\nstatus: open\r\npriority: high\r\n---\r\n\r\nbody\r\n";
        Assert.Equal(Utf8Bom(expected), File.ReadAllBytes(path));
    }

    [Fact]
    public void PatchExisting_WithoutFrontmatter_OnlyBodyPatchCanChangeTheFile()
    {
        var path = Write("plain body");
        var current = SyncDocFile.Read(path, "t", path);
        var desired = Doc("done", "replacement");

        SyncDocFile.PatchExisting(path, current, desired, patchFields: true, patchBody: false);
        Assert.Equal("plain body", File.ReadAllText(path));

        SyncDocFile.PatchExisting(path, current, desired, patchFields: false, patchBody: true);
        Assert.Equal("replacement", File.ReadAllText(path));
    }

    [Fact]
    public void ProjectedFieldMove_PatchesOriginalThenMovesWithoutReRendering()
    {
        var oldDirectory = Path.Combine(_directory, "open");
        var newDirectory = Path.Combine(_directory, "done");
        Directory.CreateDirectory(oldDirectory);
        var oldPath = Path.Combine(oldDirectory, "t.md");
        var original = "---\r\n# keep this comment\r\nstatus: open\r\narea: backend\r\n---\r\n\r\nbody\r\n";
        File.WriteAllBytes(oldPath, Utf8Bom(original));
        var repo = SyncDocFile.Read(oldPath, "t", oldPath);
        repo.ExternalId = "page";
        var baseStore = new BaseSnapshotStore(Path.Combine(_directory, "snapshot.json"));
        baseStore.SetDualBodyBase(repo, new DynaDocs.Sync.Projection.DualBodyBase("body\n", "body\n"));
        var adapter = new FieldAdapter(new SyncRecord
        {
            ExternalId = "page",
            Fields = [new SyncField { Key = "status", Value = "done" }, new SyncField { Key = "area", Value = "backend" }],
            Body = "body\n",
        });
        var runner = new SyncRunner(adapter, baseStore, (_, fields, _) =>
            Path.Combine(fields.Single(field => field.Key == "status").Value == "done" ? newDirectory : oldDirectory, "t.md"),
            useProjectedBodies: true);

        runner.Run([repo]);

        var newPath = Path.Combine(newDirectory, "t.md");
        Assert.False(File.Exists(oldPath));
        Assert.Equal(Utf8Bom("---\r\n# keep this comment\r\nstatus: done\r\narea: backend\r\n---\r\n\r\nbody\r\n"),
            File.ReadAllBytes(newPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private string Write(string content)
    {
        var path = Path.Combine(_directory, "t.md");
        File.WriteAllText(path, content);
        return path;
    }

    private string WriteUtf8Bom(string content)
    {
        var path = Path.Combine(_directory, "t.md");
        File.WriteAllBytes(path, Utf8Bom(content));
        return path;
    }

    private static byte[] Utf8Bom(string content) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(content)];

    private static SyncDoc Doc(string status, string body, params (string Key, string Value)[] extra) => new()
    {
        LocalId = "t",
        Fields = [new SyncField { Key = "status", Value = status }, .. extra.Select(field => new SyncField { Key = field.Key, Value = field.Value })],
        Body = body,
        SourcePath = "",
    };

    private sealed class FieldAdapter(SyncRecord record) : ISyncAdapter
    {
        public IReadOnlyList<SyncRecord> ReadExternalState() => [record];

        public void Apply(SyncChangeSet changes, IDictionary<string, string> assigned, ICollection<string> deleted,
            ICollection<string> emptyBodied)
        {
        }
    }
}
