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
