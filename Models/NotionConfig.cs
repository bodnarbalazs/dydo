namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Optional <c>notion</c> section of dydo.json (Decision 025 / 027). Names the parent page the PM spine
/// databases are provisioned under; if unset, <c>DYDO_NOTION_PARENT_PAGE</c> is used. The integration
/// token is never stored here — only the storage <em>policy</em> — so this file stays safe to commit.
/// </summary>
public sealed class NotionConfig
{
    [JsonPropertyName("parentPageId")]
    public string? ParentPageId { get; set; }

    /// <summary>
    /// Project-level token-storage policy (Decision 027 §3). <c>"local"</c> (default) keeps the token in a
    /// gitignored per-machine secret store — DPAPI on Windows, <c>0600</c> plaintext elsewhere — and never
    /// commits it. <c>"vault"</c> commits it as authenticated ciphertext for repo-portability; that mode is
    /// not implemented yet (Slice B) and every vault code path fails loudly rather than degrading silently.
    /// </summary>
    [JsonPropertyName("tokenStorage")]
    public string TokenStorage { get; set; } = "local";
}
