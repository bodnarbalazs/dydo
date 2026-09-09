namespace DynaDocs.Tests.Steps;

using System.Text.RegularExpressions;
using Reqnroll;

[Binding]
public sealed class CaptainCapacitySteps
{
    private static readonly string[] AdmiralPrompts =
    [
        "Templates/skill-admiral.template.md",
        "dydo/_system/templates/skill-admiral.template.md",
        ".claude/skills/admiral/SKILL.md",
        ".agents/skills/admiral/SKILL.md"
    ];

    private static readonly string[] CaptainPrompts =
    [
        "Templates/skill-issue-captain.template.md",
        "dydo/_system/templates/skill-issue-captain.template.md",
        ".claude/skills/issue-captain/SKILL.md",
        ".agents/skills/issue-captain/SKILL.md"
    ];

    private static readonly string[] SharedGuides =
    [
        "Templates/working-tree-contract.template.md",
        "dydo/guides/working-tree-contract.md"
    ];

    [Given("a pickable Issue has a captain, an exact worker brief, and a native task-tree budget")]
    [Given("the Issue requires specification, production, hardening, fresh Issue review, and a later Merge Sub-issue")]
    [Given("the native inventory accounts for the configured open spawned-thread budget")]
    [Given("the local configuration records agents.max_concurrent_threads_per_session as 16 with preservation evidence")]
    [Given("a completed researcher disappeared before or with one accepted replacement spawn")]
    [When("the next captain-owned stage is runnable")]
    [When("native capacity cannot hold those agents concurrently")]
    [When("the captain makes one bounded attempt to commission the exact next stage and the host refuses it for capacity")]
    [When("later necessary captain and fresh spec-review spawns are refused at four live inventory entries")]
    public void ContextIsRecorded()
    {
    }

    [Then("the Admiral commissions or resumes the Issue Captain from the record")]
    [Then("the Admiral does not dispatch that saved brief directly")]
    public void AdmiralKeepsCaptainBoundary()
    {
        AssertEvery(AdmiralPrompts,
            "Budget all open spawned threads in the native task tree before commissioning.",
            "Commission or resume the captain from its record.",
            "Never use a saved captain brief to dispatch crew.",
            "On one bounded refusal, preserve the record and avoid blind retries;");
        AssertEvery(SharedGuides,
            "The Admiral budgets all open spawned threads in the native task tree before commissioning or resuming an Issue Captain from its record.");
    }

    [Then("only that captain commissions the stage with its exact scope")]
    [Then("the worker return goes to the captain")]
    public void CaptainOwnsCrewScope() => AssertEvery(CaptainPrompts,
        "You alone commission every specifier, production worker, hardener and reviewer for this Issue; keep each worker's scope exact.",
        "Every worker return comes back to you.");

    [Then("the canonical Admiral and Issue Captain prompts and the shared guide state that a saved brief is a portable handoff and never Admiral-to-crew authority")]
    public void SavedBriefDoesNotTransferAuthority() => AssertEvery(AdmiralPrompts.Concat(CaptainPrompts).Concat(SharedGuides),
        "A saved brief carries scope and resume context, never Admiral-to-crew dispatch authority.");

    [Then("the captain schedules the stages serially within the configured open-thread budget")]
    [Then("completion or interruption is not treated as slot release without native evidence")]
    [Then("no required fresh specifier or reviewer is reused or omitted to fit the budget")]
    public void CaptainBudgetsSerialFreshStages()
    {
        AssertEvery(CaptainPrompts,
            "Budget the whole open-thread tree and run necessary stages serially when capacity requires it, preserving every fresh specifier and fresh reviewer.",
            "Do not treat completion, return or interruption as capacity release without native evidence.");
        AssertEvery(SharedGuides,
            "The captain alone commissions its exact-scope crew, budgets the whole open-thread tree and runs necessary stages serially when capacity requires it, preserving fresh specifier and fresh reviewer obligations.",
            "Completion, return or interruption is not capacity release without native evidence.");
    }

    [Then("the captain preserves the record, candidate, hop SHA, and exact brief")]
    [Then("the captain does not broaden the brief, retry blindly, or ask the Admiral to dispatch the crew")]
    [Then("documented lifecycle handling is used only when its effect is established for this host")]
    [Then("if no documented handling makes the captain-owned stage runnable, the captain releases or returns the concrete limitation for escalation through Admiral to human")]
    public void RefusalPreservesCaptainOwnership()
    {
        AssertEvery(CaptainPrompts,
            "On one bounded capacity refusal, preserve the record, candidate, hop SHA and exact brief.",
            "Do not broaden the brief or retry blindly; use documented lifecycle handling only when its effect is established for this host, then return or release the concrete host limitation through the normal hierarchy.");
        AssertEvery(SharedGuides,
            "On one bounded capacity refusal, the captain preserves the record, candidate, hop SHA and exact brief; it does not broaden the brief or retry blindly.",
            "Use documented lifecycle handling only when its effect is established for this host.",
            "If it cannot make captain-owned work runnable, the captain returns or releases the concrete host limitation through the normal hierarchy; the Admiral never dispatches the saved brief.");
        AssertEvery(AdmiralPrompts,
            "On one bounded refusal, preserve the record and avoid blind retries; escalate the exact host limitation only after documented capacity or lifecycle handling cannot make captain-owned work runnable.");
    }

    [Then("the evidence records that configuration consumption and effectiveness remain unproved in the existing task")]
    [Then("it treats the accepted replacement as possible ordinary reclamation")]
    [Then("it does not claim desktop reload, slot reclamation, backend, version, model, or lifecycle behavior")]
    [Then("it leaves durable project configuration emission to DYD-86 and broader lifecycle claims to DYD-88")]
    public void EvidenceStaysBounded() => AssertEvery(SharedGuides,
        "Configuration and observed native result are separate evidence.",
        "It does not claim configuration consumption, effectiveness, reload, reclamation, backend, version, model or lifecycle behavior without direct host evidence.");

    private static void AssertEvery(IEnumerable<string> paths, params string[] expectations)
    {
        foreach (var path in paths)
        {
            var content = File.ReadAllText(Path.Combine(RepositoryRoot(), path));
            foreach (var expectation in expectations)
                Assert.Contains(Normalize(expectation), Normalize(content), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static string Normalize(string value) => Regex.Replace(value, @"\s+", " ");
}
