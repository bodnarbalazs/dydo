namespace DynaDocs.Tests.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Commands;
using DynaDocs.Services;

public class ChiefOfStaffSyncTests : IDisposable
{
    private const string MemorySweepHeading = "### 5. Memory sweep";
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
    public void AuthoredChiefOfStaffTemplates_ContainMemorySweepExactlyOnce()
    {
        var builtIn = TemplateGenerator.ReadBuiltInTemplate("skill-chief-of-staff.template.md");
        var projectSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "dydo", "_system", "templates", "skill-chief-of-staff.template.md"));

        AssertMemorySweep(builtIn);
        AssertMemorySweep(projectSource);
    }

    [Fact]
    public void SyncChiefOfStaff_EmitsMemorySweepIntoBothSkillPaths_WithoutAgents_AndIsByteIdentical()
    {
        var chiefOfStaff = RoleDefinitionService.DiscoverRoles(_testDir)
            .Single(role => role.Name == "chief-of-staff");
        var claudeAgent = Path.Combine(_testDir, ".claude", "agents", "chief-of-staff.md");
        var codexAgent = Path.Combine(_testDir, ".codex", "agents", "chief-of-staff.toml");
        var claudeSkill = Path.Combine(_testDir, ".claude", "skills", "chief-of-staff", "SKILL.md");
        var codexSkill = Path.Combine(_testDir, ".agents", "skills", "chief-of-staff", "SKILL.md");

        SyncCommand.SyncSkillOnlyRole(chiefOfStaff, _testDir);
        SyncCommand.SyncCodexSkill(chiefOfStaff, _testDir);

        Assert.False(File.Exists(claudeAgent));
        Assert.False(File.Exists(codexAgent));
        AssertMemorySweep(File.ReadAllText(claudeSkill));
        AssertMemorySweep(File.ReadAllText(codexSkill));
        var firstClaudeSkill = File.ReadAllBytes(claudeSkill);
        var firstCodexSkill = File.ReadAllBytes(codexSkill);

        SyncCommand.SyncSkillOnlyRole(chiefOfStaff, _testDir);
        SyncCommand.SyncCodexSkill(chiefOfStaff, _testDir);

        Assert.Equal(firstClaudeSkill, File.ReadAllBytes(claudeSkill));
        Assert.Equal(firstCodexSkill, File.ReadAllBytes(codexSkill));
        Assert.False(File.Exists(claudeAgent));
        Assert.False(File.Exists(codexAgent));
    }

    private static void AssertMemorySweep(string content)
    {
        var prose = Regex.Replace(content, @"\s+", " ");

        Assert.Equal(1, content.Split(MemorySweepHeading, StringSplitOptions.None).Length - 1);
        Assert.Contains("explicitly human-scoped auto-memory store", prose);
        Assert.Contains("**route**, **retire**, or **keep**", prose);
        Assert.Contains("harness mechanics dydo genuinely cannot hold", prose);
        Assert.Contains("Before the first sweep, get human authorization", prose);
        Assert.Contains("later authorized sweeps, report each disposition", prose);
        Assert.Contains("durable dydo knowledge or a live Linear Issue", prose);
        Assert.Contains("never a new repository PM record", prose);
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
