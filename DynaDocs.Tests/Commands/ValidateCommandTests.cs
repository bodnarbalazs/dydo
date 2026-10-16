namespace DynaDocs.Tests.Commands;

using DynaDocs.Services;

[Collection("Integration")]
public class ValidateCommandTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _originalDir;

    public ValidateCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-validate-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _originalDir = Environment.CurrentDirectory;
        Environment.CurrentDirectory = _testDir;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDir;
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch (IOException) { }
    }

    [Fact]
    public void Create_ReturnsValidateCommand()
    {
        var command = DynaDocs.Commands.ValidateCommand.Create();

        Assert.Equal("validate", command.Name);
    }

    [Fact]
    public void Validate_NoIssues_ReturnsZero()
    {
        SetupValidProjectNoWarnings();

        var command = DynaDocs.Commands.ValidateCommand.Create();
        var exitCode = command.Parse("").Invoke();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Validate_NoIssues_PrintsPassedMessage()
    {
        SetupValidProjectNoWarnings();

        var (stdout, _) = CaptureOutput(() =>
        {
            var command = DynaDocs.Commands.ValidateCommand.Create();
            return command.Parse("").Invoke();
        });

        Assert.Contains("Validation passed", stdout);
    }

    [Fact]
    public void Validate_WithErrors_ReturnsOne()
    {
        // No dydo.json → error
        var command = DynaDocs.Commands.ValidateCommand.Create();
        var exitCode = command.Parse("").Invoke();

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Validate_WithErrors_PrintsErrorCount()
    {
        // No dydo.json → error
        var (_, stderr) = CaptureOutput(() =>
        {
            var command = DynaDocs.Commands.ValidateCommand.Create();
            return command.Parse("").Invoke();
        });

        Assert.Contains("Errors", stderr);
    }

    [Fact]
    public void Validate_AcceptsValidTestingRunner()
    {
        SetupValidProjectNoWarnings();
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"),
            "{\"testing\":{\"runner\":[\"runner\",\"\"]}}");

        var exitCode = DynaDocs.Commands.ValidateCommand.Create().Parse("").Invoke();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Validate_RejectsInvalidTestingRunner()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "{\"testing\":{\"runner\":[]}}");

        var exitCode = DynaDocs.Commands.ValidateCommand.Create().Parse("").Invoke();

        Assert.Equal(1, exitCode);
    }

    private static (string stdout, string stderr) CaptureOutput(Func<int> action)
    {
        var (_, stdout, stderr) = ConsoleCapture.All(action);
        return (stdout, stderr);
    }

    private void SetupValidProjectNoWarnings()
    {
        // Minimal valid dydo.json with no roles → no warnings
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"),
            """{"version":1,"structure":{"root":"dydo"},"integrations":{"claude":false,"codex":false},"scanExclude":["_system/.local/","_system/audit/","agents/"],"nudges":[],"frameworkHashes":{}}""");
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo", "_system"));
    }

    [Fact]
    public void Validate_Root_ReadsDydoJsonFromThatProjectNotCwd()
    {
        SetupValidProjectNoWarnings();
        var other = Path.Combine(_testDir, "other");
        Directory.CreateDirectory(Path.Combine(other, "dydo", "guides"));
        File.WriteAllText(Path.Combine(other, "dydo.json"), "{\"testing\":{\"runner\":[]}}");

        var (code, stdout, _) = ConsoleCapture.All(() =>
            DynaDocs.Commands.ValidateCommand.Create().Parse(["--root", Path.Combine(other, "dydo", "guides")]).Invoke());

        Assert.Equal(1, code);
        Assert.Contains($"Root: {other}", stdout);
    }

    [Fact]
    public void Validate_RootInNoDydoProject_IsToolError()
    {
        SetupValidProjectNoWarnings();
        var nowhere = Path.Combine(Path.GetTempPath(), "dydo-validate-nowhere-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(nowhere);
        try
        {
            var (code, _, stderr) = ConsoleCapture.All(() =>
                DynaDocs.Commands.ValidateCommand.Create().Parse(["--root", nowhere]).Invoke());

            Assert.Equal(2, code);
            Assert.Contains($"No dydo project found at {nowhere}", stderr);
        }
        finally
        {
            Directory.Delete(nowhere, true);
        }
    }

    [Fact]
    public void Validate_WithoutRoot_PrintsNoRootLine()
    {
        SetupValidProjectNoWarnings();

        var (stdout, _) = CaptureOutput(() => DynaDocs.Commands.ValidateCommand.Create().Parse("").Invoke());

        Assert.DoesNotContain("Root:", stdout);
    }
}
