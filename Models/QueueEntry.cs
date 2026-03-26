namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// A pending entry in a dispatch queue. Stores the terminal launch parameters
/// that were deferred when the queue already had an active item.
/// </summary>
public class QueueEntry
{
    [JsonPropertyName("agent")]
    public required string Agent { get; set; }

    [JsonPropertyName("task")]
    public required string Task { get; set; }

    [JsonPropertyName("launchInTab")]
    public bool LaunchInTab { get; set; }

    [JsonPropertyName("autoClose")]
    public bool AutoClose { get; set; }

    [JsonPropertyName("worktreeId")]
    public string? WorktreeId { get; set; }

    [JsonPropertyName("windowName")]
    public string? WindowName { get; set; }

    [JsonPropertyName("workingDirOverride")]
    public string? WorkingDirOverride { get; set; }

    [JsonPropertyName("cleanupWorktreeId")]
    public string? CleanupWorktreeId { get; set; }

    [JsonPropertyName("mainProjectRoot")]
    public string? MainProjectRoot { get; set; }

    [JsonPropertyName("enqueued")]
    public DateTime Enqueued { get; set; }
}
