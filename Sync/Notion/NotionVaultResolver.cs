namespace DynaDocs.Sync.Notion;

using DynaDocs.Models;

/// <summary>
/// Resolves the token from an encrypted vault (Decision 027 §3), in precedence order: the locally-cached
/// <em>derived key</em> → the namespaced <c>DYDO_&lt;SLUG&gt;_NOTION_PASSPHRASE</c> env var (for CI) → an
/// interactive passphrase prompt. The passphrase source is injected so the flow stays Console-free under
/// test. Any cached key or supplied passphrase that fails to decrypt is treated as a miss and falls through
/// to the next tier — the "detect it can't decrypt, then prompt" UX. A successful <em>interactive</em>
/// decrypt refreshes the local key cache so the next run is non-interactive; the CI env path deliberately
/// does not write a cache (the runner is ephemeral). The passphrase is never cached or logged.
/// </summary>
public static class NotionVaultResolver
{
    public static string PassphraseEnvVar(string slug) => $"DYDO_{slug}_NOTION_PASSPHRASE";

    public static string? Resolve(DydoConfig? config, string? projectRoot, string dydoRoot, Func<string?>? promptPassphrase)
    {
        var envelope = NotionVault.ReadVault(NotionTokenStore.VaultPathFor(dydoRoot));
        if (envelope == null)
            return null;

        var keyCachePath = NotionTokenStore.KeyCachePathFor(dydoRoot);

        var cachedKey = ReadCachedKey(keyCachePath);
        if (cachedKey != null)
        {
            var token = NotionVault.DecryptWithKey(envelope, cachedKey);
            if (token != null)
                return token;
        }

        var slug = NotionTokenResolver.SlugFor(config, projectRoot);
        if (slug.Length > 0)
        {
            var envPassphrase = Environment.GetEnvironmentVariable(PassphraseEnvVar(slug));
            if (!string.IsNullOrEmpty(envPassphrase))
            {
                var token = NotionVault.Decrypt(envelope, envPassphrase);
                if (token != null)
                    return token;
            }
        }

        if (promptPassphrase != null)
        {
            var passphrase = promptPassphrase();
            if (!string.IsNullOrEmpty(passphrase))
            {
                var rawKey = NotionVault.DeriveKeyBytes(passphrase, envelope);
                if (rawKey != null)
                {
                    var token = NotionVault.DecryptWithKey(envelope, rawKey);
                    if (token != null)
                    {
                        NotionTokenStore.Write(keyCachePath, Convert.ToBase64String(rawKey));
                        return token;
                    }
                }
            }
        }

        return null;
    }

    private static byte[]? ReadCachedKey(string keyCachePath)
    {
        var encoded = NotionTokenStore.Read(keyCachePath);
        if (encoded == null)
            return null;

        try { return Convert.FromBase64String(encoded); }
        catch (FormatException) { return null; }
    }
}
