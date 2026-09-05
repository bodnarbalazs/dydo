namespace DynaDocs.Tests.Steps;

public class WorkflowRetirementAssertionTests
{
    private const string Claude = "Synced 1 agent(s) to .claude/ (agents + skills): implementer\nSynced 1 skill(s) to .claude/ (skills only): co-thinker\n";
    private const string Codex = "Synced Codex artifacts to .agents/skills and .codex/agents.\n";

    private static string Summary(string integration, string separator) =>
        (integration == "claude" ? Claude : Codex) +
        $"Skipped {(integration == "claude" ? "Codex" : "Claude")} artifacts {separator} not recorded in dydo.json integrations (add it with 'dydo init <integration> --join').\n";

    public static IEnumerable<object[]> AcceptedSummaries()
    {
        foreach (var host in new[] { "claude", "codex" })
        foreach (var separator in new[] { "—", "-" })
        foreach (var ending in new[] { "lf", "crlf", "none" })
            yield return [host, separator, ending];
    }

    [Theory]
    [MemberData(nameof(AcceptedSummaries))]
    public void NativeSummary_AcceptsExactlySupportedRepresentations(string host, string separator, string ending)
    {
        var output = Summary(host, separator);
        if (ending == "crlf") output = output.Replace("\n", "\r\n", StringComparison.Ordinal);
        if (ending == "none") output = output.TrimEnd('\n');
        WorkflowRetirementSteps.AssertNativeSummary(new(0, output, ""), host);
    }

    public static IEnumerable<object[]> RejectedSeparators()
    {
        foreach (var host in new[] { "claude", "codex" })
        foreach (var separator in new[] { "–", "−", ":", "", "--" })
            yield return [host, separator];
    }

    [Theory]
    [MemberData(nameof(RejectedSeparators))]
    public void NativeSummary_RejectsOtherSeparators(string host, string separator) =>
        Assert.IsAssignableFrom<Xunit.Sdk.XunitException>(Record.Exception(() =>
            WorkflowRetirementSteps.AssertNativeSummary(new(0, Summary(host, separator), ""), host)));

    public static IEnumerable<object[]> DamagedSummaries()
    {
        foreach (var host in new[] { "claude", "codex" })
        {
            var valid = Summary(host, "-");
            yield return [host, "nonzero", 23, valid, ""];
            yield return [host, "stderr", 0, valid, "unexpected"];
            yield return [host, "empty", 0, "", ""];
            yield return [host, "prefix", 0, "unexpected" + valid, ""];
            yield return [host, "suffix", 0, valid + "unexpected", ""];
            yield return [host, "extra-line", 0, valid + "\n", ""];
            yield return [host, "workflow", 0, "Synced 0 workflow(s) to .claude/workflows/.\n" + valid, ""];
            yield return [host, "missing-summary", 0, valid[(valid.IndexOf('\n') + 1)..], ""];
            yield return [host, "missing-skip", 0, host == "claude" ? Claude : Codex, ""];
            yield return [host, "wrong-order", 0, string.Join('\n', valid.TrimEnd('\n').Split('\n').Reverse()), ""];
            yield return [host, "missing-text", 0, valid.Replace("not recorded ", "", StringComparison.Ordinal), ""];
            yield return [host, "double-space", 0, valid.Replace("artifacts -", "artifacts  -", StringComparison.Ordinal), ""];
            yield return [host, "tab", 0, valid.Replace("artifacts -", "artifacts\t-", StringComparison.Ordinal), ""];
            yield return [host, "placeholder", 0, valid.Replace("<integration>", "codex", StringComparison.Ordinal), ""];
            yield return [host, "leading-space", 0, " " + valid, ""];
            yield return [host, "trailing-space", 0, valid.TrimEnd('\n') + " \n", ""];
        }
        foreach (var count in new[] { "0", "-1", "01" })
            yield return ["claude", "agent-count-" + count, 0, Summary("claude", "-").Replace("1 agent", count + " agent", StringComparison.Ordinal), ""];
        yield return ["claude", "empty-agent-names", 0, Summary("claude", "-").Replace(": implementer", ": ", StringComparison.Ordinal), ""];
        yield return ["claude", "zero-skills", 0, Summary("claude", "-").Replace("1 skill", "0 skill", StringComparison.Ordinal), ""];
        yield return ["claude", "empty-skill-names", 0, Summary("claude", "-").Replace(": co-thinker", ": ", StringComparison.Ordinal), ""];
    }

    [Theory]
    [MemberData(nameof(DamagedSummaries))]
    public void NativeSummary_RejectsDamagedResult(string host, string damage, int exit, string stdout, string stderr)
    {
        var error = Record.Exception(() => WorkflowRetirementSteps.AssertNativeSummary(new(exit, stdout, stderr), host));
        Assert.True(error is Xunit.Sdk.XunitException, damage);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("all")]
    public void NativeSummary_AcceptsBothHostSelection(string host) =>
        WorkflowRetirementSteps.AssertNativeSummary(new(0, Claude + Codex, ""), host);

    [Theory]
    [InlineData("custom.js", false)]
    [InlineData("custom.js", true)]
    [InlineData("inquisition.js.bak", false)]
    [InlineData("inquisition.js.bak", true)]
    [InlineData("run-sprint.js.bak", false)]
    [InlineData("run-sprint.js.bak", true)]
    [InlineData("nested/inquisition.js", false)]
    [InlineData("nested/inquisition.js", true)]
    public void CustomFilesPreserved_RejectsDeletionOrByteChange(string relative, bool delete)
    {
        var scenario = new CliScenario();
        try
        {
            var steps = new WorkflowRetirementSteps(scenario);
            steps.MigrationFiles();
            var directory = Path.Combine(scenario.DirectoryPath, ".claude", "workflows");
            File.Delete(Path.Combine(directory, "run-sprint.js"));
            File.Delete(Path.Combine(directory, "inquisition.js"));
            steps.CustomFilesPreserved();
            var path = Path.Combine(directory, relative);
            if (delete) File.Delete(path);
            else File.WriteAllBytes(path, [0]);
            Assert.IsAssignableFrom<Xunit.Sdk.XunitException>(Record.Exception(steps.CustomFilesPreserved));
        }
        finally { scenario.Cleanup(); }
    }

    [Theory]
    [InlineData(".claude/agents/implementer.md")]
    [InlineData(".claude/skills/implementer/SKILL.md")]
    [InlineData(".claude/skills/co-thinker/SKILL.md")]
    [InlineData(".codex/agents/implementer.toml")]
    [InlineData(".agents/skills/implementer/SKILL.md")]
    [InlineData(".agents/skills/co-thinker/SKILL.md")]
    public async Task NativeArtifacts_RejectsEmptyAndMissingFiles(string relative)
    {
        var scenario = new CliScenario();
        try
        {
            await scenario.RunAsync("sync");
            scenario.Result.AssertSuccess();
            WorkflowRetirementSteps.AssertNativeArtifacts(scenario.DirectoryPath, "all");
            var path = Path.Combine(scenario.DirectoryPath, relative);
            File.WriteAllBytes(path, []);
            Assert.IsAssignableFrom<Xunit.Sdk.XunitException>(Record.Exception(() => WorkflowRetirementSteps.AssertNativeArtifacts(scenario.DirectoryPath, "all")));
            File.Delete(path);
            Assert.IsAssignableFrom<Xunit.Sdk.XunitException>(Record.Exception(() => WorkflowRetirementSteps.AssertNativeArtifacts(scenario.DirectoryPath, "all")));
        }
        finally { scenario.Cleanup(); }
    }
}
