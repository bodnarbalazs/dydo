namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// The difference between a snapshot and its base (baseline or another delta).
/// </summary>
public class SnapshotDelta
{
    [JsonPropertyName("files_added")]
    public List<string> FilesAdded { get; set; } = [];

    [JsonPropertyName("files_removed")]
    public List<string> FilesRemoved { get; set; } = [];

    [JsonPropertyName("folders_added")]
    public List<string> FoldersAdded { get; set; } = [];

    [JsonPropertyName("folders_removed")]
    public List<string> FoldersRemoved { get; set; } = [];

    [JsonPropertyName("doc_links_added")]
    public Dictionary<string, List<string>> DocLinksAdded { get; set; } = new();

    [JsonPropertyName("doc_links_removed")]
    public Dictionary<string, List<string>> DocLinksRemoved { get; set; } = new();

    public bool IsEmpty =>
        FilesAdded.Count == 0 && FilesRemoved.Count == 0 &&
        FoldersAdded.Count == 0 && FoldersRemoved.Count == 0 &&
        DocLinksAdded.Count == 0 && DocLinksRemoved.Count == 0;
}
