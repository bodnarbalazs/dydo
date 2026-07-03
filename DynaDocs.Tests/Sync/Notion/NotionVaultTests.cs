namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Notion;

/// <summary>Vault crypto (Decision 027 §5): Argon2id + XChaCha20-Poly1305 round-trip, authentication
/// failure on a wrong passphrase or tampered envelope, and the self-describing envelope whose parameters
/// (not a hard-coded constant) drive decryption. No secret ever appears in the serialized envelope.</summary>
[Collection("NotionArgon2id")]
public class NotionVaultTests
{
    private const string Token = "ntn_secret_token_value_42";
    private const string Passphrase = "Corr3ct-Horse-Battery";

    // Cheap-but-valid Argon2id cost for the suite: 8 MiB (8192 KiB) is comfortably above libsodium's 8 KiB
    // floor, and a single pass keeps derivation fast. Production seals at DefaultMemoryKib (asserted below,
    // without deriving at that cost).
    private const int TinyMemoryKib = 8192;
    private const int TinyPasses = 1;

    private static NotionVaultEnvelope Seal(string token = Token, string passphrase = Passphrase) =>
        NotionVault.Encrypt(token, passphrase, TinyMemoryKib, TinyPasses);

    [Fact]
    public void Encrypt_ThenDecrypt_SamePassphrase_ReturnsTokenExactly()
    {
        var envelope = Seal();
        Assert.Equal(Token, NotionVault.Decrypt(envelope, Passphrase));
    }

    [Fact]
    public void Decrypt_WrongPassphrase_ReturnsNull_NoThrow()
    {
        var envelope = Seal();
        Assert.Null(NotionVault.Decrypt(envelope, "Wr0ng-Passphrase-X"));
    }

    [Fact]
    public void DefaultCost_IsProduction64Mib()
    {
        // The one place the production constant is asserted — no derivation at this cost, just the value.
        Assert.Equal(65536, NotionVault.DefaultMemoryKib);
        Assert.Equal(3, NotionVault.DefaultPasses);
    }

    [Fact]
    public void DefaultCost_StaysUnderMemoryCeiling()
    {
        // Regression guard: a future units mistake (KiB↔bytes) fails this fast assertion instead of the
        // machine. 262144 KiB = 256 MiB is a generous ceiling; the real default is 64 MiB.
        Assert.True(NotionVault.DefaultMemoryKib <= 262144);
    }

    [Fact]
    public void Encrypt_ProducesFreshSaltAndNonce_PerCall()
    {
        var a = Seal();
        var b = Seal();

        Assert.NotEqual(a.Salt, b.Salt);
        Assert.NotEqual(a.Nonce, b.Nonce);
        Assert.NotEqual(a.Ciphertext, b.Ciphertext);
    }

    [Fact]
    public void Envelope_IsVersioned_CarriesKdfParams_AndNoPlaintext()
    {
        var envelope = Seal();

        Assert.Equal(NotionVault.CurrentVersion, envelope.Version);
        Assert.Equal(NotionVault.KdfId, envelope.Kdf);
        Assert.Equal(TinyMemoryKib, envelope.MemoryKib);
        Assert.Equal(TinyPasses, envelope.Passes);
        Assert.Equal(NotionVault.Parallelism, envelope.Parallelism);

        // Every binary field is valid base64.
        Assert.Equal(16, Convert.FromBase64String(envelope.Salt).Length);
        Assert.Equal(24, Convert.FromBase64String(envelope.Nonce).Length);
        Assert.True(Convert.FromBase64String(envelope.Ciphertext).Length > 0);
    }

