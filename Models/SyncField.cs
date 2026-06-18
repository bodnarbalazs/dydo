namespace DynaDocs.Models;

/// <summary>One ordered frontmatter entry. A list of these preserves authoring order.</summary>
public sealed class SyncField
{
    public required string Key { get; init; }
    public required string Value { get; init; }
}
