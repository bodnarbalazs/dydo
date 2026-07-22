namespace DynaDocs.Sync.Notion;

using System.Text.Json;
using DynaDocs.Serialization;

/// <summary>
/// The daemon's cheap-tick pre-filter state for one object type (ns-13): the remote stamp cursor and the last-seen
/// repo-file mtimes, persisted under the same gitignored <c>_system/.local/sync/&lt;adapter&gt;/</c> tree as the
/// base snapshot and keyed the same way (<see cref="ParentPageKey.Hash8"/> via the adapter name), so a scratch
/// board's cursor never bleeds into the configured board's. The manual full sync SEEDS this (<see cref="Seed"/>) so
/// a daemon starting afterwards begins warm — no gap. A missing or unreadable file loads empty, which makes the next
/// tick degrade to correctness (null cursor ⇒ reconcile local changes + establish the baseline, never a silent skip).
/// </summary>
public sealed class NotionDeltaState
{
    /// <summary>A sentinel cursor older than any real Notion stamp, used when a data source is EMPTY at baseline time
    /// (no page has a stamp to seed from). It keeps the cursor non-null so the next tick is a normal filtered tick,
    /// and being older than everything, the first real page that appears is caught (F2).</summary>
    public const string SentinelEpoch = "0001-01-01T00:00:00.000Z";

    private readonly string _path;
    private NotionDeltaStateFile _file;

    public NotionDeltaState(string filePath)
    {
        _path = filePath;
        _file = Load(filePath);
    }

    public static string PathFor(string dydoRoot, string adapterName) =>
        Path.Combine(dydoRoot, "_system", ".local", "sync", adapterName, "delta.json");

    /// <summary>The max server stamp seen so far — the <c>on_or_after</c> bound the next filtered query uses. Null
    /// only when no state exists yet (a fresh/degraded tick establishes it).</summary>
    public string? Cursor => _file.Cursor;

    public IReadOnlyDictionary<string, long> Files => _file.Files;

    /// <summary>Advance the cursor to the newest stamp seen this tick, comparing inclusively (ordinal on the ISO
    /// string): a stamp equal to the current cursor is kept, never regressed, so a boundary re-hit cannot roll the
    /// cursor backward.</summary>
    public void AdvanceCursor(string? maxStampSeen)
    {
        if (maxStampSeen != null && (_file.Cursor == null || string.CompareOrdinal(maxStampSeen, _file.Cursor) > 0))
            _file.Cursor = maxStampSeen;
    }

    /// <summary>Replace the recorded file→mtime map with this tick's scan, so the next tick diffs against it.</summary>
    public void SetFiles(Dictionary<string, long> files) => _file.Files = files;

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_file, SyncSnapshotJsonContext.Default.NotionDeltaStateFile));
    }

    /// <summary>Seed this type's cheap-tick state from a just-completed manual full sync (ns-13 F2c): the file mtimes
    /// are replaced with the current post-sync on-disk state, and the cursor is ADVANCED to the max server stamp the
    /// sync saw (or the sentinel epoch when the board is empty) — never regressed, so a concurrent daemon tick's
    /// newer cursor is not rolled back. A daemon started afterwards begins warm: its first tick sees no phantom
    /// changes and no remote gap.</summary>
    public static void Seed(string filePath, string? maxStamp, Dictionary<string, long> files)
    {
        var state = new NotionDeltaState(filePath);
        state.SetFiles(files);
        state.AdvanceCursor(maxStamp ?? SentinelEpoch);
        state.Save();
    }

    /// <summary>Scan a type's docs dir to path→mtime (UTC ticks), honouring the same <c>_</c>-prefix skip the
    /// reconcile does — the shared shape of both the manual-sync seed and the daemon's per-tick diff.</summary>
    public static Dictionary<string, long> ScanMtimes(string dir)
    {
        var mtimes = new Dictionary<string, long>(StringComparer.Ordinal);
        if (!Directory.Exists(dir))
            return mtimes;
        foreach (var path in Directory.EnumerateFiles(dir, "*.md", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(dir, path);
            if (relative.Split('/', '\\').Any(segment => segment.StartsWith('_')))
                continue;
            mtimes[path] = File.GetLastWriteTimeUtc(path).Ticks;
        }
        return mtimes;
    }

    private static NotionDeltaStateFile Load(string path)
    {
        if (!File.Exists(path))
            return new NotionDeltaStateFile();
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), SyncSnapshotJsonContext.Default.NotionDeltaStateFile)
                ?? new NotionDeltaStateFile();
        }
        catch (JsonException)
        {
            // A corrupt pre-filter file must not wedge the daemon — degrade to a full tick (null cursor, empty
            // mtime map) rather than throwing every 15s.
            return new NotionDeltaStateFile();
        }
    }
}
