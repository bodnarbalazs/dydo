namespace DynaDocs.Tests.Services;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

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
        Assert.Contains("project-planner", names);
        Assert.Contains("issue-planner", names);
        Assert.Contains("co-thinker", names);
        Assert.Contains("chief-of-staff", names);
        Assert.Contains("admiral", names);
        Assert.Contains("inquisitor", names);
        Assert.Contains("self-improvement", names);
        Assert.Contains("wayfinder", names);
        Assert.Contains("grilling", names);
        Assert.Contains("grill-me", names);
        Assert.Contains("bro", names);
        Assert.Contains("writing-for-agents", names);
        // Retired roles stay retired — including any whose template file dydo still ships
        // through a transition, or sync's retired-artifact sweep would be suppressed by its own
        // source and the role would outlive its retirement in every initialized project.
        Assert.DoesNotContain("judge", names);
        Assert.All(SyncCommand.RetiredManagedRoles, retired => Assert.DoesNotContain(retired, names));
    }

    // Derived from the source tree rather than from the shipped-set API, so it still proves
    // the exclusion after that API became the place the exclusion happens.
    [Fact]
    public void DiscoverRoles_ShippedRoles_AreTheAuthoredSkillTemplatesMinusRetiredNames()
    {
        var authored = Directory
            .GetFiles(Path.Combine(RepositoryRoot(), "Templates"), "skill-*.template.md")
            .Select(path => Path.GetFileName(path)!["skill-".Length..^".template.md".Length]);

        var names = RoleDefinitionService.DiscoverRoles(_testDir).Select(r => r.Name).ToList();

        Assert.Equal(
            authored.Except(SyncCommand.RetiredManagedRoles).OrderBy(n => n, StringComparer.Ordinal),
            names.OrderBy(n => n, StringComparer.Ordinal));
        Assert.NotEmpty(names);
    }

    // A retired name must also leave the set `dydo init` mirrors and `dydo template update`
    // hash-tracks: a mirrored copy would be unioned back into discovery and revive the role in
    // every initialized project, and while it stays tracked the stale-copy prune cannot remove it.
    [Fact]
    public void ShippedTemplateSet_ExcludesRetiredRoles()
    {
        foreach (var retired in SyncCommand.RetiredManagedRoles)
        {
            var templateName = $"skill-{retired}.template.md";
            Assert.DoesNotContain(templateName, TemplateGenerator.GetAllTemplateNames());
            Assert.DoesNotContain(templateName, TemplateGenerator.GetBuiltInSkillTemplateNames());
            Assert.DoesNotContain(
                $"_system/templates/{templateName}", TemplateCommand.FrameworkTemplateFiles);
        }
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.csproj")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the DynaDocs repository root.");
    }

    // A project that wants a retired name back drops in its own template; sync then keeps the
    // role and its artifacts. The retirement is dydo's default, not the project's ceiling.
    [Fact]
    public void DiscoverRoles_ProjectTemplateForARetiredName_RevivesTheRole()
    {
        var templatesDir = CreateProjectTemplatesDir();
        foreach (var retired in SyncCommand.RetiredManagedRoles)
        {
            File.WriteAllText(Path.Combine(templatesDir, $"skill-{retired}.template.md"),
                $"---\nmode: {retired}\ndescription: Project-owned {retired}.\nemit: skill\n---\n\n# Revived\n");
        }

        var roles = RoleDefinitionService.DiscoverRoles(_testDir).ToDictionary(r => r.Name);

        Assert.All(SyncCommand.RetiredManagedRoles,
            retired => Assert.Equal($"Project-owned {retired}.", roles[retired].Description));
    }

    [Fact]
    public void DiscoverRoles_EmitShapes_MatchTheNativePivot()
    {
        var roles = RoleDefinitionService.DiscoverRoles(_testDir).ToDictionary(r => r.Name);

        // Spawnable roles emit agent + skill; Project Planner remains usable as a session hat too.
        Assert.True(roles["project-planner"].EmitAgent);
        Assert.True(roles["issue-planner"].EmitAgent);
        Assert.True(roles["code-writer"].EmitAgent);
        Assert.True(roles["reviewer"].EmitAgent);
        Assert.True(roles["test-writer"].EmitAgent);
        Assert.True(roles["docs-writer"].EmitAgent);
        // The remaining coordinating methodologies are skill-only.
        Assert.False(roles["co-thinker"].EmitAgent);
        Assert.False(roles["chief-of-staff"].EmitAgent);
        Assert.False(roles["admiral"].EmitAgent);
        Assert.False(roles["self-improvement"].EmitAgent);
        Assert.False(roles["wayfinder"].EmitAgent);
        Assert.False(roles["grilling"].EmitAgent);
        Assert.False(roles["grill-me"].EmitAgent);
        Assert.False(roles["bro"].EmitAgent);
        Assert.False(roles["writing-for-agents"].EmitAgent);
        // DR 045 section 9's explicit-only list, narrowed to roles that exist today: a human
        // command that becomes model-invocable fires behind the human's back. Which other roles
        // are model-invoked is the taxonomy's business, so it is derived, not listed here.
        Assert.True(roles["grill-me"].ExplicitInvocation);
        Assert.True(roles["bro"].ExplicitInvocation);
        Assert.True(roles["admiral"].ExplicitInvocation);
    }

    // The frontmatter is the contract: every discovered flag must equal what that role's own
    // template declares. Pinning a roster instead freezes a taxonomy that is still moving, and
    // leaves a red test that no later Issue owns this file to fix.
    [Fact]
    public void DiscoverRoles_Flags_MatchEachTemplatesOwnFrontmatter()
    {
        foreach (var role in RoleDefinitionService.DiscoverRoles(_testDir))
        {
            var fields = FrontmatterParser.ParseFields(
                TemplateGenerator.ReadBuiltInTemplate(role.TemplateFile)) ?? [];

            Assert.Equal(Declares(fields, "emit", "agent", fallback: true), role.EmitAgent);
            Assert.Equal(Declares(fields, "read-only", "true"), role.ReadOnly);
            Assert.Equal(Declares(fields, "delegates", "true"), role.Delegates);
            Assert.Equal(Declares(fields, "invocation", "explicit"), role.ExplicitInvocation);
        }
    }

    private static bool Declares(
        IReadOnlyDictionary<string, string> fields, string key, string value, bool fallback = false) =>
        fields.TryGetValue(key, out var actual)
            ? actual.Equals(value, StringComparison.OrdinalIgnoreCase)
            : fallback;

    [Fact]
    public void DiscoverRoles_ReviewerAndInquisitor_AreReadOnlyBaseRoles()
    {
        var roles = RoleDefinitionService.DiscoverRoles(_testDir);

        // Read-only is how "reviewers don't write code" is natively enforced, so these two must
        // carry it. Which other roles do is the taxonomy's business, derived from frontmatter by
        // DiscoverRoles_Flags_MatchEachTemplatesOwnFrontmatter.
        Assert.True(roles.Single(r => r.Name == "reviewer").ReadOnly);
        Assert.True(roles.Single(r => r.Name == "inquisitor").ReadOnly);
    }

    // A description is what routes a model to the skill, so an empty one is a compile-time
    // defect. The wording is the source's business; its presence is the compiler's.
    [Fact]
    public void DiscoverRoles_ShippedRoles_HaveDescriptions()
    {
        Assert.All(RoleDefinitionService.DiscoverRoles(_testDir),
            r => Assert.False(string.IsNullOrWhiteSpace(r.Description),
                $"role '{r.Name}' has no description"));
    }

    [Fact]
    public void DiscoverRoles_CustomTemplate_ParsesDelegates()
    {
        var templatesDir = CreateProjectTemplatesDir();
        File.WriteAllText(Path.Combine(templatesDir, "skill-fan-out.template.md"),
            "---\nmode: fan-out\nemit: agent\ndelegates: true\n---\n\n# Fan Out\n");

        Assert.True(RoleDefinitionService.DiscoverRoles(_testDir)
            .Single(r => r.Name == "fan-out").Delegates);
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
        Assert.False(custom.Delegates);
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
