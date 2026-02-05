namespace DynaDocs.Tests.EndToEnd;

using System.Diagnostics;

/// <summary>
/// True end-to-end tests that spawn the CLI as a subprocess.
/// These tests verify the application works as a user would actually run it.
///
/// Unlike the "integration" tests which call command methods directly,
/// these tests run: dotnet dydo.dll &lt;args&gt;
///
/// This catches issues like:
/// - Command construction failures (invalid Option aliases, etc.)
/// - Missing dependencies at runtime
/// - Startup crashes
/// </summary>
[Collection("EndToEnd")]
public class CliEndToEndTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _dllPath;

    public CliEndToEndTests(EndToEndFixture fixture)
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-e2e-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _dllPath = fixture.DllPath;
    }

    public void Dispose()
    {
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
                    Thread.Sleep(100);
                }
            }
        }
        GC.SuppressFinalize(this);
    }

    #region Startup / Help Tests

    /// <summary>
    /// The most basic test: does the CLI start without crashing?
    /// </summary>
    [Fact]
    public async Task Help_DoesNotCrash()
    {
        var result = await RunDydoAsync("--help");

        Assert.True(result.ExitCode == 0,
            $"dydo --help crashed with exit code {result.ExitCode}:\nStderr: {result.Stderr}\nStdout: {result.Stdout}");
        Assert.Contains("dydo", result.Stdout);
    }

    /// <summary>
    /// Verify all subcommands can show help without crashing.
    /// This catches command construction errors like the AuditCommand whitespace alias bug.
    /// </summary>
    [Theory]
    [InlineData("init")]
    [InlineData("check")]
    [InlineData("agent")]
    [InlineData("guard")]
    [InlineData("audit")]
    [InlineData("dispatch")]
    [InlineData("inbox")]
    [InlineData("task")]
    [InlineData("review")]
    [InlineData("whoami")]
    [InlineData("workspace")]
    [InlineData("clean")]
    [InlineData("fix")]
    [InlineData("index")]
    [InlineData("graph")]
    public async Task Subcommand_Help_DoesNotCrash(string subcommand)
    {
        var result = await RunDydoAsync($"{subcommand} --help");

        Assert.True(result.ExitCode == 0,
            $"dydo {subcommand} --help crashed with exit code {result.ExitCode}:\nStderr: {result.Stderr}");
    }

    #endregion

    #region Init Tests

    /// <summary>
    /// The first thing every user does: initialize a project.
    /// </summary>
    [Theory]
    [InlineData("claude")]
    [InlineData("none")]
    public async Task Init_CreatesProject(string integration)
    {
        var result = await RunDydoAsync($"init {integration} --name testuser --agents 2");

        Assert.True(result.ExitCode == 0,
            $"dydo init {integration} failed:\nStderr: {result.Stderr}\nStdout: {result.Stdout}");
        Assert.True(File.Exists(Path.Combine(_testDir, "dydo.json")),
            "dydo.json was not created");
        Assert.True(Directory.Exists(Path.Combine(_testDir, "dydo")),
            "dydo/ directory was not created");
    }

    [Fact]
    public async Task Init_ThenCheck_RunsWithoutCrash()
    {
        // Initialize
        var initResult = await RunDydoAsync("init none --name testuser --agents 2");
        Assert.True(initResult.ExitCode == 0, $"init failed: {initResult.Stderr}");

        // Check should run without crashing (may report validation issues, that's OK)
        // We're testing that the CLI works, not that templates are perfect
        var checkResult = await RunDydoAsync("check .");

        // Exit code 1 = validation errors found (expected)
        // Exit code 2 = tool error (unexpected)
        // The important thing is it doesn't crash with an unhandled exception
        Assert.True(checkResult.ExitCode <= 1,
            $"check crashed:\nStderr: {checkResult.Stderr}\nStdout: {checkResult.Stdout}");
        Assert.Contains("Checking", checkResult.Stdout); // Verify it actually ran
    }

    [Fact]
    public async Task Init_ThenAgentList_ShowsAgents()
    {
        // Initialize with 3 agents
        var initResult = await RunDydoAsync("init none --name testuser --agents 3");
        Assert.True(initResult.ExitCode == 0, $"init failed: {initResult.Stderr}");

        // List should show the agents
        var listResult = await RunDydoAsync("agent list");
        Assert.True(listResult.ExitCode == 0, $"agent list failed: {listResult.Stderr}");
        Assert.Contains("Adele", listResult.Stdout);
        Assert.Contains("Brian", listResult.Stdout);
        Assert.Contains("Charlie", listResult.Stdout);
    }

    [Fact]
    public async Task Init_ThenWhoami_ShowsHumanInfo()
    {
        // Initialize
        var initResult = await RunDydoAsync("init none --name testuser --agents 2");
        Assert.True(initResult.ExitCode == 0, $"init failed: {initResult.Stderr}");

        // Whoami should show human info
        var whoamiResult = await RunDydoAsync("whoami");
        Assert.True(whoamiResult.ExitCode == 0, $"whoami failed: {whoamiResult.Stderr}");
        Assert.Contains("testuser", whoamiResult.Stdout);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Init_WithoutName_ShowsError()
    {
        var result = await RunDydoAsync("init claude");

        // Should fail gracefully, not crash
        Assert.True(result.ExitCode != 0, "Should fail without --name");
        Assert.False(string.IsNullOrEmpty(result.Stderr) && string.IsNullOrEmpty(result.Stdout),
            "Should show some output explaining the error");
    }

    [Fact]
    public async Task Check_WithoutInit_ShowsError()
    {
        var result = await RunDydoAsync("check .");

        // Should fail gracefully (no dydo.json)
        // Exit code 2 indicates tool error (crash), which we don't want
        Assert.True(result.ExitCode != 2,
            $"check crashed on empty directory:\nStderr: {result.Stderr}");
        Assert.False(string.IsNullOrEmpty(result.Stderr) && string.IsNullOrEmpty(result.Stdout),
            "Should show some output explaining the error");
    }

    #endregion

    private async Task<CliResult> RunDydoAsync(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{_dllPath}\" {args}",
            WorkingDirectory = _testDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Set DYDO_HUMAN for commands that need it
        psi.Environment["DYDO_HUMAN"] = "testuser";

        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new CliResult(process.ExitCode, stdout, stderr);
    }

    private record CliResult(int ExitCode, string Stdout, string Stderr);
}

