namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Services;

public class ChiefOfStaffSyncTests : IDisposable
{
    private readonly string _testDir;

    public ChiefOfStaffSyncTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-chief-of-staff-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        new FolderScaffolder().Scaffold(Path.Combine(_testDir, "dydo"));
        File.Copy(
            Path.Combine(FindRepositoryRoot(), "dydo", "_system", "templates", "skill-chief-of-staff.template.md"),
            Path.Combine(_testDir, "dydo", "_system", "templates", "skill-chief-of-staff.template.md"),
            overwrite: true);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void AuthoredChiefOfStaffTemplates_KeepBoardRoleAndExcludePersonalMemoryPolicy()
    {
        foreach (var source in new[]
        {
            TemplateGenerator.ReadBuiltInTemplate("skill-chief-of-staff.template.md"),
            File.ReadAllText(Path.Combine(
                FindRepositoryRoot(), "dydo", "_system", "templates", "skill-chief-of-staff.template.md")),
        })
        {
            Assert.Contains("### Triage", source);
            Assert.Contains("### Report", source);
            Assert.Contains("### Mediate", source);
            Assert.Contains("### Keep the board honest", source);
            Assert.Contains("self-improvement", source);
            Assert.DoesNotContain("memory", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Managers Doctrine", source);
        }
    }

    [Fact]
    public void SyncChiefOfStaff_EmitsIdenticalSkillsWithoutAgentDefinitions()
    {
        var role = RoleDefinitionService.DiscoverRoles(_testDir)
            .Single(candidate => candidate.Name == "chief-of-staff");
        var claudeSkill = Path.Combine(_testDir, ".claude", "skills", "chief-of-staff", "SKILL.md");
        var codexSkill = Path.Combine(_testDir, ".agents", "skills", "chief-of-staff", "SKILL.md");

        SyncCommand.SyncSkillOnlyRole(role, _testDir);
        SyncCommand.SyncCodexSkill(role, _testDir);

        Assert.Equal(File.ReadAllBytes(claudeSkill), File.ReadAllBytes(codexSkill));
        Assert.False(File.Exists(Path.Combine(_testDir, ".claude", "agents", "chief-of-staff.md")));
        Assert.False(File.Exists(Path.Combine(_testDir, ".codex", "agents", "chief-of-staff.toml")));

        var firstClaude = File.ReadAllBytes(claudeSkill);
        var firstCodex = File.ReadAllBytes(codexSkill);
        SyncCommand.SyncSkillOnlyRole(role, _testDir);
        SyncCommand.SyncCodexSkill(role, _testDir);
        Assert.Equal(firstClaude, File.ReadAllBytes(claudeSkill));
        Assert.Equal(firstCodex, File.ReadAllBytes(codexSkill));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.csproj")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the DynaDocs repository root.");
    }
}
