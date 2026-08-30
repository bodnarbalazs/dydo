namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Services;

public class WayfinderHarmonyTests : IDisposable
{
    private readonly string _testDir;

    public WayfinderHarmonyTests()
    {
        _testDir = Path.Combine(
            Path.GetTempPath(), "dydo-wayfinder-harmony-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        new FolderScaffolder().Scaffold(Path.Combine(_testDir, "dydo"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    // Every dydo link in a compiled skill must resolve from the folder the skill is emitted
    // into — an unresolvable pointer is a context the agent silently never reads.
    [Fact]
    public void CompiledSkills_DydoLinksResolveFromTheEmittedSkillFolder()
    {
        foreach (var role in RoleDefinitionService.DiscoverRoles(_testDir))
        {
            var skillPath = CompileSkill(role.Name);
            var skillDir = Path.GetDirectoryName(skillPath)!;

            foreach (var target in DydoLinkTargets(File.ReadAllText(skillPath)))
            {
                Assert.StartsWith("../../../dydo/", target);
                var resolved = Path.GetFullPath(Path.Combine(
                    skillDir, target.Replace('/', Path.DirectorySeparatorChar)));
                Assert.True(File.Exists(resolved),
                    $"{role.Name}: compiled link '{target}' did not resolve to {resolved}");
            }
        }
    }

    private static IEnumerable<string> DydoLinkTargets(string skill) =>
        System.Text.RegularExpressions.Regex.Matches(skill, @"\]\(([^)\s]+)\)")
            .Select(match => match.Groups[1].Value)
            .Where(target => target.Contains("/dydo/", StringComparison.Ordinal));

    // A rubric is authored one folder below SKILL.md and is copied verbatim, so its own climbs
    // must survive compilation untouched.
    [Fact]
    public void ReviewerRubrics_AreEmittedVerbatimBesideTheSkill()
    {
        var reviewer = RoleDefinitionService.DiscoverRoles(_testDir)
            .Single(role => role.Name == "reviewer");
        SyncCommand.SyncRole(reviewer, _testDir);

        foreach (var (fileName, expected) in TemplateGenerator.GetSkillResources("reviewer"))
        {
            var emitted = File.ReadAllText(Path.Combine(
                _testDir, ".claude", "skills", "reviewer", "resources", fileName));
            Assert.Equal(expected.Replace("\r\n", "\n"), emitted);
        }
    }

    // DR 045 section 11 retires the Waypoint ontology and the session-choreography vocabulary;
    // no compiled skill may reintroduce either.
    [Fact]
    public void CompiledSkills_CarryNoRetiredNavigationVocabulary()
    {
        foreach (var role in RoleDefinitionService.DiscoverRoles(_testDir))
        {
            var skill = File.ReadAllText(CompileSkill(role.Name));

            Assert.DoesNotContain("Waypoint", skill, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("top-level session", skill, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("invoke Wayfinder", skill, StringComparison.OrdinalIgnoreCase);
        }
    }

    private string CompileSkill(string roleName)
    {
        var role = RoleDefinitionService.DiscoverRoles(_testDir)
            .Single(candidate => candidate.Name == roleName);
        SyncCommand.SyncSkillOnlyRole(role, _testDir);
        return Path.Combine(_testDir, ".claude", "skills", roleName, "SKILL.md");
    }
}
