namespace DynaDocs.Tests.Integration;

using System.CommandLine;
using System.Diagnostics;
using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// Derived classes must add [Collection("Integration")]: tests share process-global state
/// and must run sequentially.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected string TestDir { get; }
    protected string DydoDir => Path.Combine(TestDir, "dydo");

    private readonly string _originalDir;
    private readonly string? _originalWindow;
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalErr;
    private readonly TextReader _originalIn;

    protected IntegrationTestBase()
    {
        TestDir = Path.Combine(Path.GetTempPath(), "dydo-integration-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(TestDir);

        _originalDir = Environment.CurrentDirectory;
        _originalWindow = Environment.GetEnvironmentVariable("DYDO_WINDOW");
        _originalOut = Console.Out;
        _originalErr = Console.Error;
        _originalIn = Console.In;
        // Clear env vars that leak into dispatch logic
        Environment.SetEnvironmentVariable("DYDO_WINDOW", null);

        Environment.CurrentDirectory = TestDir;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDir;
        Environment.SetEnvironmentVariable("DYDO_WINDOW", _originalWindow);
        Console.SetOut(_originalOut);
        Console.SetError(_originalErr);
        Console.SetIn(_originalIn);

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

    protected async Task<CommandResult> RunAsync(Command command, params string[] args)
    {
        var (exitCode, stdout, stderr) = await ConsoleCapture.AllAsync(
            async () => await command.Parse(args).InvokeAsync());
        return new CommandResult(exitCode, stdout, stderr);
    }

    protected async Task<CommandResult> InitProjectAsync(string integration = "none")
    {
        var command = InitCommand.Create();
        return await RunAsync(command, integration);
    }

    protected async Task<CommandResult> JoinProjectAsync(string integration = "none")
    {
        var command = InitCommand.Create();
        return await RunAsync(command, integration, "--join");
    }

    protected const string TestSessionId = "test-integration-session";

    protected async Task<CommandResult> CheckAsync(string? path = null)
    {
        var command = CheckCommand.Create();
        var args = path != null ? new[] { path } : Array.Empty<string>();
        return await RunAsync(command, args);
    }

    protected async Task<CommandResult> GuardAsync(string action, string path)
    {
        var command = GuardCommand.Create();
        return await RunAsync(command, "--action", action, "--path", path);
    }

    protected async Task<CommandResult> GuardWithStdinAsync(string json, params string[] args)
    {
        var command = GuardCommand.Create();
        var stdinReader = new StringReader(json);

        var (exitCode, stdout, stderr) = await ConsoleCapture.AllAsyncWithStdin(
            stdinReader, async () => await command.Parse(args).InvokeAsync());
        return new CommandResult(exitCode, stdout, stderr);
    }



    protected void AssertFileExists(string relativePath)
    {
        var fullPath = Path.Combine(TestDir, relativePath);
        Assert.True(File.Exists(fullPath), $"Expected file to exist: {relativePath}");
    }

    protected void AssertDirectoryExists(string relativePath)
    {
        var fullPath = Path.Combine(TestDir, relativePath);
        Assert.True(Directory.Exists(fullPath), $"Expected directory to exist: {relativePath}");
    }

    protected void AssertFileNotExists(string relativePath)
    {
        var fullPath = Path.Combine(TestDir, relativePath);
        Assert.False(File.Exists(fullPath), $"Expected file to NOT exist: {relativePath}");
    }

    protected void AssertFileContains(string relativePath, string expectedContent)
    {
        var fullPath = Path.Combine(TestDir, relativePath);
        Assert.True(File.Exists(fullPath), $"File does not exist: {relativePath}");
        var content = File.ReadAllText(fullPath);
        Assert.Contains(expectedContent, content);
    }

    protected string ReadFile(string relativePath)
    {
        var fullPath = Path.Combine(TestDir, relativePath);
        return File.ReadAllText(fullPath);
    }

    protected void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(TestDir, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
    }
}

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

    /// <summary>
    /// Asserts the guard allowed a hook-mode call: exit 0 and no decision on stdout. Exit 0 alone
    /// does not prove an allow, because a PreToolUse refusal also exits 0 (LC-1021).
    /// </summary>
    public bool IsPreToolUseAllowed => ExitCode == 0 && Stdout.Length == 0;

    public void AssertPreToolUseAllowed()
    {
        Assert.True(IsPreToolUseAllowed, $"Expected an allowed call but got exit {ExitCode}.\nStderr: {Stderr}\nStdout: {Stdout}");
    }

    /// <summary>
    /// Asserts the guard refused a PreToolUse call the way Codex and Claude Code both honour:
    /// exit 0 and stdout holding exactly one deny-decision object (LC-1021). Returns the reason.
    /// </summary>
    public string AssertPreToolUseDenied()
    {
        Assert.True(ExitCode == 0, $"Expected exit 0 with a deny decision but got {ExitCode}.\nStderr: {Stderr}\nStdout: {Stdout}");
        using var json = System.Text.Json.JsonDocument.Parse(Stdout.Trim());
        var root = json.RootElement;
        Assert.Equal(System.Text.Json.JsonValueKind.Object, root.ValueKind);
        Assert.Equal(["hookSpecificOutput"], root.EnumerateObject().Select(p => p.Name));
        var output = root.GetProperty("hookSpecificOutput");
        Assert.Equal(["hookEventName", "permissionDecision", "permissionDecisionReason"],
            output.EnumerateObject().Select(p => p.Name));
        Assert.Equal("PreToolUse", output.GetProperty("hookEventName").GetString());
        Assert.Equal("deny", output.GetProperty("permissionDecision").GetString());
        var reason = output.GetProperty("permissionDecisionReason").GetString();
        Assert.False(string.IsNullOrWhiteSpace(reason));
        return reason!;
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
