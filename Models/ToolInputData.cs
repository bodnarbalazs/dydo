namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Tool input data from the hook.
/// Contains the file path and other tool-specific data.
/// </summary>
public class ToolInputData
{
    [JsonPropertyName("file_path")]
    public string? FilePath { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("old_string")]
    public string? OldString { get; set; }

    [JsonPropertyName("new_string")]
    public string? NewString { get; set; }

    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    /// <summary>File path of a NotebookEdit tool call (its analogue of file_path).</summary>
    [JsonPropertyName("notebook_path")]
    public string? NotebookPath { get; set; }

    /// <summary>
    /// OpenCode delivers edits as apply_patch: a patch document naming its targets in marker
    /// lines, with no file_path of its own. HookInputExtensions.GetFilePaths parses them out.
    /// </summary>
    [JsonPropertyName("patch_text")]
    public string? PatchText { get; set; }

    [JsonPropertyName("run_in_background")]
    public bool? RunInBackground { get; set; }
}
