namespace DynaDocs.Tests.Services;

using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Utils;

public class SkillTemplateServiceTests
{
    #region DiscoverSkills

    [Fact]
    public void DiscoverSkills_FindsEveryShippedSkill()
    {
        var names = SkillTemplateService.DiscoverSkills().Select(s => s.Name).ToList();

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
        // Retired skills stay retired — including any whose template file dydo still ships
        // through a transition, or sync's retired-artifact sweep would be suppressed by its own
        // source and the skill would outlive its retirement in every initialized project.
        Assert.DoesNotContain("judge", names);
        Assert.All(SyncCommand.RetiredSkills, retired => Assert.DoesNotContain(retired, names));
    }

    // Derived from the source tree rather than from the shipped-set API, so it still proves
    // the exclusion after that API became the place the exclusion happens.
    [Fact]
    public void DiscoverSkills_ShippedSkills_AreTheAuthoredSkillTemplatesMinusRetiredNames()
    {
        var authored = Directory
            .GetFiles(Path.Combine(RepositoryRoot(), "Templates"), "skill-*.template.md")
            .Select(path => Path.GetFileName(path)!["skill-".Length..^".template.md".Length]);

        var names = SkillTemplateService.DiscoverSkills().Select(s => s.Name).ToList();

        Assert.Equal(
            authored.Except(SyncCommand.RetiredSkills).OrderBy(n => n, StringComparer.Ordinal),
            names.OrderBy(n => n, StringComparer.Ordinal));
        Assert.NotEmpty(names);
    }

