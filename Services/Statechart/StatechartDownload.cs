namespace DynaDocs.Services.Statechart;

internal sealed record StatechartDownload(string Name, string Url, string Integrity, long Limit);
