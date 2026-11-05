namespace DynaDocs.Commands;

using System.CommandLine;
using System.Globalization;
using DynaDocs.Services.Map;
using DynaDocs.Services.Survey;
using DynaDocs.Utils;

/// <summary>
/// <c>dydo survey &lt;project&gt;</c>: read one Linear Project once, on demand, and print its lane findings.
/// It reads through <c>dydo map</c>'s <see cref="LinearReader"/> and writes nothing anywhere.
/// </summary>
public static class SurveyCommand
{
    private const string LinearEndpoint = "https://api.linear.app/graphql";

    // dydo's usage errors exit 2, the same code as a tool error.
    private const int UsageError = ExitCodes.ToolError;

    public static Command Create()
    {
        // Zero or more, so a missing or extra argument is this command's usage error, not the parser's.
        var projectArgument = new Argument<string[]>("project")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Description = "Project UUID, slugId, or Linear Project URL"
        };

        var command = new Command("survey",
            "Print a read-only digest of one Linear Project's lanes: unblocked, pickable and unlaned issues, stale ledger rows. Needs LINEAR_API_KEY.");
        command.Arguments.Add(projectArgument);
        command.SetAction((parseResult, ct) => RunAsync(
            parseResult.GetValue(projectArgument) ?? [],
            Environment.GetEnvironmentVariable("LINEAR_API_KEY"),
            // Test seam: the same fake-Linear endpoint dydo map's tests use.
            Environment.GetEnvironmentVariable("DYDO_LINEAR_ENDPOINT"),
            TimeProvider.System,
            ct));
        return command;
    }

    internal static async Task<int> RunAsync(
        string[] arguments, string? apiKey, string? endpoint, TimeProvider clock, CancellationToken ct)
    {
        if (arguments is not [var argument] || ProjectReference.Parse(argument) is not { } projectId)
        {
            Console.Error.WriteLine(
                $"dydo survey takes one Project UUID, slugId, or Linear Project URL; got: {string.Join(" ", arguments)}");
            return UsageError;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.Write(MapCommand.MissingKeyHelp);
            return ExitCodes.ToolError;
        }

        using var http = new HttpClient();
        var reader = new LinearReader(new LinearGraphQL(
            http, new Uri(string.IsNullOrEmpty(endpoint) ? LinearEndpoint : endpoint), apiKey));
        LinearProjectRead read;
        try
        {
            read = await reader.ReadProjectAsync(projectId, ct);
        }
        catch (MapApiException ex)
        {
            // The reader names what failed: an unknown Project (a usage error, the same exit 2), a ticket
            // gone mid-read, a refused key, an HTTP failure or no answer.
            Console.Error.WriteLine($"dydo survey: {ex.Message}");
            return ExitCodes.ToolError;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            // Linear answered, but not in the shape the queries ask for.
            Console.Error.WriteLine($"dydo survey: Linear sent an unexpected response: {ex.Message}");
            return ExitCodes.ToolError;
        }

        var findings = SurveyRules.Find(read);
        var time = clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        Console.WriteLine($"dydo survey {read.Graph.Project.Name} {time}: {findings.Count} findings");
        foreach (var finding in findings)
            Console.WriteLine(finding);
        return ExitCodes.Success;
    }
}
