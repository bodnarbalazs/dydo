namespace DynaDocs.Sync;

using System.Text.Json.Serialization;

/// <summary>Observed body-write outcomes from one adapter application.</summary>
public sealed class SyncApplyResult
{
    [JsonPropertyName("bodyWriteReceipts")]
    public IReadOnlyList<BodyWriteReceipt> BodyWriteReceipts { get; init; } = [];
}
