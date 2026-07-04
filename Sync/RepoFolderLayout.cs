namespace DynaDocs.Sync;

using DynaDocs.Models;
using DynaDocs.Sync.Model;

/// <summary>
/// Resolves a synced object's canonical repo file path from its status, making frontmatter status the
/// source of truth and folder placement derived presentation (slice brief §3). A model object type may
/// route a status-like select property's options into subfolders (e.g. an Issue with status <c>closed</c>
/// files under the <c>closed/</c> subfolder; unmapped options file at the dir root). The sync engine pools
/// docs from every subfolder and re-files a doc when its status changes, keeping the local id (filename
/// stem) stable so a move is never seen as delete+create. A type with no routing keeps every doc flat in
/// its dir, exactly as before.
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
        _folders = folders ?? new Dictionary<string, string>();
    }

    /// <summary>Build the layout for a model object type under its canonical dir: routing comes from the
    /// type's folder-routing property, if any.</summary>
    public static RepoFolderLayout For(SyncObjectType type, string dir)
    {
        var routing = type.FolderRouting();
        return new RepoFolderLayout(dir, routing?.Field, routing?.Folders);
    }

    /// <summary>The canonical file path for a doc: its dir plus the subfolder its status routes to, plus
    /// <c>&lt;localId&gt;.md</c>. Signature matches the path resolver the <see cref="SyncRunner"/> takes.</summary>
    public string PathFor(string localId, IReadOnlyList<SyncField> fields)
    {
        var subfolder = Subfolder(fields);
        return subfolder.Length == 0
            ? Path.Combine(_dir, localId + ".md")
            : Path.Combine(_dir, subfolder, localId + ".md");
    }

    private string Subfolder(IReadOnlyList<SyncField> fields)
    {
        if (_statusField == null)
            return "";
        var status = fields.FirstOrDefault(f => f.Key.Equals(_statusField, StringComparison.OrdinalIgnoreCase))?.Value;
        return status != null && _folders.TryGetValue(status, out var sub) ? sub : "";
    }
}
