namespace DynaDocs.Sync;

/// <summary>
/// A transport create may have landed, but more than one live external record carries the durable operation id.
/// The runner converts this to a fenced, visible unresolved result rather than guessing which record to adopt.
/// </summary>
public sealed class AmbiguousCreateIdentityException(string operationId, int matchCount) : InvalidOperationException(
    $"Ambiguous external create recovery for operation '{operationId}': {matchCount} live records match.")
{
    public string OperationId { get; } = operationId;
}
