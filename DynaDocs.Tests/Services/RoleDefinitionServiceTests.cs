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
    public void BuildPermissionMap_ReturnsAllNineRoles()
    {
        var pathSets = new Dictionary<string, List<string>>
        {
            ["source"] = ["Commands/**", "Services/**"],
            ["tests"] = ["DynaDocs.Tests/**"]
        };
        var roles = RoleDefinitionService.GetBaseRoleDefinitions();
        var result = _service.BuildPermissionMap(roles, pathSets);

        Assert.Equal(9, result.Count);
        Assert.Contains("code-writer", result.Keys);
        Assert.Contains("reviewer", result.Keys);
        Assert.Contains("orchestrator", result.Keys);
        Assert.Contains("judge", result.Keys);
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
        Assert.Contains("dydo/agents/{self}/**", writable);
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
                WritablePaths = ["{infra}", "dydo/agents/{self}/**"],
                ReadOnlyPaths = ["{source}"],
                TemplateFile = "t.md"
            }
        };

        var map = _service.BuildPermissionMap(roles, pathSets);
        var (writable, readOnly) = map["infra-writer"];

        Assert.Equal(["terraform/**", "k8s/**", "dydo/agents/{self}/**"], writable);
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

        Assert.Equal(9, files.Length);
        Assert.Contains(files, f => Path.GetFileName(f) == "code-writer.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "reviewer.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "co-thinker.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "docs-writer.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "planner.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "test-writer.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "orchestrator.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "inquisitor.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "judge.role.json");
    }

    [Fact]
    public void WriteBaseRoleDefinitions_RoundTripsCorrectly()
    {
        _service.WriteBaseRoleDefinitions(_testDir);

        var loaded = _service.LoadRoleDefinitions(_testDir);
        var original = RoleDefinitionService.GetBaseRoleDefinitions();

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
            Assert.Equal(orig.Constraints.Count, match.Constraints.Count);
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
    public void ValidateRoleDefinition_EmptyWritablePaths_Fails()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = [], ReadOnlyPaths = [],
            TemplateFile = "t.md"
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("writable"));
    }

    [Fact]
    public void ValidateRoleDefinition_UnknownConstraintType_Fails()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            Constraints = [new RoleConstraint { Type = "bogus", Message = "test" }]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("bogus"));
    }

    [Fact]
    public void ValidateRoleDefinition_RoleTransitionMissingFromRole_Fails()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            Constraints = [new RoleConstraint { Type = "role-transition", Message = "test" }]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("fromRole"));
    }

    [Fact]
    public void ValidateRoleDefinition_RequiresDispatchMissingRequiredRoles_Fails()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            Constraints = [new RoleConstraint { Type = "requires-dispatch", Message = "test" }]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("requiredRoles"));
    }

    [Fact]
    public void ValidateRoleDefinition_RequiresDispatchWithRequiredRoles_Passes()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            Constraints = [new RoleConstraint
            {
                Type = "requires-dispatch",
                RequiredRoles = ["reviewer"],
                Message = "Must dispatch reviewer."
            }]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.True(valid);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRoleDefinition_RequiresPriorMissingRequiredRoles_Fails()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            Constraints = [new RoleConstraint { Type = "requires-prior", Message = "test" }]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("requiredRoles"));
    }

    [Fact]
    public void ValidateRoleDefinition_RequiresPriorEmptyRequiredRoles_Fails()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            Constraints = [new RoleConstraint { Type = "requires-prior", RequiredRoles = [], Message = "test" }]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("requiredRoles"));
    }

    [Fact]
    public void ValidateRoleDefinition_PanelLimitMissingMaxCount_Fails()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            Constraints = [new RoleConstraint { Type = "panel-limit", Message = "test" }]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("maxCount"));
    }

    [Fact]
    public void ValidateRoleDefinition_PanelLimitZeroMaxCount_Fails()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            Constraints = [new RoleConstraint { Type = "panel-limit", MaxCount = 0, Message = "test" }]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("maxCount"));
    }

    [Fact]
    public void ValidateConstraint_DispatchRestriction_RequiresTargetRole()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            Constraints = [new RoleConstraint
            {
                Type = "dispatch-restriction",
                RequiredRoles = ["code-writer"],
                Message = "test"
            }]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("targetRole"));
    }

    [Fact]
    public void ValidateConstraint_DispatchRestriction_RequiresRequiredRoles()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            Constraints = [new RoleConstraint
            {
                Type = "dispatch-restriction",
                TargetRole = "code-writer",
                Message = "test"
            }]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("requiredRoles"));
    }

    [Fact]
    public void ValidateConstraint_DispatchRestriction_ValidPasses()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            Constraints = [new RoleConstraint
            {
                Type = "dispatch-restriction",
                TargetRole = "code-writer",
                RequiredRoles = ["code-writer"],
                OnlyWhenDispatched = true,
                Message = "test"
            }]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.True(valid);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRoleDefinition_RequiresDispatchEmptyRequiredRoles_Fails()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            Constraints = [new RoleConstraint { Type = "requires-dispatch", RequiredRoles = [], Message = "test" }]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("requiredRoles"));
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

        var inquisitor = loaded.Single(r => r.Name == "inquisitor");
        Assert.True(inquisitor.CanOrchestrate);

        var judge = loaded.Single(r => r.Name == "judge");
        Assert.True(judge.CanOrchestrate);

        var codeWriter = loaded.Single(r => r.Name == "code-writer");
        Assert.False(codeWriter.CanOrchestrate);
    }

    [Fact]
    public void WriteBaseRoleDefinitions_RoundTrips_OnlyWhenDispatched()
    {
        _service.WriteBaseRoleDefinitions(_testDir);

        var loaded = _service.LoadRoleDefinitions(_testDir);

        var codeWriter = loaded.Single(r => r.Name == "code-writer");
        var dispatchConstraint = codeWriter.Constraints.Single(c => c.Type == "requires-dispatch");
        Assert.True(dispatchConstraint.OnlyWhenDispatched);
        Assert.Equal(["reviewer"], dispatchConstraint.RequiredRoles);
        Assert.True(dispatchConstraint.RequireAll);
    }

    [Fact]
    public void WriteBaseRoleDefinitions_RoundTrips_RequireAll()
    {
        _service.WriteBaseRoleDefinitions(_testDir);

        var loaded = _service.LoadRoleDefinitions(_testDir);

        var inquisitor = loaded.Single(r => r.Name == "inquisitor");
        var constraint = inquisitor.Constraints.Single(c => c.Type == "requires-dispatch");
        Assert.False(constraint.RequireAll);
        Assert.Equal(["judge", "inquisitor"], constraint.RequiredRoles);
    }

    [Fact]
    public void GetBaseRoleDefinitions_Judge_CanWriteInquisitions()
    {
        var roles = RoleDefinitionService.GetBaseRoleDefinitions();
        var judge = roles.Single(r => r.Name == "judge");
        Assert.Contains("dydo/project/inquisitions/**", judge.WritablePaths);
    }

    [Fact]
    public void GetBaseRoleDefinitions_Judge_CanWriteIssues()
    {
        var roles = RoleDefinitionService.GetBaseRoleDefinitions();
        var judge = roles.Single(r => r.Name == "judge");
        Assert.Contains("dydo/project/issues/**", judge.WritablePaths);
    }

    [Fact]
    public void WriteBaseRoleDefinitions_RoundTrips_JudgeWritablePaths()
    {
        _service.WriteBaseRoleDefinitions(_testDir);

        var loaded = _service.LoadRoleDefinitions(_testDir);
        var judge = loaded.Single(r => r.Name == "judge");

        Assert.Contains("dydo/project/inquisitions/**", judge.WritablePaths);
        Assert.Contains("dydo/project/issues/**", judge.WritablePaths);
    }

    [Fact]
    public void GetBaseRoleDefinitions_ReviewerDispatchRestriction_AllowsInquisitorDispatcher()
    {
        var roles = RoleDefinitionService.GetBaseRoleDefinitions();
        var reviewer = roles.Single(r => r.Name == "reviewer");
        var constraint = reviewer.Constraints.Single(c => c.Type == "dispatch-restriction");

        Assert.Contains("inquisitor", constraint.RequiredRoles!);
        Assert.Contains("code-writer", constraint.RequiredRoles!);
    }

    #endregion

    #region ConditionalMustReads

    [Fact]
    public void GetBaseRoleDefinitions_CodeWriter_HasConditionalMustReads()
    {
        var roles = RoleDefinitionService.GetBaseRoleDefinitions();
        var codeWriter = roles.Single(r => r.Name == "code-writer");

        Assert.Single(codeWriter.ConditionalMustReads);
        Assert.Equal(".merge-source", codeWriter.ConditionalMustReads[0].When!.MarkerExists);
        Assert.Contains("how-to-merge-worktrees.md", codeWriter.ConditionalMustReads[0].Path);
    }

    [Fact]
    public void GetBaseRoleDefinitions_Reviewer_HasConditionalMustReads()
    {
        var roles = RoleDefinitionService.GetBaseRoleDefinitions();
        var reviewer = roles.Single(r => r.Name == "reviewer");

        Assert.Equal(3, reviewer.ConditionalMustReads.Count);

        // Task name match for merge review
        var mergeEntry = reviewer.ConditionalMustReads.Single(c => c.Path.Contains("how-to-review-worktree-merges"));
        Assert.Equal("*-merge", mergeEntry.When!.TaskNameMatches);

        // Always-apply task file
        var taskEntry = reviewer.ConditionalMustReads.Single(c => c.Path.Contains("{task}"));
        Assert.Null(taskEntry.When);

        // Dispatched-by-role for docs-writer
        var docsEntry = reviewer.ConditionalMustReads.Single(c => c.Path.Contains("writing-docs"));
        Assert.Equal("docs-writer", docsEntry.When!.DispatchedByRole);
    }

    [Fact]
    public void ValidateRoleDefinition_InvalidConditionalMustRead_ReportsError()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            ConditionalMustReads =
            [
                new ConditionalMustRead
                {
                    When = new ConditionalMustReadCondition(), // no conditions set
                    Path = "some/file.md"
                }
            ]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("at least one condition"));
    }

    [Fact]
    public void ValidateRoleDefinition_EmptyConditionalMustReadPath_ReportsError()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            ConditionalMustReads =
            [
                new ConditionalMustRead { Path = "" }
            ]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("path"));
    }

    [Fact]
    public void ValidateRoleDefinition_MarkerExistsWithPathSeparator_ReportsError()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            ConditionalMustReads =
            [
                new ConditionalMustRead
                {
                    When = new ConditionalMustReadCondition { MarkerExists = "sub/dir/.marker" },
                    Path = "some/file.md"
                }
            ]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("filename"));
    }

    [Fact]
    public void ValidateRoleDefinition_EmptyTaskNameMatches_ReportsError()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            ConditionalMustReads =
            [
                new ConditionalMustRead
                {
                    When = new ConditionalMustReadCondition { TaskNameMatches = "  " },
                    Path = "some/file.md"
                }
            ]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("taskNameMatches"));
    }

    [Fact]
    public void ValidateRoleDefinition_EmptyDispatchedByRole_ReportsError()
    {
        var role = new RoleDefinition
        {
            Name = "test", Description = "Test", Base = false,
            WritablePaths = ["src/**"], ReadOnlyPaths = [],
            TemplateFile = "t.md",
            ConditionalMustReads =
            [
                new ConditionalMustRead
                {
                    When = new ConditionalMustReadCondition { DispatchedByRole = "" },
                    Path = "some/file.md"
                }
            ]
        };

        var valid = _service.ValidateRoleDefinition(role, out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("dispatchedByRole"));
    }

    [Fact]
    public void ConditionalMustReads_RoundTripsViaJson()
    {
        _service.WriteBaseRoleDefinitions(_testDir);

        var loaded = _service.LoadRoleDefinitions(_testDir);
        var original = RoleDefinitionService.GetBaseRoleDefinitions();

        foreach (var orig in original)
        {
            var match = loaded.Single(r => r.Name == orig.Name);
            Assert.Equal(orig.ConditionalMustReads.Count, match.ConditionalMustReads.Count);

            for (int i = 0; i < orig.ConditionalMustReads.Count; i++)
            {
                Assert.Equal(orig.ConditionalMustReads[i].Path, match.ConditionalMustReads[i].Path);

                if (orig.ConditionalMustReads[i].When == null)
                {
                    Assert.Null(match.ConditionalMustReads[i].When);
                }
                else
                {
                    Assert.Equal(orig.ConditionalMustReads[i].When!.MarkerExists, match.ConditionalMustReads[i].When?.MarkerExists);
                    Assert.Equal(orig.ConditionalMustReads[i].When!.TaskNameMatches, match.ConditionalMustReads[i].When?.TaskNameMatches);
                    Assert.Equal(orig.ConditionalMustReads[i].When!.DispatchedByRole, match.ConditionalMustReads[i].When?.DispatchedByRole);
                }
            }
        }
    }

    #endregion
}
