namespace DynaDocs.Tests.Survey;

using DynaDocs.Commands;
using DynaDocs.Tests.Map;
using DynaDocs.Utils;
using static DynaDocs.Tests.Map.LinearJson;

public sealed class SurveyCommandTests : IDisposable
{
    private readonly FakeLinear _linear = new();
    private readonly FakeLinearServer _server;

    public SurveyCommandTests() => _server = new FakeLinearServer(_linear);

    public void Dispose()
    {
        _server.Dispose();
        _linear.Dispose();
    }

    private Task<(int ExitCode, string Stdout, string Stderr)> Run(params string[] arguments) =>
        ConsoleCapture.AllAsync(() => SurveyCommand.RunAsync(
            arguments, FakeLinear.ApiKey, _server.Endpoint.ToString(), new FixedClock(), CancellationToken.None));

    [Theory]
    [InlineData]
    [InlineData("25acbbb9a90b", "25acbbb9a90c")]
    [InlineData("map")]
    public async Task NotExactlyOneProject_IsAUsageError_BeforeAnyRequest(params string[] arguments)
    {
        var (exitCode, stdout, stderr) = await Run(arguments);

        Assert.Equal(ExitCodes.ToolError, exitCode);
        Assert.Contains("takes one Project UUID, slugId, or Linear Project URL", stderr);
        Assert.Empty(stdout);
        Assert.Empty(_linear.Calls);
    }

    [Fact]
    public async Task AnUnknownOption_IsTheSameUsageError()
    {
        var (exitCode, _, stderr) = await ConsoleCapture.AllAsync(
            () => SurveyCommand.Create().Parse(["--bogus"]).InvokeAsync());

        Assert.Equal(ExitCodes.ToolError, exitCode);
        Assert.Contains("got: --bogus", stderr);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task MissingKey_PrintsMapsKeyHelp_BeforeAnyRequest(string? key)
    {
        var (exitCode, stdout, stderr) = await ConsoleCapture.AllAsync(() => SurveyCommand.RunAsync(
            ["25acbbb9a90b"], key, _server.Endpoint.ToString(), new FixedClock(), CancellationToken.None));

        Assert.Equal(ExitCodes.ToolError, exitCode);
        Assert.Equal(MapCommand.MissingKeyHelp, stderr);
        Assert.Empty(stdout);
        Assert.Empty(_linear.Calls);
    }

    [Fact]
    public async Task ARead_PrintsTheHeaderInUtc_ThenOneLinePerFinding()
    {
        _linear.Serve(call => call.Operation == "ProjectIssues"
            ? IssuesPage(Node(Issue("1")))
            : ProjectContent("\"## Ledger\\n- DYD-1 lane\""));

        var (exitCode, stdout, stderr) = await Run("25acbbb9a90b");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(
            "dydo survey Map 2026-11-04T21:40:05Z: 1 findings\n"
            + "pickable DYD-1 Issue 1 — unstarted, unassigned, no open blocker\n",
            stdout.ReplaceLineEndings("\n"));
        Assert.Empty(stderr);
    }

    [Fact]
    public async Task AnUnexpectedAnswerShape_IsAToolError_NamingIt()
    {
        _linear.Serve(_ => """{"data":{"project":{"id":"p-map"}}}""");

        var (exitCode, stdout, stderr) = await Run("25acbbb9a90b");

        Assert.Equal(ExitCodes.ToolError, exitCode);
        Assert.Contains("Linear sent an unexpected response", stderr);
        Assert.Empty(stdout);
    }

    [Fact]
    public void Command_TakesTheProjectArgument_AndSaysItNeedsTheKey()
    {
        var command = SurveyCommand.Create();

        Assert.Equal("survey", command.Name);
        Assert.Equal("project", Assert.Single(command.Arguments).Name);
        Assert.Contains("LINEAR_API_KEY", command.Description);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 4, 22, 40, 5, TimeSpan.FromHours(1));
    }
}
