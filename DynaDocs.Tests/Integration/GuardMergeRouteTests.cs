namespace DynaDocs.Tests.Integration;

using System.Text.Json;
using System.Text.Json.Nodes;
using DynaDocs.Commands;
using static DynaDocs.Tests.Integration.PushedRepository;

/// <summary>
/// The guard's merge-route hard blocks through the real hook entry point (DYD-425, DR 061 decision
/// 7): raw PR merges, pushes to the default branch and default-branch changes are refused under
/// Claude's Bash and PowerShell tools and Codex's argv arrays, naming dydo merge, while look-alike
/// text and ordinary pushes pass. The project is a git tree on <c>feature</c>, pushed with -u.
/// </summary>
[Collection("Integration")]
public class GuardMergeRouteTests : IntegrationTestBase
{
    private const string Sha40 = "0123456789abcdef0123456789abcdef01234567";

    private const string MergeMutation = "mutation { mergePullRequest(input: {pullRequestId: \"PR_1\"}) { clientMutationId } }";

    // Claude's two shell tools, then Codex's three argv forms: a bash envelope, a pwsh envelope and bare argv.
    public static readonly string[] Envelopes = ["claude-bash", "claude-powershell", "codex-bash", "codex-pwsh", "codex-argv"];

    private string Worktree => Path.Combine(TestDir, "wt");

    private async Task InitPushedProject()
    {
        await InitProjectAsync("none");
        File.AppendAllText(Path.Combine(TestDir, ".gitignore"), "\n/origin.git/\n/wt/\n");
        Create(TestDir, Path.Combine(TestDir, "origin.git"));
    }

    // Words containing a space or quote are single-quoted, which bash and PowerShell both read literally.
    private static string Line(string[] words) =>
        string.Join(' ', words.Select(w => w.Any(c => c is ' ' or '"' or '{' or '}' or '(' or ')') ? $"'{w}'" : w));

    private string Payload(string[] words, string envelope, bool worker = false, string? workdir = null)
    {
        var line = Line(words);
        object command = envelope switch
        {
            "codex-bash" => new[] { "bash", "-lc", line },
            "codex-pwsh" => new[] { "pwsh", "-Command", line },
            "codex-argv" => words,
            _ => line,
        };
        var toolInput = new Dictionary<string, object?> { ["command"] = command };
        if (workdir != null) toolInput["workdir"] = workdir;
        var payload = new Dictionary<string, object?>
        {
            ["session_id"] = TestSessionId,
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = envelope == "claude-powershell" ? "PowerShell" : "Bash",
            ["tool_input"] = toolInput,
            ["cwd"] = TestDir,
        };
        if (envelope.StartsWith("codex", StringComparison.Ordinal)) payload["turn_id"] = "turn-1";
        if (worker)
        {
            payload["agent_id"] = "wkr-merge-route-1";
            payload["agent_type"] = "code-writer";
        }
        return JsonSerializer.Serialize(payload);
    }

    // Runs each command under each envelope and returns every decision that is not the expected
    // one, so a table reports all its misses at once. A refusal must carry every reason part.
    private async Task<List<string>> Misses(bool denied, IEnumerable<string[]> commands, string[]? envelopes = null,
        bool worker = false, string? workdir = null, params string[] reasonParts)
    {
        List<string> misses = [];
        foreach (var words in commands)
        foreach (var envelope in envelopes ?? Envelopes)
        {
            var result = await GuardWithStdinAsync(Payload(words, envelope, worker, workdir));
            var wasDenied = !result.IsPreToolUseAllowed;
            if (wasDenied != denied || (denied && reasonParts.Any(part => !result.Stderr.Contains(part))))
                misses.Add($"{envelope} {(wasDenied ? "denied" : "allowed")}: {Line(words)}\n    {result.Stderr.Trim().Replace("\n", "\n    ")}");
        }
        return misses;
    }

    private static void AssertNoMisses(List<string> misses) =>
        Assert.True(misses.Count == 0, $"{misses.Count} misses:\n{string.Join("\n", misses)}");

    private static string[][] Words(params string[] lines) => [.. lines.Select(l => l.Split(' '))];

