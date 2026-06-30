namespace DynaDocs.Sync.Notion;

/// <summary>A non-success Notion REST response. Carries the HTTP status and the raw error body
/// (Notion error bodies do not contain the token, so they are safe to surface).</summary>
public sealed class NotionApiException(int statusCode, string body)
    : Exception($"Notion API returned {statusCode}: {body}")
{
    public int StatusCode { get; } = statusCode;
}
