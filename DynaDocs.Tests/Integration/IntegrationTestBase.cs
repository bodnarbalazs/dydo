namespace DynaDocs.Tests.Integration;

using System.CommandLine;
using System.Diagnostics;
using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// Base class for CLI integration tests.
/// Provides isolated temp directory, console capture, and environment management.
///
/// All derived classes must add [Collection("Integration")] attribute to ensure
/// sequential execution, since tests share process-global state.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected string TestDir { get; }
    protected string DydoDir => Path.Combine(TestDir, "dydo");

    private readonly string _originalDir;
    private readonly string? _originalHuman;
    private readonly string? _originalWindow;
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalErr;
    private readonly TextReader _originalIn;
    private readonly Func<string, int, int?>? _originalFindAncestorOverride;

    protected IntegrationTestBase()
    {
        // Create unique temp directory
        TestDir = Path.Combine(Path.GetTempPath(), "dydo-integration-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(TestDir);

        // Save original state
        _originalDir = Environment.CurrentDirectory;
        _originalHuman = Environment.GetEnvironmentVariable("DYDO_HUMAN");
        _originalWindow = Environment.GetEnvironmentVariable("DYDO_WINDOW");
        _originalOut = Console.Out;
        _originalErr = Console.Error;
        _originalIn = Console.In;
        _originalFindAncestorOverride = ProcessUtils.FindAncestorProcessOverride;

        // Pin the claude-ancestor lookup to this test process so .session.ClaimedPid stamped
        // during claim, and AgentRegistry.IsOwnedByCaller's check downstream, both resolve
        // to a stable value regardless of where dotnet test was launched from. Without this,
        // integration tests pass only when run under a real claude shell (developer machine)
        // and fail in CI. Reset in Dispose. Closes test fallout from #0183/F1 + #0195/F11.
        ProcessUtils.FindAncestorProcessOverride = (_, _) => Environment.ProcessId;

        // Clear env vars that leak into dispatch logic
        Environment.SetEnvironmentVariable("DYDO_WINDOW", null);

        // Set working directory to test dir
        Environment.CurrentDirectory = TestDir;
    }

    public void Dispose()
    {
        // Restore original state
        Environment.CurrentDirectory = _originalDir;
        Environment.SetEnvironmentVariable("DYDO_HUMAN", _originalHuman);
        Environment.SetEnvironmentVariable("DYDO_WINDOW", _originalWindow);
        Console.SetOut(_originalOut);
        Console.SetError(_originalErr);
        Console.SetIn(_originalIn);
        ProcessUtils.FindAncestorProcessOverride = _originalFindAncestorOverride;

        // Clean up test directory
        if (Directory.Exists(TestDir))
        {
            try
            {
                Directory.Delete(TestDir, true);
            }
            catch
            {
                // Ignore cleanup failures in tests
            }
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Set the DYDO_HUMAN environment variable.
    /// </summary>
    protected void SetHuman(string name)
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", name);
    }

    /// <summary>
    /// Clear the DYDO_HUMAN environment variable.
    /// </summary>
    protected void ClearHuman()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    /// <summary>
    /// Run a command and capture output.
    /// </summary>
    protected async Task<CommandResult> RunAsync(Command command, params string[] args)
    {
        var (exitCode, stdout, stderr) = await ConsoleCapture.AllAsync(
            async () => await command.Parse(args).InvokeAsync());
        return new CommandResult(exitCode, stdout, stderr);
    }

    /// <summary>
    /// Initialize a DynaDocs project with default settings.
    /// </summary>
    protected async Task<CommandResult> InitProjectAsync(
        string integration = "none",
        string humanName = "testuser",
        int agentCount = 3)
    {
        SetHuman(humanName);
        var command = InitCommand.Create();
        return await RunAsync(command, integration, "--name", humanName, "--agents", agentCount.ToString());
    }

    /// <summary>
    /// Join an existing DynaDocs project.
    /// </summary>
    protected async Task<CommandResult> JoinProjectAsync(
        string integration = "none",
        string humanName = "alice",
        int agentCount = 2)
    {
        var command = InitCommand.Create();
        return await RunAsync(command, integration, "--join", "--name", humanName, "--agents", agentCount.ToString());
    }

    // Test session ID for integration tests
    protected const string TestSessionId = "test-integration-session";

    /// <summary>
    /// Claim an agent via the runtime registry. The agent-claim CLI was removed with the roster
    /// (DR-041); identity is assigned at spawn now. Tests still exercise the surviving runtime claim
    /// on <see cref="AgentRegistry"/> to set up agent state for the guard/PM/registry behaviour they assert.
    /// </summary>
    protected Task<CommandResult> ClaimAgentAsync(string nameOrAuto = "auto")
    {
        var agentName = nameOrAuto;
        if (nameOrAuto.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            // For auto, get first agent in pool
            var configPath = Path.Combine(TestDir, "dydo.json");
            if (File.Exists(configPath))
            {
                var content = File.ReadAllText(configPath);
                var match = System.Text.RegularExpressions.Regex.Match(content, @"""pool"":\s*\[\s*""([^""]+)""");
                if (match.Success)
                    agentName = match.Groups[1].Value;
            }
        }
        else if (nameOrAuto.Length == 1 && char.IsLetter(nameOrAuto[0]))
        {
            // Resolve single letter to agent name (A -> Adele, B -> Brian, etc.)
            var letter = char.ToUpperInvariant(nameOrAuto[0]);
            agentName = ResolveLetterToAgentName(letter);
        }

        // Store pending session (simulates guard hook interception, which is how a session id reaches claim)
        StorePendingSession(agentName);
        StoreSessionContext();

        var ok = new AgentRegistry(TestDir).ClaimAgent(agentName, out var error);

        // Refresh the .session-context with the verified two-line shape now that the .session file exists,
        // so subsequent commands resolve the agent (post-#0196 the single-line shape reads as null).
        StoreSessionContext();

        return Task.FromResult(new CommandResult(ok ? 0 : ExitCodeToolError, string.Empty, ok ? string.Empty : error));
    }

    protected Task<CommandResult> ClaimAgentWithRuntimeAsync(string agentName, string host, string model)
    {
        var registry = new AgentRegistry(TestDir);
        registry.StorePendingSessionId(agentName, TestSessionId, host, model);
        StoreSessionContext();

        var ok = registry.ClaimAgent(agentName, out var error);

        StoreSessionContext();
        return Task.FromResult(new CommandResult(ok ? 0 : ExitCodeToolError, string.Empty, ok ? string.Empty : error));
    }

    private const int ExitCodeToolError = 2;

    /// <summary>
    /// Resolve a letter to an agent name.
    /// </summary>
    private string ResolveLetterToAgentName(char letter)
    {
        var names = new[] { "Adele", "Brian", "Charlie", "Dexter", "Emma", "Frank", "Grace", "Henry",
            "Iris", "Jack", "Kate", "Liam", "Mia", "Noah", "Olivia", "Peter", "Quinn", "Ruby",
            "Sam", "Tina", "Uma", "Victor", "Wendy", "Xavier", "Yuki", "Zelda" };
        var index = letter - 'A';
        return index >= 0 && index < names.Length ? names[index] : letter.ToString();
    }

    /// <summary>
    /// Release the current agent via the runtime registry.
    /// </summary>
    protected Task<CommandResult> ReleaseAgentAsync()
    {
        StoreSessionContext();
        var registry = new AgentRegistry(TestDir);
        var ok = registry.ReleaseAgent(registry.GetSessionContext(), out var error);
        return Task.FromResult(new CommandResult(ok ? 0 : ExitCodeToolError, string.Empty, ok ? string.Empty : error));
    }

    /// <summary>
    /// Set agent role via the runtime registry. Mirrors Decision 021 by registering a listening
    /// general-wait marker after role-set. Tests that exercise the block itself pass
    /// <c>registerGeneralWait: false</c>.
    /// </summary>
    protected Task<CommandResult> SetRoleAsync(string role, string? task = null,
        bool registerGeneralWait = true)
    {
        StoreSessionContext();
        var registry = new AgentRegistry(TestDir);
        var ok = registry.SetRole(registry.GetSessionContext(), role, task, out var error);
        var result = new CommandResult(ok ? 0 : ExitCodeToolError, string.Empty, ok ? string.Empty : error);

        if (registerGeneralWait && result.ExitCode == 0)
        {
            var agent = registry.GetCurrentAgent(TestSessionId);
            if (agent != null)
                registry.CreateListeningWaitMarker(agent.Name, "_general-wait", agent.Name, Environment.ProcessId);
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// Store pending session for claim (simulates guard hook).
    /// </summary>
    protected void StorePendingSession(string agentName)
    {
        var pendingPath = Path.Combine(DydoDir, "agents", agentName, ".pending-session");
        var dir = Path.GetDirectoryName(pendingPath);
        if (dir != null) Directory.CreateDirectory(dir);
        File.WriteAllText(pendingPath, TestSessionId);
    }

    /// <summary>
    /// Store session context (simulates guard hook). Post-#0196 the
    /// session-context file is only honored when the verified two-line format
    /// (sessionId\nagentName) cross-checks against the per-agent .session file —
    /// so this helper scans agents/ for the .session that owns TestSessionId
    /// and writes the verified format. Pre-claim (no .session yet) it falls
    /// back to the legacy single-line shape, which post-#0196 reads as null
    /// but doesn't matter for tests that only need the context published before
    /// they claim (e.g. ClaimAgentAsync's call before the claim runs).
    /// </summary>
    protected void StoreSessionContext()
    {
        var contextPath = Path.Combine(DydoDir, "agents", ".session-context");
        var dir = Path.GetDirectoryName(contextPath);
        if (dir != null) Directory.CreateDirectory(dir);

        string content = TestSessionId;
        var agentsDir = Path.Combine(DydoDir, "agents");
        if (Directory.Exists(agentsDir))
        {
            foreach (var sessionFile in Directory.EnumerateFiles(agentsDir, ".session", SearchOption.AllDirectories))
            {
                string body;
                try { body = File.ReadAllText(sessionFile); }
                catch { continue; }
                if (body.Contains($"\"{TestSessionId}\""))
                {
                    var agentName = Path.GetFileName(Path.GetDirectoryName(sessionFile)!);
                    content = $"{TestSessionId}\n{agentName}";
                    break;
                }
            }
        }

        File.WriteAllText(contextPath, content);
    }

    /// <summary>
    /// Run check command.
    /// </summary>
    protected async Task<CommandResult> CheckAsync(string? path = null)
    {
        var command = CheckCommand.Create();
        var args = path != null ? new[] { path } : Array.Empty<string>();
        return await RunAsync(command, args);
    }

    /// <summary>
    /// Run guard command.
    /// </summary>
    protected async Task<CommandResult> GuardAsync(string action, string path)
    {
        StoreSessionContext();
        var command = GuardCommand.Create();
        return await RunAsync(command, "--action", action, "--path", path);
    }

    /// <summary>
    /// Run guard command with JSON piped to stdin (simulates Claude Code hook).
    /// </summary>
    protected async Task<CommandResult> GuardWithStdinAsync(string json)
    {
        var command = GuardCommand.Create();
        var stdinReader = new StringReader(json);

        var (exitCode, stdout, stderr) = await ConsoleCapture.AllAsyncWithStdin(
            stdinReader, async () => await command.Parse(Array.Empty<string>()).InvokeAsync());
        return new CommandResult(exitCode, stdout, stderr);
    }

    /// <summary>
    /// Read all must-read files for the current agent so the guard allows writes.
    /// Call this after SetRoleAsync if the test needs to perform writes.
    /// </summary>
    protected async Task ReadMustReadsAsync()
    {
        var registry = new DynaDocs.Services.AgentRegistry(TestDir);
        var agent = registry.GetCurrentAgent(TestSessionId);
        if (agent == null || agent.UnreadMustReads.Count == 0) return;

        foreach (var mustRead in agent.UnreadMustReads.ToList())
        {
            await GuardAsync("read", mustRead);
        }
    }


    /// <summary>
    /// Assert file exists in test directory.
    /// </summary>
    protected void AssertFileExists(string relativePath)
    {
        var fullPath = Path.Combine(TestDir, relativePath);
        Assert.True(File.Exists(fullPath), $"Expected file to exist: {relativePath}");
    }

    /// <summary>
    /// Assert directory exists in test directory.
    /// </summary>
    protected void AssertDirectoryExists(string relativePath)
    {
        var fullPath = Path.Combine(TestDir, relativePath);
        Assert.True(Directory.Exists(fullPath), $"Expected directory to exist: {relativePath}");
    }

    /// <summary>
    /// Assert file does NOT exist in test directory.
    /// </summary>
    protected void AssertFileNotExists(string relativePath)
    {
        var fullPath = Path.Combine(TestDir, relativePath);
        Assert.False(File.Exists(fullPath), $"Expected file to NOT exist: {relativePath}");
    }

    /// <summary>
    /// Assert file contains text.
    /// </summary>
    protected void AssertFileContains(string relativePath, string expectedContent)
    {
        var fullPath = Path.Combine(TestDir, relativePath);
        Assert.True(File.Exists(fullPath), $"File does not exist: {relativePath}");
        var content = File.ReadAllText(fullPath);
        Assert.Contains(expectedContent, content);
    }

    /// <summary>
    /// Read file content from test directory.
    /// </summary>
    protected string ReadFile(string relativePath)
    {
        var fullPath = Path.Combine(TestDir, relativePath);
        return File.ReadAllText(fullPath);
    }

    /// <summary>
    /// Write file to test directory.
    /// </summary>
    protected void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(TestDir, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
    }
}

/// <summary>
/// Result of running a CLI command.
/// </summary>
public record CommandResult(int ExitCode, string Stdout, string Stderr)
{
    public bool IsSuccess => ExitCode == 0;
    public bool HasError => ExitCode != 0;

    public void AssertSuccess()
    {
        Assert.True(IsSuccess, $"Expected success but got exit code {ExitCode}.\nStderr: {Stderr}\nStdout: {Stdout}");
    }

    public void AssertExitCode(int expected)
    {
        Assert.Equal(expected, ExitCode);
    }

    public void AssertStdoutContains(string text)
    {
        Assert.Contains(text, Stdout);
    }

    public void AssertStderrContains(string text)
    {
        Assert.Contains(text, Stderr);
    }
}
