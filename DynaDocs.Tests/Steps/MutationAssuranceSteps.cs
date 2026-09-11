namespace DynaDocs.Tests.Steps;

using System.Diagnostics;
using Reqnroll;

[Binding]
[Scope(Feature = "Mutation assurance runs real campaigns over isolated changed code")]
public sealed class MutationAssuranceSteps(ScenarioContext context)
{
    // Each scenario title, and each outline argument row, is witnessed by exactly one named
    // probe. The observation belongs to this scenario alone; no other scenario inherits it.
    private string? _observation;

    [Given(@"^a\ candidate\ with\ an\ uncommitted\ modification\ and\ an\ untracked\ new\ executable\ target$")]
    [Given(@"^a\ candidate\ with\ one\ changed\ executable\ ""[^""]+""\ target\ since\ BASE$")]
    [Given(@"^a\ foreign\ invocation\ holds\ the\ python\ summary\ lock\ beside\ a\ published\ python\ summary$")]
    [Given(@"^a\ manifest\ whose\ three\ mutation\ rows\ are\ configured\ with\ the\ real\ adapter$")]
    [Given(@"^a\ python\ candidate\ whose\ only\ change\ since\ BASE\ is\ a\ test\ file\ listed\ by\ two\ targets$")]
    [Given(@"^a\ ""[^""]+""\ campaign\ whose\ engine\ is\ long\-lived$")]
    [Given(@"^a\ ""[^""]+""\ candidate\ whose\ change\ since\ BASE\ is\ ""[^""]+""$")]
    [Given(@"^the\ campaign\ has\ ""[^""]+""$")]
    [Given(@"^the\ DynaDocs\ project\ testing\ manifest$")]
    [Given(@"^the\ only\ change\ since\ BASE\ is\ a\ Markdown\ file$")]
    [Given(@"^the\ Stryker\.NET\ report\ lists\ every\ mutant\ of\ an\ unselected\ file\ as\ Ignored\ with\ reason\ ""Removed\ by\ mutate\ filter""$")]
    [Given(@"^the\ ""[^""]+""\ engine\ reports\ every\ valid\ generated\ mutant\ as\ killed$")]
    [Given(@"^the\ ""[^""]+""\ engine\ reports\ one\ mutant\ with\ native\ status\ ""[^""]+""$")]
    [Given(@"^the\ ""[^""]+""\ template\ under\ DynaDocs\.Tests/coverage/mutation$")]
    [When(@"^I\ run\ ""\-\-force\-run""$")]
    [When(@"^I\ run\ ""capabilities""$")]
    [When(@"^I\ run\ ""gate\ mutation\ \-\-since\ BASE""$")]
    [When(@"^I\ run\ ""gate\ mutation\ \-\-since\ BASE\ \-\-stack\ [^""]+""$")]
    [When(@"^the\ facade\ receives\ an\ interrupt\ during\ that\ campaign$")]
    [Then(@"^a\ template\ with\ any\ other\ value\ for\ it\ makes\ the\ campaign\ invalid$")]
    [Then(@"^dotnet,\ python\ and\ node\ each\ report\ mutation\ configured$")]
    [Then(@"^each\ summary\ records\ selection\ mode\ none,\ an\ empty\ changedTargets,\ zero\ counts\ and\ a\ witness\ naming\ the\ Markdown\ path\ as\ no\ obligation$")]
    [Then(@"^every\ stack\ row\ has\ state\ passed\ and\ resultExit\ 0$")]
    [Then(@"^every\ test,\ static\ and\ coverage\ row\ is\ selected\ in\ manifest\ order$")]
    [Then(@"^its\ only\ required\ artifact\ is\ ""[^""]+""$")]
    [Then(@"^its\ ""[^""]+""\ is\ ""[^""]+""$")]
    [Then(@"^measurementComplete\ is\ false\ and\ the\ command\ exits\ 2$")]
    [Then(@"^measurementComplete\ is\ true$")]
    [Then(@"^measurementComplete\ is\ true\ and\ the\ command\ exits\ 1$")]
    [Then(@"^no\ configured\ command\ runs$")]
    [Then(@"^no\ engine\ starts$")]
    [Then(@"^no\ mutation\ adapter\ starts\ and\ no\ mutation\ summary\ is\ written$")]
    [Then(@"^the\ caller's\ tree\ is\ byte\-identical\ after\ the\ run$")]
    [Then(@"^the\ command\ succeeds$")]
    [Then(@"^the\ engine\ process\ tree\ is\ gone\ before\ the\ row\ is\ reported$")]
    [Then(@"^the\ engine\ receives\ every\ maintained\ ""[^""]+""\ target\ and\ nothing\ narrower$")]
    [Then(@"^the\ engine\ receives\ exactly\ those\ two\ files$")]
    [Then(@"^the\ foreign\ lock\ and\ the\ foreign\ summary\ are\ byte\-identical\ after\ the\ run$")]
    [Then(@"^the\ inventory\ and\ the\ campaign\ observe\ both\ files\ inside\ a\ snapshot\ outside\ the\ caller's\ tree$")]
    [Then(@"^the\ run\ report\ and\ the\ published\ summary\ both\ record\ exitCode\ 130,\ one\ gap\ interrupted,\ no\ finding\ and\ measurementComplete\ false$")]
    [Then(@"^the\ run\ report\ names\ ""mutation\ slot\ busy""\ with\ the\ foreign\ lock\ path$")]
    [Then(@"^the\ snapshot\ is\ removed\ and\ unregistered\ and\ the\ summary\ lock\ is\ released$")]
    [Then(@"^the\ snapshot\ path\ no\ longer\ exists\ and\ git\ worktree\ list\ no\ longer\ registers\ it$")]
    [Then(@"^the\ summary\ counts\ exclude\ those\ mutants\ and\ the\ witness\ names\ the\ unselected\ file$")]
    [Then(@"^the\ summary\ counts\ killed\ equals\ valid,\ score\ is\ 100\ and\ measurementComplete\ is\ true$")]
    [Then(@"^the\ summary\ gap\ names\ ""[^""]*""$")]
    [Then(@"^the\ summary\ has\ exactly\ one\ finding\ with\ status\ ""[^""]+""\ naming\ the\ path,\ span,\ mutator\ and\ raw\ report\ reference$")]
    [Then(@"^the\ summary\ has\ no\ finding\ and\ no\ gap$")]
    [Then(@"^the\ summary\ records\ gate\ mutation,\ the\ candidate\ commit,\ dirty\ state\ and\ source\ fingerprint,\ the\ inventory\ path\ and\ hash,\ tool\ versions,\ ordered\ commands\ with\ raw\ exits,\ and\ raw\ report\ paths\ with\ hashes$")]
    [Then(@"^the\ summary\ selection\ mode\ is\ changed\ and\ changedTargets\ are\ exactly\ those\ two\ targets$")]
    [Then(@"^the\ summary\ selection\ mode\ is\ changed\ and\ names\ the\ changed\ target$")]
    [Then(@"^the\ summary\ selection\ mode\ is\ widened\ with\ reason\ ""[^""]+""$")]
    [Then(@"^the\ ""[^""]+""\ mutation\ command\ is\ current\-python\ with\ argv\ DynaDocs\.Tests/coverage/mutation_adapter\.py\ \-\-stack\ \S+\ \-\-since\ \{base\}$")]
    [Then(@"^the\ ""[^""]+""\ row\ has\ state\ failed,\ childExit\ 1\ and\ resultExit\ 1$")]
    [Then(@"^the\ ""[^""]+""\ row\ has\ state\ interrupted\ and\ resultExit\ 130\ and\ the\ command\ exits\ 130$")]
    [Then(@"^the\ ""[^""]+""\ row\ has\ state\ invalid,\ childExit\ 2\ and\ resultExit\ 2$")]
    [Then(@"^the\ ""[^""]+""\ row\ has\ state\ passed,\ childExit\ 0\ and\ resultExit\ 0$")]
    public void AssertNamedMutationContract()
    {
        _observation ??= RunProbe(ProbeName());
        Assert.Equal("passed", _observation);
    }

