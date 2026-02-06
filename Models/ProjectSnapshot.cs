namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a snapshot of the project state at session claim time.
/// Captures git-tracked files, folder structure, and doc-to-doc links
/// for visualization replay.
/// </summary>
public class ProjectSnapshot
{
    /// <summary>
    /// Git commit hash at snapshot time (full hash).
    /// </summary>
    [JsonPropertyName("git_commit")]
    public required string GitCommit { get; set; }

    /// <summary>
    /// All files tracked by git at snapshot time, as relative paths.
    /// Paths use forward slashes for consistency.
    /// </summary>
    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = [];

    /// <summary>
    /// All folders in the project (derived from file paths).
    /// Includes all parent directories up to root.
    /// </summary>
    [JsonPropertyName("folders")]
    public List<string> Folders { get; set; } = [];

    /// <summary>
    /// Doc-to-doc reference links extracted from markdown files.
    /// Key: source file path (normalized), Value: list of target file paths.
    /// </summary>
    [JsonPropertyName("doc_links")]
    public Dictionary<string, List<string>> DocLinks { get; set; } = new();
}
