namespace DynaDocs.Tests.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Services;

public class ValidationServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly ValidationService _service;

    public ValidationServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-validation-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _service = new ValidationService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private void CreateDydoJson(object? config = null)
    {
        var json = config != null
            ? JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            : """{"version":1,"structure":{"root":"dydo"},"paths":{"source":["src/**"],"tests":["tests/**"]},"integrations":{"claude":true},"dispatch":{"launchInTab":false,"autoClose":false}}""";
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), json);
    }

    #region ValidateSystem — dydo.json

    [Fact]
    public void ValidateSystem_MissingDydoJson_ReportsError()
    {
        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("not found"));
    }

    [Fact]
    public void ValidateSystem_InvalidJsonInDydoJson_ReportsError()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "{not valid json}");

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "error" && i.File == "dydo.json" && i.Message.Contains("Invalid JSON"));
    }

    [Fact]
    public void ValidateSystem_ValidDydoJson_NoErrors()
    {
        CreateDydoJson();

        var issues = _service.ValidateSystem(_testDir);

        Assert.DoesNotContain(issues, i => i.File == "dydo.json");
    }

    #endregion

    #region ValidateSystem — Exit code semantics

    [Fact]
    public void ValidateSystem_CleanProject_ReturnsNoErrors()
    {
        CreateDydoJson();

        var issues = _service.ValidateSystem(_testDir);
        var errors = issues.Where(i => i.Severity == "error").ToList();

        Assert.Empty(errors);
    }

    #endregion

    #region ValidateSystem — Null deserialization

    [Fact]
    public void ValidateSystem_DydoJsonDeserializesToNull_ReportsError()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "null");

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("deserialize"));
    }

    #endregion

    #region Nudge Validation

    [Fact]
    public void ValidateSystem_NudgeWithInvalidRegex_ReportsError()
    {
        CreateDydoJson(new
        {
            version = 1, structure = new { root = "dydo" },
            paths = new { source = new[] { "src/**" }, tests = new[] { "tests/**" } },
            agents = new { pool = Array.Empty<string>(), assignments = new Dictionary<string, string[]>() },
            nudges = new[] { new { pattern = "[invalid(regex", message = "test", severity = "block" } }
        });

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("invalid regex"));
    }

    [Fact]
    public void ValidateSystem_NudgeWithEmptyPattern_ReportsError()
    {
        CreateDydoJson(new
        {
            version = 1, structure = new { root = "dydo" },
            paths = new { source = new[] { "src/**" }, tests = new[] { "tests/**" } },
            agents = new { pool = Array.Empty<string>(), assignments = new Dictionary<string, string[]>() },
            nudges = new[] { new { pattern = "", message = "test", severity = "block" } }
        });

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("empty pattern"));
    }

    [Fact]
    public void ValidateSystem_NudgeWithEmptyMessage_ReportsError()
    {
        CreateDydoJson(new
        {
            version = 1, structure = new { root = "dydo" },
            paths = new { source = new[] { "src/**" }, tests = new[] { "tests/**" } },
            agents = new { pool = Array.Empty<string>(), assignments = new Dictionary<string, string[]>() },
            nudges = new[] { new { pattern = "test.*pattern", message = "", severity = "block" } }
        });

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("empty message"));
    }

    [Fact]
    public void ValidateSystem_NudgeWithInvalidSeverity_ReportsError()
    {
        CreateDydoJson(new
        {
            version = 1, structure = new { root = "dydo" },
            paths = new { source = new[] { "src/**" }, tests = new[] { "tests/**" } },
            agents = new { pool = Array.Empty<string>(), assignments = new Dictionary<string, string[]>() },
            nudges = new[] { new { pattern = "test", message = "test", severity = "invalid" } }
        });

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("invalid severity"));
    }

    [Fact]
    public void ValidateSystem_NudgeWithValidConfig_NoNudgeErrors()
    {
        CreateDydoJson(new
        {
            version = 1, structure = new { root = "dydo" },
            paths = new { source = new[] { "src/**" }, tests = new[] { "tests/**" } },
            agents = new { pool = Array.Empty<string>(), assignments = new Dictionary<string, string[]>() },
            nudges = new[] { new { pattern = @"dotnet test.*coverlet", message = "Use gap_check.py instead.", severity = "warn" } }
        });

        var issues = _service.ValidateSystem(_testDir);

        Assert.DoesNotContain(issues, i => i.Message.Contains("Nudge"));
    }

    [Fact]
    public void ValidateSystem_ToolScopedNoticeNudge_NoNudgeErrors()
    {
        // Decision 026 §4 shipped nudge: glob pattern (not a valid-regex concern),
        // tools list, "notice" severity — all must validate clean.
        CreateDydoJson(new
        {
            version = 1, structure = new { root = "dydo" },
            paths = new { source = new[] { "src/**" }, tests = new[] { "tests/**" } },
            agents = new { pool = Array.Empty<string>(), assignments = new Dictionary<string, string[]>() },
            nudges = new[] { new { pattern = "{source}|{tests}", message = "Delegate to a workflow.", severity = "notice", tools = new[] { "Edit", "Write", "NotebookEdit" } } }
        });

        var issues = _service.ValidateSystem(_testDir);

        Assert.DoesNotContain(issues, i => i.Message.Contains("Nudge"));
    }

    #endregion
}
