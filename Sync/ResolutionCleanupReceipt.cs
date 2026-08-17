namespace DynaDocs.Sync;

using System.Text.Json.Serialization;

/// <summary>Durable proof that a resolution receipt committed before its shadow was removed.</summary>
public sealed class ResolutionCleanupReceipt
{
    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }

    [JsonPropertyName("operationId")]
    public required string OperationId { get; init; }

    [JsonPropertyName("resolvedBody")]
    public required string ResolvedBody { get; init; }
}
