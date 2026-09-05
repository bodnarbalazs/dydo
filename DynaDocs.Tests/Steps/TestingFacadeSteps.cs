namespace DynaDocs.Tests.Steps;

using System.Diagnostics;
using System.Text.Json;
using Reqnroll;

[Binding]
[Scope(Feature = "One project-local interface runs tests and assurance honestly")]
public sealed class TestingFacadeSteps(ScenarioContext context)
{
    // Each exact feature step is witnessed by its scenario's named subprocess probe.
    // The cache belongs to this scenario only; no other scenario can inherit its result.
    private string? _observation;

    [Given(@"^a\ configured\ Node\ fixture\ command\ and\ no\ unavailable\ selected\ peer$")]
    [Given(@"^a\ recognized\ operation\ and\ a\ valid\ artifact\ root$")]
    [Given(@"^a\ recognized\ operation\ whose\ selected\ work\ is\ ""a\ test\ or\ policy\ measurement\ failed\ and\ no\ work\ was\ unavailable""$")]
    [Given(@"^a\ recognized\ operation\ whose\ selected\ work\ is\ ""all\ configured\ work\ passed""$")]
    [Given(@"^a\ recognized\ operation\ whose\ selected\ work\ is\ ""an\ interrupted\ adapter\ completed\ its\ cleanup""$")]
    [Given(@"^a\ recognized\ operation\ whose\ selected\ work\ is\ ""invalid,\ missing,\ malformed,\ unsupported\ or\ unavailable""$")]
    [Given(@"^a\ valid\ project\ testing\ manifest$")]
    [Given(@"^configured,\ unavailable\ and\ invalid\ test\ rows\ in\ the\ manifest$")]
    [Given(@"^configured\ and\ unavailable\ capabilities\ in\ the\ manifest$")]
    [Given(@"^configured\ tests\ for\ the\ ""dotnet"",\ ""frontend""\ and\ ""python""\ stacks$")]
    [Given(@"^exactly\ one\ argv\ element\ in\ the\ selected\ mutation\ command\ equals\ ""\{base\}""$")]
    [Given(@"^selected\ independent\ rows\ and\ one\ row\ with\ ""a\ command\ vector\ containing\ an\ angle\ placeholder""$")]
    [Given(@"^selected\ independent\ rows\ and\ one\ row\ with\ ""a\ cwd\ or\ artifact\ path\ escaping\ the\ repository""$")]
    [Given(@"^selected\ independent\ rows\ and\ one\ row\ with\ ""a\ missing\ executable""$")]
    [Given(@"^selected\ independent\ rows\ and\ one\ row\ with\ ""an\ empty\ command\ vector""$")]
    [Given(@"^selected\ independent\ rows\ and\ one\ row\ with\ ""an\ unavailable\ selected\ capability""$")]
    [Given(@"^selected\ independent\ rows\ and\ one\ row\ with\ ""an\ undeclared\ capability""$")]
    [Given(@"^selected\ independent\ rows\ and\ one\ row\ with\ ""malformed\ configured\ evidence""$")]
    [Given(@"^selected\ rows\ that\ pass,\ fail,\ are\ invalid\ and\ are\ unavailable$")]
    [Given(@"^the\ ""dotnet""\ stack\ has\ a\ configured\ test\ command\ and\ configured\ hard\ gates$")]
    [Given(@"^the\ ASP\.NET\ test\ row\ remains\ unavailable\ and\ the\ Python\ test\ row\ remains\ invalid$")]
    [Given(@"^the\ DynaDocs\ dotnet\ test\ row\ runs\ the\ actual\ run_tests\.py\ adapter\ with\ unbuffered\ current\ Python$")]
    [Given(@"^the\ DynaDocs\ project\ testing\ manifest$")]
    [Given(@"^the\ DynaDocs\ runner\ and\ the\ canonical\ portable\ runner$")]
    [Given(@"^the\ canonical\ portable\ testing\ manifest$")]
    [Given(@"^the\ portable\ frontend\ test\ row\ is\ fully\ adapted$")]
    [Given(@"^the\ request\ or\ manifest\ has\ ""a\ missing\ top\-level\ field""$")]
    [Given(@"^the\ request\ or\ manifest\ has\ ""a\ non\-array\ stacks\ field""$")]
    [Given(@"^the\ request\ or\ manifest\ has\ ""a\ schema\ value\ other\ than\ 1""$")]
    [Given(@"^the\ request\ or\ manifest\ has\ ""an\ unknown\ selected\ stack""$")]
    [Given(@"^the\ request\ or\ manifest\ has\ ""duplicate\ stack\ names""$")]
    [Given(@"^the\ request\ or\ manifest\ has\ ""invalid\ JSON""$")]
    [Given(@"^the\ request\ or\ manifest\ has\ ""invalid\ operation\ syntax""$")]
    [Given(@"^the\ request\ or\ manifest\ has\ ""mutation\ without\ \-\-since\ BASE""$")]
    [Given(@"^the\ selected\ configured\ mutation\ command\ has\ ""an\ argv\ element\ containing\ \-\-baseline=\{base\}""$")]
    [Given(@"^the\ selected\ configured\ mutation\ command\ has\ ""no\ argv\ element\ equal\ to\ \{base\}""$")]
    [Given(@"^the\ selected\ configured\ mutation\ command\ has\ ""two\ argv\ elements\ equal\ to\ \{base\}""$")]
    [Given(@"^the\ selected\ stack\ has\ a\ configured\ mutation\ command$")]
    [Given(@"^the\ selected\ stack\ has\ configured\ static,\ coverage\ and\ mutation\ commands$")]
    [Given(@"^unrelated\ rows\ remain\ unavailable\ or\ invalid$")]
    [Given(@"^valid,\ unavailable\ and\ invalid\ test,\ static\ and\ coverage\ rows$")]
    [Then(@"^""BASE""\ replaces\ that\ whole\ element\ as\ one\ unchanged\ argv\ item\ at\ that\ position$")]
    [Then(@"^ASP\.NET\ requires\ worktree\ isolation\ with\ copied\ working\ changes\ but\ its\ adapter\ evidence\ is\ unavailable$")]
    [Then(@"^Node\ conformance\ exercises\ real\ child\ argv,\ standard\ streams\ and\ exit\ propagation\ through\ the\ configured\ Node\ adapter$")]
    [Then(@"^artifacts\ is\ an\ array\ of\ objects\ with\ repository\-relative\ path\ and\ required\ boolean$")]
    [Then(@"^candidate\ is\ commit\ and\ dirty,\ operation\ is\ name\ and\ optional\ since,\ and\ selectedStacks\ preserves\ manifest\ order$")]
    [Then(@"^capabilities\ has\ exactly\ test,\ static,\ coverage\ and\ mutation\ rows$")]
    [Then(@"^command\ kind\ is\ argv\ or\ current\-python\ with\ a\ nonempty\ array\ of\ string\ argv\ items$")]
    [Then(@"^configured\ rows\ require\ command\ and\ artifacts\ and\ forbid\ reason\ while\ unavailable\ rows\ require\ reason,\ forbid\ command\ and\ artifacts,\ and\ may\ carry\ non\-executable\ exampleArgv$")]
    [Then(@"^current\-python\ prefixes\ argv\ with\ sys\.executable\ while\ argv\ commands\ execute\ their\ array\ unchanged$")]
    [Then(@"^cwd,\ adapter\ paths,\ artifactRoot\ and\ declared\ artifact\ paths\ resolve\ inside\ the\ repository\ root\ without\ absolute\ paths\ or\ parent\ escape$")]
    [Then(@"^dotnet\ requires\ worktree\ isolation\ with\ copied\ working\ changes\ and\ names\ run_tests\.py\ as\ verified\ adapter\ evidence$")]
    [Then(@"^each\ invalid\ or\ unavailable\ row\ alone\ is\ skipped\ and\ reported$")]
    [Then(@"^each\ result\ is\ stack,\ capability,\ state,\ argv,\ cwd,\ isolation,\ childExit,\ resultExit,\ artifacts\ and\ optional\ reason$")]
    [Then(@"^each\ result\ records\ stack,\ capability,\ state,\ argv,\ working\ directory,\ isolation\ requirement,\ adapter\ evidence,\ child\ exit,\ artifacts\ and\ reason\ as\ applicable$")]
    [Then(@"^every\ declared\ test,\ static\ and\ coverage\ row\ is\ selected\ in\ manifest\ order$")]
    [Then(@"^every\ declared\ test\ row\ is\ reported\ as\ invalid\ or\ unavailable$")]
    [Then(@"^every\ declared\ test\ row\ is\ selected\ in\ manifest\ order$")]
    [Then(@"^every\ independent\ selected\ result\ is\ reported\ before\ the\ aggregate\ result$")]
    [Then(@"^every\ independent\ valid\ selected\ row\ still\ runs$")]
    [Then(@"^every\ stack\ and\ test,\ static,\ coverage\ and\ mutation\ state\ is\ reported\ with\ its\ reason\ when\ unavailable$")]
    [Then(@"^every\ stack\ has\ exactly\ name,\ kind,\ cwd,\ isolation\ and\ capabilities$")]
    [Then(@"^every\ valid\ configured\ selected\ command\ runs$")]
    [Then(@"^every\ valid\ configured\ test\ command\ runs\ once$")]
    [Then(@"^git\ worktree\ list\ no\ longer\ contains\ the\ temporary\ path$")]
    [Then(@"^help\ explains\ stack\ selection,\ defaults,\ native\ test\ arguments,\ result\ artifacts,\ exit\ 0\ for\ pass,\ 1\ for\ measured\ failure,\ 2\ for\ invalid\ or\ unavailable\ work,\ and\ 130\ for\ interruption\ after\ adapter\ cleanup$")]
    [Then(@"^help\ is\ printed$")]
    [Then(@"^help\ names\ test,\ all,\ gate\ static,\ gate\ coverage,\ gate\ mutation,\ capabilities\ and\ \-\-force\-run$")]
    [Then(@"^independent\ valid\ selected\ commands\ still\ run$")]
    [Then(@"^isolation\ requirement\ is\ in\-place,\ git\-worktree\-copy\-working\-changes\ or\ per\-run\-artifacts$")]
    [Then(@"^it\ allows\ up\ to\ 30\ seconds\ for\ run_tests\.py\ and\ dotnet\ to\ unwind\ through\ the\ wrapper's\ finally\ cleanup\ before\ escalation$")]
    [Then(@"^it\ declares\ ASP\.NET,\ React/Vite\ and\ Python/uv\ stacks$")]
    [Then(@"^it\ records\ schema\ 1,\ candidate\ commit\ and\ dirty\ state,\ operation,\ selected\ stacks,\ ordered\ results\ and\ aggregate\ exit$")]
    [Then(@"^its\ Node\ test\ command\ invokes\ node\ \-\-test\ for\ the\ facade\ conformance\ test$")]
    [Then(@"^its\ Python\ test\ command\ invokes\ unittest\ discovery\ for\ the\ facade\ conformance\ tests$")]
    [Then(@"^its\ artifact\ root\ is\ DynaDocs\.Tests/coverage/results$")]
    [Then(@"^its\ dotnet\ test\ command\ invokes\ DynaDocs\.Tests/coverage/run_tests\.py\ with\ the\ current\ Python\ interpreter$")]
    [Then(@"^its\ prospective\ ASP\.NET\ and\ React/Vite\ argv\ vectors\ show\ dotnet\ test\ and\ Node\ with\ Vitest$")]
    [Then(@"^mutation\ does\ not\ run$")]
    [Then(@"^mutation\ is\ unavailable\ pending\ DYD\-103$")]
    [Then(@"^no\ configured\ command\ runs$")]
    [Then(@"^no\ result\ artifact\ is\ created$")]
    [Then(@"^no\ static,\ coverage\ or\ mutation\ command\ runs$")]
    [Then(@"^non\-test\ configured\ gates\ declare\ at\ least\ one\ required\ artifact\ whose\ normalized\ path\ exists\ after\ a\ successful\ child\ exit$")]
    [Then(@"^one\ JSON\ result\ is\ written\ beneath\ a\ unique\ run\ directory$")]
    [Then(@"^only\ the\ ""coverage""\ command\ runs\ for\ that\ stack$")]
    [Then(@"^only\ the\ ""mutation""\ command\ runs\ for\ that\ stack$")]
    [Then(@"^only\ the\ ""static""\ command\ runs\ for\ that\ stack$")]
    [Then(@"^only\ the\ dotnet\ test\ command\ runs$")]
    [Then(@"^only\ the\ frontend\ test\ command\ runs$")]
    [Then(@"^project\ paths\ and\ the\ frontend\ artifact\ variable\ remain\ visible\ angle\ placeholders$")]
    [Then(@"^schema\ is\ the\ integer\ 1,\ artifactRoot\ is\ repository\-relative,\ and\ stacks\ is\ an\ ordered\ nonempty\ array\ with\ unique\ nonempty\ names$")]
    [Then(@"^standard\ error\ contains\ ""NODE_STDERR_SENTINEL""$")]
    [Then(@"^standard\ output\ contains\ ""NODE_STDOUT_SENTINEL""$")]
    [Then(@"^state\ is\ passed,\ failed,\ unavailable,\ invalid\ or\ interrupted;\ childExit\ is\ the\ raw\ integer\ or\ null;\ resultExit\ is\ 0,\ 1,\ 2\ or\ 130$")]
    [Then(@"^static,\ coverage\ and\ mutation\ are\ unavailable\ with\ adoption\ reasons$")]
    [Then(@"^static,\ coverage\ and\ mutation\ commands\ do\ not\ run$")]
    [Then(@"^static\ and\ coverage\ are\ unavailable\ pending\ DYD\-96$")]
    [Then(@"^that\ mutation\ command\ does\ not\ run$")]
    [Then(@"^the\ ASP\.NET\ and\ Python\ test\ rows\ are\ reported\ without\ running$")]
    [Then(@"^the\ Node\ result\ has\ state\ failed\ and\ resultExit\ 1$")]
    [Then(@"^the\ command\ exits\ 0$")]
    [Then(@"^the\ command\ exits\ 1$")]
    [Then(@"^the\ command\ exits\ 130$")]
    [Then(@"^the\ command\ exits\ 2$")]
    [Then(@"^the\ command\ exits\ 2\ after\ all\ selected\ rows\ are\ reported$")]
    [Then(@"^the\ command\ exits\ 2\ because\ unavailable\ outranks\ a\ measured\ failure$")]
    [Then(@"^the\ command\ runs\ without\ a\ shell$")]
    [Then(@"^the\ command\ succeeds$")]
    [Then(@"^the\ command\ succeeds\ without\ running\ a\ configured\ command$")]
    [Then(@"^the\ defective\ row\ does\ not\ run\ and\ is\ reported\ as\ invalid\ or\ unavailable$")]
    [Then(@"^the\ diagnostic\ identifies\ ""a\ missing\ top\-level\ field""$")]
    [Then(@"^the\ diagnostic\ identifies\ ""a\ non\-array\ stacks\ field""$")]
    [Then(@"^the\ diagnostic\ identifies\ ""a\ schema\ value\ other\ than\ 1""$")]
    [Then(@"^the\ diagnostic\ identifies\ ""an\ unknown\ selected\ stack""$")]
    [Then(@"^the\ diagnostic\ identifies\ ""duplicate\ stack\ names""$")]
    [Then(@"^the\ diagnostic\ identifies\ ""invalid\ JSON""$")]
    [Then(@"^the\ diagnostic\ identifies\ ""invalid\ operation\ syntax""$")]
    [Then(@"^the\ diagnostic\ identifies\ ""mutation\ without\ \-\-since\ BASE""$")]
    [Then(@"^the\ diagnostic\ requires\ \-\-since\ BASE$")]
    [Then(@"^the\ diagnostic\ requires\ exactly\ one\ argv\ element\ equal\ to\ ""\{base\}""\ and\ no\ substring\ occurrence$")]
    [Then(@"^the\ dotnet\ test\ command\ does\ not\ run$")]
    [Then(@"^the\ facade\ exits\ 1$")]
    [Then(@"^the\ facade\ exits\ 130$")]
    [Then(@"^the\ facade\ sends\ the\ platform\-appropriate\ interrupt\ to\ the\ adapter\ process\ group\ without\ immediately\ killing\ the\ wrapper$")]
    [Then(@"^the\ fixture\ child\ exit\ 17\ is\ recorded\ unchanged$")]
    [Then(@"^the\ fixture\ receives\ the\ literal\ argv\ elements\ ""\-\-""\ and\ ""árvíztűrő\ tükörfúrógép""\ in\ order$")]
    [Then(@"^the\ frontend\ and\ Python\ isolation\ requirements\ are\ per\-run\ artifacts\ and\ in\-place$")]
    [Then(@"^the\ frontend\ and\ python\ test\ commands\ each\ run\ once\ in\ manifest\ order$")]
    [Then(@"^the\ frontend\ test\ command\ runs$")]
    [Then(@"^the\ human\-readable\ summary\ prints\ that\ result\ path$")]
    [Then(@"^the\ native\ arguments\ ""\-\-filter\ FullyQualifiedName\~ParserTests""\ are\ appended\ as\ separate\ argv\ items$")]
    [Then(@"^the\ reported\ temporary\ path\ no\ longer\ exists$")]
    [Then(@"^the\ result\ artifact\ has\ aggregateExit\ 1$")]
    [Then(@"^the\ result\ artifact\ has\ aggregateExit\ 130\ and\ is\ printed\ after\ cleanup$")]
    [Then(@"^the\ result\ artifact\ records\ aggregate\ exit\ 0$")]
    [Then(@"^the\ result\ artifact\ records\ aggregate\ exit\ 1$")]
    [Then(@"^the\ result\ artifact\ records\ aggregate\ exit\ 130$")]
    [Then(@"^the\ result\ artifact\ records\ aggregate\ exit\ 2$")]
    [Then(@"^the\ result\ names\ the\ candidate,\ argv,\ working\ directory,\ isolation\ requirement,\ adapter\ evidence,\ artifacts\ and\ capability\ state$")]
    [Then(@"^the\ result\ row\ has\ state\ interrupted,\ raw\ childExit\ or\ null,\ and\ resultExit\ 130$")]
    [Then(@"^the\ result\ shape\ is\ schema,\ candidate,\ operation,\ selectedStacks,\ results\ and\ aggregateExit$")]
    [Then(@"^the\ runner\ does\ not\ add\ another\ \-\-since\ option$")]
    [Then(@"^the\ same\ conformance\ suite\ verifies\ their\ grammar,\ manifest\ validation,\ execution\ order,\ result\ schema\ and\ exit\ aggregation$")]
    [Then(@"^their\ source\ bytes\ are\ identical$")]
    [Then(@"^unavailable\ and\ invalid\ selected\ rows\ are\ reported\ without\ running\ their\ commands$")]
    [Then(@"^unavailable\ and\ invalid\ test\ rows\ are\ reported\ without\ running\ their\ commands$")]
    [Then(@"^unavailable\ isolation\ evidence\ has\ a\ nonempty\ reason\ and\ makes\ that\ stack's\ selected\ row\ unavailable$")]
    [Then(@"^verified\ in\-place\ evidence\ has\ kind\ direct\ while\ every\ other\ verified\ isolation\ requirement\ has\ kind\ adapter\ and\ an\ existing\ repository\-contained\ path$")]
    [When(@"^I\ invoke\ an\ operation\ that\ selects\ them$")]
    [When(@"^I\ invoke\ the\ project\ testing\ runner$")]
    [When(@"^I\ invoke\ the\ project\ testing\ runner\ without\ arguments$")]
    [When(@"^I\ request\ the\ project\ testing\ runner\ help$")]
    [When(@"^I\ run\ ""\-\-force\-run""$")]
    [When(@"^I\ run\ ""all""\ from\ the\ portable\ example$")]
    [When(@"^I\ run\ ""all""\ with\ the\ untouched\ portable\ example$")]
    [When(@"^I\ run\ ""all""\ without\ stack\ selection$")]
    [When(@"^I\ run\ ""all\ \-\-stack\ frontend,python""$")]
    [When(@"^I\ run\ ""capabilities""$")]
    [When(@"^I\ run\ ""gate\ coverage\ \-\-stack\ dotnet""$")]
    [When(@"^I\ run\ ""gate\ mutation\ \-\-since\ BASE\ \-\-stack\ dotnet""$")]
    [When(@"^I\ run\ ""gate\ mutation\ \-\-stack\ dotnet""\ without\ \-\-since$")]
    [When(@"^I\ run\ ""gate\ static\ \-\-stack\ dotnet""$")]
    [When(@"^I\ run\ ""test\ \-\-stack\ dotnet\ \-\-\ \-\-filter\ FullyQualifiedName\~ParserTests""$")]
    [When(@"^I\ run\ ""test\ \-\-stack\ frontend""$")]
    [When(@"^I\ run\ ""test\ \-\-stack\ node\ \-\-\ \-\-\ 'árvíztűrő\ tükörfúrógép'""$")]
    [When(@"^I\ run\ the\ requested\ operation$")]
    [When(@"^the\ facade\ observes\ the\ adapter's\ registered\ temporary\ worktree\ path\ and\ receives\ an\ interrupt$")]
    [When(@"^the\ operation\ completes$")]
    [When(@"^the\ operation\ completes\ with\ pass,\ failure,\ unavailability\ or\ interruption$")]
    public void AssertNamedFacadeContract()
    {
        _observation ??= RunProbe(ProbeName());
        Assert.Equal("passed", _observation);
    }

