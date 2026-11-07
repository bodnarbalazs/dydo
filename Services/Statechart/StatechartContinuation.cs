namespace DynaDocs.Services.Statechart;

internal sealed record StatechartContinuation(string Path, FileStream ExecutionLock);
