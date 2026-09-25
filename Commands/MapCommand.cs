namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services.Map;
using DynaDocs.Utils;

public static class MapCommand
{
    private const string LinearEndpoint = "https://api.linear.app/graphql";

    internal const string MissingKeyHelp = """
        LINEAR_API_KEY is not set, so dydo map cannot read Linear.
        Create a personal API key in Linear under Settings > Account > Security & access >
        Personal API keys (https://linear.app/settings/account/security), then set it:
          export LINEAR_API_KEY=<your key>          (bash, zsh)
          $env:LINEAR_API_KEY = "<your key>"        (PowerShell)

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
            parseResult.GetValue(noBrowserOption) ? null : BrowserLauncher.Open,
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
        var linear = new LinearGraphQL(
            http, new Uri(string.IsNullOrEmpty(endpoint) ? LinearEndpoint : endpoint), apiKey);
        using var server = new MapServer(new MapApi(new LinearReader(linear)), ViewerBundle.Embedded);

        var url = server.Start();
        Console.WriteLine($"dydo map: serving {url} (Ctrl+C to stop)");
        openBrowser?.Invoke(url);
        await server.RunAsync(ct);
        return ExitCodes.Success;
    }
}
