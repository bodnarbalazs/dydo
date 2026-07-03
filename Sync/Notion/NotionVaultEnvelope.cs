namespace DynaDocs.Sync.Notion;

using System.Text.Json.Serialization;

/// <summary>
/// The self-describing, versioned envelope committed to <c>dydo/_system/notion.vault</c> (Decision 027 §5).
/// It carries everything needed to re-derive the key and authenticate-decrypt the token — <em>except</em>
/// the passphrase: the KDF id and its cost parameters, the random salt, the AEAD nonce, and the ciphertext
/// (all binary fields base64-encoded). The parameters live in the envelope, not in code, so a future cost
/// retune still decrypts vaults written under the old parameters. It holds no plaintext token.
/// </summary>
public sealed class NotionVaultEnvelope
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("kdf")]
    public string Kdf { get; set; } = "";

    /// <summary>Argon2id memory cost, in KiB (NSec/libsodium's MemorySize unit — NOT bytes).</summary>
    [JsonPropertyName("memoryKib")]
    public int MemoryKib { get; set; }

    [JsonPropertyName("passes")]
    public int Passes { get; set; }

    [JsonPropertyName("parallelism")]
    public int Parallelism { get; set; }

    [JsonPropertyName("salt")]
    public string Salt { get; set; } = "";

    [JsonPropertyName("nonce")]
    public string Nonce { get; set; } = "";

    [JsonPropertyName("ciphertext")]
    public string Ciphertext { get; set; } = "";
}