    [Given(@"^this\ schema\ 1\ manifest\ shape:$")]
    public void AssertConcreteManifestShape(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(1, document.RootElement.GetProperty("schema").GetInt32());
        Assert.Equal("current-python", document.RootElement.GetProperty("stacks")[0]
            .GetProperty("capabilities").GetProperty("test").GetProperty("command").GetProperty("kind").GetString());
        AssertNamedFacadeContract();
    }

    [Then(@"^its\ prospective\ Python/uv\ argv\ vector\ is\ exactly\ these\ items\ in\ order:$")]
    public void AssertPortablePythonArguments(Table table)
    {
        Assert.Equal(new[] { "uv", "run", "--locked", "--extra", "dev", "-m", "pytest" },
            table.Rows.Select(row => row["item"]));
        AssertNamedFacadeContract();
    }

    private string ProbeName() => context.ScenarioInfo.Title switch
    {
        "Help describes the complete stable interface" => "test_help",
        "Bare invocation is useful but is not a test or gate" => "test_bare",
        "Run one stack's selected tests without hidden gates" => "test_targeted",
        "Run all configured tests for selected stacks" => "test_selected_all",
        "Run every configured test by default" => "test_default_all",
        "Mutation requires a comparison base" => "test_global_base",
        "A project adapter places the mutation base in its native argv" => "test_mutation_base",
        "Capabilities report configuration without running it" => "test_capabilities",
        "Full-G compatibility never degrades to tests only" => "test_full_g",
        "Independent work is exhausted before aggregation" => "test_aggregation",
        "A started operation leaves one machine-readable result" => "test_result_artifact",
        "Schema 1 has one small concrete manifest and result shape" => "test_schema",
        "The portable three-stack example is visibly unfinished" => "test_portable",
        "Partial adoption runs valid peers without hiding remaining work" => "test_partial_all",
        "Partial adoption permits a fully adapted targeted test" => "test_partial_targeted",
        "DynaDocs uses its real adapters and admits missing assurance" => "test_active_manifest",
        "Interrupting the real isolated dotnet adapter leaves no worktree behind" => "test_interrupting_the_real_dotnet_adapter_cleans_its_worktree",
        "The project and portable runners cannot drift apart" => "test_identity",
        "Node conformance preserves observable process behavior" => "test_node",
        "Hard-gate operations stay separate" => context.ScenarioInfo.Arguments["capability"]?.ToString() switch
        {
            "static" => "test_gate_static",
            "coverage" => "test_gate_coverage",
            "mutation" => "test_gate_mutation",
            _ => throw new InvalidOperationException("Unmapped facade example"),
        },
        "An ambiguous mutation base contract fails closed" => context.ScenarioInfo.Arguments["problem"]?.ToString() switch
        {
            "no argv element equal to {base}" => "test_mutation_zero",
            "two argv elements equal to {base}" => "test_mutation_two",
            "an argv element containing --baseline={base}" => "test_mutation_substring",
            _ => throw new InvalidOperationException("Unmapped facade example"),
        },
        "Globally invalid input prevents all execution" => context.ScenarioInfo.Arguments["problem"]?.ToString() switch
        {
            "invalid JSON" => "test_global_json",
            "a schema value other than 1" => "test_global_schema",
            "a missing top-level field" => "test_global_field",
            "a non-array stacks field" => "test_global_stacks",
            "duplicate stack names" => "test_global_duplicate",
            "an unknown selected stack" => "test_global_unknown",
            "invalid operation syntax" => "test_global_syntax",
            "mutation without --since BASE" => "test_global_base",
            _ => throw new InvalidOperationException("Unmapped facade example"),
        },
        "Results preserve the meaning of the exit code" => context.ScenarioInfo.Arguments["exit"]?.ToString() switch
        {
            "0" => "test_exit_pass",
            "1" => "test_exit_failure",
            "2" => "test_exit_unavailable",
            "130" => "test_interrupting_the_real_dotnet_adapter_cleans_its_worktree",
            _ => throw new InvalidOperationException("Unmapped facade example"),
        },
        "A row-local defect skips only that row" => context.ScenarioInfo.Arguments["problem"]?.ToString() switch
        {
            "an undeclared capability" => "test_row_capability",
            "an unavailable selected capability" => "test_row_unavailable",
            "an empty command vector" => "test_row_empty",
            "a command vector containing an angle placeholder" => "test_row_placeholder",
            "a missing executable" => "test_row_executable",
            "malformed configured evidence" => "test_row_evidence",
            "a cwd or artifact path escaping the repository" => "test_row_escape",
            _ => throw new InvalidOperationException("Unmapped facade example"),
        },
        _ => throw new InvalidOperationException("Unmapped facade scenario: " + context.ScenarioInfo.Title),
    };

    private static string RunProbe(string probe)
    {
        var interpreter = Environment.GetEnvironmentVariable("PYTHON")
            ?? (OperatingSystem.IsWindows() ? "py" : "python3");
        var start = new ProcessStartInfo(interpreter)
        {
            WorkingDirectory = RepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["PYTHON"] = interpreter;
        start.Environment["PYTHONPATH"] = Path.Combine(start.WorkingDirectory, "DynaDocs.Tests", "coverage", "tests");
        start.ArgumentList.Add("-m");
        start.ArgumentList.Add("unittest");
        start.ArgumentList.Add("test_testing_facade.TestingFacadeTests." + probe);
        start.ArgumentList.Add("test_testing_facade.PortableTestingFacadeTests." + probe);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, probe + Environment.NewLine + output.GetAwaiter().GetResult()
            + error.GetAwaiter().GetResult());
        return "passed";
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;
        }
        throw new InvalidOperationException("Cannot locate the isolated repository root");
    }
}
