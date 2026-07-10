namespace DynaDocs.Tests.Commands;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Behavioural tests for the host-agnostic <c>dydo read</c> verb (0254 lead fix). The verb PRINTS
/// a target's content and registers the read in one code path — display-equals-ack. These tests
/// pin the two acking paths (inbox message id, file path → must-read), the no-blind-ack invariant
/// (content is always emitted when a read is registered), the unknown-target error, and the
/// claimed-identity requirement.
/// </summary>
[Collection("Integration")]
public class ReadCommandTests : IDisposable
{
    private const string TestSessionId = "read-cmd-test-session";
    private const string AgentName = "Brian";

    private readonly string _testDir;
    private readonly string _dydoDir;
    private readonly string _agentsDir;
    private readonly string _workspace;
    private readonly string _originalDir;
    private readonly string? _originalAgentEnv;

    public ReadCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-read-" + Guid.NewGuid().ToString("N")[..8]);
        _dydoDir = Path.Combine(_testDir, "dydo");
        _agentsDir = Path.Combine(_dydoDir, "agents");
        _workspace = Path.Combine(_agentsDir, AgentName);
        Directory.CreateDirectory(Path.Combine(_workspace, "inbox"));

        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {
                "version": 1,
                "structure": { "root": "dydo" },
                "agents": {
                    "pool": ["Adele", "Brian"],
                    "assignments": { "testuser": ["Adele", "Brian"] }
                }
            }
            """);

        _originalDir = Environment.CurrentDirectory;
        _originalAgentEnv = Environment.GetEnvironmentVariable("DYDO_AGENT");
        Environment.SetEnvironmentVariable("DYDO_AGENT", null);
        Environment.CurrentDirectory = _testDir;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDir;
        Environment.SetEnvironmentVariable("DYDO_AGENT", _originalAgentEnv);

        if (Directory.Exists(_testDir))
        {
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    Directory.Delete(_testDir, true);
                    return;
                }
                catch (IOException) when (i < 2)
                {
                    Thread.Sleep(50 * (i + 1));
                }
            }
        }
    }

    [Fact]
    public void Read_InboxMessageId_PrintsMessageAndMarksRead()
    {
        SeedAgent(unreadMustReads: [], unreadMessages: ["aaaa1111"]);
        WriteInboxMessage("aaaa1111", from: "Adele", subject: "greeting", body: "Hello Brian, please review.");

        var (code, stdout, _) = RunRead("aaaa1111");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("greeting", stdout);
        Assert.Contains("Hello Brian", stdout);

        var after = new AgentRegistry(_testDir).GetAgentState(AgentName)!;
        Assert.Empty(after.UnreadMessages);
    }

    [Fact]
    public void Read_FilePath_PrintsContentAndCompletesMustRead()
    {
        var mustRead = Path.Combine(_dydoDir, "understand", "about.md");
        Directory.CreateDirectory(Path.GetDirectoryName(mustRead)!);
        File.WriteAllText(mustRead, "# About\nUNIQUE_CONTENT_MARKER");
        SeedAgent(unreadMustReads: ["dydo/understand/about.md"], unreadMessages: []);

        var (code, stdout, _) = RunRead("dydo/understand/about.md");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("UNIQUE_CONTENT_MARKER", stdout);

        var after = new AgentRegistry(_testDir).GetAgentState(AgentName)!;
        Assert.Empty(after.UnreadMustReads);
    }

    [Fact]
    public void Read_RegistersOnlyAfterEmittingContent_DisplayEqualsAck()
    {
        // A body well over the 200-char inbox-show preview cutoff: display-equals-ack means the
        // FULL body must be emitted, not the truncated preview, before the read is registered.
        var longBody = "START_MARKER " + new string('x', 250) + " END_MARKER";
        Assert.True(longBody.Length > 200);

        SeedAgent(unreadMustReads: [], unreadMessages: ["aaaa1111"]);
        WriteInboxMessage("aaaa1111", from: "Adele", subject: "subj", body: longBody);

        var (code, stdout, _) = RunRead("aaaa1111");

        Assert.Equal(ExitCodes.Success, code);
        // Display-equals-ack: the same call that registered the read also emitted the content.
        Assert.False(string.IsNullOrWhiteSpace(stdout));
        // The entire body — including content past the 200-char preview cutoff — must be printed,
        // and the preview truncation ellipsis must not appear.
        Assert.Contains(longBody, stdout);
        Assert.Contains("END_MARKER", stdout);
        Assert.DoesNotContain("...", stdout);

        var after = new AgentRegistry(_testDir).GetAgentState(AgentName)!;
        Assert.Empty(after.UnreadMessages);
    }

    [Fact]
    public void Read_UnknownTarget_ErrorsAndMarksNothing()
    {
        WriteInboxMessage("aaaa1111", from: "Adele", subject: "greeting", body: "unrelated");
        SeedAgent(unreadMustReads: ["dydo/understand/about.md"], unreadMessages: ["aaaa1111"]);

        var (code, _, stderr) = RunRead("zzz-nonexistent");

        Assert.Equal(ExitCodes.ToolError, code);
        Assert.Contains("inbox", stderr);
        Assert.Contains("file", stderr);

        var after = new AgentRegistry(_testDir).GetAgentState(AgentName)!;
        Assert.Contains("aaaa1111", after.UnreadMessages);
        Assert.Contains("dydo/understand/about.md", after.UnreadMustReads);
    }

    [Fact]
    public void Read_NoClaimedIdentity_ErrorsWithoutMarking()
    {
        WriteInboxMessage("aaaa1111", from: "Adele", subject: "greeting", body: "Hello.");
        // No SeedAgent — no claimed identity for this process.

        var (code, stdout, stderr) = RunRead("aaaa1111");

        Assert.Equal(ExitCodes.ToolError, code);
        Assert.Contains("identity", stderr);
        Assert.DoesNotContain("Hello.", stdout);
    }

    [Fact]
    public void Read_OffLimitsFilePath_BlockedWithoutContentOrRead()
    {
        // The verb must honour the universal off-limits tier the guard applies to Read-tool and
        // shell reads — an off-limits file is refused with the same BLOCKED shape, no content, no
        // read registered, non-zero exit.
        WriteOffLimits("**/*.secret");
        var secret = Path.Combine(_testDir, "config", "prod.secret");
        Directory.CreateDirectory(Path.GetDirectoryName(secret)!);
        File.WriteAllText(secret, "SECRET_MARKER_do_not_leak");
        // Seed it as a must-read too, so we can prove the read is NOT registered on a block.
        SeedAgent(unreadMustReads: ["config/prod.secret"], unreadMessages: []);

        var (code, stdout, stderr) = RunRead("config/prod.secret");

        Assert.Equal(ExitCodes.ToolError, code);
        Assert.DoesNotContain("SECRET_MARKER_do_not_leak", stdout);
        Assert.Contains("BLOCKED", stderr);
        Assert.Contains("off-limits", stderr);
        Assert.Contains("**/*.secret", stderr);
        Assert.Contains("files-off-limits.md", stderr);

        // No read registered: the off-limits path stays on the unread must-read list.
        var after = new AgentRegistry(_testDir).GetAgentState(AgentName)!;
        Assert.Contains("config/prod.secret", after.UnreadMustReads);
    }

    [Fact]
    public void Read_OffLimitsRootFileByBareName_BlockedNoContentNoRead()
    {
        // Regression (c1-audit-f1b): a ROOT-LEVEL off-limits file addressed by BARE filename must be
        // blocked. A bare name has no separator, so GuardCommand.IsBootstrapFile treated it as a
        // "root-level bootstrap file" and ShouldBypassOffLimits skipped the whole off-limits check —
        // leaking the secret. Absolutizing the resolved path before the checks closes that hole.
        WriteOffLimits(".env");
        var secret = Path.Combine(_testDir, ".env");
        File.WriteAllText(secret, "API_SECRET=leak_me");
        // Seed it as a must-read too, so we can prove the read is NOT registered on a block.
        SeedAgent(unreadMustReads: [".env"], unreadMessages: []);

        var (code, stdout, stderr) = RunRead(".env");

        Assert.NotEqual(ExitCodes.Success, code);
        Assert.DoesNotContain("leak_me", stdout);
        Assert.Contains("BLOCKED", stderr);
        Assert.Contains("off-limits", stderr);
        Assert.Contains(".env", stderr);
        Assert.Contains("files-off-limits.md", stderr);

        var after = new AgentRegistry(_testDir).GetAgentState(AgentName)!;
        Assert.Contains(".env", after.UnreadMustReads);
    }

    [Fact]
    public void Read_HardcodedSystemOffLimitsByBareName_Blocked()
    {
        // The hardcoded SystemOffLimits pattern "dydo.json" must also be enforced when addressed by
        // bare filename from the project root (no separator → previously mis-classified as a
        // bootstrap root file that bypassed off-limits). dydo.json already exists at the test root.
        SeedAgent(unreadMustReads: [], unreadMessages: []);

        var (code, stdout, stderr) = RunRead("dydo.json");

        Assert.NotEqual(ExitCodes.Success, code);
        // dydo.json's contents (agent pool config) must not be emitted on a block.
        Assert.DoesNotContain("\"pool\"", stdout);
        Assert.Contains("BLOCKED", stderr);
        Assert.Contains("off-limits", stderr);
        Assert.Contains("dydo.json", stderr);
    }

    [Fact]
    public void Read_ExemptBootstrapFile_StillReadableDespiteOffLimitsPattern()
    {
        // Bootstrap/mode files carry the guard's read-tier exemption (ShouldBypassOffLimits), so a
        // broad off-limits pattern that would otherwise cover them must not block the read.
        WriteOffLimits("**/*.md");
        var bootstrap = Path.Combine(_dydoDir, "index.md");
        Directory.CreateDirectory(_dydoDir);
        File.WriteAllText(bootstrap, "# Index\nBOOTSTRAP_MARKER");
        SeedAgent(unreadMustReads: [], unreadMessages: []);

        var (code, stdout, _) = RunRead("dydo/index.md");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("BOOTSTRAP_MARKER", stdout);
    }

    [Fact]
    public void Read_FromWorktreeCwd_NormalFileReadableAndMustReadCompletes()
    {
        // Guard parity for the verb's primary deployment: a dispatched shell-host agent runs from a
        // dydo worktree CWD. ResolveWorktreePath must remap the target back to the main-project path
        // BEFORE the off-limits check, so a normal file is not misfired against dydo/_system/**.
        var (worktreeRoot, _) = SeedWorktreeAgent(
            offLimits: "**/*.secret", unreadMustReads: ["dydo/understand/about.md"]);
        var about = Path.Combine(worktreeRoot, "dydo", "understand", "about.md");
        Directory.CreateDirectory(Path.GetDirectoryName(about)!);
        File.WriteAllText(about, "# About\nWORKTREE_ABOUT_MARKER");
        Environment.CurrentDirectory = worktreeRoot;

        var (code, stdout, _) = RunRead("dydo/understand/about.md");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("WORKTREE_ABOUT_MARKER", stdout);

        var after = new AgentRegistry(worktreeRoot).GetAgentState(AgentName)!;
        Assert.Empty(after.UnreadMustReads);
    }

    [Fact]
    public void Read_FromWorktreeCwd_OffLimitsFileBlockedNamingUserPattern()
    {
        // The off-limits verdict from a worktree CWD must name the USER pattern (**/*.secret), not
        // the hardcoded dydo/_system/** pattern that an un-normalized worktree path would falsely
        // trip on the worktree-marker segment.
        var (worktreeRoot, _) = SeedWorktreeAgent(
            offLimits: "**/*.secret", unreadMustReads: []);
        var secret = Path.Combine(worktreeRoot, "dydo", "config", "prod.secret");
        Directory.CreateDirectory(Path.GetDirectoryName(secret)!);
        File.WriteAllText(secret, "WORKTREE_SECRET_MARKER");
        Environment.CurrentDirectory = worktreeRoot;

        var (code, stdout, stderr) = RunRead("dydo/config/prod.secret");

        Assert.Equal(ExitCodes.ToolError, code);
        Assert.DoesNotContain("WORKTREE_SECRET_MARKER", stdout);
        Assert.Contains("BLOCKED", stderr);
        Assert.Contains("**/*.secret", stderr);
        Assert.DoesNotContain("dydo/_system/**", stderr);
    }

    private static (int exitCode, string stdout, string stderr) RunRead(string target) =>
        ConsoleCapture.All(() => ReadCommand.Create().Parse(target).Invoke());

    private void SeedAgent(IReadOnlyList<string> unreadMustReads, IReadOnlyList<string> unreadMessages) =>
        WriteAgentState(_workspace, _agentsDir, unreadMustReads, unreadMessages);

    private static void WriteAgentState(
        string workspace, string agentsDir,
        IReadOnlyList<string> unreadMustReads, IReadOnlyList<string> unreadMessages)
    {
        Directory.CreateDirectory(workspace);
        var mustReadsYaml = string.Join(", ", unreadMustReads.Select(p => $"\"{p}\""));
        var messagesYaml = string.Join(", ", unreadMessages.Select(id => $"\"{id}\""));

        File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
            ---
            agent: {{AgentName}}
            role: co-thinker
            task: read-test
            status: working
            assigned: testuser
            dispatched-by: null
            dispatched-by-role: null
            window-id: null
            auto-close: false
            started: null
            writable-paths: []
            readonly-paths: []
            unread-must-reads: [{{mustReadsYaml}}]
            unread-messages: [{{messagesYaml}}]
            task-role-history: {}
            ---

            # {{AgentName}} — Session State
            """);

        var session = new AgentSession
        {
            Agent = AgentName,
            SessionId = TestSessionId,
            Claimed = DateTime.UtcNow,
            ClaimedPid = Environment.ProcessId
        };
        File.WriteAllText(Path.Combine(workspace, ".session"),
            JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));
        File.WriteAllText(Path.Combine(agentsDir, ".session-agent"), AgentName);

        Environment.SetEnvironmentVariable("DYDO_AGENT", AgentName);
    }

    private void WriteOffLimits(string pattern)
    {
        Directory.CreateDirectory(_dydoDir);
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), OffLimitsMarkdown(pattern));
    }

    // Wildcard patterns (**/, *) must be declared in a fenced code block, not a "- " list item:
    // the list parser TrimStart('-','*',' ')s leading '*' chars and would corrupt "**/*.secret".
    private static string OffLimitsMarkdown(string pattern) =>
        "# Off-Limits Files\n\n```\n" + pattern + "\n```\n";

    /// <summary>
    /// Builds a dydo-worktree layout under the main project (dydo/_system/.local/worktrees/&lt;id&gt;/
    /// with its own dydo.json + off-limits + seeded agent) so tests can exercise the verb from the
    /// worktree CWD a dispatched shell-host agent runs in. Returns the worktree root and workspace.
    /// </summary>
    private (string worktreeRoot, string workspace) SeedWorktreeAgent(
        string offLimits, IReadOnlyList<string> unreadMustReads)
    {
        var worktreeRoot = Path.Combine(
            _dydoDir, "_system", ".local", "worktrees", "wt" + Guid.NewGuid().ToString("N")[..6]);
        var wtDydo = Path.Combine(worktreeRoot, "dydo");
        var wtAgents = Path.Combine(wtDydo, "agents");
        var wtWorkspace = Path.Combine(wtAgents, AgentName);
        Directory.CreateDirectory(Path.Combine(wtWorkspace, "inbox"));

        File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"), """
            {
                "version": 1,
                "structure": { "root": "dydo" },
                "agents": {
                    "pool": ["Adele", "Brian"],
                    "assignments": { "testuser": ["Adele", "Brian"] }
                }
            }
            """);

        Directory.CreateDirectory(wtDydo);
        File.WriteAllText(Path.Combine(wtDydo, "files-off-limits.md"), OffLimitsMarkdown(offLimits));

        WriteAgentState(wtWorkspace, wtAgents, unreadMustReads, unreadMessages: []);
        return (worktreeRoot, wtWorkspace);
    }

    private void WriteInboxMessage(string id, string from, string subject, string body)
    {
        var filename = $"{id}-msg-{subject}.md";
        File.WriteAllText(Path.Combine(_workspace, "inbox", filename), $"""
            ---
            id: {id}
            type: message
            from: {from}
            subject: {subject}
            received: 2026-07-09T10:00:00Z
            ---

            ## Body

            {body}
            """);
    }
}
