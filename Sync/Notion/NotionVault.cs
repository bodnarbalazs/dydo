namespace DynaDocs.Sync.Notion;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DynaDocs.Serialization;
using NSec.Cryptography;

/// <summary>
/// The opt-in encrypted token vault (Decision 027 §5) — the one mode where crypto is meaningful, because
/// the ciphertext travels in git (repo-leak threat domain) while the passphrase never does. The token is
/// sealed with XChaCha20-Poly1305 under a key derived from the passphrase via Argon2id (memory-hard, so a
/// leaked <c>notion.vault</c> resists offline brute force). Every cost parameter, the salt, and the nonce
/// are written into the <see cref="NotionVaultEnvelope"/>, so a future parameter retune still decrypts old
/// files. Decryption returns <c>null</c> on any authentication failure — a wrong passphrase or a tampered
/// envelope — and never raises a secret-bearing exception. No self-rolled primitives: NSec/libsodium only.
/// </summary>
public static class NotionVault
{
    public const int CurrentVersion = 1;
    public const string KdfId = "argon2id";

    // Argon2id cost (Decision 027 §5), tuned against the committed-ciphertext offline-brute-force threat:
    // 64 MiB memory-hard cost with 3 passes. Parallelism is pinned to 1 because libsodium's Argon2id
    // rejects a lane count greater than 1.
    // CRITICAL: NSec's Argon2Parameters.MemorySize is in KiB, NOT bytes (empirically probed) — 65536 KiB = 64 MiB.
    // The prior value (64*1024*1024) requested 64 GiB per derivation and exhausted machine RAM (a single
    // derivation ballooned a testhost to 56+ GB). The KiB unit is named into the constant so a future edit
    // cannot silently reintroduce a bytes-style value. Do NOT "restore" it to a bytes-style value.
    public const int DefaultMemoryKib = 64 * 1024; // 65536 KiB = 64 MiB
    public const int DefaultPasses = 3;
    public const int Parallelism = 1;

    // Envelope-parameter bounds, enforced on decrypt. The vault is committed to git — its threat domain —
    // so a tampered or bad-merged file must never be trusted into the KDF: an out-of-range MemoryKib is a
    // data-triggered memory bomb (the KiB-units class that blew 56 GB), and excessive passes stall the CPU.
    // 8 KiB is libsodium's Argon2id floor; the 512 MiB ceiling is 8× the default; passes are capped at 16.
    private const int MinMemoryKib = 8;
    private const int MaxMemoryKib = 512 * 1024;
    private const int MinPasses = 1;
    private const int MaxPasses = 16;

    private const int SaltBytes = 16;   // libsodium Argon2id salt length
    private const int NonceBytes = 24;  // XChaCha20-Poly1305 nonce length

    // Bound into the AEAD as associated data: domain separation + version pinning, so a v1 blob can never
    // be reinterpreted under different envelope semantics without failing authentication.
    private static readonly byte[] AssociatedData = Encoding.ASCII.GetBytes("dydo-notion-vault-v1");

    private static readonly AeadAlgorithm Aead = AeadAlgorithm.XChaCha20Poly1305;

    /// <summary>Seals <paramref name="token"/> under <paramref name="passphrase"/> with a fresh random salt
    /// and nonce, returning the versioned envelope to persist. The plaintext token never leaves memory. The
    /// cost parameters default to the production constants; they are overridable only so tests can seal at a
    /// cheap-but-valid cost — production callers pass neither and get the full memory-hard cost.</summary>
    public static NotionVaultEnvelope Encrypt(string token, string passphrase, int? memoryKib = null, int? passes = null)
    {
        var costMemoryKib = memoryKib ?? DefaultMemoryKib;
        var costPasses = passes ?? DefaultPasses;

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);

        using var key = DeriveKey(passphrase, salt, costMemoryKib, costPasses, exportable: false);
        var ciphertext = Aead.Encrypt(key, nonce, AssociatedData, Encoding.UTF8.GetBytes(token));

