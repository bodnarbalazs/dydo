namespace DynaDocs.Tests.Steps;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Reqnroll;

[Binding]
[Scope(Feature = "Gate adapters preserve measurement and cleanup outcomes")]
public sealed class AssuranceAdoptionSteps
{
    // Every example builds its own fixture repository and runs both real facades over it, so a
    // column can only be green when the canonical and the derived runner both observe it.
    private static readonly (string Name, string Path)[] Facades =
    [
        ("canonical", "DynaDocs.Tests/coverage/gap_check.py"),
        ("derived", "dydo/reference/gap-check.example.py"),
    ];

    private static readonly string[] Capabilities = ["test", "static", "coverage", "mutation"];

    private readonly List<(string Artifact, string Script)> _adapters = [];
    private readonly List<string> _fixtures = [];
    private List<(string Facade, string Directory, int Exit, JsonNode Payload)>? _observations;
    private string _gate = "test";

    [AfterScenario("DYD-96")]
    public void RemoveFixtures()
    {
        foreach (var directory in _fixtures)
            TestDirectory.Delete(directory);
    }

    [Given(@"^a configured ""([^""]+)"" fixture adapter that completes with native exit (\d+)$")]
    public void GivenGateFixture(string gate, int native)
    {
        _gate = gate;
        _adapters.Add(Completing(gate, _adapters.Count, native));
    }

    [Given(@"^a configured test fixture command that normally returns native exit (\d+)$")]
    public void GivenTestFixture(int native)
    {
        _gate = "test";
        _adapters.Add(Completing("test", _adapters.Count, native));
    }

    [Given(@"^three ordered independent ""([^""]+)"" adapters returning native exits 1, 2 and 0$")]
    public void GivenIndependentFixtures(string gate)
    {
        _gate = gate;
        foreach (var native in new[] { 1, 2, 0 })
            _adapters.Add(Completing(gate, _adapters.Count, native));
    }

    [Given(@"^an ordered ""([^""]+)"" adapter that starts a long-lived owned child and then cancels it$")]
    public void GivenInterruptedFixture(string gate)
    {
        _gate = gate;
        _adapters.Add(Interrupted(gate));
    }

    [Given(@"^the adapter produces its declared evidence and completes owned cleanup before returning$")]
    public void GivenDeclaredEvidence() =>
        Assert.All(_adapters, adapter => Assert.Contains(Literal(adapter.Artifact), adapter.Script));

    [Given(@"^the facade itself receives no interruption$")]
    public void GivenNoFacadeInterruption() =>
        Assert.All(_adapters, adapter => Assert.DoesNotContain("signal", adapter.Script));

    [Given(@"^the adapter waits for that child to stop and removes its owned scratch before returning 130$")]
    public void GivenOwnedCleanup()
    {
        Assert.Contains("p.terminate();p.wait();s.rmdir()", _adapters[0].Script);
        Assert.EndsWith("raise SystemExit(130)", _adapters[0].Script);
    }

    [Given(@"^a later independent adapter would write a dispatch marker$")]
    public void GivenLaterAdapter()
    {
        _adapters.Add(Marker(_gate, _adapters.Count));
        Assert.Contains("'later-marker'", _adapters[^1].Script);
    }

    [When(@"^I invoke that gate through each canonical and derived testing facade$")]
    [When(@"^I invoke the test through each canonical and derived testing facade$")]
    public void WhenInvoked() => _observations = [.. Facades.Select(Invoke)];

    [Then(@"^its result row has state ""([^""]+)"", childExit (\d+) and resultExit (\d+)$")]
    public void ThenResultRow(string state, int childExit, int resultExit)
    {
        foreach (var (facade, _, _, payload) in Observations())
        {
            var row = payload["results"]![0]!;
            Assert.Equal($"{facade}: {state}/{childExit}/{resultExit}",
                $"{facade}: {row["state"]}/{row["childExit"]}/{row["resultExit"]}");
        }
    }

