namespace DynaDocs.Tests.Survey;

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DynaDocs.Commands;
using DynaDocs.Tests.Map;
using Reqnroll;
using static DynaDocs.Tests.Map.LinearJson;

[Binding]
public sealed partial class SurveySteps
{
    private static readonly Dictionary<string, string> StateTypes = new()
    {
        ["Triage"] = "triage", ["Backlog"] = "backlog", ["Todo"] = "unstarted", ["In Progress"] = "started",
        ["Done"] = "completed", ["Canceled"] = "canceled", ["Duplicate"] = "duplicate",
    };

    private readonly FakeLinear _linear = new();
    private readonly FakeLinearServer _server;
    private readonly string? _savedKey = Environment.GetEnvironmentVariable("LINEAR_API_KEY");
    private readonly string? _savedEndpoint = Environment.GetEnvironmentVariable("DYDO_LINEAR_ENDPOINT");
    private string? _key = FakeLinear.ApiKey;
    private string _endpoint;
    private string _body = "";
    private List<IssueRow> _issues = [];
    private (int ExitCode, string Stdout, string Stderr) _run;
    private (DateTimeOffset Before, DateTimeOffset After) _window;

    public SurveySteps()
    {
        _server = new FakeLinearServer(_linear);
        _endpoint = _server.Endpoint.ToString();
        Serve();
    }

    [Given("the Project's body is:")]
    public void Body(string body)
    {
        _body = body;
        Serve();
    }

    [Given("the Project has these issues:")]
    public void Issues(DataTable table)
    {
        _issues = [.. table.Rows.Select(row => new IssueRow(
            Number(row["key"]), StateTypes[row["state"]], row["assignee"], row["parent"], row["blocked by"],
            row["label"], row["priority"], row["project"] == "Map"))];
        Serve();
    }

    [Given("a quiet Project")]
    public void Quiet()
    {
        _body = "## Lane ledger\n- DYD-1 lane";
        _issues = [new(1, "started", "Ada", "", "", "", "0", true)];
        Serve();
    }

    [Given("Linear has no such Project")]
    public void NoSuchProject() => _linear.Serve(_ => """{"data":{"project":null}}""");

    [Given("LINEAR_API_KEY is not set")]
    public void NoKey() => _key = null;

    [Given("Linear fails with {string}")]
    public void Fails(string failure)
    {
        switch (failure)
        {
            case "HTTP 500":
                _linear.Answer = _ => (HttpStatusCode.InternalServerError, "{}");
                break;
            case "a rejected key":
                _linear.Answer = _ => (HttpStatusCode.BadRequest,
                    """{"errors":[{"message":"Authentication required","extensions":{"type":"authentication error"}}]}""");
                break;
            case "no answer":
                _endpoint = $"http://localhost:{FakeLinearServer.FreePort()}/graphql";
                break;
            default:
                throw new ArgumentException(failure);
        }
    }

    [When("I survey {string}")]
    public async Task SurveyProject(string argument)
    {
        Environment.SetEnvironmentVariable("LINEAR_API_KEY", _key);
        Environment.SetEnvironmentVariable("DYDO_LINEAR_ENDPOINT", _endpoint);
        var before = DateTimeOffset.UtcNow;
        _run = await ConsoleCapture.AllAsync(() => SurveyCommand.Create().Parse([argument]).InvokeAsync());
        _window = (before, DateTimeOffset.UtcNow);
    }

    [Then("the survey exits {int}")]
    public void Exits(int exitCode) => Assert.Equal(exitCode, _run.ExitCode);

    [Then("the survey prints:")]
    public void Prints(string expected)
    {
        var time = Time().Match(_run.Stdout);
        Assert.True(time.Success, _run.Stdout);
        var at = DateTimeOffset.Parse(time.Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(at, _window.Before.AddSeconds(-1), _window.After);
        Assert.Equal(expected.ReplaceLineEndings("\n") + "\n",
            Time().Replace(_run.Stdout, "<time>", 1).ReplaceLineEndings("\n"));
        Assert.Empty(_run.Stderr);
    }

    [Then("the error says {string}")]
    public void ErrorSays(string message)
    {
        Assert.Contains(message, _run.Stderr);
        Assert.Empty(_run.Stdout);
    }

    [Then("the error is dydo map's missing-key help")]
    public void MissingKeyHelp() => Assert.Equal(MapCommand.MissingKeyHelp, _run.Stderr);

    [Then("Linear was asked for Project {string}")]
    public void AskedFor(string project)
    {
        if (project == "nothing")
            Assert.Empty(_linear.Calls);
        else
            Assert.Equal(project, _linear.Calls[0].Var("projectId"));
    }

    [Then("every Linear request was a query")]
    public void OnlyQueries()
    {
        Assert.NotEmpty(_linear.Calls);
        Assert.All(_linear.Calls, call =>
        {
            Assert.StartsWith("query ", call.Query.TrimStart(), StringComparison.Ordinal);
            Assert.DoesNotContain("mutation", call.Query, StringComparison.Ordinal);
            Assert.Equal(FakeLinear.ApiKey, call.Authorization);
        });
    }

    private void Serve() => _linear.Serve(call => call.Operation == "ProjectIssues"
        ? IssuesPage(string.Join(",", _issues.Where(issue => issue.InMap).Select(NodeOf)))
        : ProjectContent(JsonSerializer.Serialize(_body)));

    // A Map issue carries the blockers it lists and the Other issues it blocks; Other issues appear only
    // as those far ends, as Linear's graph shows them.
    private string NodeOf(IssueRow issue) => Node(
        IssueJson(issue),
        relations: Connection(string.Join(",", _issues
            .Where(other => !other.InMap && other.BlockedBy == issue.Key)
            .Select(other => Outgoing($"r{issue.Number}-{other.Number}", "blocks", IssueJson(other))))),
        inverseRelations: Connection(string.Join(",", _issues
            .Where(blocker => blocker.Key == issue.BlockedBy)
            .Select(blocker => Incoming($"r{blocker.Number}-{issue.Number}", "blocks", IssueJson(blocker))))));

    private static string IssueJson(IssueRow issue) => WithPriority(Issue(
        issue.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
        state: $$"""{"name":"{{StateTypes.Single(state => state.Value == issue.Type).Key}}","type":"{{issue.Type}}","color":"#000000"}""",
        project: issue.InMap ? InMap : InOther,
        assignee: issue.Assignee == "" ? "null" : $$"""{"name":"{{issue.Assignee}}"}""",
        parent: issue.Parent == "" ? "null" : $$"""{"id":"{{Number(issue.Parent)}}"}""",
        labels: issue.Label == "" ? Labels() : Labels($"{issue.Label}:#000000")), issue.Priority);

    private static int Number(string key) => int.Parse(key["DYD-".Length..], System.Globalization.CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ")]
    private static partial Regex Time();

    [AfterScenario("survey")]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("LINEAR_API_KEY", _savedKey);
        Environment.SetEnvironmentVariable("DYDO_LINEAR_ENDPOINT", _savedEndpoint);
        _server.Dispose();
        _linear.Dispose();
    }

    private sealed record IssueRow(
        int Number, string Type, string Assignee, string Parent, string BlockedBy, string Label, string Priority, bool InMap)
    {
        public string Key => $"DYD-{Number}";
    }
}
