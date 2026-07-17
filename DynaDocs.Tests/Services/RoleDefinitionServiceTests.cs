namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class RoleDefinitionServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly RoleDefinitionService _service;

    public RoleDefinitionServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-roledef-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _service = new RoleDefinitionService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private string CreateRolesDir()
    {
        var rolesDir = Path.Combine(_testDir, "dydo", "_system", "roles");
        Directory.CreateDirectory(rolesDir);
        return rolesDir;
    }

    private void WriteRoleFile(string rolesDir, RoleDefinition role)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(role,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        File.WriteAllText(Path.Combine(rolesDir, $"{role.Name}.role.json"), json);
    }

    #region LoadRoleDefinitions

    [Fact]
    public void LoadRoleDefinitions_ReadsAllJsonFiles()
    {
        var rolesDir = CreateRolesDir();
        var role1 = new RoleDefinition
        {
            Name = "test-role-1", Description = "Test 1", Base = true,
            WritablePaths = ["src/**"], ReadOnlyPaths = ["docs/**"],
            TemplateFile = "mode-test.template.md"
        };
        var role2 = new RoleDefinition
        {
            Name = "test-role-2", Description = "Test 2", Base = false,
            WritablePaths = ["lib/**"], ReadOnlyPaths = ["**"],
            TemplateFile = "mode-test2.template.md"
        };
        WriteRoleFile(rolesDir, role1);
        WriteRoleFile(rolesDir, role2);

        var roles = _service.LoadRoleDefinitions(_testDir);

        Assert.Equal(2, roles.Count);
        Assert.Contains(roles, r => r.Name == "test-role-1");
        Assert.Contains(roles, r => r.Name == "test-role-2");
    }

    [Fact]
    public void LoadRoleDefinitions_EmptyDirectory_ReturnsEmpty()
    {
        CreateRolesDir();

        var roles = _service.LoadRoleDefinitions(_testDir);

        Assert.Empty(roles);
    }

    [Fact]
    public void LoadRoleDefinitions_NoDirectory_ReturnsEmpty()
    {
        var roles = _service.LoadRoleDefinitions(_testDir);

        Assert.Empty(roles);
    }

    #endregion

    #region BuildPermissionMap

    [Fact]
    public void BuildPermissionMap_ReturnsAllBaseRoles()
    {
        var pathSets = new Dictionary<string, List<string>>
        {
            ["source"] = ["Commands/**", "Services/**"],
            ["tests"] = ["DynaDocs.Tests/**"]
        };
        var roles = RoleDefinitionService.GetBaseRoleDefinitions();
        var result = _service.BuildPermissionMap(roles, pathSets);

        Assert.Equal(8, result.Count);
        Assert.Contains("code-writer", result.Keys);
        Assert.Contains("chief-of-staff", result.Keys);
        Assert.Contains("reviewer", result.Keys);
        Assert.Contains("orchestrator", result.Keys);
        Assert.Contains("planner", result.Keys);
    }

    [Fact]
    public void BuildPermissionMap_ExpandsPathSetsCorrectly()
    {
        var pathSets = new Dictionary<string, List<string>>
        {
            ["source"] = ["src/**"],
            ["tests"] = ["tests/**"]
        };
        var roles = RoleDefinitionService.GetBaseRoleDefinitions();
        var result = _service.BuildPermissionMap(roles, pathSets);

        var (writable, readOnly) = result["code-writer"];
        Assert.Contains("src/**", writable);
        Assert.Contains("tests/**", writable);
        Assert.Contains("dydo/**", readOnly);
    }

    #endregion

    #region Path Set Resolution

    [Fact]
    public void ExpandPathSets_ExpandsSourceAndTests()
    {
        var pathSets = new Dictionary<string, List<string>>
        {
            ["source"] = ["src/**", "lib/**"],
            ["tests"] = ["tests/**"]
        };
        var roles = new List<RoleDefinition>
        {
            new()
            {
                Name = "test-role", Description = "test", Base = true,
                WritablePaths = ["{source}", "{tests}", "extra/**"],
                ReadOnlyPaths = ["docs/**"],
                TemplateFile = "t.md"
            }
        };

        var map = _service.BuildPermissionMap(roles, pathSets);
        var (writable, _) = map["test-role"];

        Assert.Equal(["src/**", "lib/**", "tests/**", "extra/**"], writable);
    }

    [Fact]
    public void ExpandPathSets_CustomPathSets()
    {
        var pathSets = new Dictionary<string, List<string>>
        {
            ["source"] = ["src/**"],
            ["tests"] = ["tests/**"],
            ["infra"] = ["terraform/**", "k8s/**"]
        };
        var roles = new List<RoleDefinition>
        {
            new()
            {
                Name = "infra-writer", Description = "test", Base = false,
                WritablePaths = ["{infra}", "config/**"],
                ReadOnlyPaths = ["{source}"],
                TemplateFile = "t.md"
            }
        };

        var map = _service.BuildPermissionMap(roles, pathSets);
        var (writable, readOnly) = map["infra-writer"];

        Assert.Equal(["terraform/**", "k8s/**", "config/**"], writable);
        Assert.Equal(["src/**"], readOnly);
    }

    [Fact]
    public void ExpandPathSets_UnresolvedSetLeftAsIs()
    {
        var pathSets = new Dictionary<string, List<string>>
        {
            ["source"] = ["src/**"]
        };
        var roles = new List<RoleDefinition>
        {
            new()
            {
                Name = "test-role", Description = "test", Base = true,
                WritablePaths = ["{nonexistent}"],
                ReadOnlyPaths = [],
                TemplateFile = "t.md"
            }
        };

        var map = _service.BuildPermissionMap(roles, pathSets);
        var (writable, _) = map["test-role"];

        Assert.Equal(["{nonexistent}"], writable);
    }

    #endregion

    #region ResolvePathSets

    [Fact]
    public void ResolvePathSets_UsesPathSetsWhenPresent()
    {
        var config = new DydoConfig
        {
            Paths = new PathsConfig
            {
                Source = ["src/**"],
                Tests = ["tests/**"],
                PathSets = new Dictionary<string, List<string>>
                {
                    ["source"] = ["custom-src/**"],
                    ["tests"] = ["custom-tests/**"],
                    ["docs"] = ["docs/**"]
                }
            }
        };

        var result = _service.ResolvePathSets(config);

        Assert.Equal(["custom-src/**"], result["source"]);
        Assert.Equal(["custom-tests/**"], result["tests"]);
        Assert.Equal(["docs/**"], result["docs"]);
    }

    [Fact]
    public void ResolvePathSets_FallsBackToSourceAndTests()
    {
        var config = new DydoConfig
        {
            Paths = new PathsConfig
            {
                Source = ["Commands/**", "Services/**"],
                Tests = ["DynaDocs.Tests/**"]
            }
        };

        var result = _service.ResolvePathSets(config);

        Assert.Equal(["Commands/**", "Services/**"], result["source"]);
        Assert.Equal(["DynaDocs.Tests/**"], result["tests"]);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ResolvePathSets_NullConfig_UsesDefaults()
    {
        var result = _service.ResolvePathSets(null);

        Assert.Equal(["src/**"], result["source"]);
        Assert.Equal(["tests/**"], result["tests"]);
    }

    #endregion

    #region WriteBaseRoleDefinitions

    [Fact]
    public void WriteBaseRoleDefinitions_CreatesCorrectFiles()
    {
        _service.WriteBaseRoleDefinitions(_testDir);

        var rolesDir = Path.Combine(_testDir, "dydo", "_system", "roles");
        var files = Directory.GetFiles(rolesDir, "*.role.json");

        // Seven claimable Tier-1 roles (chief-of-staff joined per Decision 026 §3).
        // planner is skill-only (no role file); inquisitor/judge are retired (Decision 024);
        // sprint-auditor folded into the reviewer (DR-039).
        Assert.Equal(7, files.Length);
        Assert.Contains(files, f => Path.GetFileName(f) == "code-writer.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "reviewer.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "co-thinker.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "chief-of-staff.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "docs-writer.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "test-writer.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "orchestrator.role.json");
        Assert.DoesNotContain(files, f => Path.GetFileName(f) == "planner.role.json");
        Assert.DoesNotContain(files, f => Path.GetFileName(f) == "inquisitor.role.json");
        Assert.DoesNotContain(files, f => Path.GetFileName(f) == "judge.role.json");
    }

    [Fact]
    public void WriteBaseRoleDefinitions_RoundTripsCorrectly()
    {
        _service.WriteBaseRoleDefinitions(_testDir);

        var loaded = _service.LoadRoleDefinitions(_testDir);
        // Non-claimable roles (planner) are not written to disk, so
        // compare against the claimable subset only.
        var original = RoleDefinitionService.GetBaseRoleDefinitions()
            .Where(r => !RoleDefinitionService.NonClaimableRoles.Contains(r.Name))
            .ToList();

        Assert.Equal(original.Count, loaded.Count);

        foreach (var orig in original)
        {
            var match = loaded.Single(r => r.Name == orig.Name);
            Assert.Equal(orig.Description, match.Description);
            Assert.Equal(orig.Base, match.Base);
            Assert.Equal(orig.WritablePaths, match.WritablePaths);
            Assert.Equal(orig.ReadOnlyPaths, match.ReadOnlyPaths);
            Assert.Equal(orig.TemplateFile, match.TemplateFile);
            Assert.Equal(orig.DenialHint, match.DenialHint);
        }
    }

    #endregion

    #region ValidateRoleDefinition

    [Fact]
    public void ValidateRoleDefinition_ValidRole_Passes()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test role", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "mode-test.template.md"
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.True(valid);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRoleDefinition_MissingName_Fails()
    {
        var role = new RoleDefinition
        {
            Name = "", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md"
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("name"));
    }

    [Fact]
    public void ValidateRoleDefinition_MissingTemplateFile_Fails()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = ""
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("Template file"));
    }

    [Fact]
    public void ValidateRoleDefinition_EmptyWritablePaths_IsValidReadOnlyRole()
    {
        // No writable paths is valid: it is how a read-only role (e.g. reviewer) is expressed,
        // compiled by dydo sync into a no-Edit/Write tool profile.
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = [], ReadOnlyPaths = [],
            TemplateFile = "t.md"
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.True(valid);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRoleDefinition_AllBaseRoles_AreValid()
    {
        foreach (var role in RoleDefinitionService.GetBaseRoleDefinitions())
        {
            var valid = _service.ValidateRoleDefinition(role, out var errors);
            Assert.True(valid, $"Base role '{role.Name}' failed validation: {string.Join(", ", errors)}");
        }
    }

    [Fact]
    public void WriteBaseRoleDefinitions_RoundTrips_CanOrchestrate()
    {
        _service.WriteBaseRoleDefinitions(_testDir);

        var loaded = _service.LoadRoleDefinitions(_testDir);

        var orchestrator = loaded.Single(r => r.Name == "orchestrator");
        Assert.True(orchestrator.CanOrchestrate);

        var codeWriter = loaded.Single(r => r.Name == "code-writer");
        Assert.False(codeWriter.CanOrchestrate);
    }

    [Theory]
    [InlineData("code-writer")]
    [InlineData("co-thinker")]
    [InlineData("orchestrator")]
    public void GetBaseRoleDefinitions_BacklogWriters_CanWriteBacklog(string roleName)
    {
        var roles = RoleDefinitionService.GetBaseRoleDefinitions();
        var role = roles.Single(r => r.Name == roleName);
        Assert.Contains("dydo/project/backlog/**", role.WritablePaths);
    }

    [Fact]
    public void GetBaseRoleDefinitions_DropsRetiredRoles()
    {
        var names = RoleDefinitionService.GetBaseRoleDefinitions().Select(r => r.Name).ToList();
        Assert.DoesNotContain("inquisitor", names);
        Assert.DoesNotContain("judge", names);
    }

    [Fact]
    public void GetBaseRoleDefinitions_Planner_IsSkillOnly()
    {
        var roles = RoleDefinitionService.GetBaseRoleDefinitions();
        // planner stays in the base definitions to drive skill generation...
        Assert.Contains(roles, r => r.Name == "planner");
        // ...but is flagged skill-only, so it is not written as a claimable role file.
        Assert.Contains("planner", RoleDefinitionService.SkillOnlyRoles);
    }

    [Fact]
    public void GetBaseRoleDefinitions_ChiefOfStaff_IsClaimableManager()
    {
        // Decision 026 §3: chief-of-staff is a claimable Tier-1 manager mode like
        // orchestrator/co-thinker — PM-object writable surface, never source or tests.
        var chief = RoleDefinitionService.GetBaseRoleDefinitions().Single(r => r.Name == "chief-of-staff");

        Assert.DoesNotContain("chief-of-staff", RoleDefinitionService.NonClaimableRoles);
        Assert.Equal("mode-chief-of-staff.template.md", chief.TemplateFile);
        Assert.DoesNotContain("{source}", chief.WritablePaths);
        Assert.DoesNotContain("{tests}", chief.WritablePaths);
        Assert.Contains("dydo/project/backlog/**", chief.WritablePaths);
        // Never in an approval path — it must not be a CanOrchestrate verdict-CC target.
        Assert.False(chief.CanOrchestrate);
    }

    #endregion

}
