namespace DynaDocs.Sync;

/// <summary>Serializable ordered frontmatter entry for the base snapshot store.</summary>
public sealed class SyncFieldEntry
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}