    public static readonly string[][] RawMerges =
    [
        ["gh", "pr", "merge", "5"],
        ["gh", "pr", "merge", "5", "--auto", "--merge"],
        ["gh", "pr", "merge", "--squash", "https://github.com/o/r/pull/5"],
        ["gh", "-R", "o/r", "pr", "merge", "5"],
        ["gh", "--repo", "o/r", "pr", "merge", "5"],
        ["gh.exe", "pr", "merge", "5"],
        ["/usr/local/bin/GH", "pr", "merge", "5"],
        ["gh", "api", "-X", "PUT", "repos/o/r/pulls/5/merge"],
        ["gh", "api", "--method", "PUT", "/repos/o/r/pulls/5/merge", "-f", "sha=abc"],
        ["gh", "api", "-X", "PUT", "repos/{owner}/{repo}/pulls/5/merge"],
        ["gh", "api", "-X", "PUT", "repos/o/r/pulls/5/merge-async"],
        ["gh", "api", "repos/o/r/merges", "-f", "base=feature/x", "-f", "head=abc"],
        ["gh", "api", "-X", "PUT", "https://api.github.com/repos/o/r/pulls/5/merge"],
        ["gh", "api", "https://ghe.example.com/api/v3/repos/o/r/merges", "-f", "base=feature/x"],
        ["gh", "api", "graphql", "-f", $"query={MergeMutation}"],
        ["gh", "api", "graphql", "-F", "query=mutation { enablePullRequestAutoMerge(input: {pullRequestId: \"PR_1\"}) { clientMutationId } }"],
        ["gh", "api", "graphql", "--raw-field", "query=mutation { mergeBranch(input: {repositoryId: \"R_1\", base: \"main\", head: \"x\"}) { clientMutationId } }"],
    ];

    [Fact]
    public async Task RawMerge_UnderEveryEnvelope_IsDeniedNamingDydoMergeWithBothPins()
    {
        await InitPushedProject();

        AssertNoMisses(await Misses(denied: true, RawMerges,
            reasonParts: ["dydo merge", "--match-head-commit", "--match-base-commit", "git ls-remote origin refs/heads/<base>", "baseRefOid"]));
    }

    [Fact]
    public async Task PushToADefaultName_UnderEveryEnvelope_IsDeniedAsTheUsers()
    {
        await InitPushedProject();

        AssertNoMisses(await Misses(denied: true, Words(
            "git push origin HEAD:main",
            "git push origin HEAD:master",
            "git push origin feature:refs/heads/main",
            "git push origin main",
            "git push origin refs/heads/master",
            "git push -u origin feature:Main",
            "git push origin feature:MASTER",
            "git push --force-with-lease origin HEAD:main",
            "git push origin feature/x HEAD:main",
            "git -C . push origin HEAD:main",
            "git push origin refs/heads/*:refs/heads/*",
            "git push --all origin"),
            reasonParts: ["dydo merge", "the default branch is the user's"]));
    }

    [Fact]
    public async Task DefaultBranchChange_UnderEveryEnvelope_IsDeniedAsTheUsers()
    {
        await InitPushedProject();

        AssertNoMisses(await Misses(denied: true, Words(
            "gh repo edit --default-branch feature/x",
            "gh repo edit o/r --default-branch=feature/x",
            "gh -R o/r repo edit --default-branch feature/x",
            "gh api -X PATCH repos/o/r -f default_branch=feature/x",
            "gh api --method=PATCH /repos/o/r -F default_branch=feature/x",
            "gh api -XPATCH https://api.github.com/repos/o/r --raw-field default_branch=feature/x"),
            reasonParts: ["dydo merge", "the default branch is the user's"]));
    }

    // The local origin/HEAD names the default branch too, whatever it is called.
    [Fact]
    public async Task PushToTheOriginHeadName_IsDenied()
    {
        await InitPushedProject();
        Git(TestDir, "push", "origin", "feature:trunk");
        Git(TestDir, "symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/trunk");

        AssertNoMisses(await Misses(denied: true, Words("git push origin HEAD:trunk", "git push origin feature:refs/heads/trunk"),
            reasonParts: ["trunk", "the default branch is the user's"]));
    }

    // A bare git push, or one naming only its remote, goes to the current branch's upstream.
    [Fact]
    public async Task BarePush_OnABranchWhoseUpstreamIsMain_IsDenied()
    {
        await InitPushedProject();
        Git(TestDir, "push", "origin", "feature:main");
        Git(TestDir, "checkout", "-b", "work");
        Git(TestDir, "branch", "-u", "origin/main");

        AssertNoMisses(await Misses(denied: true, Words("git push", "git push origin", "git push -u origin"),
            reasonParts: ["main", "dydo merge"]));
    }

    [Fact]
    public async Task PushOfHead_OnMain_IsDenied()
    {
        await InitPushedProject();
        Git(TestDir, "checkout", "-b", "main");

        AssertNoMisses(await Misses(denied: true, Words("git push origin HEAD", "git push origin @", "git push -u origin HEAD"),
            reasonParts: ["main", "dydo merge"]));
    }

