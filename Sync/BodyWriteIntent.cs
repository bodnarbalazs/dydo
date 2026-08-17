namespace DynaDocs.Sync;

using System.Text.Json.Serialization;

/// <summary>A durable, typed journal entry written before a projected body mutation.</summary>
public sealed class BodyWriteIntent
{
    [JsonPropertyName("operationId")]
    public required string OperationId { get; init; }

    [JsonPropertyName("kind")]
    public required BodyWriteOperationKind Kind { get; init; }

    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; init; }

    [JsonPropertyName("priorLocalBody")]
    public required string PriorLocalBody { get; init; }

    [JsonPropertyName("priorExternalBody")]
    public required string PriorExternalBody { get; init; }

    [JsonPropertyName("intendedLocalBody")]
    public required string IntendedLocalBody { get; init; }
}
