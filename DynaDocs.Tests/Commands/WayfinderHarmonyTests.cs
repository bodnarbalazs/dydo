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
    public void PlannerSkill_ResolvesGlossaryLink_AndPlansOnlyTheVisibleIncrement()
    {
        var skillPath = CompileSkill("planner");
        var skill = File.ReadAllText(skillPath);

        const string glossaryTarget = "../../../dydo/reference/dydo-glossary.md";
        Assert.Contains($"[dydo glossary]({glossaryTarget})", skill);
        Assert.Contains("plan only the currently visible Issue or bounded Project-plan", skill);
        Assert.Contains("Never turn Fog into speculative Linear work", skill);

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
    public void ManagerSkills_KeepProjectNavigationInTheCurrentTopLevelConversation()
    {
        var coThinker = File.ReadAllText(CompileSkill("co-thinker"));
        Assert.Contains("Only the human\npromotes a FutureFeature into exactly one Linear Initiative, Project, or Issue", coThinker.Replace("\r\n", "\n"));
        Assert.Contains("skill in this same top-level conversation", coThinker);
        Assert.Contains("Grilling is a method for eliciting and nailing down intent", coThinker);

        var chiefOfStaff = File.ReadAllText(CompileSkill("chief-of-staff"));
        Assert.Contains("only authority that\n  promotes a FutureFeature into exactly one Linear Initiative, Project, or Issue", chiefOfStaff.Replace("\r\n", "\n"));
        Assert.Contains("top-level manager to\n  Wayfinder", chiefOfStaff.Replace("\r\n", "\n"));
        Assert.Contains("do not start another top-level session or choose its Waypoints", chiefOfStaff);

        var orchestrator = File.ReadAllText(CompileSkill("orchestrator"));
        Assert.Contains("Return the audited delivery result and its evidence to the invoking top-level manager", orchestrator);
        Assert.Contains("Never\n  choose the next Waypoint or spawn or coordinate top-level sessions", orchestrator.Replace("\r\n", "\n"));
    }

    private string CompileSkill(string roleName)
    {
        var role = RoleDefinitionService.DiscoverRoles(_testDir)
            .Single(candidate => candidate.Name == roleName);
        SyncCommand.SyncSkillOnlyRole(role, _testDir);
        return Path.Combine(_testDir, ".claude", "skills", roleName, "SKILL.md");
    }
}
