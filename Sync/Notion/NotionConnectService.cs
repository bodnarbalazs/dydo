namespace DynaDocs.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Runs <c>dydo notion connect</c> (Decision 027 §4). In the default local mode it stores the show-once
/// integration token in the gitignored secret store; with <c>--vault</c> it seals the token as committed
/// authenticated ciphertext (Argon2id + XChaCha20-Poly1305) under a strong passphrase, so the encrypted
/// token can travel in git while the passphrase does not. Either way it records the storage policy (and an
/// optional parent page) in dydo.json. The token is read through <paramref name="readToken"/> and the
/// passphrase through <paramref name="readPassphrase"/> so a TTY paste can be masked while tests stay
/// Console-free; neither is ever echoed, logged, or written to the committed config.
/// </summary>
public static class NotionConnectService
{
    public static int Execute(
        IConfigService config,
        Func<string?> readToken,
        Func<bool> confirmOverwrite,
        string? parentPageId,
        bool vault,
        Func<string?> readPassphrase,
        TextWriter output,
        TextWriter error)
    {
        var configPath = config.FindConfigFile();
        var loaded = config.LoadConfig();
        if (configPath == null || loaded == null)
        {
            error.WriteLine("notion connect: no dydo.json found; run inside a dydo project.");
            return ExitCodes.ToolError;
        }

        var dydoRoot = config.GetDydoRoot();
        var targetPath = vault ? NotionTokenStore.VaultPathFor(dydoRoot) : NotionTokenStore.PathFor(dydoRoot);
        if (File.Exists(targetPath) && !confirmOverwrite())
        {
            output.WriteLine("notion connect: kept the existing token.");
            return ExitCodes.Success;
        }

        var token = readToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            error.WriteLine("notion connect: no token provided.");
            return ExitCodes.ToolError;
        }

        return vault
            ? ConnectVault(config, loaded, configPath, dydoRoot, token.Trim(), parentPageId, readPassphrase, output, error)
            : ConnectLocal(config, loaded, configPath, dydoRoot, token.Trim(), parentPageId, output);
    }

    private static int ConnectLocal(
        IConfigService config, DydoConfig loaded, string configPath, string dydoRoot,
        string token, string? parentPageId, TextWriter output)
    {
        NotionTokenStore.Write(NotionTokenStore.PathFor(dydoRoot), token);
        // Switching (back) to local mode: drop the now-orphaned committed vault + its stale key cache so a
        // ciphertext blob no config references can't linger in the tree.
        NotionTokenStore.Delete(NotionTokenStore.VaultPathFor(dydoRoot));
        NotionTokenStore.Delete(NotionTokenStore.KeyCachePathFor(dydoRoot));
        Persist(config, loaded, configPath, NotionTokenStore.LocalMode, parentPageId);
        output.WriteLine("notion connect: token stored locally (dydo/_system/.local/, gitignored). Run `dydo notion sync`.");
        return ExitCodes.Success;
    }

    private static int ConnectVault(
        IConfigService config, DydoConfig loaded, string configPath, string dydoRoot,
        string token, string? parentPageId, Func<string?> readPassphrase, TextWriter output, TextWriter error)
    {
        var passphrase = readPassphrase();
        if (string.IsNullOrEmpty(passphrase))
        {
            error.WriteLine("notion connect: no passphrase provided.");
            return ExitCodes.ToolError;
        }

        if (passphrase != readPassphrase())
        {
            error.WriteLine("notion connect: passphrases did not match.");
            return ExitCodes.ToolError;
        }

        var weakness = NotionPassphrasePolicy.Validate(passphrase);
        if (weakness != null)
        {
            error.WriteLine($"notion connect: {weakness}");
            return ExitCodes.ToolError;
        }

        NotionVault.WriteVault(NotionTokenStore.VaultPathFor(dydoRoot), NotionVault.Encrypt(token, passphrase));
        // The new vault has a fresh salt, so any cached derived key is stale — drop it rather than let the
        // next resolve fail-then-prompt on a mismatch. Also drop the now-orphaned local plaintext secret.
        NotionTokenStore.Delete(NotionTokenStore.KeyCachePathFor(dydoRoot));
        NotionTokenStore.Delete(NotionTokenStore.PathFor(dydoRoot));
        Persist(config, loaded, configPath, NotionTokenStore.VaultMode, parentPageId);
        output.WriteLine("notion connect: token sealed in the encrypted vault (dydo/_system/notion.vault, committed). Run `dydo notion sync`.");
        return ExitCodes.Success;
    }

    private static void Persist(IConfigService config, DydoConfig loaded, string configPath, string mode, string? parentPageId)
    {
        loaded.Notion ??= new NotionConfig();
        loaded.Notion.TokenStorage = mode;
        if (!string.IsNullOrWhiteSpace(parentPageId))
            loaded.Notion.ParentPageId = parentPageId;
        config.SaveConfig(loaded, configPath);
    }
}
