namespace DynaDocs.Sync;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;

/// <summary>
/// Persists the last-synced <see cref="SyncDoc"/> state per object (the 3-way-merge base) plus
/// the local↔external id mapping. Lives under <c>dydo/_system/.local/sync/&lt;adapter&gt;/</c>
/// — gitignored and outside the canonical synced tree, so the shadow never syncs or commits
/// (Decision 025 open item: base-snapshot store). Serialized with source-generated
/// System.Text.Json for Native AOT.
/// </summary>
public sealed class BaseSnapshotStore
{
    private readonly string _path;
    private readonly Dictionary<string, SyncSnapshot> _byLocalId;
    private readonly Dictionary<string, string> _lastActivity;

    public BaseSnapshotStore(string filePath)
    {
        _path = filePath;
        var file = Load(filePath);
        _byLocalId = file.Objects.ToDictionary(o => o.LocalId);
        _lastActivity = file.LastActivity;
    }

    /// <summary>The shadow store path for an adapter under the gitignored .local/ tree.</summary>
    public static string PathFor(string dydoRoot, string adapterName) =>
        Path.Combine(dydoRoot, "_system", ".local", "sync", adapterName, "snapshot.json");

    public SyncDoc? Get(string localId)
    {
        return _byLocalId.TryGetValue(localId, out var snap) ? ToDoc(snap) : null;
    }

    public IReadOnlyCollection<string> LocalIds => _byLocalId.Keys;

    /// <summary>Advance the base for an object to its newly-synced state.</summary>
    public void Set(SyncDoc doc)
    {
        _byLocalId[doc.LocalId] = ToSnapshot(doc);
    }

    public void Remove(string localId)
    {
        _byLocalId.Remove(localId);
        _lastActivity.Remove(localId);
    }

    /// <summary>The engine-derived last-activity date for an object (DR 030 §3), or null before its first
    /// genuine repo-side change is recorded.</summary>
    public string? GetLastActivity(string localId) =>
        _lastActivity.TryGetValue(localId, out var value) ? value : null;

    /// <summary>Record an object's last genuine repo-side change (DR 030 §3). Kept out of the object's
    /// field snapshot — and therefore out of frontmatter — so persisting it never provokes an edit loop.</summary>
    public void SetLastActivity(string localId, string isoDate) => _lastActivity[localId] = isoDate;

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var file = new SyncSnapshotFile
        {
            Objects = _byLocalId.Values.OrderBy(o => o.LocalId).ToList(),
            LastActivity = _lastActivity.OrderBy(e => e.Key).ToDictionary(e => e.Key, e => e.Value),
        };
        var json = JsonSerializer.Serialize(file, SyncSnapshotJsonContext.Default.SyncSnapshotFile);
        File.WriteAllText(_path, json);
    }

    private static SyncSnapshotFile Load(string path)
    {
        if (!File.Exists(path))
            return new SyncSnapshotFile();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize(json, SyncSnapshotJsonContext.Default.SyncSnapshotFile)
            ?? new SyncSnapshotFile();
    }

    private static SyncDoc ToDoc(SyncSnapshot s) => new()
    {
        LocalId = s.LocalId,
        ExternalId = s.ExternalId,
        Fields = s.Fields.Select(f => new SyncField { Key = f.Key, Value = f.Value }).ToList(),
        Body = s.Body,
        SourcePath = "",
    };

    private static SyncSnapshot ToSnapshot(SyncDoc d) => new()
    {
        LocalId = d.LocalId,
        ExternalId = d.ExternalId,
        Fields = d.Fields.Select(f => new SyncFieldEntry { Key = f.Key, Value = f.Value }).ToList(),
        Body = d.Body,
    };
}