    private string Argument(string name) => context.ScenarioInfo.Arguments[name]?.ToString()
        ?? throw new InvalidOperationException("Unmapped mutation example: " + name);

    private string ProbeName() => context.ScenarioInfo.Title switch
    {
        "Capabilities report the configured mutation rows without running them" =>
            "test_capabilities_report_the_configured_mutation_rows",
        "Each stack's mutation row names the exclusive adapter and its base placeholder" =>
            "test_each_stack_mutation_row_names_the_adapter_and_base_placeholder",
        "Full-G compatibility still never runs mutation" =>
            "test_full_g_compatibility_still_never_runs_mutation",
        "A campaign whose valid mutants are all killed passes" => Argument("stack") switch
        {
            "dotnet" => "test_an_all_killed_dotnet_campaign_passes",
            "python" => "test_an_all_killed_python_campaign_passes",
            "node" => "test_an_all_killed_node_campaign_passes",
            _ => throw new InvalidOperationException("Unmapped mutation example"),
        },
        "Mutants Stryker.NET removed by its mutate filter are witnessed, not counted" =>
            "test_mutate_filter_removals_are_witnessed_not_counted",
        "A mutant that is not killed is a measured finding" =>
            (Argument("stack"), Argument("native")) switch
            {
                ("dotnet", "Survived") => "test_a_survived_dotnet_mutant_is_a_finding",
                ("dotnet", "NoCoverage") => "test_an_uncovered_dotnet_mutant_is_a_finding",
                ("dotnet", "Timeout") => "test_a_timed_out_dotnet_mutant_is_a_finding",
                ("dotnet", "RuntimeError") => "test_a_dotnet_runtime_error_mutant_is_a_finding",
                ("dotnet", "Ignored") => "test_an_ignored_dotnet_mutant_in_a_selected_file_is_a_finding",
                ("dotnet", "Pending") => "test_a_pending_dotnet_mutant_is_a_finding",
                ("node", "Survived") => "test_a_survived_node_mutant_is_a_finding",
                ("node", "Timeout") => "test_a_timed_out_node_mutant_is_a_finding",
                ("node", "RuntimeError") => "test_a_node_runtime_error_mutant_is_a_finding",
                ("python", "survived") => "test_a_survived_python_mutant_is_a_finding",
                ("python", "killed after the engine timeout") =>
                    "test_a_python_mutant_killed_after_the_engine_timeout_is_a_finding",
                ("python", "killed with no completion marker") =>
                    "test_a_python_kill_with_no_completion_marker_is_a_finding",
                ("python", "killed whose marker reads exit=0") =>
                    "test_a_python_kill_whose_marker_reads_exit_zero_is_a_finding",
                ("python", "killed whose marker is another campaign's") =>
                    "test_a_python_kill_whose_marker_is_another_campaigns_is_a_finding",
                ("python", "skipped") => "test_a_skipped_python_mutant_is_an_unrun_finding",
                ("python", "no-test") => "test_a_python_mutant_with_no_test_is_an_unrun_finding",
                _ => throw new InvalidOperationException("Unmapped mutation example"),
            },
        "Missing or invalid measurement is invalid, never a pass" =>
            (Argument("stack"), Argument("problem")) switch
            {
                ("dotnet", "the engine is not restored") =>
                    "test_an_unrestored_dotnet_engine_is_invalid",
                ("node", "the engine is not restored") =>
                    "test_an_unrestored_node_engine_is_invalid",
                ("python", "the engine is not restored") =>
                    "test_an_unrestored_python_engine_is_invalid",
                ("python", "a failing baseline test run") =>
                    "test_a_failing_python_baseline_is_invalid",
                ("node", "a failing baseline test run") =>
                    "test_a_failing_node_baseline_is_invalid",
                ("dotnet", "a nonzero engine exit without a report") =>
                    "test_a_nonzero_dotnet_exit_without_a_report_is_invalid",
                ("dotnet", "a malformed report") => "test_a_malformed_dotnet_report_is_invalid",
                ("node", "a report that omits the selected file") =>
                    "test_a_node_report_that_omits_the_selected_file_is_invalid",
                ("dotnet", "a report with a non-Ignored mutant in an unselected file") =>
                    "test_a_non_ignored_mutant_in_an_unselected_dotnet_file_is_invalid",
                ("node", "a report with an unselected file") =>
                    "test_an_unselected_file_in_a_node_report_is_invalid",
                ("python", "a session whose work item has no result") =>
                    "test_a_python_session_whose_work_item_has_no_result_is_invalid",
                ("python", "a worker outcome of exception") =>
                    "test_a_python_worker_outcome_of_exception_is_invalid",
                ("python", "a test outcome of incompetent") =>
                    "test_a_python_test_outcome_of_incompetent_is_invalid",
                ("dotnet", "a source file changed by the engine and not restored") =>
                    "test_a_source_the_engine_changed_and_did_not_restore_is_invalid",
                ("node", "zero generated mutants") =>
                    "test_a_node_campaign_with_zero_generated_mutants_is_invalid",
                ("dotnet", "every generated mutant a compile error") =>
                    "test_a_dotnet_campaign_of_only_compile_errors_is_invalid",
                ("dotnet", "a base that no commit resolves") =>
                    "test_a_base_no_commit_resolves_is_invalid",
                ("node", "a base that is not an ancestor of the candidate") =>
                    "test_a_base_that_is_not_an_ancestor_is_invalid",
                ("dotnet", "an inventory with nonempty errors") =>
                    "test_an_inventory_with_nonempty_errors_is_invalid",
                ("dotnet", "a changed C# target outside DynaDocs.csproj") =>
                    "test_a_changed_c_sharp_target_outside_the_project_is_invalid",
                ("node", "a changed JavaScript target without an extension") =>
                    "test_a_changed_extensionless_javascript_target_is_invalid",
                ("dotnet", "a template whose concurrency is 2") =>
                    "test_a_template_whose_concurrency_departs_is_invalid",
                _ => throw new InvalidOperationException("Unmapped mutation example"),
            },
        "A busy mutation slot is refused without touching its owner" =>
            "test_a_busy_mutation_slot_is_refused_without_touching_its_owner",
        "A change touching no maintained source passes as a witnessed no-op" =>
            "test_a_change_touching_no_maintained_source_passes_as_a_witnessed_no_op",
        "Uncertain selection widens to the whole stack" =>
            (Argument("stack"), Argument("change")) switch
            {
                ("dotnet", "a renamed executable target") =>
                    "test_a_renamed_dotnet_target_widens_the_stack",
                ("python", "a deleted executable target") =>
                    "test_a_deleted_python_target_widens_the_stack",
                ("node", "a test file that no target lists") =>
                    "test_a_node_test_no_target_lists_widens_the_stack",
                ("dotnet", "the .config/dotnet-tools.json manifest") =>
                    "test_a_changed_tool_manifest_widens_the_dotnet_stack",
                _ => throw new InvalidOperationException("Unmapped mutation example"),
            },
        "A test-only change reruns exactly the targets that list it" =>
            "test_a_test_only_change_reruns_exactly_the_targets_that_list_it",
        "Dirty and untracked content is measured without touching the caller's tree" =>
            "test_dirty_and_untracked_content_is_measured_without_touching_the_callers_tree",
        "Interruption completes owned cleanup before 130" => Argument("stack") switch
        {
            "dotnet" => "test_interrupting_a_dotnet_campaign_completes_its_cleanup_first",
            "python" => "test_interrupting_a_python_campaign_completes_its_cleanup_first",
            "node" => "test_interrupting_a_node_campaign_completes_its_cleanup_first",
            _ => throw new InvalidOperationException("Unmapped mutation example"),
        },
        "Campaign settings are pinned in the templates" =>
            (Argument("template"), Argument("setting")) switch
            {
                ("stryker-net.json", "concurrency") => "test_the_dotnet_template_pins_concurrency",
                ("stryker-net.json", "thresholds") =>
                    "test_the_dotnet_template_pins_its_thresholds",
                ("stryker-net.json", "reporters") => "test_the_dotnet_template_pins_its_reporters",
                ("stryker-js.json", "coverageAnalysis") =>
                    "test_the_node_template_pins_coverage_analysis_off",
                ("stryker-js.json", "concurrency") => "test_the_node_template_pins_concurrency",
                ("stryker-js.json", "inPlace") => "test_the_node_template_pins_in_place",
                ("cosmic-ray.toml", "distributor") =>
                    "test_the_python_template_pins_the_local_distributor",
                _ => throw new InvalidOperationException("Unmapped mutation example"),
            },
        _ => throw new InvalidOperationException(
            "Unmapped mutation scenario: " + context.ScenarioInfo.Title),
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
        foreach (var argument in new[]
                 {
                     "-m", "unittest", "discover", "-s", "DynaDocs.Tests/coverage/tests",
                     "-p", "test_mutation_facade.py", "-k", probe,
                 })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var transcript = probe + Environment.NewLine + output.GetAwaiter().GetResult()
            + error.GetAwaiter().GetResult();
        Assert.True(process.ExitCode == 0, transcript);
        // -k selects nothing silently, and a skipped probe witnesses nothing, so neither can
        // stand in for a scenario that passed. The count comes from unittest's own summary
        // line, because a probe name may spell the word itself.
        Assert.True(transcript.Contains("Ran 1 test", StringComparison.Ordinal), transcript);
        Assert.False(transcript.Contains("skipped=", StringComparison.Ordinal), transcript);
        return "passed";
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Cannot locate the repository root");
    }
}
