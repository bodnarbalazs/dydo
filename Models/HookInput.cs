namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Input received from Claude Code hooks via stdin.
/// This represents the JSON structure sent by PreToolUse hooks.
/// </summary>
public class HookInput
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("transcript_path")]
    public string? TranscriptPath { get; set; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; set; }

    [JsonPropertyName("permission_mode")]
    public string? PermissionMode { get; set; }

    [JsonPropertyName("hook_event_name")]
    public string? HookEventName { get; set; }

    [JsonPropertyName("tool_name")]
    public string? ToolName { get; set; }

    [JsonPropertyName("tool_input")]
    public ToolInputData? ToolInput { get; set; }

    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; set; }
}

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
}

/// <summary>
/// Helper methods for working with hook input
/// </summary>
public static class HookInputExtensions
{
    /// <summary>
    /// Get the action type from the tool name
    /// </summary>
    public static string GetAction(this HookInput input)
    {
        return input.ToolName?.ToLowerInvariant() switch
        {
            "write" => "write",
            "edit" => "edit",
            "bash" => "execute",
            "read" => "read",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Get the file path being operated on
    /// </summary>
    public static string? GetFilePath(this HookInput input)
    {
        return input.ToolInput?.FilePath;
    }

    /// <summary>
    /// Check if this is a write operation (Write or Edit tool)
    /// </summary>
    public static bool IsWriteOperation(this HookInput input)
    {
        var toolName = input.ToolName?.ToLowerInvariant();
        return toolName == "write" || toolName == "edit";
    }
}
