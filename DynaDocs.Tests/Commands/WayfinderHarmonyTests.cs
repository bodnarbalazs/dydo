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

    [Fact]
    public void PlannerSkill_ResolvesGlossaryLink_AndRejectsSpeculativeCompleteRoutes()
    {
        var skillPath = CompileSkill("planner");
        var skill = File.ReadAllText(skillPath);

        const string glossaryTarget = "../../../dydo/reference/dydo-glossary.md";
        Assert.Contains($"[dydo glossary]({glossaryTarget})", skill);
        Assert.Contains("foggy beyond its visible frontier", skill);
        Assert.Contains("instead of manufacturing a complete route", skill);

        var resolvedGlossary = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(skillPath)!,
            glossaryTarget.Replace('/', Path.DirectorySeparatorChar)));
        Assert.True(File.Exists(resolvedGlossary), $"Compiled glossary link did not resolve: {resolvedGlossary}");
    }

    [Fact]
    public void ReviewerPlanResource_TreatsOnlyProjectBlockingFogAsASpecGap()
    {
        var reviewer = RoleDefinitionService.DiscoverRoles(_testDir)
            .Single(role => role.Name == "reviewer");
        SyncCommand.SyncRole(reviewer, _testDir);

        var resource = File.ReadAllText(Path.Combine(
            _testDir, ".claude", "skills", "reviewer", "resources", "plan.md"));
        Assert.Contains("Wayfinding Fog is not a specification gap unless the current Project depends on resolving it", resource);
        Assert.Contains("do not fail it for uncertainty deliberately left outside its frontier", resource);
    }

    [Fact]
    public void ManagerSkills_PreserveHumanNavigationAuthorityWithoutWaypointOrSessionChoreography()
    {
        var coThinker = File.ReadAllText(CompileSkill("co-thinker"));
        Assert.Contains("Only the human promotes a FutureFeature to Linear", coThinker);
        Assert.Contains("recommend—never invoke—Wayfinder to the human", coThinker);

        var chiefOfStaff = File.ReadAllText(CompileSkill("chief-of-staff"));
        Assert.Contains("Only the human promotes a FutureFeature to Linear", chiefOfStaff);

        var orchestrator = File.ReadAllText(CompileSkill("orchestrator"));
        Assert.Contains("Ask the human only for authority or judgment", orchestrator);

        foreach (var skill in new[] { coThinker, chiefOfStaff, orchestrator })
        {
            Assert.DoesNotContain("Waypoint", skill);
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
