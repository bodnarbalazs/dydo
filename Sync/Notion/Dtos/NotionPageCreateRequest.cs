namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Body for POST /v1/pages: a data-source parent, properties, and optional body blocks.</summary>
public sealed class NotionPageCreateRequest
{
    [JsonPropertyName("parent")]
    public NotionParent Parent { get; set; } = new();

    [JsonPropertyName("properties")]
    public Dictionary<string, NotionPropertyValue> Properties { get; set; } = new();

    [JsonPropertyName("icon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionIcon? Icon { get; set; }

    [JsonPropertyName("children")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NotionBlock>? Children { get; set; }

    /// <summary>Optional page body as a markdown string (DR 035 §1 create-with-body): when set, Notion creates
    /// the child page AND its body in one atomic call. The docs mirror uses it so there is no create-then-write
    /// window — a two-step CreatePage + UpdatePageMarkdown could throw after the page exists, recording a
    /// full-body base against an empty Notion page, which the next tick reads as an external clear and wipes the
    /// canonical repo file (issue 0235). Mutually exclusive with <see cref="Children"/> (the docs mirror never
    /// sets both). Omitted when null, so a bodyless create (the structure phase, and the spine) is unaffected.</summary>
    [JsonPropertyName("markdown")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Markdown { get; set; }
}
