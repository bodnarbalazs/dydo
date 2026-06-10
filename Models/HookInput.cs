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

    /// <summary>
    /// Unique id of the sub-agent making this call. Present only when the hook fires
    /// inside a sub-agent (Agent tool or workflow-spawned). Absence = Tier-1 main thread.
    /// </summary>
    [JsonPropertyName("agent_id")]
    public string? AgentId { get; set; }

    /// <summary>
    /// Agent type name of the sub-agent (e.g. "reviewer", "Explore", "workflow-subagent").
    /// Carries the role for Tier-2 workers. Present only alongside agent_id.
    /// </summary>
    [JsonPropertyName("agent_type")]
    public string? AgentType { get; set; }

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
