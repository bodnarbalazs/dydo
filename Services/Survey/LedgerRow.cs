namespace DynaDocs.Services.Survey;

/// <summary>One ledger table row or list item, and the issue identifiers it names, in order.</summary>
internal sealed record LedgerRow(string Text, IReadOnlyList<string> Keys);