/// <summary>
/// Fixture that ensures the project is built before E2E tests run.
/// </summary>
public class EndToEndFixture : IDisposable
{
    public string DllPath { get; }

    public EndToEndFixture()
    {
        var projectDir = FindProjectDirectory();
        DllPath = Path.Combine(projectDir, "bin", "Debug", "net10.0", "dydo.dll");

        // Build the project if needed
        if (!File.Exists(DllPath))
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build -c Debug",
                WorkingDirectory = projectDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(psi)!;
            process.WaitForExit(120000); // 2 minute timeout

            if (process.ExitCode != 0 || !File.Exists(DllPath))
            {
                throw new InvalidOperationException(
                    $"Failed to build project. DLL not found at: {DllPath}");
            }
        }
    }

    public void Dispose() { }

    private static string FindProjectDirectory()
    {
        // Walk up from test assembly location to find DynaDocs.csproj
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            // Check current directory
            if (File.Exists(Path.Combine(dir, "DynaDocs.csproj")))
                return dir;

            // Check parent (we're in DynaDocs.Tests/bin/Debug/net10.0)
            var parent = Path.GetDirectoryName(dir);
            if (parent != null)
            {
                var siblingPath = Path.Combine(parent, "DynaDocs", "DynaDocs.csproj");
                if (File.Exists(siblingPath))
                    return Path.Combine(parent, "DynaDocs");

                // Try going up more levels
                var grandparent = Path.GetDirectoryName(parent);
                if (grandparent != null)
                {
                    siblingPath = Path.Combine(grandparent, "DynaDocs.csproj");
                    if (File.Exists(siblingPath))
                        return grandparent;
                }
            }

            dir = parent;
        }

        // Last resort: use relative path from test output directory
        // DynaDocs.Tests/bin/Debug/net10.0 -> DynaDocs (4 levels up, then into DynaDocs)
        var fallback = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        if (File.Exists(Path.Combine(fallback, "DynaDocs.csproj")))
            return fallback;

        throw new InvalidOperationException(
            $"Could not find DynaDocs.csproj. Started from: {AppContext.BaseDirectory}");
    }
}

/// <summary>
/// Collection definition for E2E tests.
/// Uses a shared fixture to avoid rebuilding for each test class.
/// </summary>
[CollectionDefinition("EndToEnd")]
public class EndToEndCollection : ICollectionFixture<EndToEndFixture> { }
