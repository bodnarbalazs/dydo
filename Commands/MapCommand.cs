namespace DynaDocs.Commands;

using System.CommandLine;
using System.Diagnostics;
using DynaDocs.Services.Map;
using DynaDocs.Utils;

public static class MapCommand
{
    private const string LinearEndpoint = "https://api.linear.app/graphql";

    internal const string MissingKeyHelp = """
        LINEAR_API_KEY is not set, so dydo map cannot read Linear.
        Create a personal API key in Linear under Settings > Account > Security & access >
        Personal API keys (https://linear.app/settings/account/security), then replace <your key> below.

        Windows PowerShell - use now and save for your Windows account across restarts:
          $env:LINEAR_API_KEY = '<your key>'
          [Environment]::SetEnvironmentVariable('LINEAR_API_KEY', $env:LINEAR_API_KEY, 'User')
        The first line alone lasts only for this session. The second saves it for future apps.
        Fully restart existing terminal/editor apps to pick up the saved value.

        Bash/zsh - use in this session only:
          export LINEAR_API_KEY='<your key>'
        To keep it across restarts, also add that export line to your shell startup file:
        Bash: ~/.bashrc; ensure your login startup file (e.g. ~/.bash_profile) sources it.
        Zsh: ~/.zshrc (or $ZDOTDIR/.zshrc if ZDOTDIR is set).
        New interactive shells will load it; run dydo map from a configured shell.

        """;

    public static Command Create()
    {
        var noBrowserOption = new Option<bool>("--no-browser")
        {
            Description = "Print the URL without opening a browser"
        };

        var command = new Command("map", Description(ViewerBundle.Embedded.IsBuilt));
        command.Options.Add(noBrowserOption);
        command.SetAction((parseResult, ct) => RunAsync(
            Environment.GetEnvironmentVariable("LINEAR_API_KEY"),
            // Test seam: the full-stack e2e points dydo map at a fake Linear.
            Environment.GetEnvironmentVariable("DYDO_LINEAR_ENDPOINT"),
            parseResult.GetValue(noBrowserOption) ? null : url => BrowserLauncher.Open(url, Process.Start),
            ct));
        return command;
    }

    internal static string Description(bool viewerBuilt) =>
        "Serve a read-only map of a Linear Project on localhost until Ctrl+C. Needs LINEAR_API_KEY. " +
        (viewerBuilt
            ? "The viewer is built in."
            : "This build has no viewer: it was not built, so / shows a 'viewer not built' page.");

    internal static async Task<int> RunAsync(
        string? apiKey, string? endpoint, Action<Uri>? openBrowser, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.Write(MissingKeyHelp);
            return ExitCodes.ToolError;
        }

        using var http = new HttpClient();
        var effectiveEndpoint = new Uri(string.IsNullOrEmpty(endpoint) ? LinearEndpoint : endpoint);
        var linear = new LinearGraphQL(http, effectiveEndpoint, apiKey);
        var cache = new MapSnapshotCache(effectiveEndpoint, apiKey, Environment.GetEnvironmentVariable("DYDO_MAP_CACHE_DIR"));
        using var server = new MapServer(new MapApi(new LinearReader(linear), cache), ViewerBundle.Embedded);

        var url = server.Start();
        Console.WriteLine($"dydo map: serving {url} (Ctrl+C to stop)");
        openBrowser?.Invoke(url);
        await server.RunAsync(ct);
        return ExitCodes.Success;
    }
}
