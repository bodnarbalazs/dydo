namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Body for PATCH /v1/blocks/{id}/children: a flat list of blocks to append.</summary>
public sealed class NotionAppendChildrenRequest
{
    [JsonPropertyName("children")]
    public List<NotionBlock> Children { get; set; } = [];
}
