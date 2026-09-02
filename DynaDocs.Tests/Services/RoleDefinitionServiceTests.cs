namespace DynaDocs.Tests.Services;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public class RoleDefinitionServiceTests
{
    private readonly RoleDefinitionService _service = new();

    #region DiscoverRoles

    [Fact]
    public void DiscoverRoles_FindsAllShippedRoles()
    {
        var names = RoleDefinitionService.DiscoverRoles().Select(r => r.Name).ToList();

        Assert.Contains("code-writer", names);
        Assert.Contains("reviewer", names);
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

        var names = RoleDefinitionService.DiscoverRoles().Select(r => r.Name).ToList();

        Assert.Equal(
            authored.Except(SyncCommand.RetiredManagedRoles).OrderBy(n => n, StringComparer.Ordinal),
            names.OrderBy(n => n, StringComparer.Ordinal));
        Assert.NotEmpty(names);
    }

    // A retired name must also leave the shipped inventory.
    [Fact]
    public void ShippedTemplateSet_ExcludesRetiredRoles()
    {
        foreach (var retired in SyncCommand.RetiredManagedRoles)
        {
            var templateName = $"skill-{retired}.template.md";
            Assert.DoesNotContain(templateName, TemplateGenerator.GetAllTemplateNames());
            Assert.DoesNotContain(templateName, TemplateGenerator.GetBuiltInSkillTemplateNames());
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

    [Fact]
    public void DiscoverRoles_EmitShapes_MatchTheNativePivot()
    {
        var roles = RoleDefinitionService.DiscoverRoles().ToDictionary(r => r.Name);

        // Spawnable roles emit agent + skill; Project Planner remains usable as a session hat too.
        Assert.True(roles["project-planner"].EmitAgent);
        Assert.True(roles["issue-planner"].EmitAgent);
        Assert.True(roles["code-writer"].EmitAgent);
        Assert.True(roles["reviewer"].EmitAgent);
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
        foreach (var role in RoleDefinitionService.DiscoverRoles())
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
        var roles = RoleDefinitionService.DiscoverRoles();

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
        Assert.All(RoleDefinitionService.DiscoverRoles(),
            r => Assert.False(string.IsNullOrWhiteSpace(r.Description),
                $"role '{r.Name}' has no description"));
    }

    [Fact]
    public void Parse_ReadsDelegates()
    {
        var role = RoleDefinitionService.Parse("skill-fan-out.template.md",
            "---\nmode: fan-out\nemit: agent\ndelegates: true\n---\n\n# Fan Out\n");

        Assert.True(role.Delegates);
    }

    [Fact]
    public void Parse_ReadsDescriptionEmitAndReadOnly()
    {
        var role = RoleDefinitionService.Parse("skill-security-auditor.template.md",
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

        Assert.Equal("security-auditor", role.Name);
        Assert.Equal("Audits changes for security regressions.", role.Description);
        Assert.True(role.EmitAgent);
        Assert.True(role.ReadOnly);
        Assert.Equal("skill-security-auditor.template.md", role.TemplateFile);
    }

    [Fact]
    public void Parse_AbsentKeys_DefaultToWritableAgent()
    {
        var role = RoleDefinitionService.Parse("skill-infra-writer.template.md",
            "---\nmode: infra-writer\n---\n\n# Infra Writer\n");

        Assert.True(role.EmitAgent);
        Assert.False(role.ReadOnly);
        Assert.False(role.ExplicitInvocation);
        Assert.False(role.Delegates);
        Assert.Equal("", role.Description);
    }

    [Fact]
    public void Parse_ReadsExplicitInvocation()
    {
        var role = RoleDefinitionService.Parse("skill-human-only.template.md",
            "---\nmode: human-only\ninvocation: explicit\n---\n\n# Human Only\n");

        Assert.True(role.ExplicitInvocation);
    }

    [Fact]
    public void Parse_RejectsInvalidInvocation()
    {
        var error = Assert.Throws<InvalidDataException>(() => RoleDefinitionService.Parse(
            "skill-invalid.template.md",
            "---\nmode: invalid\ninvocation: sometimes\n---\n\n# Invalid\n"));

        Assert.Contains("skill-invalid.template.md", error.Message);
        Assert.Contains("expected 'automatic' or 'explicit'", error.Message);
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