        return new NotionVaultEnvelope
        {
            Version = CurrentVersion,
            Kdf = KdfId,
            MemoryKib = costMemoryKib,
            Passes = costPasses,
            Parallelism = Parallelism,
            Salt = Convert.ToBase64String(salt),
            Nonce = Convert.ToBase64String(nonce),
            Ciphertext = Convert.ToBase64String(ciphertext),
        };
    }

    /// <summary>Convenience round-trip used by tests and the reveal path: derive from the passphrase and
    /// authenticate-decrypt in one call. Returns the token, or <c>null</c> on a wrong passphrase or any
    /// malformed/off-spec envelope — never throws.</summary>
    public static string? Decrypt(NotionVaultEnvelope envelope, string passphrase)
    {
        var rawKey = DeriveKeyBytes(passphrase, envelope);
        return rawKey == null ? null : DecryptWithKey(envelope, rawKey);
    }

    /// <summary>Re-derives the raw 32-byte symmetric key for an existing envelope (using the envelope's own
    /// stored parameters and salt). Exposed so the resolver can cache the derived key locally and skip the
    /// expensive Argon2id pass on subsequent runs. Returns <c>null</c> — never throws — when the committed
    /// envelope is off-spec: an unsupported version/kdf, an out-of-bounds cost (a tampered <c>notion.vault</c>
    /// must not be able to trigger a giant Argon2 allocation), or a salt that is null / not base64 / not 16
    /// bytes.</summary>
    public static byte[]? DeriveKeyBytes(string passphrase, NotionVaultEnvelope envelope)
    {
        if (!IsSupportedEnvelope(envelope))
            return null;

        byte[] salt;
        try { salt = Convert.FromBase64String(envelope.Salt); }
        catch (FormatException) { return null; }
        if (salt.Length != SaltBytes)
            return null;

        try
        {
            using var key = DeriveKey(passphrase, salt, envelope.MemoryKib, envelope.Passes, exportable: true);
            return key.Export(KeyBlobFormat.RawSymmetricKey);
        }
        catch (ArgumentException)
        {
            // Belt-and-suspenders: any residual parameter NSec rejects becomes a decryption miss, not a throw.
            return null;
        }
    }

    /// <summary>Bounds the committed envelope's public KDF parameters before they reach NSec, so a tampered
    /// or bad-merged <c>notion.vault</c> is treated as undecryptable rather than a memory bomb or a crash.</summary>
    private static bool IsSupportedEnvelope(NotionVaultEnvelope e) =>
        e.Version == CurrentVersion
        && e.Kdf == KdfId
        && e.Salt != null
        && e.MemoryKib >= MinMemoryKib && e.MemoryKib <= MaxMemoryKib
        && e.Passes >= MinPasses && e.Passes <= MaxPasses;

    /// <summary>Authenticate-decrypts using a previously derived raw key (the cached-key fast path). Returns
    /// <c>null</c> — never throws — on a malformed envelope, a stale/wrong key, or a tampered ciphertext.</summary>
    public static string? DecryptWithKey(NotionVaultEnvelope envelope, byte[] rawKey)
    {
        byte[] nonce, ciphertext;
        try
        {
            nonce = Convert.FromBase64String(envelope.Nonce);
            ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        }
        catch (FormatException)
        {
            return null;
        }

        if (rawKey.Length != Aead.KeySize || nonce.Length != Aead.NonceSize || ciphertext.Length < Aead.TagSize)
            return null;

        using var key = Key.Import(Aead, rawKey, KeyBlobFormat.RawSymmetricKey);
        var plaintext = Aead.Decrypt(key, nonce, AssociatedData, ciphertext);
        return plaintext == null ? null : Encoding.UTF8.GetString(plaintext);
    }

    /// <summary>Reads and parses the vault envelope at <paramref name="vaultPath"/>, or <c>null</c> if the
    /// file is absent or unparseable.</summary>
    public static NotionVaultEnvelope? ReadVault(string vaultPath)
    {
        if (!File.Exists(vaultPath))
            return null;

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(vaultPath), NotionVaultJsonContext.Default.NotionVaultEnvelope);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void WriteVault(string vaultPath, NotionVaultEnvelope envelope)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(vaultPath)!);
        File.WriteAllText(vaultPath, JsonSerializer.Serialize(envelope, NotionVaultJsonContext.Default.NotionVaultEnvelope));
    }

    private static Key DeriveKey(string passphrase, ReadOnlySpan<byte> salt, int memoryKib, int passes, bool exportable)
    {
        var kdf = PasswordBasedKeyDerivationAlgorithm.Argon2id(new Argon2Parameters
        {
            DegreeOfParallelism = Parallelism,
            MemorySize = memoryKib,
            NumberOfPasses = passes,
        });

        var creationParams = new KeyCreationParameters
        {
            ExportPolicy = exportable ? KeyExportPolicies.AllowPlaintextExport : KeyExportPolicies.None,
        };

        // NSec's DeriveKey(string, ...) hashes the passphrase as its raw UTF-16LE bytes (MemoryMarshal.AsBytes),
        // not UTF-8. Self-consistent here (encrypt and decrypt both use this overload); noted because a future
        // non-.NET tool decrypting notion.vault must match this encoding to derive the same key.
        return kdf.DeriveKey(passphrase, salt, Aead, creationParams);
    }
}