    // git -C names the tree whose HEAD is pushed.
    [Fact]
    public async Task PushOfHead_ThroughGitDashCIntoAWorktreeOnMain_IsDenied()
    {
        await InitPushedProject();
        Git(TestDir, "worktree", "add", "-b", "main", Worktree);

        AssertNoMisses(await Misses(denied: true, Words("git -C wt push origin HEAD", $"git -C {Worktree} push"), reasonParts: ["main"]));
        AssertNoMisses(await Misses(denied: false, Words("git push origin HEAD", "git push")));
    }

    // DYD-739: a git -C tree written with a backslash, as a Windows path is. A bash reading drops the
    // backslash and names a tree that does not exist; the tree as written is on main, so every envelope refuses.
    [Fact]
    public async Task PushOfHead_ThroughGitDashCIntoABackslashTreeOnMain_IsDenied()
    {
        await InitPushedProject();
        var tree = Path.Combine(TestDir, @"w\t");
        Git(TestDir, "worktree", "add", "-b", "main", tree);

        AssertNoMisses(await Misses(denied: true, Words(@"git -C w\t push origin HEAD", $"git -C {tree} push"), reasonParts: ["main"]));
    }

    // DYD-739 review: a backslash outside a -C tree stays bash's. An escaped quote is text, and a
    // backslash-newline joins one command, so these bash lines pass, on a branch whose upstream is main.
    // An unmarked Bash call on Windows is read both ways (ExecutingShell), so there only Codex's bash envelope is bash's alone.
    [Fact]
    public async Task BashLine_BackslashInTextOrAContinuation_IsAllowed()
    {
        await InitPushedProject();
        Git(TestDir, "push", "origin", "feature:main");
        Git(TestDir, "checkout", "-b", "work");
        Git(TestDir, "branch", "-u", "origin/main");
        string[] lines =
        [
            "git commit -m \"Document \\\"git fetch && gh pr merge 12\\\" usage\"",
            "git commit -m \"don't \\\"x; gh repo edit --default-branch y\\\"\"",
            "git push \\\n  -u origin work",
            "printf \"example: \\\"; gh pr merge 5; \\\"\\n\"",
            "echo \\\ngh pr merge 5",
        ];
        string[] envelopes = OperatingSystem.IsWindows() ? ["codex-bash"] : ["claude-bash", "codex-bash"];

        List<string> misses = [];
        foreach (var line in lines)
        foreach (var envelope in envelopes)
        {
            var json = JsonNode.Parse(Payload([], envelope))!;
            json["tool_input"]!["command"] = envelope == "codex-bash" ? new JsonArray("bash", "-lc", line) : line;
            var result = await GuardWithStdinAsync(json.ToJsonString());
            if (!result.IsPreToolUseAllowed)
                misses.Add($"{envelope} denied: {line}\n    {result.Stderr.Trim().Replace("\n", "\n    ")}");
        }
        AssertNoMisses(misses);
    }

    // DYD-665: inside a word pwsh 7.6.9 keeps a backtick-escaped carriage return and ends the
    // statement at the line feed after it, so the route on the next line runs: Claude's PowerShell
    // tool, a Codex pwsh envelope and an unknown shell refuse it.
    [Fact]
    public async Task BacktickCrlfInAWord_BeforeARoute_IsDeniedWherePwshRunsIt()
    {
        await InitPushedProject();
        string[] lines = ["x`\r\ngit push origin main", "x`\r\ngh pr merge 5"];

        List<string> misses = [];
        foreach (var line in lines)
        {
            misses.AddRange(await PwshLineMisses(denied: true, line));
            var unknown = await GuardWithStdinAsync("", "--command", line);
            if (unknown.ExitCode != 2 || !unknown.Stderr.Contains("dydo merge"))
                misses.Add($"unknown exit {unknown.ExitCode}: {line}\n    {unknown.Stderr.Trim()}");
        }
        AssertNoMisses(misses);
    }

    // Its nearest honest twin: between words, or after a quoted argument, the backtick continues the
    // line, so pwsh runs one echo whose arguments spell the route, and both pwsh lanes allow it.
    [Fact]
    public async Task BacktickCrlfBetweenWords_BeforeARoute_IsAllowedInPwsh()
    {
        await InitPushedProject();

        AssertNoMisses(await PwshLineMisses(denied: false,
            "echo x `\r\ngit push origin main", "echo 'x'`\r\ngh pr merge 5", "echo \"x\"`\r\ngh pr merge 5"));
    }

