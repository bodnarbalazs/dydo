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

    // DR 045 section 11 retires the Waypoint ontology from the vocabulary, and nothing else in
    // the navigation wording: the same DR calls the implementer the hat a top-level session
    // wears, and makes wayfinder a method other roles invoke. Banning those phrases would fail
    // DR-conformant prose.
    [Fact]
    public void CompiledSkills_CarryNoRetiredWaypointOntology()
    {
        foreach (var role in RoleDefinitionService.DiscoverRoles(_testDir))
            Assert.DoesNotContain(
                "Waypoint", File.ReadAllText(CompileSkill(role.Name)), StringComparison.OrdinalIgnoreCase);
    }

    private string CompileSkill(string roleName)
    {
        var role = RoleDefinitionService.DiscoverRoles(_testDir)
            .Single(candidate => candidate.Name == roleName);
        SyncCommand.SyncSkillOnlyRole(role, _testDir);
        return Path.Combine(_testDir, ".claude", "skills", roleName, "SKILL.md");
    }
}
