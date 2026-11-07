namespace DynaDocs.Tests.EndToEnd;

using System.Diagnostics;

/// <summary>
/// DYD-675 through the built CLI: Program.cs is the one entry the in-process guard tests bypass, so
/// a '@' text reaching `dydo guard` must be shown to survive the real parse, not only a test's.
/// </summary>
[Collection("EndToEnd")]
public class GuardAtCommandProcessTests : IDisposable
{
    private readonly string _projectDir;
    private readonly string _dllPath;

    public GuardAtCommandProcessTests(EndToEndFixture fixture)
    {
        _dllPath = fixture.DllPath;
        _projectDir = Path.Combine(Path.GetTempPath(), "dydo-e2e-at-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_projectDir);
        Assert.Equal(0, Run("", "init", "none").ExitCode);
    }

    public void Dispose()
    {
        try { Directory.Delete(_projectDir, true); }
        catch (IOException) { /* a temp directory another process still holds is left for the OS */ }
        GC.SuppressFinalize(this);
    }

    private (int ExitCode, string Stdout, string Stderr) Run(string stdin, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _projectDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(_dllPath);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = Process.Start(start)!;
        process.StandardInput.Write(stdin);
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.Result, stderr.Result);
    }

    [Theory]
    [InlineData("@'\nWrite-Output hi\n'@", 0, "")]
    [InlineData("@nofile", 0, "")]
    [InlineData("@'\ngit push --force origin main\n'@", 2, "Force push")]
    public void GuardCommand_TextStartingWithAt_GetsAVerdictNotAResponseFileError(string command, int exitCode, string blockedFor)
    {
        var result = Run("", "guard", "--command", command);

        Assert.DoesNotContain("Response file", result.Stderr);
        Assert.Equal(exitCode, result.ExitCode);
        Assert.Contains(blockedFor, result.Stderr);
    }

    [Fact]
    public void GuardCommand_AtExistingFile_IsTextAndTheFileIsNotRead()
    {
        var file = Path.Combine(_projectDir, "response.rsp");
        File.WriteAllText(file, "\"git push --force origin main\"\n");

        var result = Run("", "guard", "--command", "@" + file);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("", result.Stderr);
    }

    [Theory]
    [InlineData("--path")]
    [InlineData("--action")]
    public void GuardPathAndAction_TextStartingWithAt_IsNotAResponseFile(string option)
    {
        var arguments = option == "--path"
            ? new[] { "guard", "--action", "read", "--path", "@nofile" }
            : ["guard", "--action", "@nofile", "--path", "src/file.cs"];

        var result = Run("", arguments);

        Assert.DoesNotContain("Response file", result.Stderr);
        Assert.Contains(result.ExitCode, new[] { 0, 2 });
    }

    [Fact]
    public void GuardHookLane_HereStringOnStdin_IsDenied()
    {
        var payload = "{\"session_id\":\"e2e-at\",\"tool_name\":\"PowerShell\",\"tool_input\":{\"command\":\"@'\\ngit push --force origin main\\n'@\"}}";

        var result = Run(payload, "guard", "--host", "claude");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"permissionDecision\":\"deny\"", result.Stdout);
    }
}
