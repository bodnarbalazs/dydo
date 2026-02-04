namespace DynaDocs.Tests.Integration;

using System.CommandLine;
using DynaDocs.Commands;

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
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalErr;
    private readonly TextReader _originalIn;

    protected IntegrationTestBase()
    {
        // Create unique temp directory
        TestDir = Path.Combine(Path.GetTempPath(), "dydo-integration-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(TestDir);

        // Save original state
        _originalDir = Environment.CurrentDirectory;
        _originalHuman = Environment.GetEnvironmentVariable("DYDO_HUMAN");
        _originalOut = Console.Out;
        _originalErr = Console.Error;
        _originalIn = Console.In;

        // Set working directory to test dir
        Environment.CurrentDirectory = TestDir;
    }

    public void Dispose()
    {
        // Restore original state
        Environment.CurrentDirectory = _originalDir;
        Environment.SetEnvironmentVariable("DYDO_HUMAN", _originalHuman);
        Console.SetOut(_originalOut);
        Console.SetError(_originalErr);
        Console.SetIn(_originalIn);

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
        var stdoutWriter = new StringWriter();
        var stderrWriter = new StringWriter();

        Console.SetOut(stdoutWriter);
        Console.SetError(stderrWriter);

        try
        {
            var exitCode = await command.Parse(args).InvokeAsync();
            return new CommandResult(exitCode, stdoutWriter.ToString(), stderrWriter.ToString());
        }
        finally
        {
            Console.SetOut(_originalOut);
            Console.SetError(_originalErr);
        }
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

    /// <summary>
    /// Run whoami command.
    /// </summary>
    protected async Task<CommandResult> WhoamiAsync()
    {
        var command = WhoamiCommand.Create();
        return await RunAsync(command);
    }

    /// <summary>
    /// Claim an agent.
    /// </summary>
    protected async Task<CommandResult> ClaimAgentAsync(string nameOrAuto = "auto")
    {
        var command = AgentCommand.Create();
        return await RunAsync(command, "claim", nameOrAuto);
    }

    /// <summary>
    /// Release the current agent.
    /// </summary>
    protected async Task<CommandResult> ReleaseAgentAsync()
    {
        var command = AgentCommand.Create();
        return await RunAsync(command, "release");
    }

    /// <summary>
    /// Set agent role.
    /// </summary>
    protected async Task<CommandResult> SetRoleAsync(string role, string? task = null)
    {
        var command = AgentCommand.Create();
        var args = task != null
            ? new[] { "role", role, "--task", task }
            : new[] { "role", role };
        return await RunAsync(command, args);
    }

    /// <summary>
    /// List agents.
    /// </summary>
    protected async Task<CommandResult> ListAgentsAsync(bool freeOnly = false)
    {
        var command = AgentCommand.Create();
        var args = freeOnly ? new[] { "list", "--free" } : new[] { "list" };
        return await RunAsync(command, args);
    }

    /// <summary>
    /// Get agent status.
    /// </summary>
    protected async Task<CommandResult> AgentStatusAsync(string? name = null)
    {
        var command = AgentCommand.Create();
        var args = name != null ? new[] { "status", name } : new[] { "status" };
        return await RunAsync(command, args);
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
        var command = GuardCommand.Create();
        return await RunAsync(command, "--action", action, "--path", path);
    }

    /// <summary>
    /// Run guard command with JSON piped to stdin (simulates Claude Code hook).
    /// </summary>
    protected async Task<CommandResult> GuardWithStdinAsync(string json)
    {
        var command = GuardCommand.Create();
        var stdoutWriter = new StringWriter();
        var stderrWriter = new StringWriter();
        var stdinReader = new StringReader(json);

        Console.SetOut(stdoutWriter);
        Console.SetError(stderrWriter);
        Console.SetIn(stdinReader);

        try
        {
            var exitCode = await command.Parse(Array.Empty<string>()).InvokeAsync();
            return new CommandResult(exitCode, stdoutWriter.ToString(), stderrWriter.ToString());
        }
        finally
        {
            Console.SetOut(_originalOut);
            Console.SetError(_originalErr);
            Console.SetIn(_originalIn);
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
