namespace DynaDocs.Tests.Steps;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Tests.Integration;
using Reqnroll;

[Binding]
public sealed class MachineModeSteps : IntegrationTestBase
{
    private readonly string? _originalClaudeCode = Environment.GetEnvironmentVariable("CLAUDECODE");
    private string _toolName = "Bash";
    private string _permissionMode = "default";
    private string? _agentId;
    private CommandResult _result = new(-1, "", "");

    private ModeState State => new(Path.Combine(TestDir, "machine"));

    [AfterScenario]
    public void Restore()
    {
        Environment.SetEnvironmentVariable("CLAUDECODE", _originalClaudeCode);
        Dispose();
    }

    [Given("a fresh machine mode")]
    public void FreshMachineMode() => Assert.False(Directory.Exists(Path.Combine(TestDir, "machine")));

    [When("I run dydo mode show")]
    public async Task Show() => _result = await RunAsync(ModeCommand.Create(State), "show");

    [When("I run dydo mode set {string} {string}")]
    public async Task Set(string flag, string value) =>
        _result = await RunAsync(ModeCommand.Create(State), "set", flag, value);

    [Then("the mode set succeeds")]
    public void SetSucceeds() => _result.AssertSuccess();

    [Then("the mode set is refused naming {string}")]
    public void SetRefused(string refused)
    {
        _result.AssertExitCode(2);
        Assert.Contains($"'{refused}'", _result.Stderr);
    }

    [Then("the mode shows {string} as {string}")]
    public async Task Shows(string flag, string value)
    {
        var shown = await RunAsync(ModeCommand.Create(State), "show");
        shown.AssertSuccess();
        Assert.Contains($"{flag}: {value}", shown.Stdout.Split(Environment.NewLine));
    }

    [Given("an agent on {string} in {string} permission mode")]
    public async Task Agent(string host, string permissionMode)
    {
        Environment.SetEnvironmentVariable("CLAUDECODE", host switch
        {
            "Claude Code" => "1",
            "Codex" or "unknown" => null,
            _ => throw new ArgumentException($"Unknown host in scenario: {host}")
        });
        _toolName = host == "Codex" ? "shell_command" : "Bash";
        _permissionMode = permissionMode;
        (await InitProjectAsync("none")).AssertSuccess();
    }

    [Given("a sub-agent on {string} in {string} permission mode")]
    public async Task SubAgent(string host, string permissionMode)
    {
        await Agent(host, permissionMode);
        _agentId = "sub-agent-1";
    }

    [When("the agent runs {string}")]
    public async Task AgentRuns(string command) => _result = await GuardWithStdinAsync(Hook(command));

    [When("the agent runs {string} twice")]
    public async Task AgentRunsTwice(string command)
    {
        await AgentRuns(command);
        AssertFirstTryBlocked();
        await AgentRuns(command);
    }

    [Then("the guard blocks it and tells the chief of staff to run it again")]
    public void AssertFirstTryBlocked()
    {
        var reason = _result.AssertPreToolUseDenied();
        Assert.StartsWith("BLOCKED: dydo mode set changes the mode of every agent on this machine.", reason);
        Assert.Contains("Chief of staff: run the same command again", reason);
    }

    [Then("the guard allows it")]
    public void Allows() => _result.AssertPreToolUseAllowed();

    [Then("the guard asks the user about {string}")]
    public void Asks(string command)
    {
        Assert.Equal(0, _result.ExitCode);
        using var json = JsonDocument.Parse(_result.Stdout.Trim());
        var output = json.RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("PreToolUse", output.GetProperty("hookEventName").GetString());
        Assert.Equal("ask", output.GetProperty("permissionDecision").GetString());
        Assert.Contains($"`{command}`", output.GetProperty("permissionDecisionReason").GetString());
    }

    [Then("the guard denies it and asks the user to run {string} in their own terminal")]
    public void DeniesForTerminal(string command)
    {
        var reason = _result.AssertPreToolUseDenied();
        Assert.StartsWith("BLOCKED: This host cannot ask the user to approve dydo mode set", reason);
        Assert.EndsWith($"Ask the user to run it in their own terminal: {command}", reason);
    }

    private string Hook(string command)
    {
        var hook = new Dictionary<string, object>
        {
            ["session_id"] = TestSessionId,
            ["hook_event_name"] = "PreToolUse",
            ["permission_mode"] = _permissionMode,
            ["tool_name"] = _toolName,
            ["tool_input"] = new Dictionary<string, string> { ["command"] = command }
        };
        if (_agentId != null)
            hook["agent_id"] = _agentId;
        return JsonSerializer.Serialize(hook);
    }
}
