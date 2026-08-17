namespace DynaDocs.Sync;

using System.Text.Json.Serialization;

/// <summary>The adapter's observed external body after one journaled mutation.</summary>
public sealed class BodyWriteReceipt
{
    [JsonPropertyName("operationId")]
    public required string OperationId { get; init; }

    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }

    [JsonPropertyName("externalId")]
    public required string ExternalId { get; init; }

    [JsonPropertyName("observedExternalBody")]
    public required string ObservedExternalBody { get; init; }
}
