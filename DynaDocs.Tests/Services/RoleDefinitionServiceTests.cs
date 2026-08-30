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

    private string CreateProjectTemplatesDir()
    {
        var templatesDir = Path.Combine(_testDir, "dydo", "_system", "templates");
        Directory.CreateDirectory(templatesDir);
        return templatesDir;
    }

    #region DiscoverRoles

    [Fact]
    public void DiscoverRoles_FindsAllShippedRoles()
    {
        var names = RoleDefinitionService.DiscoverRoles(_testDir).Select(r => r.Name).ToList();

        Assert.Contains("code-writer", names);
        Assert.Contains("reviewer", names);
        Assert.Contains("test-writer", names);
        Assert.Contains("docs-writer", names);
        Assert.Contains("planner", names);
        Assert.Contains("orchestrator", names);
        Assert.Contains("co-thinker", names);
        Assert.Contains("chief-of-staff", names);
        Assert.Contains("inquisitor", names);
        Assert.Contains("self-improvement", names);
        Assert.Contains("wayfinder", names);
        Assert.Contains("grilling", names);
        Assert.Contains("grill-me", names);
        Assert.Contains("bro", names);
        Assert.Contains("writing-for-agents", names);
        // Retired roles stay retired.
        Assert.DoesNotContain("judge", names);
        Assert.DoesNotContain("sprint-auditor", names);
    }

    [Fact]
    public void DiscoverRoles_EmitShapes_MatchTheNativePivot()
    {
        var roles = RoleDefinitionService.DiscoverRoles(_testDir).ToDictionary(r => r.Name);

        // Workers emit agent + skill.
        Assert.True(roles["code-writer"].EmitAgent);
        Assert.True(roles["reviewer"].EmitAgent);
        Assert.True(roles["test-writer"].EmitAgent);
        Assert.True(roles["docs-writer"].EmitAgent);
        // Planner and the Tier-1 manager modes are skill-only.
        Assert.False(roles["planner"].EmitAgent);
        Assert.False(roles["orchestrator"].EmitAgent);
        Assert.False(roles["co-thinker"].EmitAgent);
        Assert.False(roles["chief-of-staff"].EmitAgent);
        Assert.False(roles["self-improvement"].EmitAgent);
        Assert.False(roles["wayfinder"].EmitAgent);
        Assert.False(roles["grilling"].EmitAgent);
        Assert.False(roles["grill-me"].EmitAgent);
        Assert.False(roles["bro"].EmitAgent);
        Assert.False(roles["writing-for-agents"].EmitAgent);
        Assert.StartsWith("Explicitly invoked by the human", roles["wayfinder"].Description);
        Assert.Contains("stress-test their thinking", roles["grilling"].Description,
            StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Explicitly invoked by the human", roles["grill-me"].Description);
        Assert.StartsWith("Explicitly invoked by the human", roles["bro"].Description);
        Assert.StartsWith("Writing documents for agents", roles["writing-for-agents"].Description);

        Assert.True(roles["wayfinder"].ExplicitInvocation);
        Assert.True(roles["grill-me"].ExplicitInvocation);
        Assert.True(roles["bro"].ExplicitInvocation);
        Assert.False(roles["grilling"].ExplicitInvocation);
        Assert.False(roles["writing-for-agents"].ExplicitInvocation);
        Assert.False(roles["reviewer"].ExplicitInvocation);
    }

    [Fact]
    public void DiscoverRoles_ReviewerAndInquisitor_AreReadOnlyBaseRoles()
    {
        var roles = RoleDefinitionService.DiscoverRoles(_testDir);

        Assert.True(roles.Single(r => r.Name == "reviewer").ReadOnly);
        Assert.True(roles.Single(r => r.Name == "inquisitor").ReadOnly);
        Assert.All(
            roles.Where(r => r.Name is not ("reviewer" or "inquisitor")),
            r => Assert.False(r.ReadOnly));
    }

    [Fact]
    public void DiscoverRoles_ShippedRoles_HaveDescriptions()
    {
        Assert.All(RoleDefinitionService.DiscoverRoles(_testDir),
            r => Assert.False(string.IsNullOrWhiteSpace(r.Description),
                $"role '{r.Name}' has no description"));
    }

    [Fact]
    public void DiscoverRoles_CustomProjectTemplate_BecomesARole()
    {
        var templatesDir = CreateProjectTemplatesDir();
        File.WriteAllText(Path.Combine(templatesDir, "skill-security-auditor.template.md"),
            """
            ---
            mode: security-auditor
            description: Audits changes for security regressions.
            emit: agent
            read-only: true
            ---

            # Security Auditor

            ## Mindset

            Suspicious by default.
            """);

        var roles = RoleDefinitionService.DiscoverRoles(_testDir);
        var custom = roles.Single(r => r.Name == "security-auditor");

        Assert.Equal("Audits changes for security regressions.", custom.Description);
        Assert.True(custom.EmitAgent);
        Assert.True(custom.ReadOnly);
        Assert.Equal("skill-security-auditor.template.md", custom.TemplateFile);
    }

    [Fact]
    public void DiscoverRoles_CustomTemplate_DefaultsToWritableAgent()
    {
        var templatesDir = CreateProjectTemplatesDir();
        File.WriteAllText(Path.Combine(templatesDir, "skill-infra-writer.template.md"),
            "---\nmode: infra-writer\n---\n\n# Infra Writer\n");

        var custom = RoleDefinitionService.DiscoverRoles(_testDir)
            .Single(r => r.Name == "infra-writer");

        Assert.True(custom.EmitAgent);
        Assert.False(custom.ReadOnly);
        Assert.False(custom.ExplicitInvocation);
        Assert.Equal("", custom.Description);
    }

    [Fact]
    public void DiscoverRoles_CustomTemplate_ParsesExplicitInvocation()
    {
        var templatesDir = CreateProjectTemplatesDir();
        File.WriteAllText(Path.Combine(templatesDir, "skill-human-only.template.md"),
            "---\nmode: human-only\ninvocation: explicit\n---\n\n# Human Only\n");

        var custom = RoleDefinitionService.DiscoverRoles(_testDir)
            .Single(r => r.Name == "human-only");

        Assert.True(custom.ExplicitInvocation);
    }

    [Fact]
    public void DiscoverRoles_CustomTemplate_RejectsInvalidInvocation()
    {
        var templatesDir = CreateProjectTemplatesDir();
        File.WriteAllText(Path.Combine(templatesDir, "skill-invalid.template.md"),
            "---\nmode: invalid\ninvocation: sometimes\n---\n\n# Invalid\n");

        var error = Assert.Throws<InvalidDataException>(
            () => RoleDefinitionService.DiscoverRoles(_testDir));

        Assert.Contains("skill-invalid.template.md", error.Message);
        Assert.Contains("expected 'automatic' or 'explicit'", error.Message);
    }

    [Fact]
    public void DiscoverRoles_ProjectOverride_FrontmatterWinsOverSeed()
    {
        var templatesDir = CreateProjectTemplatesDir();
        File.WriteAllText(Path.Combine(templatesDir, "skill-reviewer.template.md"),
            """
            ---
            mode: reviewer
            description: Custom reviewer description.
            emit: skill
            read-only: false
            ---

            # Reviewer
            """);

        var reviewer = RoleDefinitionService.DiscoverRoles(_testDir)
            .Single(r => r.Name == "reviewer");

        Assert.Equal("Custom reviewer description.", reviewer.Description);
        Assert.False(reviewer.EmitAgent);
        Assert.False(reviewer.ReadOnly);
    }

    [Fact]
    public void DiscoverRoles_NoProjectTemplates_StillFindsShippedRoles()
    {
        // _testDir has no dydo/_system/templates at all.
        Assert.NotEmpty(RoleDefinitionService.DiscoverRoles(_testDir));
    }

    [Fact]
    public void DiscoverRoles_IgnoresLegacyModeTemplate()
    {
        var templatesDir = CreateProjectTemplatesDir();
        File.WriteAllText(Path.Combine(templatesDir, "mode-legacy-role.template.md"),
            "---\nmode: legacy-role\n---\n\n# Legacy Role\n");

        Assert.DoesNotContain(RoleDefinitionService.DiscoverRoles(_testDir),
            role => role.Name == "legacy-role");
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
}
