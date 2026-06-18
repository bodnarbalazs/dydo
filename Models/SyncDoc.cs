namespace DynaDocs.Models;

/// <summary>
/// The canonical, transport-agnostic representation of one synced object (Decision 025).
/// A dydo doc file (YAML frontmatter + markdown body) maps to/from a SyncDoc, and the
/// sync engine reconciles SyncDocs against any external view via <c>ISyncAdapter</c>.
/// Nothing here is Notion-specific: <see cref="Fields"/> is just an ordered key→value
/// map (frontmatter) and <see cref="Body"/> is the markdown after the frontmatter.
/// </summary>
public sealed class SyncDoc
{
    /// <summary>Stable local identity — the doc's filename stem (e.g. the task name).</summary>
    public required string LocalId { get; init; }

    /// <summary>The external view's id for this object, once it has been created there.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Frontmatter as an ordered key→value map; order is preserved on write.</summary>
    public required List<SyncField> Fields { get; init; }

    /// <summary>The markdown body after the frontmatter block.</summary>
    public required string Body { get; init; }

    /// <summary>Repo-relative source path the doc was read from / writes back to.</summary>
    public required string SourcePath { get; init; }

    public string? GetField(string key) =>
        Fields.FirstOrDefault(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
}