    [Fact]
    public void WrittenVault_OnDisk_ContainsNoPlaintextToken_AndReParses()
    {
        var dir = TempDir();
        try
        {
            var vaultPath = Path.Combine(dir, "notion.vault");
            NotionVault.WriteVault(vaultPath, Seal());

            var onDisk = File.ReadAllText(vaultPath);
            Assert.DoesNotContain(Token, onDisk);

            var reloaded = NotionVault.ReadVault(vaultPath);
            Assert.NotNull(reloaded);
            Assert.Equal(Token, NotionVault.Decrypt(reloaded!, Passphrase));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Decrypt_HonorsEnvelopeParams_NotHardcodedDefaults()
    {
        var envelope = Seal();

        // Mutating a stored cost parameter changes the derived key, so decryption must now fail — proof
        // that the params are read from the envelope (a future default retune still decrypts old files).
        // Both tampered values stay above libsodium's floor, so the derivation succeeds but yields a wrong key.
        var tamperedPasses = Clone(envelope);
        tamperedPasses.Passes = envelope.Passes + 1;
        Assert.Null(NotionVault.Decrypt(tamperedPasses, Passphrase));

        var tamperedMemory = Clone(envelope);
        tamperedMemory.MemoryKib = envelope.MemoryKib / 2;
        Assert.Null(NotionVault.Decrypt(tamperedMemory, Passphrase));

        // The untouched envelope still decrypts.
        Assert.Equal(Token, NotionVault.Decrypt(envelope, Passphrase));
    }

    [Fact]
    public void DecryptWithKey_TamperedCiphertext_ReturnsNull()
    {
        var envelope = Seal();
        var key = NotionVault.DeriveKeyBytes(Passphrase, envelope);
        Assert.NotNull(key);

        var raw = Convert.FromBase64String(envelope.Ciphertext);
        raw[0] ^= 0xFF;
        envelope.Ciphertext = Convert.ToBase64String(raw);

        Assert.Null(NotionVault.DecryptWithKey(envelope, key!));
    }

    [Fact]
    public void DecryptWithKey_WrongSizedKey_ReturnsNull()
    {
        var envelope = Seal();
        Assert.Null(NotionVault.DecryptWithKey(envelope, new byte[16]));
    }

    [Fact]
    public void ReadVault_MissingFile_ReturnsNull()
    {
        Assert.Null(NotionVault.ReadVault(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void ReadVault_CorruptJson_ReturnsNull()
    {
        var dir = TempDir();
        try
        {
            var vaultPath = Path.Combine(dir, "notion.vault");
            File.WriteAllText(vaultPath, "{not valid json");
            Assert.Null(NotionVault.ReadVault(vaultPath));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Decrypt_MalformedOrOffSpecEnvelope_ReturnsNull_NeverThrows()
    {
        // A committed notion.vault travels in git, so a tampered/bad-merged envelope must be rejected as
        // undecryptable BEFORE its parameters reach the KDF — never a throw, and never a giant allocation.
        var valid = Seal();

        // Salt: not base64, wrong length, and null (legal JSON overriding the "" initializer).
        Assert.Null(NotionVault.Decrypt(With(valid, e => e.Salt = "!!not-base64!!"), Passphrase));
        Assert.Null(NotionVault.Decrypt(With(valid, e => e.Salt = Convert.ToBase64String(new byte[8])), Passphrase));
        Assert.Null(NotionVault.Decrypt(With(valid, e => e.Salt = null!), Passphrase));

        // Cost out of bounds: the committed-file memory bomb (huge memory / passes), plus zero.
        Assert.Null(NotionVault.Decrypt(With(valid, e => e.MemoryKib = int.MaxValue), Passphrase));
        Assert.Null(NotionVault.Decrypt(With(valid, e => e.MemoryKib = 0), Passphrase));
        Assert.Null(NotionVault.Decrypt(With(valid, e => e.Passes = 0), Passphrase));
        Assert.Null(NotionVault.Decrypt(With(valid, e => e.Passes = 1000), Passphrase));

        // Unsupported version / kdf.
        Assert.Null(NotionVault.Decrypt(With(valid, e => e.Version = 999), Passphrase));
        Assert.Null(NotionVault.Decrypt(With(valid, e => e.Kdf = "scrypt"), Passphrase));

        // DeriveKeyBytes (the resolver's cached-key/prompt paths) honours the same guard.
        Assert.Null(NotionVault.DeriveKeyBytes(Passphrase, With(valid, e => e.MemoryKib = int.MaxValue)));

        // The untouched envelope still decrypts — the guard rejects only off-spec inputs.
        Assert.Equal(Token, NotionVault.Decrypt(valid, Passphrase));
    }

    private static NotionVaultEnvelope With(NotionVaultEnvelope e, Action<NotionVaultEnvelope> mutate)
    {
        var clone = Clone(e);
        mutate(clone);
        return clone;
    }

    private static NotionVaultEnvelope Clone(NotionVaultEnvelope e) => new()
    {
        Version = e.Version,
        Kdf = e.Kdf,
        MemoryKib = e.MemoryKib,
        Passes = e.Passes,
        Parallelism = e.Parallelism,
        Salt = e.Salt,
        Nonce = e.Nonce,
        Ciphertext = e.Ciphertext,
    };

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dydo-vault-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
