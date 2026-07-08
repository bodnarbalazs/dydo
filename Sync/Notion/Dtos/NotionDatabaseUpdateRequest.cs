namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Body for PATCH /v1/databases/{id}: archive (trash) the database. Notion's 2025-09-03+ data-source
/// API soft-deletes a database with <c>in_trash</c> — the only field a reset needs (Notion exposes no hard
/// delete, so archive is the wipe).</summary>
public sealed class NotionDatabaseUpdateRequest
{
    [JsonPropertyName("in_trash")]
    public bool InTrash { get; set; }
}
