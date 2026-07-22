namespace DynaDocs.Sync.Notion;

/// <summary>The gitignored per-type cheap-tick state for the sync daemon (ns-13), stored beside the type's base
/// snapshot under <c>_system/.local/sync/&lt;adapter&gt;/delta.json</c>. Holds the remote <see cref="Cursor"/> (the
/// max <c>last_edited_time</c> seen, in Notion's own clock — never the local clock) and the last-seen mtime of every
/// repo file, so a tick fetches only pages edited since the cursor and parses only files whose mtime moved. A
/// missing or corrupt file degrades to a full tick (every page a hit, every file changed) — correctness over
/// cheapness.</summary>
public sealed class NotionDeltaStateFile
{
    public string? Cursor { get; set; }

    /// <summary>Repo file path → last-seen last-write mtime (UTC ticks). A key gone from the current scan is a
    /// local deletion; a changed value is a local edit.</summary>
    public Dictionary<string, long> Files { get; set; } = new();
}
