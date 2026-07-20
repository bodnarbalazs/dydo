namespace DynaDocs.Sync.Notion;

/// <summary>A non-success Notion REST response. Carries the HTTP status and the raw error body
/// (Notion error bodies do not contain the token, so they are safe to surface). An optional
/// <see cref="Context"/> names what the sync was provisioning when Notion rejected the request:
/// Notion's schema errors — notably <c>"Type error with formula"</c> — identify neither the object
/// type nor the offending property, so the provisioner re-throws with that context to make an
/// otherwise-opaque 400 actionable.</summary>
public sealed class NotionApiException(int statusCode, string body, string? context = null)
    : Exception(context == null
        ? $"Notion API returned {statusCode}: {body}"
        : $"{context} — Notion API returned {statusCode}: {body}")
{
    public int StatusCode { get; } = statusCode;

    /// <summary>The raw Notion error body, preserved so a re-throw can re-wrap it with added context
    /// without nesting the "Notion API returned…" prefix.</summary>
    public string Body { get; } = body;

    /// <summary>What the caller was provisioning when Notion rejected the request, or null for a bare
    /// transport error straight off the wire.</summary>
    public string? Context { get; } = context;

    /// <summary>Whether this failure leaves a non-idempotent create's outcome AMBIGUOUS — the request may
    /// have reached Notion and created the object before the error surfaced. True for a transport throw
    /// (status 0: no response ever arrived) and the retryable 5xx server errors; false for a rate response
    /// (429/529, an unambiguous rejection — nothing was created) or any hard 4xx. A create sender's recovery
    /// re-queries on this and adopts an existing object rather than blindly re-creating (ns-5).
    /// <para>This set mirrors <c>NotionClient.IsServerError</c> (plus status 0 for a transport throw): the
    /// statuses a non-idempotent create suppresses at the send layer are exactly the ones recovery re-queries
    /// on. The two must move together.</para></summary>
    public bool AmbiguousOutcome => StatusCode is 0 or 500 or 502 or 503 or 504;
}
