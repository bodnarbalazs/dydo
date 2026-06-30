namespace DynaDocs.Sync.Model;

/// <summary>A malformed sync model: a missing type, an unknown relation target, or a relation cycle.</summary>
public sealed class SyncModelException(string message) : Exception(message);
