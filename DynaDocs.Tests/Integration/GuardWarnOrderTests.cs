namespace DynaDocs.Tests.Integration;

using System.Text.Json;
using System.Text.Json.Nodes;
using DynaDocs.Commands;

/// <summary>
/// DYD-386 through the real hook entry point: a warn nudge runs only after every hard block on the
/// same call has passed, so a hard block refuses every try and its call neither arms nor spends the
/// warn's once-only marker. When nothing hard-blocks, the warn blocks once and passes the rerun.
/// </summary>
[Collection("Integration")]
public sealed class GuardWarnOrderTests : IntegrationTestBase
{
    // Matches every command, so each hard block below meets a warn on the same call.
    private const string WarnEverything = ".+";
    private const string WarnMessage = "Warn on every command.";
    private const string BlockMessage = "Custom block nudge.";
    private static readonly string[] ClaudeHook = ["--host", "claude"];

    private async Task InitWithNudges()
    {
        await InitProjectAsync("none");
        var config = JsonNode.Parse(ReadFile("dydo.json"))!.AsObject();
        var nudges = config["nudges"] as JsonArray ?? [];
        nudges.Add(new JsonObject { ["pattern"] = WarnEverything, ["message"] = WarnMessage, ["severity"] = "warn" });
        nudges.Add(new JsonObject { ["pattern"] = "risky-gamma", ["message"] = BlockMessage, ["severity"] = "block" });
        config["nudges"] = nudges;
        WriteFile("dydo.json", config.ToJsonString());
    }

    private string MarkerPath(string name) => Path.Combine(GuardCommand.GuardEnv.Load(TestDir).MarkerDir, name);

    private string WarnMarker => MarkerPath($".nudge-{GuardCommand.ComputeNudgeHash(WarnEverything)}");

    private void Arm(string marker)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        File.WriteAllText(marker, "armed before the first try");
    }

    private static string Hook(string command, bool worker = false, string permissionMode = "default")
    {
        var payload = new Dictionary<string, object?>
        {
            ["session_id"] = TestSessionId,
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = "Bash",
            ["permission_mode"] = permissionMode,
            ["tool_input"] = new { command },
        };
        if (worker)
        {
            payload["agent_id"] = "wkr-test-1";
            payload["agent_type"] = "reviewer";
        }
        return JsonSerializer.Serialize(payload);
    }

    private static string Decision(CommandResult result)
    {
        if (result.IsPreToolUseAllowed) return "allow";
        using var json = JsonDocument.Parse(result.Stdout.Trim());
        return json.RootElement.GetProperty("hookSpecificOutput").GetProperty("permissionDecision").GetString()!;
    }

    // Each hard block on the shell path, with a warn matching the same command: the hard block wins
    // every try, and the warn marker keeps whatever state it had before the first try.
    [Theory]
    [InlineData("dydo init", true, "BLOCKED: Sub-agents may run only these dydo commands", false)]
    [InlineData("dydo init", true, "BLOCKED: Sub-agents may run only these dydo commands", true)]
    [InlineData("rm -rf /", false, "BLOCKED: Dangerous command pattern detected.", false)]
    [InlineData("rm -rf /", false, "BLOCKED: Dangerous command pattern detected.", true)]
    [InlineData("risky-gamma --now", false, "BLOCKED: " + BlockMessage, false)]
    [InlineData("risky-gamma --now", false, "BLOCKED: " + BlockMessage, true)]
    [InlineData("cd tools && echo hi", false, "BLOCKED: Don't chain cd / Set-Location", false)]
    [InlineData("cd tools && echo hi", false, "BLOCKED: Don't chain cd / Set-Location", true)]
    [InlineData("echo x > $OUT", false, "BLOCKED: Command contains bypass patterns", false)]
    [InlineData("echo x > $OUT", false, "BLOCKED: Command contains bypass patterns", true)]
    [InlineData("rm .claude/agent-memory/reviewer/MEMORY.md", false, "BLOCKED: Agents do not write host memory", false)]
    [InlineData("rm .claude/agent-memory/reviewer/MEMORY.md", false, "BLOCKED: Agents do not write host memory", true)]
    [InlineData("cat .env", false, "BLOCKED: Command references off-limits path.", false)]
    [InlineData("cat .env", false, "BLOCKED: Command references off-limits path.", true)]
    [InlineData("rm dydo.json", false, "BLOCKED: Path is protected", false)]
    [InlineData("rm dydo.json", false, "BLOCKED: Path is protected", true)]
    public async Task HardBlock_WithAMatchingWarn_RefusesEveryTryAndLeavesTheWarnMarker(
        string command, bool worker, string refusal, bool markerBefore)
    {
        await InitWithNudges();
        if (markerBefore) Arm(WarnMarker);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var reason = (await GuardWithStdinAsync(Hook(command, worker))).AssertPreToolUseDenied();

            Assert.True(reason.Contains(refusal) && !reason.Contains(WarnMessage), $"try {attempt}: {reason}");
            Assert.True(File.Exists(WarnMarker) == markerBefore, $"try {attempt} changed the warn marker");
        }
    }

    [Fact]
    public async Task WarnAlone_BlocksOnceThenPassesTheRerun()
    {
        await InitWithNudges();

        var first = (await GuardWithStdinAsync(Hook("echo hi"))).AssertPreToolUseDenied();
        var second = await GuardWithStdinAsync(Hook("echo hi"));

        Assert.Contains("BLOCKED: " + WarnMessage, first);
        second.AssertPreToolUseAllowed();
        Assert.False(File.Exists(WarnMarker));
    }

    // Contract item 3: a host that cannot ask refuses every try, and neither the command warn's
    // marker nor the mode-set reminder's marker moves.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModeSet_OnAHostThatCannotAsk_RefusesEveryTryAndLeavesTheMarkers(bool markersBefore)
    {
        await InitWithNudges();
        var reminderMarker = MarkerPath(".nudge-mode-set");
        if (markersBefore)
        {
            Arm(WarnMarker);
            Arm(reminderMarker);
        }

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var result = await GuardWithStdinAsync(Hook("dydo mode set overnight on", permissionMode: "dontAsk"), ClaudeHook);

            var reason = result.AssertPreToolUseDenied();
            Assert.True(reason.StartsWith("BLOCKED: This host cannot ask"), $"try {attempt}: {reason}");
            Assert.True(File.Exists(WarnMarker) == markersBefore, $"try {attempt} changed the warn marker");
            Assert.True(File.Exists(reminderMarker) == markersBefore, $"try {attempt} changed the reminder marker");
        }
    }

    // Where the host can ask, a warn and the mode-set reminder block together once, and the rerun
    // asks: they never take turns.
    [Fact]
    public async Task ModeSet_WithAWarn_OnAHostThatCanAsk_AsksOnEveryRerun()
    {
        await InitWithNudges();
        var decisions = new List<string>();

        for (var attempt = 1; attempt <= 4; attempt++)
            decisions.Add(Decision(await GuardWithStdinAsync(Hook("dydo mode set overnight on"), ClaudeHook)));

        Assert.Equal(["deny", "ask", "deny", "ask"], decisions);
    }

    [Fact]
    public async Task ModeSet_WithAWarn_OnAHostThatCanAsk_NamesBothOnTheFirstTry()
    {
        await InitWithNudges();

        var reason = (await GuardWithStdinAsync(Hook("dydo mode set overnight on"), ClaudeHook)).AssertPreToolUseDenied();

        Assert.Contains("BLOCKED: " + WarnMessage, reason);
        Assert.Contains("BLOCKED: dydo mode set changes the mode", reason);
    }
}
