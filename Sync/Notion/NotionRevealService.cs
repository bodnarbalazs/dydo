namespace DynaDocs.Sync.Notion;

using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Runs <c>dydo notion reveal-token</c> (Decision 027 §4): a guarded break-glass that prints the stored
/// token to stdout — genuinely useful because Notion shows the token only once. It is gated behind an
/// explicit <c>--yes</c> (or an interactive y/N confirmation) and a warning, since printing a secret leaks
/// it into terminal scrollback. The token is read from the local store and never logged.
/// </summary>
public static class NotionRevealService
{
    public static int Execute(
        IConfigService config,
        bool yes,
        Func<bool> confirm,
        TextWriter output,
        TextWriter error)
    {
        if ((config.LoadConfig()?.Notion?.TokenStorage ?? NotionTokenStore.LocalMode) == NotionTokenStore.VaultMode)
        {
            error.WriteLine(NotionTokenStore.VaultNotImplementedMessage);
            return ExitCodes.ToolError;
        }

        var token = NotionTokenStore.Read(NotionTokenStore.PathFor(config.GetDydoRoot()));
        if (token == null)
        {
            error.WriteLine("notion reveal-token: no token stored. Run `dydo notion connect` first.");
            return ExitCodes.ToolError;
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
