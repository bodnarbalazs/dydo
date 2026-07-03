namespace DynaDocs.Sync.Notion;

using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Runs <c>dydo notion reveal-token</c> (Decision 027 §4): a guarded break-glass that prints the stored
/// token to stdout — genuinely useful because Notion shows the token only once. In vault mode it first
/// obtains the passphrase (cached key → env → interactive prompt, via <see cref="NotionVaultResolver"/>)
/// and decrypts; in local mode it reads the plaintext store. Printing is then gated behind an explicit
/// <c>--yes</c> (or an interactive y/N confirmation) and a warning, since it leaks the secret into terminal
/// scrollback. The token is never logged; the passphrase is read through the injected source.
/// </summary>
public static class NotionRevealService
{
    public static int Execute(
        IConfigService config,
        bool yes,
        Func<bool> confirm,
        Func<string?> promptPassphrase,
        TextWriter output,
        TextWriter error)
    {
        var loaded = config.LoadConfig();
        var storage = loaded?.Notion?.TokenStorage ?? NotionTokenStore.LocalMode;

        string? token;
        if (storage == NotionTokenStore.VaultMode)
        {
            token = NotionVaultResolver.Resolve(
                loaded, config.GetProjectRoot(), config.GetDydoRoot(), promptPassphrase);
            if (token == null)
            {
                error.WriteLine("notion reveal-token: could not unlock the vault (missing vault or wrong passphrase). Run `dydo notion connect --vault` first.");
                return ExitCodes.ToolError;
            }
        }
        else
        {
            token = NotionTokenStore.Read(NotionTokenStore.PathFor(config.GetDydoRoot()));
            if (token == null)
            {
                error.WriteLine("notion reveal-token: no token stored. Run `dydo notion connect` first.");
                return ExitCodes.ToolError;
            }
        }

        error.WriteLine("notion reveal-token: WARNING — this prints your Notion token to the terminal (scrollback exposure).");
        if (!yes && !confirm())
        {
            error.WriteLine("notion reveal-token: cancelled.");
            return ExitCodes.Success;
        }

        output.WriteLine(token);
        return ExitCodes.Success;
    }
}
