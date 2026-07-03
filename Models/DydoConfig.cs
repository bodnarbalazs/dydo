namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Root configuration object for dydo.json
/// </summary>
public class DydoConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Optional project slug. When set, it is the source for the namespaced Notion token env var
    /// (<c>DYDO_&lt;NAME&gt;_NOTION_TOKEN</c>, Decision 027 §2); when unset, the sanitized project-root
    /// directory name is used instead.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("structure")]
    public StructureConfig Structure { get; set; } = new();

    [JsonPropertyName("paths")]
    public PathsConfig Paths { get; set; } = new();

    [JsonPropertyName("agents")]
    public AgentsConfig Agents { get; set; } = new();

    [JsonPropertyName("integrations")]
    public Dictionary<string, bool> Integrations { get; set; } = new();

    [JsonPropertyName("dispatch")]
    public DispatchConfig Dispatch { get; set; } = new();

    [JsonPropertyName("worktree")]
    public WorktreeConfig Worktree { get; set; } = new();

    [JsonPropertyName("notion")]
    public NotionConfig? Notion { get; set; }

    [JsonPropertyName("queues")]
    public List<string> Queues { get; set; } = new();

    [JsonPropertyName("scanExclude")]
    public List<string> ScanExclude { get; set; } = new();

    [JsonPropertyName("nudges")]
    public List<NudgeConfig> Nudges { get; set; } = new();

    [JsonPropertyName("frameworkHashes")]
    public Dictionary<string, string> FrameworkHashes { get; set; } = new();
}
