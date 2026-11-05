namespace DynaDocs.Services.Survey;

/// <summary>
/// One ledger table row or list item: the issue identifiers it names, link targets included, in order, and
/// for a table row with a <c>State</c> column, that cell's text, trimmed. A list item has no state.
/// </summary>
internal sealed record LedgerRow(IReadOnlyList<string> Keys, string? State);
