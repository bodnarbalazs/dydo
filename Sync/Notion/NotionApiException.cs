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
}
