namespace DynaDocs.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Runs <c>dydo notion connect</c> (Decision 027 §4): store the show-once integration token in the local
/// secret store and record the local storage policy (plus an optional parent page) in dydo.json. The token
/// itself is read through the injected <paramref name="readToken"/> so the command can mask a TTY paste while
/// tests stay Console-free; it is never echoed and never written to the committed config. Every external
/// dependency is injected so the flow is unit-testable with no network and no Console.
/// </summary>
public static class NotionConnectService
{
    public static int Execute(
        IConfigService config,
        Func<string?> readToken,
        Func<bool> confirmOverwrite,
        string? parentPageId,
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

        var secretPath = NotionTokenStore.PathFor(config.GetDydoRoot());
        if (NotionTokenStore.Exists(secretPath) && !confirmOverwrite())
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

        NotionTokenStore.Write(secretPath, token.Trim());

        loaded.Notion ??= new NotionConfig();
        loaded.Notion.TokenStorage = NotionTokenStore.LocalMode;
        if (!string.IsNullOrWhiteSpace(parentPageId))
            loaded.Notion.ParentPageId = parentPageId;
        config.SaveConfig(loaded, configPath);

        output.WriteLine("notion connect: token stored locally (dydo/_system/.local/, gitignored). Run `dydo notion sync`.");
        return ExitCodes.Success;
    }
}
