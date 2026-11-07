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

    private string WarnMarker(string command) => MarkerPath(GuardCommand.CommandWarnMarkerName(WarnEverything, command));

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
        if (markerBefore) Arm(WarnMarker(command));

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var reason = (await GuardWithStdinAsync(Hook(command, worker))).AssertPreToolUseDenied();

            Assert.True(reason.Contains(refusal) && !reason.Contains(WarnMessage), $"try {attempt}: {reason}");
            Assert.True(File.Exists(WarnMarker(command)) == markerBefore, $"try {attempt} changed the warn marker");
        }
    }

    [Fact]
    public async Task WarnAlone_BlocksOnceThenPassesTheRerun()
    {
        await InitWithNudges();

        var first = (await GuardWithStdinAsync(Hook("echo hi"))).AssertPreToolUseDenied();
        Assert.True(File.Exists(WarnMarker("echo hi")), "the first try arms the command's marker");
        var second = await GuardWithStdinAsync(Hook("echo hi"));

        Assert.Contains("BLOCKED: " + WarnMessage, first);
        second.AssertPreToolUseAllowed();
        Assert.False(File.Exists(WarnMarker("echo hi")));
    }

    // A hard block preserves both command-keyed warnings, armed or unarmed, on every try.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModeSet_WithAHardBlock_NeitherArmsNorSpendsAnyWarn(bool markersBefore)
    {
        await InitWithNudges();
        const string command = "dydo mode set overnight on && cat .env";
        var env = GuardCommand.GuardEnv.Load(TestDir);
        if (markersBefore)
            Assert.Equal(2, GuardCommand.CheckWarnNudges(command, env, modeSet: true));
        Directory.CreateDirectory(env.MarkerDir);
        var before = Directory.GetFiles(env.MarkerDir, ".nudge-*")
            .ToDictionary(path => path, File.ReadAllText);
        Assert.Equal(markersBefore ? 2 : 0, before.Count);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var result = await GuardWithStdinAsync(Hook(command, permissionMode: "dontAsk"), ClaudeHook);
            Assert.Contains("off-limits", result.AssertPreToolUseDenied());
            var after = Directory.GetFiles(env.MarkerDir, ".nudge-*")
                .ToDictionary(path => path, File.ReadAllText);
            Assert.Equal(before.OrderBy(pair => pair.Key), after.OrderBy(pair => pair.Key));
        }
    }

    // The reminder and other warnings block together once, then pass together.
    [Fact]
    public async Task ModeSet_WithAWarn_BlocksAndPassesTogether()
    {
        await InitWithNudges();
        var decisions = new List<string>();

        for (var attempt = 1; attempt <= 4; attempt++)
            decisions.Add(Decision(await GuardWithStdinAsync(Hook("dydo mode set overnight on"), ClaudeHook)));

        Assert.Equal(["deny", "allow", "deny", "allow"], decisions);
    }

    // DYD-503 through the hook: the shipped install warn passes only the same command retried; a
    // different install after a warn is warned on its own.
    [Fact]
    public async Task InstallWarn_ADifferentInstallAfterAWarn_IsWarnedToo_AndEachRetryPasses()
    {
        await InitProjectAsync("none");
        var decisions = new List<string>();

        foreach (var command in new[] { "pnpm add left-pad", "pnpm add other", "pnpm add left-pad", "pnpm add other" })
            decisions.Add(Decision(await GuardWithStdinAsync(Hook(command))));

        Assert.Equal(["deny", "deny", "allow", "allow"], decisions);
    }

    [Fact]
    public async Task ModeSet_WithAWarn_NamesBothOnTheFirstTry()
    {
        await InitWithNudges();

        var reason = (await GuardWithStdinAsync(Hook("dydo mode set overnight on"), ClaudeHook)).AssertPreToolUseDenied();

        Assert.Contains("BLOCKED: " + WarnMessage, reason);
        Assert.Contains("BLOCKED: dydo mode set changes the mode", reason);
    }
}