    // A retired name must also leave the shipped inventory.
    [Fact]
    public void ShippedTemplateSet_ExcludesRetiredSkills()
    {
        foreach (var retired in SyncCommand.RetiredSkills)
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
    public void DiscoverSkills_EmitShapes_MatchTheNativePivot()
    {
        var skills = SkillTemplateService.DiscoverSkills().ToDictionary(s => s.Name);

        // These compile an agent as well as a skill; Project Planner remains usable as a session hat too.
        Assert.True(skills["project-planner"].EmitAgent);
        Assert.True(skills["issue-planner"].EmitAgent);
        Assert.True(skills["code-writer"].EmitAgent);
        Assert.True(skills["reviewer"].EmitAgent);
        Assert.True(skills["docs-writer"].EmitAgent);
        // The remaining coordinating methodologies compile a skill only.
        Assert.False(skills["co-thinker"].EmitAgent);
        Assert.False(skills["chief-of-staff"].EmitAgent);
        Assert.False(skills["admiral"].EmitAgent);
        Assert.False(skills["self-improvement"].EmitAgent);
        Assert.False(skills["wayfinder"].EmitAgent);
        Assert.False(skills["grilling"].EmitAgent);
        Assert.False(skills["grill-me"].EmitAgent);
        Assert.False(skills["bro"].EmitAgent);
        Assert.False(skills["writing-for-agents"].EmitAgent);
        // DR 045 section 9's explicit-only list, narrowed to skills that exist today: a human
        // command that becomes model-invocable fires behind the human's back. Which other skills
        // are model-invoked is the taxonomy's business, so it is derived, not listed here.
        Assert.True(skills["grill-me"].ExplicitInvocation);
        Assert.True(skills["bro"].ExplicitInvocation);
        Assert.True(skills["admiral"].ExplicitInvocation);
    }

    // The frontmatter is the contract: every discovered flag must equal what that template
    // declares. Pinning a roster instead freezes a taxonomy that is still moving, and
    // leaves a red test that no later Issue owns this file to fix.
    [Fact]
    public void DiscoverSkills_Flags_MatchEachTemplatesOwnFrontmatter()
    {
        foreach (var skill in SkillTemplateService.DiscoverSkills())
        {
            var fields = FrontmatterParser.ParseFields(
                TemplateGenerator.ReadBuiltInTemplate(skill.TemplateFile)) ?? [];

            Assert.Equal(Declares(fields, "emit", "agent", fallback: true), skill.EmitAgent);
            Assert.Equal(Declares(fields, "read-only", "true"), skill.ReadOnly);
            Assert.Equal(Declares(fields, "delegates", "true"), skill.Delegates);
            Assert.Equal(Declares(fields, "invocation", "explicit"), skill.ExplicitInvocation);
        }
    }

    private static bool Declares(
        IReadOnlyDictionary<string, string> fields, string key, string value, bool fallback = false) =>
        fields.TryGetValue(key, out var actual)
            ? actual.Equals(value, StringComparison.OrdinalIgnoreCase)
            : fallback;

    [Fact]
    public void DiscoverSkills_ReviewerAndInquisitor_AreReadOnlyAgents()
    {
        var skills = SkillTemplateService.DiscoverSkills();

        // Read-only is how "reviewers don't write code" is natively enforced, so these two must
        // carry it. Which other skills do is the taxonomy's business, derived from frontmatter by
        // DiscoverSkills_Flags_MatchEachTemplatesOwnFrontmatter.
        Assert.True(skills.Single(s => s.Name == "reviewer").ReadOnly);
        Assert.True(skills.Single(s => s.Name == "inquisitor").ReadOnly);
    }

    // A description is what routes a model to the skill, so an empty one is a compile-time
    // defect. The wording is the source's business; its presence is the compiler's.
    [Fact]
    public void DiscoverSkills_ShippedSkills_HaveDescriptions()
    {
        Assert.All(SkillTemplateService.DiscoverSkills(),
            s => Assert.False(string.IsNullOrWhiteSpace(s.Description),
                $"skill '{s.Name}' has no description"));
    }

    [Fact]
    public void Parse_ReadsDelegates()
    {
        var skill = SkillTemplateService.Parse("skill-fan-out.template.md",
            "---\nname: fan-out\nemit: agent\ndelegates: true\n---\n\n# Fan Out\n");

        Assert.True(skill.Delegates);
    }

    [Fact]
    public void Parse_ReadsDescriptionEmitAndReadOnly()
    {
        var skill = SkillTemplateService.Parse("skill-security-auditor.template.md",
            """
            ---
            name: security-auditor
            description: Audits changes for security regressions.
            emit: agent
            read-only: true
            ---

            # Security Auditor

            ## Mindset

            Suspicious by default.
            """);

        Assert.Equal("security-auditor", skill.Name);
        Assert.Equal("Audits changes for security regressions.", skill.Description);
        Assert.True(skill.EmitAgent);
        Assert.True(skill.ReadOnly);
        Assert.Equal("skill-security-auditor.template.md", skill.TemplateFile);
    }

    [Fact]
    public void Parse_AbsentKeys_DefaultToWritableAgent()
    {
        var skill = SkillTemplateService.Parse("skill-infra-writer.template.md",
            "---\nname: infra-writer\n---\n\n# Infra Writer\n");

        Assert.True(skill.EmitAgent);
        Assert.False(skill.ReadOnly);
        Assert.False(skill.ExplicitInvocation);
        Assert.False(skill.Delegates);
        Assert.Equal("", skill.Description);
    }

    [Fact]
    public void Parse_ReadsExplicitInvocation()
    {
        var skill = SkillTemplateService.Parse("skill-human-only.template.md",
            "---\nname: human-only\ninvocation: explicit\n---\n\n# Human Only\n");

        Assert.True(skill.ExplicitInvocation);
    }

    [Fact]
    public void Parse_RejectsInvalidInvocation()
    {
        var error = Assert.Throws<InvalidDataException>(() => SkillTemplateService.Parse(
            "skill-invalid.template.md",
            "---\nname: invalid\ninvocation: sometimes\n---\n\n# Invalid\n"));

        Assert.Contains("skill-invalid.template.md", error.Message);
        Assert.Contains("expected 'automatic' or 'explicit'", error.Message);
    }

    // The compiled SKILL.md emits the name, so a template that never got the key — or got a
    // different one — would ship an identity the filename contradicts. Failing the sync by file
    // name is cheaper than finding it in a compiled artifact.
    [Fact]
    public void Parse_MissingName_ThrowsNamingTheFile()
    {
        var error = Assert.Throws<InvalidDataException>(() => SkillTemplateService.Parse(
            "skill-nameless.template.md",
            "---\nmode: nameless\n---\n\n# Nameless\n"));

        Assert.Contains("skill-nameless.template.md", error.Message);
        Assert.Contains("expected 'name: nameless'", error.Message);
    }

    [Fact]
    public void Parse_NameDifferentFromFilename_ThrowsNamingBoth()
    {
        var error = Assert.Throws<InvalidDataException>(() => SkillTemplateService.Parse(
            "skill-drift.template.md",
            "---\nname: drifted\n---\n\n# Drift\n"));

        Assert.Contains("skill-drift.template.md", error.Message);
        Assert.Contains("'name: drifted'", error.Message);
        Assert.Contains("expected 'drift'", error.Message);
    }

    #endregion
}
