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
            : """{"version":1,"structure":{"root":"dydo"},"paths":{"source":["src/**"],"tests":["tests/**"]},"agents":{"pool":[],"assignments":{}},"integrations":{"claude":true},"dispatch":{"launchInTab":false,"autoClose":false}}""";
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), json);
    }

    private string CreateRolesDir()
    {
        var rolesDir = Path.Combine(_testDir, "dydo", "_system", "roles");
        Directory.CreateDirectory(rolesDir);
        return rolesDir;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private void WriteRoleFile(string rolesDir, RoleDefinition role)
    {
        var json = JsonSerializer.Serialize(role, JsonOptions);
        File.WriteAllText(Path.Combine(rolesDir, $"{role.Name}.role.json"), json);
    }

    private void CreateTemplateFile(string name)
    {
        var dir = Path.Combine(_testDir, "Templates");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name), "# Template\n");
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

    #region ValidateSystem — Role files

    [Fact]
    public void ValidateSystem_InvalidJsonInRoleFile_ReportsError()
    {
        CreateDydoJson();
        var rolesDir = CreateRolesDir();
        File.WriteAllText(Path.Combine(rolesDir, "broken.role.json"), "not json");

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("Invalid JSON"));
    }

    [Fact]
    public void ValidateSystem_UnknownConstraintType_ReportsError()
    {
        CreateDydoJson();
        var rolesDir = CreateRolesDir();
        var role = new RoleDefinition
        {
            Name = "test-role", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "mode-test-role.template.md",
            Constraints = [new RoleConstraint { Type = "invalid-type", Message = "test" }]
        };
        WriteRoleFile(rolesDir, role);
        CreateTemplateFile("mode-test-role.template.md");

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("invalid-type"));
    }

    [Fact]
    public void ValidateSystem_UnresolvablePathSetReference_ReportsWarning()
    {
        CreateDydoJson();
        var rolesDir = CreateRolesDir();
        var role = new RoleDefinition
        {
            Name = "test-role", Description = "Test", Base = false,
            WritablePaths = ["{nonexistent}"], ReadOnlyPaths = [],
            TemplateFile = "mode-test-role.template.md"
        };
        WriteRoleFile(rolesDir, role);
        CreateTemplateFile("mode-test-role.template.md");

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "warning" && i.Message.Contains("nonexistent"));
    }

    [Fact]
    public void ValidateSystem_MissingTemplateFile_ReportsWarning()
    {
        CreateDydoJson();
        var rolesDir = CreateRolesDir();
        var role = new RoleDefinition
        {
            Name = "test-role", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "mode-does-not-exist.template.md"
        };
        WriteRoleFile(rolesDir, role);

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "warning" && i.Message.Contains("does-not-exist"));
    }

    [Fact]
    public void ValidateSystem_ValidRoleFile_NoErrors()
    {
        CreateDydoJson();
        var rolesDir = CreateRolesDir();
        var role = new RoleDefinition
        {
            Name = "deploy-manager", Description = "Manages deployments", Base = false,
            WritablePaths = ["src/**", "dydo/agents/{self}/**"], ReadOnlyPaths = [],
            TemplateFile = "mode-deploy-manager.template.md",
            DenialHint = "Deploy-manager can only edit source and own workspace."
        };
        WriteRoleFile(rolesDir, role);
        CreateTemplateFile("mode-deploy-manager.template.md");

        var issues = _service.ValidateSystem(_testDir);

        var errors = issues.Where(i => i.Severity == "error").ToList();
        Assert.Empty(errors);
    }

    #endregion

    #region ValidateRoleFile

    [Fact]
    public void ValidateRoleFile_ReturnsIssuesForSingleFile()
    {
        CreateDydoJson();
        var rolesDir = CreateRolesDir();
        var filePath = Path.Combine(rolesDir, "bad.role.json");
        File.WriteAllText(filePath, "invalid json");

        var issues = _service.ValidateRoleFile(_testDir, filePath);

        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("Invalid JSON"));
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

    #region ValidateSystem — Unreadable role file

    [Fact]
    public void ValidateRoleFile_UnreadableFile_ReportsError()
    {
        CreateDydoJson();
        var rolesDir = CreateRolesDir();
        // Point at a directory instead of a file to trigger a read exception
        var dirAsFile = Path.Combine(rolesDir, "fake.role.json");
        Directory.CreateDirectory(dirAsFile);

        var issues = _service.ValidateRoleFile(_testDir, dirAsFile);

        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("Cannot read file"));
    }

    #endregion

    #region ValidateSystem — Null role deserialization

    [Fact]
    public void ValidateRoleFile_NullDeserialization_ReportsError()
    {
        CreateDydoJson();
        var rolesDir = CreateRolesDir();
        var filePath = Path.Combine(rolesDir, "nullrole.role.json");
        File.WriteAllText(filePath, "null");

        var issues = _service.ValidateRoleFile(_testDir, filePath);

        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("null"));
    }

    #endregion

    #region ValidateSystem — Empty constraint message

    [Fact]
    public void ValidateSystem_ConstraintWithEmptyMessage_ReportsError()
    {
        CreateDydoJson();
        var rolesDir = CreateRolesDir();
        var role = new RoleDefinition
        {
            Name = "empty-msg-role", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "mode-empty-msg-role.template.md",
            DenialHint = "hint",
            Constraints = [new RoleConstraint { Type = "must-read-all", Message = "" }]
        };
        WriteRoleFile(rolesDir, role);
        CreateTemplateFile("mode-empty-msg-role.template.md");

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("empty message"));
    }

    #endregion

    #region ValidateSystem — Agent state parse failure

    [Fact]
    public void ValidateSystem_MalformedAgentState_ReportsError()
    {
        CreateDydoJson();
        var agentDir = Path.Combine(_testDir, "dydo", "agents", "BadAgent");
        Directory.CreateDirectory(agentDir);
        // Write a state.md that will fail to parse (no valid frontmatter)
        File.WriteAllText(Path.Combine(agentDir, "state.md"), "not valid frontmatter at all");

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.File.Contains("BadAgent") && i.Severity == "error");
    }

    #endregion

    #region Custom role DenialHint warning

    [Fact]
    public void ValidateSystem_CustomRoleNoDenialHint_ReportsWarning()
    {
        CreateDydoJson();
        var rolesDir = CreateRolesDir();
        var role = new RoleDefinition
        {
            Name = "no-hint-role", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "mode-no-hint-role.template.md",
            DenialHint = null
        };
        WriteRoleFile(rolesDir, role);
        CreateTemplateFile("mode-no-hint-role.template.md");

        var issues = _service.ValidateSystem(_testDir);

        Assert.Contains(issues, i => i.Severity == "warning" && i.Message.Contains("denialHint"));
    }

    #endregion
}
