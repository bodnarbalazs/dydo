namespace DynaDocs.Sync;

using DynaDocs.Models;
using DynaDocs.Sync.Model;

/// <summary>
/// Resolves a synced object's canonical repo file path from its status, making frontmatter status the
/// source of truth and folder placement derived presentation (slice brief §3). A model object type may
/// route a status-like select property's options into subfolders (e.g. an Issue with status <c>resolved</c>
/// files under the <c>resolved/</c> subfolder). A <em>mapped</em> option always routes to its subfolder; an
/// <em>unmapped</em> option (one with no folder entry, e.g. <c>open</c>) never moves an existing doc — it
/// keeps its current path — and only a genuinely new doc with no existing path lands at the dir root. The
/// sync engine pools docs from every subfolder and re-files a doc when its status maps to a different
/// subfolder, keeping the local id (filename stem) stable so a move is never seen as delete+create. A type
/// with no routing keeps every doc flat in its dir, exactly as before.
/// </summary>
public sealed class RepoFolderLayout
{
    private readonly string _dir;
    private readonly string? _statusField;
    private readonly IReadOnlyDictionary<string, string> _folders;

    public RepoFolderLayout(string dir, string? statusField, IReadOnlyDictionary<string, string>? folders)
    {
        _dir = dir;
        _statusField = statusField;
        // Option values are matched case-insensitively, just like the status field name — a frontmatter
        // 'status: Resolved' must route the same as 'resolved' (slice brief §4, finding 3).
        _folders = new Dictionary<string, string>(folders ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Build the layout for a model object type under its canonical dir: routing comes from the
    /// type's folder-routing property, if any.</summary>
    public static RepoFolderLayout For(SyncObjectType type, string dir)
    {
        var routing = type.FolderRouting();
        return new RepoFolderLayout(dir, routing?.Field, routing?.Folders);
    }

    /// <summary>The canonical file path for a doc. A status that maps to a subfolder routes there. A status
    /// with no folder mapping never moves an existing doc: it keeps <paramref name="currentPath"/> when one is
    /// supplied, and only a genuinely new doc (no current path — e.g. one created from the external side) lands
    /// at the dir root (slice brief §3, finding 1). Signature matches the path resolver the
    /// <see cref="SyncRunner"/> takes.</summary>
    public string PathFor(string localId, IReadOnlyList<SyncField> fields, string? currentPath = null)
    {
        var subfolder = MappedSubfolder(fields);
        if (subfolder != null)
            return Path.Combine(_dir, subfolder, localId + ".md");
        return !string.IsNullOrEmpty(currentPath) ? currentPath : Path.Combine(_dir, localId + ".md");
    }

    /// <summary>The subfolder this doc's status maps to, or null when the type has no routing, the status
    /// field is absent, or its value has no folder mapping.</summary>
    private string? MappedSubfolder(IReadOnlyList<SyncField> fields)
    {
        if (_statusField == null)
            return null;
        var status = fields.FirstOrDefault(f => f.Key.Equals(_statusField, StringComparison.OrdinalIgnoreCase))?.Value;
        return status != null && _folders.TryGetValue(status, out var sub) ? sub : null;
    }
}