    [Then(@"^its aggregate result and process exit are both (\d+)$")]
    [Then(@"^the aggregate result and process exit are both (\d+)$")]
    public void ThenAggregate(int expected)
    {
        foreach (var (facade, _, exit, payload) in Observations())
            Assert.Equal($"{facade}: {expected}/{expected}",
                $"{facade}: {payload["aggregateExit"]}/{exit}");
    }

    [Then(@"^the published result retains schema 1 and the existing artifact shape$")]
    public void ThenPublishedShape()
    {
        foreach (var (facade, _, _, payload) in Observations())
        {
            Assert.Equal($"{facade}: 1", $"{facade}: {payload["schema"]}");
            Assert.Equal(
                new[] { "aggregateExit", "candidate", "operation", "results", "schema", "selectedStacks" },
                Keys(payload));
            Assert.Equal(
                new[] { "argv", "artifacts", "capability", "childExit", "cwd", "environment",
                        "isolation", "resultExit", "stack", "state" },
                Keys(payload["results"]![0]!));
        }
    }

    [Then(@"^all three adapters execute once in manifest order$")]
    public void ThenEveryAdapterRan()
    {
        foreach (var (facade, directory, _, payload) in Observations())
        {
            Assert.Equal($"{facade}: fixture-0,fixture-1,fixture-2",
                $"{facade}: {Joined(payload["results"]!, "stack")}");
            Assert.All(_adapters, adapter =>
                Assert.True(File.Exists(Local(directory, adapter.Artifact)), facade));
        }
    }

