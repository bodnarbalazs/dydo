namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Utils;

public class ChiefOfStaffSyncTests : IDisposable
{
    private readonly string _testDir;

    public ChiefOfStaffSyncTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-chief-of-staff-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        new FolderScaffolder().Scaffold(Path.Combine(_testDir, "dydo"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    // The shipped source carries the skill's shape and what the skill must never grow: a personal
    // memory store, or the retired tier doctrine.
    [Fact]
    public void AuthoredChiefOfStaffTemplate_HasExpectedShapeAndExcludesPersonalMemoryPolicy()
    {
        var source = Normalize(TemplateGenerator.ReadBuiltInTemplate("skill-chief-of-staff.template.md"));

        Assert.Contains("mode: chief-of-staff\n", source);
        Assert.Contains("emit: skill\n", source);
        Assert.Equal(1, SyncCommandTests.H1Count(source));
        Assert.Contains(source.Split('\n'), line => line.StartsWith("## ", StringComparison.Ordinal));
        Assert.DoesNotContain("memory", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Managers Doctrine", source);
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n");

    [Fact]
    public void SyncChiefOfStaff_EmitsIdenticalSkillsWithoutAgentDefinitions()
    {
        var template = SkillTemplateService.DiscoverSkills()
            .Single(candidate => candidate.Name == "chief-of-staff");
        var claudeSkill = Path.Combine(_testDir, ".claude", "skills", "chief-of-staff", "SKILL.md");
        var codexSkill = Path.Combine(_testDir, ".agents", "skills", "chief-of-staff", "SKILL.md");

        SyncCommand.SyncSkill(template, _testDir);
        SyncCommand.SyncCodexSkill(template, _testDir);

        // One authored source, so both hosts get the same body. Only the invocation policy is
        // host-shaped — Claude carries it in frontmatter, Codex in a sibling yaml — so comparing
        // whole files would go red the day this skill becomes explicit-only. A link to the skill's
        // own resources carries the host's skill root, so that prefix is normalized away too.
        Assert.Equal(
            SyncCommandTests.NormalizeHostSkillRoot(
                FrontmatterParser.StripFrontmatter(File.ReadAllText(claudeSkill))),
            SyncCommandTests.NormalizeHostSkillRoot(
                FrontmatterParser.StripFrontmatter(File.ReadAllText(codexSkill))));
        Assert.Equal(
            template.ExplicitInvocation,
            File.ReadAllText(claudeSkill).Contains("disable-model-invocation: true"));
        Assert.DoesNotContain("disable-model-invocation", File.ReadAllText(codexSkill));
        Assert.False(File.Exists(Path.Combine(_testDir, ".claude", "agents", "chief-of-staff.md")));
        Assert.False(File.Exists(Path.Combine(_testDir, ".codex", "agents", "chief-of-staff.toml")));

        var firstClaude = File.ReadAllBytes(claudeSkill);
        var firstCodex = File.ReadAllBytes(codexSkill);
        SyncCommand.SyncSkill(template, _testDir);
        SyncCommand.SyncCodexSkill(template, _testDir);
        Assert.Equal(firstClaude, File.ReadAllBytes(claudeSkill));
        Assert.Equal(firstCodex, File.ReadAllBytes(codexSkill));
    }
}
