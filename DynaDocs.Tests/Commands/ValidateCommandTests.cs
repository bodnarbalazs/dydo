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
    public void Validate_WithWarningsOnly_ReturnsZero()
    {
        SetupProjectWithWarningsOnly();

        var command = DynaDocs.Commands.ValidateCommand.Create();
        var exitCode = command.Parse("").Invoke();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Validate_WithWarningsOnly_PrintsWarnings()
    {
        SetupProjectWithWarningsOnly();

        var (_, stderr) = CaptureOutput(() =>
        {
            var command = DynaDocs.Commands.ValidateCommand.Create();
            return command.Parse("").Invoke();
        });

        Assert.Contains("Warnings", stderr);
    }

    [Fact]
    public void Validate_WithErrorsAndWarnings_ReturnsOne()
    {
        SetupProjectWithErrorsAndWarnings();

        var command = DynaDocs.Commands.ValidateCommand.Create();
        var exitCode = command.Parse("").Invoke();

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Validate_WithErrorsAndWarnings_PrintsBoth()
    {
        SetupProjectWithErrorsAndWarnings();

        var (_, stderr) = CaptureOutput(() =>
        {
            var command = DynaDocs.Commands.ValidateCommand.Create();
            return command.Parse("").Invoke();
        });

        Assert.Contains("Errors", stderr);
        Assert.Contains("Warnings", stderr);
    }

    private static (string stdout, string stderr) CaptureOutput(Func<int> action)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errWriter);
        try
        {
            action();
            return (outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private void SetupValidProjectNoWarnings()
    {
        // Minimal valid dydo.json with no roles → no warnings
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"),
            """{"version":1,"structure":{"root":"dydo","tasks":"project/tasks","issues":"project/issues"},"paths":{"source":[],"tests":[],"pathSets":null},"agents":{"pool":[],"assignments":{}},"integrations":{"claude":false},"dispatch":{"launchInTab":false,"autoClose":false},"tasks":{"autoCompactInterval":20},"frameworkHashes":{}}""");
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo", "_system"));
    }

    private void SetupProjectWithWarningsOnly()
    {
        SetupValidProjectNoWarnings();

        // Add a custom role with no denialHint → warning
        var rolesDir = Path.Combine(_testDir, "dydo", "_system", "roles");
        Directory.CreateDirectory(rolesDir);
        File.WriteAllText(Path.Combine(rolesDir, "custom.role.json"),
            """{"name":"custom","description":"test custom role","base":false,"writablePaths":["custom/**"],"readOnlyPaths":[],"templateFile":"mode-custom.template.md","denialHint":null,"constraints":[]}""");
    }

    private void SetupProjectWithErrorsAndWarnings()
    {
        // Write invalid dydo.json → error
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "not valid json");

        // Add a custom role with no denialHint → warning
        var rolesDir = Path.Combine(_testDir, "dydo", "_system", "roles");
        Directory.CreateDirectory(rolesDir);
        File.WriteAllText(Path.Combine(rolesDir, "custom.role.json"),
            """{"name":"custom","description":"test custom role","base":false,"writablePaths":["custom/**"],"readOnlyPaths":[],"templateFile":"mode-custom.template.md","denialHint":null,"constraints":[]}""");
    }
}
