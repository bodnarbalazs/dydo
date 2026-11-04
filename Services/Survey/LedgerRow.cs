namespace DynaDocs.Services.Survey;

/// <summary>
/// One ledger table row or list item: the text it shows, without link targets or bare URLs (a table row's
/// cells joined by <c> | </c>), and the issue identifiers it names, link targets included, in order.
/// </summary>
internal sealed record LedgerRow(string Text, IReadOnlyList<string> Keys);
