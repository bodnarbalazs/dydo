namespace DynaDocs.Serialization;

using System.Text.Json.Serialization;
using DynaDocs.Sync.Notion.Dtos;
using DynaDocs.Sync.Notion.Provisioning;

/// <summary>
/// Source-generated JSON for the Notion REST DTOs (Decision 025 §6 — Native AOT, no reflection
/// serialization, no third-party SDK). Every DTO field carries an explicit <c>[JsonPropertyName]</c>
/// matching Notion's snake_case wire names, so no naming policy is applied here.
/// </summary>
// WhenWritingNull: Notion's write API rejects null for read-only/optional fields (e.g. a rich-text
// run's plain_text), expecting them absent — so never serialize null fields on the write side.
[JsonSourceGenerationOptions(WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NotionDatabase))]
[JsonSerializable(typeof(NotionDatabaseCreateRequest))]
[JsonSerializable(typeof(NotionPage))]
[JsonSerializable(typeof(NotionPageList))]
[JsonSerializable(typeof(NotionBlockList))]
[JsonSerializable(typeof(NotionQueryRequest))]
[JsonSerializable(typeof(NotionPageCreateRequest))]
[JsonSerializable(typeof(NotionPageUpdateRequest))]
[JsonSerializable(typeof(NotionAppendChildrenRequest))]
[JsonSerializable(typeof(NotionSearchRequest))]
[JsonSerializable(typeof(NotionSearchResponse))]
internal partial class NotionJsonContext : JsonSerializerContext { }

/// <summary>
/// Source-generated JSON for the gitignored Notion provision-state file (slice brief §1). Written
/// indented for human inspection; lives outside the canonical tree so it never syncs or commits.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(NotionProvisionState))]
internal partial class NotionProvisionJsonContext : JsonSerializerContext { }
