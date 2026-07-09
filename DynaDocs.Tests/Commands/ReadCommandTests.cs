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
        SeedAgent(unreadMustReads: [], unreadMessages: ["aaaa1111"]);
        WriteInboxMessage("aaaa1111", from: "Adele", subject: "subj", body: "BODYTEXT");

        var (code, stdout, _) = RunRead("aaaa1111");

        Assert.Equal(ExitCodes.Success, code);
        // Display-equals-ack: the same call that registered the read also emitted the content.
        Assert.False(string.IsNullOrWhiteSpace(stdout));
        Assert.Contains("BODYTEXT", stdout);

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

    private static (int exitCode, string stdout, string stderr) RunRead(string target) =>
        ConsoleCapture.All(() => ReadCommand.Create().Parse(target).Invoke());

    private void SeedAgent(IReadOnlyList<string> unreadMustReads, IReadOnlyList<string> unreadMessages)
    {
        var mustReadsYaml = string.Join(", ", unreadMustReads.Select(p => $"\"{p}\""));
        var messagesYaml = string.Join(", ", unreadMessages.Select(id => $"\"{id}\""));

        File.WriteAllText(Path.Combine(_workspace, "state.md"), $$"""
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
        File.WriteAllText(Path.Combine(_workspace, ".session"),
            JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));
        File.WriteAllText(Path.Combine(_agentsDir, ".session-agent"), AgentName);

        Environment.SetEnvironmentVariable("DYDO_AGENT", AgentName);
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
