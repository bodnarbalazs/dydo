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
            : """{"version":1,"structure":{"root":"dydo"},"integrations":{"claude":true},"dispatch":{"launchInTab":false,"autoClose":false}}""";
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), json);
    }

    #region ValidateSystem - dydo.json

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

    #region ValidateSystem - Exit code semantics

    [Fact]
    public void ValidateSystem_CleanProject_ReturnsNoErrors()
    {
        CreateDydoJson();

        var issues = _service.ValidateSystem(_testDir);
        var errors = issues.Where(i => i.Severity == "error").ToList();

        Assert.Empty(errors);
    }

    #endregion

    #region ValidateSystem - Null deserialization

    [Fact]
    public void ValidateSystem_DydoJsonScalar_ReportsRootShapeError()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "null");

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("root must be an object"));
    }

    #endregion

    #region Nudge Validation

    [Fact]
    public void ValidateSystem_NudgeWithInvalidRegex_ReportsError()
    {
        CreateDydoJson(new
        {
            version = 1, structure = new { root = "dydo" },
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
            agents = new { pool = Array.Empty<string>(), assignments = new Dictionary<string, string[]>() },
            nudges = new[] { new { pattern = @"dotnet test.*coverlet", message = "Use gap_check.py instead.", severity = "warn" } }
        });

        var issues = _service.ValidateSystem(_testDir);

        Assert.DoesNotContain(issues, i => i.Message.Contains("Nudge"));
    }

    [Fact]
    public void ValidateSystem_Nudge_NormalizesAudience()
    {
        CreateDydoJson(new
        {
            version = 1, structure = new { root = "dydo" },
            nudges = new[] { new { pattern = "src/**", message = "test", severity = "notice", audience = "WORKER" } }
        });

        var issues = _service.ValidateSystem(_testDir);

        Assert.DoesNotContain(issues, i => i.Message.Contains("audience"));
    }

    [Fact]
    public void ValidateSystem_NudgeWithUnknownAudience_ReportsError()
    {
        CreateDydoJson(new
        {
            version = 1, structure = new { root = "dydo" },
            nudges = new[] { new { pattern = "src/**", message = "test", severity = "notice", audience = "team" } }
        });

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Message.Contains("invalid audience"));
    }

    [Fact]
    public void ValidateSystem_NudgeWithNullAudience_ReportsError()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {"version":1,"structure":{"root":"dydo"},"nudges":[{"pattern":"src/**","message":"test","severity":"notice","audience":null}]}
            """);

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Message.Contains("invalid audience"));
    }

    [Theory]
    [InlineData("new-file")]
    [InlineData("command")]
    [InlineData("NEW-FILE")]
    public void ValidateSystem_NudgeWithKnownMatch_NoNudgeErrors(string match)
    {
        CreateDydoJson(new
        {
            version = 1, structure = new { root = "dydo" },
            nudges = new[] { new { pattern = "^tools/(?!one-off/)", message = "Use tools/one-off/.", severity = "warn", match } }
        });

        var issues = _service.ValidateSystem(_testDir);

        Assert.DoesNotContain(issues, i => i.Message.Contains("Nudge"));
    }

    [Theory]
    [InlineData("path")]
    [InlineData("")]
    public void ValidateSystem_NudgeWithUnknownMatch_ReportsError(string match)
    {
        CreateDydoJson(new
        {
            version = 1, structure = new { root = "dydo" },
            nudges = new[] { new { pattern = "^tools/", message = "test", severity = "warn", match } }
        });

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains($"Nudge [0] has invalid match '{match}'"));
    }

    [Fact]
    public void ValidateSystem_NewFileNudgeWithInvalidRegex_ReportsError()
    {
        CreateDydoJson(new
        {
            version = 1, structure = new { root = "dydo" },
            nudges = new[] { new { pattern = "^tools/(", message = "test", severity = "block", match = "new-file" } }
        });

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("invalid regex"));
    }

    #endregion

    #region ValidateSystem - Claude guard wiring

    private const string ClaudeSettings = ".claude/settings.json";

    private const string Rejoin = "Claude agents run without the dydo guard: {0}. Run 'dydo init claude --join'.";

    private const string FixJson =
        "Claude agents run without the dydo guard: {0}. Fix the JSON, then run 'dydo init claude --join'.";

    private const string WiredSettings = """
        {"hooks":{"PreToolUse":[
          {"matcher":"Bash","hooks":[{"type":"command","command":"echo before"}]},
          {"matcher":"Anything","hooks":[{"type":"command","command":"echo mixed"},{"type":"command","command":"dydo guard --host claude"}]}
        ]}}
        """;

    private void WriteClaudeSettings(string fileName, string content)
    {
        Directory.CreateDirectory(Path.Combine(_testDir, ".claude"));
        File.WriteAllText(Path.Combine(_testDir, ".claude", fileName), content);
    }

    private List<ValidationIssue> ClaudeIssues() =>
        _service.ValidateSystem(_testDir).Where(i => i.File == ClaudeSettings).ToList();

    [Theory]
    [InlineData(WiredSettings)]
    [InlineData("\uFEFF" + WiredSettings)]
    public void ValidateSystem_ClaudeRecordedWithGuardWired_ReportsNothing(string settings)
    {
        CreateDydoJson();
        WriteClaudeSettings("settings.json", settings);

        Assert.Empty(_service.ValidateSystem(_testDir));
    }

    [Theory]
    [InlineData("{}", "it has no PreToolUse hook running 'dydo guard --host claude'")]
    [InlineData("""{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"dydo guard"}]}]}}""",
        "it has no PreToolUse hook running 'dydo guard --host claude'")]
    [InlineData("""{"hooks":{"PostToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"dydo guard --host claude"}]}]}}""",
        "it has no PreToolUse hook running 'dydo guard --host claude'")]
    [InlineData("""{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":" dydo guard --host claude"}]}]}}""",
        "it has no PreToolUse hook running 'dydo guard --host claude'")]
    [InlineData("""{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":42}]}]}}""",
        "it has no PreToolUse hook running 'dydo guard --host claude'")]
    [InlineData("""{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":["dydo guard --host claude"]}]}}""",
        "it has no PreToolUse hook running 'dydo guard --host claude'")]
    [InlineData("""{"hooks":{"PreToolUse":["dydo guard --host claude",{"matcher":"Bash"}]}}""",
        "it has no PreToolUse hook running 'dydo guard --host claude'")]
    [InlineData("""{"hooks":{"PreToolUse":{"matcher":"Bash"}}}""",
        "it has no PreToolUse hook running 'dydo guard --host claude'")]
    [InlineData("""{"hooks":[]}""", "it has no PreToolUse hook running 'dydo guard --host claude'")]
    public void ValidateSystem_ClaudeRecordedWithoutGuardHook_WarnsWithRejoin(string settings, string reason)
    {
        CreateDydoJson();
        WriteClaudeSettings("settings.json", settings);

        var issue = Assert.Single(ClaudeIssues());

        Assert.Equal("warning", issue.Severity);
        Assert.Equal(string.Format(Rejoin, reason), issue.Message);
    }

    [Fact]
    public void ValidateSystem_ClaudeRecordedWithoutSettingsFile_WarnsWithRejoin()
    {
        CreateDydoJson();

        var issue = Assert.Single(_service.ValidateSystem(_testDir));

        Assert.Equal("warning", issue.Severity);
        Assert.Equal(ClaudeSettings, issue.File);
        Assert.Equal(string.Format(Rejoin, "the file is missing"), issue.Message);
    }

    [Fact]
    public void ValidateSystem_ClaudeGuardOnlyInLocalSettings_WarnsWithRejoin()
    {
        CreateDydoJson();
        WriteClaudeSettings("settings.local.json", WiredSettings);

        var issue = Assert.Single(ClaudeIssues());

        Assert.Equal(string.Format(Rejoin, "the file is missing"), issue.Message);
    }

    [Theory]
    [InlineData("{not valid json", "the file is not valid JSON")]
    [InlineData("", "the file is not valid JSON")]
    [InlineData("""{"hooks":{},"hooks":{}}""", "the file is not valid JSON")]
    [InlineData("""{"hooks":{"PreToolUse":[{"hooks":[{"command":"dydo guard --host claude","command":"x"}]}]}}""",
        "the file is not valid JSON")]
    [InlineData("[]", "the file is not a JSON object")]
    [InlineData("null", "the file is not a JSON object")]
    [InlineData("\"dydo guard --host claude\"", "the file is not a JSON object")]
    public void ValidateSystem_ClaudeSettingsMalformedOrNotObject_WarnsToFixJsonFirst(string settings, string reason)
    {
        CreateDydoJson();
        WriteClaudeSettings("settings.json", settings);

        var issue = Assert.Single(ClaudeIssues());

        Assert.Equal("warning", issue.Severity);
        Assert.Equal(string.Format(FixJson, reason), issue.Message);
    }

    [Theory]
    [InlineData("""{"version":1,"structure":{"root":"dydo"},"integrations":{"claude":false,"codex":true}}""")]
    [InlineData("""{"version":1,"structure":{"root":"dydo"},"integrations":{"codex":true}}""")]
    [InlineData("""{"version":1,"structure":{"root":"dydo"}}""")]
    public void ValidateSystem_ClaudeNotRecorded_ChecksNoClaudeSettings(string config)
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), config);

        Assert.Empty(_service.ValidateSystem(_testDir));
    }

    [Fact]
    public void ValidateSystem_InvalidDydoJson_ChecksNoClaudeSettings()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """{"integrations":{"claude":true},"testing":{"runner":[]}}""");

        var issues = _service.ValidateSystem(_testDir);

        Assert.NotEmpty(issues);
        Assert.All(issues, i => Assert.Equal("dydo.json", i.File));
    }

    #endregion
}
