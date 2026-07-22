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

    /// <summary>Whether any base/last-activity entry was mutated since load or the last <see cref="Save"/>. The
    /// daemon's delta tick skips <see cref="Save"/> when a tick changed nothing (ns-13): at 40k entries a no-op
    /// tick must not rewrite the whole snapshot file every 15s. The full sync path saves unconditionally as
    /// before.</summary>
    public bool Dirty { get; private set; }

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

    /// <summary>Durably clear a type's base snapshot by deleting its file (review R2-1). Called BEFORE the mint —
    /// the instant a re-provision is about to create a fresh EMPTY database — so the reset survives a crash between
    /// the delete and the end-of-tick <see cref="Save"/>. An in-memory <see cref="Reset"/> alone is not persisted
    /// until that Save, so an abort in between (a transient probe failure in schema-drift, a throw in the adapter's
    /// external read, a process kill) would leave the fresh database recorded while the STALE snapshot.json survived
    /// on disk — and the next run, reusing the now-valid empty database with nothing minted, would read every
    /// base+repo pair as an external delete and wipe the repo. Ordering the delete before the mint makes the window
    /// data-preserving both ways (finding 2): a crash after the delete but before the create just re-mints next run,
    /// and a delete FAILURE aborts before any database exists — so it must surface, never silently proceed. A
    /// share-lock (AV/OneDrive/another process), a read-only attribute, or any other I/O fault is re-raised with a
    /// clear message rather than swallowed. A missing file is already reset: a no-op.</summary>
    public static void DeleteSnapshot(string filePath)
    {
        if (!File.Exists(filePath))
            return;
        try
        {
            File.Delete(filePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                $"failed to reset base snapshot '{filePath}' before re-provisioning; aborting so a stale snapshot cannot mass-delete the repo on the next run: {e.Message}", e);
        }
    }

    public SyncDoc? Get(string localId)
    {
        return _byLocalId.TryGetValue(localId, out var snap) ? ToDoc(snap) : null;
    }

    public IReadOnlyCollection<string> LocalIds => _byLocalId.Keys;

    /// <summary>Advance the base for an object to its newly-synced state.</summary>
    public void Set(SyncDoc doc)
    {
        _byLocalId[doc.LocalId] = ToSnapshot(doc);
        Dirty = true;
    }

    public void Remove(string localId)
    {
        _byLocalId.Remove(localId);
        _lastActivity.Remove(localId);
        Dirty = true;
    }

    /// <summary>Drop every recorded base, so the next reconcile sees baseDoc == null for all pairs and
    /// re-pushes each repo doc as a CREATE (finding 1). Called when a type is (re)provisioned into a fresh,
    /// EMPTY database: the old base still points at pages in the now-abandoned database, so leaving it would
    /// make the same tick read every external as deleted and mass-delete the repo. Last-activity is cleared
    /// too — the fresh pages carry none — so it re-seeds from each file's mtime on the re-create.</summary>
    public void Reset()
    {
        _byLocalId.Clear();
        _lastActivity.Clear();
        Dirty = true;
    }

    /// <summary>The engine-derived last-activity date for an object (DR 030 §3), or null before its first
    /// genuine repo-side change is recorded.</summary>
    public string? GetLastActivity(string localId) =>
        _lastActivity.TryGetValue(localId, out var value) ? value : null;

    /// <summary>Record an object's last genuine repo-side change (DR 030 §3). Kept out of the object's
    /// field snapshot — and therefore out of frontmatter — so persisting it never provokes an edit loop.</summary>
    public void SetLastActivity(string localId, string isoDate)
    {
        _lastActivity[localId] = isoDate;
        Dirty = true;
    }

    /// <summary>Drop last-activity entries with no surviving base object (finding 7). A create whose external
    /// id never confirmed (a crash mid-Apply) or a doc whose base entry never existed leaves a seeded
    /// last-activity that Retire — which fires only for objects that HAVE a base entry — can never reach, so
    /// it would leak in the snapshot forever. Swept after a completed tick, where every confirmed create has
    /// its base entry, so a surviving orphan is genuinely unreachable rather than merely pending.</summary>
    public void PruneOrphanLastActivity()
    {
        foreach (var localId in _lastActivity.Keys.Where(k => !_byLocalId.ContainsKey(k)).ToList())
        {
            _lastActivity.Remove(localId);
            Dirty = true;
        }
    }

    public void Save()
    {
        Dirty = false;
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
