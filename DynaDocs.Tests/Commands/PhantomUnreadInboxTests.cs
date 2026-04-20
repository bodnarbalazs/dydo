namespace DynaDocs.Tests.Commands;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Regression tests for the phantom unread-inbox lock bug.
///
/// Bug: when <c>state.md</c>'s <c>unread-messages</c> list contains an id whose
/// inbox file has been deleted (non-atomic clear, crash mid-operation, manual
/// cleanup), the guard blocks forever — the user sees "N unread message(s)"
/// but <c>ls inbox/</c> is empty, and there is no file to read to clear it.
///
/// Fix: <see cref="GuardCommand.NotifyUnreadMessages"/> self-heals by dropping
/// ids whose file is missing, only blocking on survivors.
/// </summary>
[Collection("Integration")]
public class PhantomUnreadInboxTests : IDisposable
{
    private const string TestSessionId = "phantom-unread-test-session";
    private const string AgentName = "Brian";

    private readonly string _testDir;
    private readonly string _dydoDir;
    private readonly string _agentsDir;
    private readonly string _workspace;
    private readonly string _originalDir;
    private readonly string? _originalAgentEnv;

    public PhantomUnreadInboxTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-phantom-" + Guid.NewGuid().ToString("N")[..8]);
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
    public void Guard_NotifyUnreadMessages_PhantomIdsDropped_DoesNotBlock()
    {
        SeedWorkingAgent(unreadIds: ["deadbeef"]);
        // No inbox file for deadbeef — pure phantom.

        var registry = new AgentRegistry(_testDir);
        var auditService = new AuditService(basePath: _testDir);
        var agent = registry.GetAgentState(AgentName)!;

        var result = GuardCommand.NotifyUnreadMessages(
            agent, "src/foo.cs", "Read", null, auditService, TestSessionId, registry);

        Assert.Null(result);
        var healed = registry.GetAgentState(AgentName)!;
        Assert.Empty(healed.UnreadMessages);
    }

    [Fact]
    public void Guard_NotifyUnreadMessages_MixedRealAndPhantom_BlocksOnRealOnly()
    {
        SeedWorkingAgent(unreadIds: ["aaaa1111", "deadbeef"]);
        WriteInboxMessage("aaaa1111", from: "Adele", subject: "hello");
        // No file for deadbeef — phantom.

        var registry = new AgentRegistry(_testDir);
        var auditService = new AuditService(basePath: _testDir);
        var agent = registry.GetAgentState(AgentName)!;

        var result = GuardCommand.NotifyUnreadMessages(
            agent, "src/foo.cs", "Read", null, auditService, TestSessionId, registry);

        Assert.Equal(ExitCodes.ToolError, result);
        var healed = registry.GetAgentState(AgentName)!;
        Assert.Single(healed.UnreadMessages);
        Assert.Equal("aaaa1111", healed.UnreadMessages[0]);
    }

    [Fact]
    public void Guard_NotifyUnreadMessages_AllRealIds_StillBlocks()
    {
        SeedWorkingAgent(unreadIds: ["aaaa1111", "bbbb2222"]);
        WriteInboxMessage("aaaa1111", from: "Adele", subject: "hello");
        WriteInboxMessage("bbbb2222", from: "Charlie", subject: "world");

        var registry = new AgentRegistry(_testDir);
        var auditService = new AuditService(basePath: _testDir);
        var agent = registry.GetAgentState(AgentName)!;

        var result = GuardCommand.NotifyUnreadMessages(
            agent, "src/foo.cs", "Read", null, auditService, TestSessionId, registry);

        Assert.Equal(ExitCodes.ToolError, result);
        var preserved = registry.GetAgentState(AgentName)!;
        Assert.Equal(2, preserved.UnreadMessages.Count);
        Assert.Contains("aaaa1111", preserved.UnreadMessages);
        Assert.Contains("bbbb2222", preserved.UnreadMessages);
    }

    private void SeedWorkingAgent(IReadOnlyList<string> unreadIds)
    {
        var unreadYaml = string.Join(", ", unreadIds.Select(id => $"\"{id}\""));
        File.WriteAllText(Path.Combine(_workspace, "state.md"), $$"""
            ---
            agent: {{AgentName}}
            role: co-thinker
            task: phantom-test
            status: working
            assigned: testuser
            dispatched-by: null
            dispatched-by-role: null
            window-id: null
            auto-close: false
            started: null
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: [{{unreadYaml}}]
            task-role-history: {}
            ---

            # {{AgentName}} — Session State
            """);

        var session = new AgentSession { Agent = AgentName, SessionId = TestSessionId, Claimed = DateTime.UtcNow };
        File.WriteAllText(Path.Combine(_workspace, ".session"),
            JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));
        File.WriteAllText(Path.Combine(_agentsDir, ".session-agent"), AgentName);
    }

    private void WriteInboxMessage(string id, string from, string subject)
    {
        var filename = $"{id}-msg-{subject}.md";
        File.WriteAllText(Path.Combine(_workspace, "inbox", filename), $"""
            ---
            id: {id}
            type: message
            from: {from}
            subject: {subject}
            ---

            body
            """);
    }
}
