namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync.Notion;

/// <summary>Vault resolution precedence (Decision 027 §3): cached derived key → namespaced env passphrase
/// (CI) → interactive prompt, with fall-through when a key/passphrase can't decrypt, and a key-cache refresh
/// on a successful interactive decrypt. The passphrase source is injected so the tests stay Console-free.
/// The envelope is derived once and reused across tests to keep the Argon2id cost bounded.</summary>
[Collection("NotionArgon2id")]
public class NotionVaultResolverTests
{
    private const string Token = "ntn_resolve_token";
    private const string Passphrase = "Str0ng-Resolver-Key";
    // Cheap-but-valid Argon2id cost (8 MiB, 1 pass); the envelope is sealed once and reused across tests.
    private static readonly NotionVaultEnvelope Envelope = NotionVault.Encrypt(Token, Passphrase, memoryKib: 8192, passes: 1);
    private static readonly byte[] CorrectKey = NotionVault.DeriveKeyBytes(Passphrase, Envelope)!;

    private static readonly DydoConfig Config = new() { Name = "proj" };
    private const string PassphraseEnvVar = "DYDO_PROJ_NOTION_PASSPHRASE";

    [Fact]
    public void Resolve_NoVaultFile_ReturnsNull_WithoutPrompting()
    {
        var dydoRoot = TempDydoRoot();
        try
        {
            var prompted = false;
            var token = NotionVaultResolver.Resolve(Config, ProjectRoot, dydoRoot, () => { prompted = true; return Passphrase; });

            Assert.Null(token);
            Assert.False(prompted);
        }
        finally { SafeDelete(dydoRoot); }
    }

    [Fact]
    public void Resolve_InteractivePrompt_Decrypts_AndRefreshesKeyCache()
    {
        var dydoRoot = TempDydoRoot();
        try
        {
            WriteVault(dydoRoot);

            var token = NotionVaultResolver.Resolve(Config, ProjectRoot, dydoRoot, () => Passphrase);

            Assert.Equal(Token, token);
            var cached = NotionTokenStore.Read(NotionTokenStore.KeyCachePathFor(dydoRoot));
            Assert.Equal(Convert.ToBase64String(CorrectKey), cached);
        }
        finally { SafeDelete(dydoRoot); }
    }

    [Fact]
    public void Resolve_CachedKey_WinsOverPrompt()
    {
        var dydoRoot = TempDydoRoot();
        try
        {
            WriteVault(dydoRoot);
            NotionTokenStore.Write(NotionTokenStore.KeyCachePathFor(dydoRoot), Convert.ToBase64String(CorrectKey));

            var prompted = false;
            var token = NotionVaultResolver.Resolve(Config, ProjectRoot, dydoRoot, () => { prompted = true; return "unused"; });

            Assert.Equal(Token, token);
            Assert.False(prompted);
        }
        finally { SafeDelete(dydoRoot); }
    }

    [Fact]
    public void Resolve_EnvPassphrase_Decrypts_WhenNoCache()
    {
        var dydoRoot = TempDydoRoot();
        try
        {
            WriteVault(dydoRoot);
            var prompted = false;
            var token = WithEnv(PassphraseEnvVar, Passphrase, () =>
                NotionVaultResolver.Resolve(Config, ProjectRoot, dydoRoot, () => { prompted = true; return "unused"; }));

            Assert.Equal(Token, token);
            Assert.False(prompted);
        }
        finally { SafeDelete(dydoRoot); }
    }

    [Fact]
    public void Resolve_StaleCachedKey_FallsThroughToPrompt()
    {
        var dydoRoot = TempDydoRoot();
        try
        {
            WriteVault(dydoRoot);
            var staleKey = (byte[])CorrectKey.Clone();
            staleKey[0] ^= 0xFF;
            NotionTokenStore.Write(NotionTokenStore.KeyCachePathFor(dydoRoot), Convert.ToBase64String(staleKey));

            var token = NotionVaultResolver.Resolve(Config, ProjectRoot, dydoRoot, () => Passphrase);

            Assert.Equal(Token, token);
            // The refreshed cache replaced the stale one.
            Assert.Equal(Convert.ToBase64String(CorrectKey), NotionTokenStore.Read(NotionTokenStore.KeyCachePathFor(dydoRoot)));
        }
        finally { SafeDelete(dydoRoot); }
    }

    [Fact]
    public void Resolve_WrongEnvPassphrase_FallsThroughToPrompt()
    {
        var dydoRoot = TempDydoRoot();
        try
        {
            WriteVault(dydoRoot);
            var token = WithEnv(PassphraseEnvVar, "Wr0ng-Env-Passphrase", () =>
                NotionVaultResolver.Resolve(Config, ProjectRoot, dydoRoot, () => Passphrase));

            Assert.Equal(Token, token);
        }
        finally { SafeDelete(dydoRoot); }
    }

    [Fact]
    public void Resolve_WrongPromptPassphrase_ReturnsNull_NoCacheWritten()
    {
        var dydoRoot = TempDydoRoot();
        try
        {
            WriteVault(dydoRoot);
            var token = NotionVaultResolver.Resolve(Config, ProjectRoot, dydoRoot, () => "Wr0ng-Prompt-Key");

            Assert.Null(token);
            Assert.False(NotionTokenStore.Exists(NotionTokenStore.KeyCachePathFor(dydoRoot)));
        }
        finally { SafeDelete(dydoRoot); }
    }

    [Fact]
    public void TokenResolver_VaultStorage_DelegatesToVaultPath()
    {
        var dydoRoot = TempDydoRoot();
        try
        {
            WriteVault(dydoRoot);
            var vaultConfig = new DydoConfig { Name = "proj", Notion = new NotionConfig { TokenStorage = "vault" } };

            var token = NotionTokenResolver.Resolve(vaultConfig, ProjectRoot, dydoRoot, () => Passphrase);

            Assert.Equal(Token, token);
        }
        finally { SafeDelete(dydoRoot); }
    }

    private const string ProjectRoot = @"C:\x\proj";

    private static void WriteVault(string dydoRoot) =>
        NotionVault.WriteVault(NotionTokenStore.VaultPathFor(dydoRoot), Envelope);

    private static string TempDydoRoot() =>
        Path.Combine(Path.GetTempPath(), "dydo-vaultres-" + Guid.NewGuid().ToString("N")[..8], "dydo");

    private static T WithEnv<T>(string name, string? value, Func<T> body)
    {
        var saved = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
        try { return body(); }
        finally { Environment.SetEnvironmentVariable(name, saved); }
    }

    private static void SafeDelete(string dydoRoot)
    {
        try { Directory.Delete(Path.GetDirectoryName(dydoRoot)!, true); } catch { }
    }
}
