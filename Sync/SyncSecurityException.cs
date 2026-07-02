namespace DynaDocs.Sync;

/// <summary>
/// Raised when externally-supplied sync input fails a safety check at the trust boundary — e.g. a
/// record's local id that would escape its canonical repo directory (path traversal). Signals a
/// rejected, potentially hostile value, not a routine reconcile outcome.
/// </summary>
public sealed class SyncSecurityException(string message) : Exception(message);