    private async Task<List<string>> PwshLineMisses(bool denied, params string[] lines)
    {
        List<string> misses = [];
        foreach (var line in lines)
        foreach (var envelope in new[] { "claude-powershell", "codex-pwsh" })
        {
            var json = JsonNode.Parse(Payload([], envelope))!;
            json["tool_input"]!["command"] = envelope == "codex-pwsh" ? new JsonArray("pwsh", "-Command", line) : line;
            var result = await GuardWithStdinAsync(json.ToJsonString());
            var wasDenied = !result.IsPreToolUseAllowed;
            if (wasDenied != denied || (denied && !result.Stderr.Contains("dydo merge")))
                misses.Add($"{envelope} {(wasDenied ? "denied" : "allowed")}: {line}\n    {result.Stderr.Trim()}");
        }
        return misses;
    }

    // A Codex workdir is the call's directory, as for PR-ready.
    [Fact]
    public async Task PushOfHead_WithACodexWorkdirOnMain_IsDenied()
    {
        await InitPushedProject();
        Git(TestDir, "worktree", "add", "-b", "main", Worktree);

        AssertNoMisses(await Misses(denied: true, Words("git push origin HEAD", "git push"), ["codex-bash", "codex-pwsh", "codex-argv"],
            workdir: Worktree, reasonParts: ["main"]));
    }

    [Fact]
    public async Task RawMerge_FromASubAgent_IsDenied()
    {
        await InitPushedProject();

        AssertNoMisses(await Misses(denied: true, Words("gh pr merge 5", "git push origin HEAD:main", "gh repo edit --default-branch x"),
            ["claude-bash", "claude-powershell"], worker: true, reasonParts: ["dydo merge"]));
    }

    [Fact]
    public async Task WrappedRoute_IsReadAndDenied()
    {
        await InitPushedProject();
        string[][] wrapped =
        [
            ["bash", "-c", "gh pr merge 5"],
            ["sh", "-c", "git status; git push origin HEAD:main"],
            ["eval", "gh pr merge 5 --auto"],
            ["pwsh", "-NoProfile", "-Command", "git push origin HEAD:master"],
            ["cmd", "/c", "gh", "pr", "merge", "5"],
            ["env", "GH_REPO=o/r", "gh", "pr", "merge", "5"],
            ["sudo", "-u", "me", "git", "push", "origin", "HEAD:main"],
        ];

        AssertNoMisses(await Misses(denied: true, wrapped, ["claude-bash", "codex-bash", "codex-argv"], reasonParts: ["dydo merge"]));
    }

    // A command substitution's command runs, so it is read like a direct line.
    [Fact]
    public async Task RouteInACommandSubstitution_IsDenied()
    {
        await InitPushedProject();
        var json = JsonNode.Parse(Payload([], "claude-bash"))!;
        json["tool_input"]!["command"] = "echo \"$(gh pr merge 5)\"";

        var reason = (await GuardWithStdinAsync(json.ToJsonString())).AssertPreToolUseDenied();

        Assert.Contains("dydo merge", reason);
    }

    // An encoded PowerShell command is unreadable; inside a git tree the PR check refuses it unread.
    [Fact]
    public async Task EncodedCommand_InAGitTree_IsDenied()
    {
        await InitPushedProject();

        AssertNoMisses(await Misses(denied: true, Words("pwsh -EncodedCommand ZwBoACAAcAByACAAbQBlAHIAZwBlACAANQA="),
            ["claude-bash", "codex-argv"]));
    }

    [Fact]
    public async Task LookAlikesAndOrdinaryWork_UnderEveryEnvelope_AreAllowed()
    {
        await InitPushedProject();
        string[][] allowed =
        [
            ["gh", "pr", "view", "5"],
            ["gh", "pr", "view", "5", "--json", "mergedAt,baseRefName"],
            ["gh", "pr", "merge", "--help"],
            ["git", "commit", "--allow-empty", "-m", "docs: never gh pr merge 5 or git push origin HEAD:main"],
            ["git", "push", "origin", "feature/x"],
            ["git", "push", "-u", "origin", "DYD-1-x"],
            ["git", "push", "origin", "HEAD:feature/x"],
            ["git", "push", "origin", "HEAD"],
            ["git", "push"],
            ["git", "push", "origin", "--tags"],
            ["gh", "api", "repos/o/r/pulls/5"],
            ["gh", "api", "repos/o/r/pulls/5/files"],
            ["gh", "api", "repos/o/r"],
            ["gh", "api", "graphql", "-f", "query=query { repository(owner: \"o\", name: \"r\") { pullRequest(number: 5) { merged } } }"],
            ["gh", "repo", "edit", "--description", "new"],
            ["gh", "repo", "view", "--json", "defaultBranchRef"],
        ];

        AssertNoMisses(await Misses(denied: false, allowed));
    }