    [Then(@"^their ordered row states are failed, invalid and passed with unchanged child exits$")]
    public void ThenOrderedStates()
    {
        foreach (var (facade, _, _, payload) in Observations())
            Assert.Equal($"{facade}: failed/1,invalid/2,passed/0",
                $"{facade}: {string.Join(',', payload["results"]!.AsArray()
                    .Select(row => $"{row!["state"]}/{row["childExit"]}"))}");
    }

    [Then(@"^the interrupted row is published only after the owned child has stopped and scratch is absent$")]
    public void ThenOwnedCleanupPrecededPublication()
    {
        foreach (var (facade, directory, _, payload) in Observations())
        {
            Assert.False(Directory.Exists(Local(directory, "owned-scratch")), facade);
            Assert.True(File.Exists(Local(directory, _adapters[0].Artifact)), facade);
            Assert.Equal($"{facade}: interrupted", $"{facade}: {payload["results"]![0]!["state"]}");
        }
    }

    [Then(@"^the later adapter does not execute and its dispatch marker is absent$")]
    public void ThenLaterAdapterNeverRan()
    {
        foreach (var (facade, directory, _, payload) in Observations())
        {
            Assert.False(File.Exists(Local(directory, "later-marker")), facade);
            Assert.False(File.Exists(Local(directory, _adapters[1].Artifact)), facade);
            Assert.Equal($"{facade}: 1", $"{facade}: {payload["results"]!.AsArray().Count}");
        }
    }

    [Then(@"^the interrupted row retains childExit 130 and resultExit 130$")]
    public void ThenInterruptedRowRetained() => ThenResultRow("interrupted", 130, 130);

    private List<(string Facade, string Directory, int Exit, JsonNode Payload)> Observations() =>
        _observations ?? throw new InvalidOperationException("No facade was invoked");

    private (string Facade, string Directory, int Exit, JsonNode Payload) Invoke(
        (string Name, string Path) facade)
    {
        var directory = Directory.CreateTempSubdirectory("dydo-assurance-").FullName;
        _fixtures.Add(directory);
        File.Copy(Local(RepositoryRoot(), facade.Path), Local(directory, "gap_check.py"));
        File.WriteAllText(Local(directory, "gap_check.json"), Manifest().ToJsonString());
        var (exit, output) = Run(directory);
        var runs = Local(directory, "results");
        var results = Directory.Exists(runs)
            ? Directory.GetFiles(runs, "result.json", SearchOption.AllDirectories)
            : [];
        Assert.True(results.Length == 1, $"{facade.Name}: {output}");
        return (facade.Name, directory, exit, JsonNode.Parse(File.ReadAllText(results[0]))!);
    }

    private (int Exit, string Output) Run(string directory)
    {
        var interpreter = Environment.GetEnvironmentVariable("PYTHON")
            ?? (OperatingSystem.IsWindows() ? "py" : "python3");
        var start = new ProcessStartInfo(interpreter)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["PYTHON"] = interpreter;
        start.ArgumentList.Add(Local(directory, "gap_check.py"));
        foreach (var argument in Request())
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode,
            output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
    }

    private string[] Request()
    {
        if (_gate == "test")
            return ["test", "--stack", "fixture-0"];
        if (_gate == "mutation")
            return ["gate", _gate, "--since", "BASE"];
        return ["gate", _gate];
    }

    private JsonObject Manifest()
    {
        var stacks = new JsonArray();
        for (var index = 0; index < _adapters.Count; index++)
            stacks.Add(Stack(index, _adapters[index]));
        return new JsonObject { ["schema"] = 1, ["artifactRoot"] = "results", ["stacks"] = stacks };
    }

    private JsonObject Stack(int index, (string Artifact, string Script) adapter)
    {
        var capabilities = new JsonObject();
        foreach (var name in Capabilities)
            capabilities[name] = new JsonObject { ["state"] = "unavailable", ["reason"] = "fixture" };
        var argv = new JsonArray("-c", adapter.Script);
        if (_gate == "mutation")
            argv.Add("{base}");
        capabilities[_gate] = new JsonObject
        {
            ["state"] = "configured",
            ["command"] = new JsonObject { ["kind"] = "current-python", ["argv"] = argv },
            ["artifacts"] = new JsonArray(
                new JsonObject { ["path"] = adapter.Artifact, ["required"] = true }),
        };
        return new JsonObject
        {
            ["name"] = $"fixture-{index}",
            ["kind"] = "python",
            ["cwd"] = ".",
            ["isolation"] = new JsonObject
            {
                ["requirement"] = "in-place",
                ["evidence"] = new JsonObject { ["state"] = "verified", ["kind"] = "direct" },
            },
            ["capabilities"] = capabilities,
        };
    }

    private static (string Artifact, string Script) Completing(string gate, int index, int native)
    {
        var artifact = $"artifacts/{gate}-{index}.json";
        return (artifact, "import json,pathlib,sys;"
            + $"p=pathlib.Path({Literal(artifact)});p.parent.mkdir(parents=True,exist_ok=True);"
            + $"p.write_text(json.dumps({{'schema':1,'exitCode':{native}}}));"
            + $"raise SystemExit({native})");
    }

    private static (string Artifact, string Script) Interrupted(string gate)
    {
        var artifact = $"artifacts/{gate}-0.json";
        return (artifact, "import json,pathlib,subprocess,sys;"
            + "s=pathlib.Path('owned-scratch');s.mkdir();"
            + "p=subprocess.Popen([sys.executable,'-c','import time;time.sleep(30)']);"
            + "p.terminate();p.wait();s.rmdir();"
            + $"a=pathlib.Path({Literal(artifact)});a.parent.mkdir(parents=True,exist_ok=True);"
            + "a.write_text(json.dumps({'schema':1,'exitCode':130}));raise SystemExit(130)");
    }

    private static (string Artifact, string Script) Marker(string gate, int index)
    {
        var artifact = $"artifacts/{gate}-{index}.json";
        return (artifact, "import json,pathlib;"
            + "pathlib.Path('later-marker').write_text('ran');"
            + $"a=pathlib.Path({Literal(artifact)});a.parent.mkdir(parents=True,exist_ok=True);"
            + "a.write_text(json.dumps({'schema':1,'exitCode':0}))");
    }

    private static string Literal(string value) => JsonSerializer.Serialize(value);

    private static string Local(string directory, string relative) =>
        Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string[] Keys(JsonNode node) => [.. node.AsObject().Select(pair => pair.Key).Order()];

    private static string Joined(JsonNode rows, string field) =>
        string.Join(',', rows.AsArray().Select(row => (string?)row![field]));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                || File.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;
        throw new InvalidOperationException("Cannot locate repository root");
    }
}