    // Parity: a look-alike naming a merge in another program's argument is data under every envelope,
    // bare argv included, so each one's decision is the same allow across all five (DYD-425 fix).
    [Fact]
    public async Task LookAlikeNamingAMerge_HasTheSameOutcome_UnderEveryEnvelope()
    {
        await InitPushedProject();
        string[][] lookAlikes =
        [
            ["grep", "-rn", "gh pr merge", "."],
            ["grep", "-rn", "gh pr merge", "dydo"],
            ["grep", "-rn", "git push origin HEAD:main", "."],
            ["echo", "gh pr merge 5"],
            ["echo", "git push origin HEAD:main"],
            ["echo", "gh repo edit --default-branch x"],
            ["git", "commit", "-m", "mention gh pr merge"],
            ["git", "commit", "-m", "never git push origin HEAD:main"],
        ];

        AssertNoMisses(await Misses(denied: false, lookAlikes));
    }

    // Bare argv still blocks a real route: gh or git as the program, or a wrapper whose payload is one.
    [Fact]
    public async Task BareArgv_RealRoute_ByProgramOrWrapper_IsDenied()
    {
        await InitPushedProject();
        string[][] routes =
        [
            ["gh", "pr", "merge", "5"],
            ["gh.exe", "pr", "merge", "5"],
            ["gh", "api", "-X", "PUT", "repos/o/r/pulls/5/merge"],
            ["git", "push", "origin", "HEAD:main"],
            ["bash", "-lc", "gh pr merge 5"],
            ["bash", "-c", "git push origin HEAD:main"],
            ["env", "gh", "pr", "merge", "5"],
            ["env", "-S", "gh pr merge 5"],
            ["nohup", "gh", "pr", "merge", "5"],
            ["eval", "gh pr merge 5"],
            ["sh", "-c", "echo hi; gh pr merge 5"],
        ];

        AssertNoMisses(await Misses(denied: true, routes, ["codex-argv"], reasonParts: ["dydo merge"]));
    }

    [Fact]
    public async Task MergeTextInAHeredocBody_IsAllowed()
    {
        await InitPushedProject();
        var payload = Payload([], "claude-bash");
        var heredoc = "cat <<'EOF' > notes.txt\ngh pr merge 5 --auto\ngit push origin HEAD:main\nEOF";
        var json = JsonNode.Parse(payload)!;
        json["tool_input"]!["command"] = heredoc;

        var result = await GuardWithStdinAsync(json.ToJsonString());

        result.AssertPreToolUseAllowed();
    }

    // Unreadable git state never blocks on its own: outside git, only a named default destination does.
    [Fact]
    public async Task PushOutsideGit_BlocksOnlyANamedDefault()
    {
        await InitProjectAsync("none");

        AssertNoMisses(await Misses(denied: false, Words("git push", "git push origin", "git push origin HEAD")));
        AssertNoMisses(await Misses(denied: true, Words("git push origin HEAD:main", "gh pr merge 5"), reasonParts: ["dydo merge"]));
    }

    [Theory]
    [InlineData("claude-bash")]
    [InlineData("claude-powershell")]
    public async Task DydoMerge_InTheWorkerLane_IsAllowed(string envelope)
    {
        await InitPushedProject();

        AssertNoMisses(await Misses(denied: false, [["dydo", "merge", "5", "--match-head-commit", Sha40]], [envelope], worker: true));
    }

    // DYD-386 ordering: a merge-route block refuses every try and never arms a warn's once-only marker.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MergeRoute_WithAMatchingWarn_RefusesEveryTryAndLeavesTheWarnMarker(bool worker)
    {
        await InitPushedProject();
        var config = JsonNode.Parse(ReadFile("dydo.json"))!.AsObject();
        var nudges = config["nudges"] as JsonArray ?? [];
        nudges.Add(new JsonObject { ["pattern"] = ".+", ["message"] = "Warn on every command.", ["severity"] = "warn" });
        config["nudges"] = nudges;
        File.WriteAllText(Path.Combine(TestDir, "dydo.json"), config.ToJsonString());
        const string command = "gh pr merge 5";
        var marker = Path.Combine(GuardCommand.GuardEnv.Load(TestDir).MarkerDir, GuardCommand.CommandWarnMarkerName(".+", command));

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var reason = (await GuardWithStdinAsync(Payload([.. command.Split(' ')], "claude-bash", worker))).AssertPreToolUseDenied();

            Assert.True(reason.Contains("dydo merge") && !reason.Contains("Warn on every command."), $"try {attempt}: {reason}");
            Assert.False(File.Exists(marker), $"try {attempt} armed the warn marker");
        }
    }
}
