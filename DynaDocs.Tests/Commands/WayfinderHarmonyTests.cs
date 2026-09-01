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

    // Acceptance criteria 2 and 6: every dydo link in a compiled skill climbs correctly out of
    // the emitted skill folder AND names a document that exists. An unresolvable pointer is a
    // context the agent silently never reads.
    //
    // The climb is checked against the emitted geometry — three levels below the project root on
    // either host — but existence is checked against THIS repository's dydo/ tree, because that
    // is where both criteria are judged after regeneration. A bare scaffold holds only framework
    // documents, so resolving there would fail any skill that references a repo-authored guide
    // (plan section 7 binds the Issue Planner to writing-good-briefs, which ships no template).
    [Fact]
    public void CompiledSkills_DydoLinksClimbToTheProjectRootAndNameRealDocuments()
    {
        const string climb = "../../../dydo/";
        var repositoryDydo = Path.Combine(RepositoryRoot(), "dydo");
        var checkedLinks = 0;

        foreach (var role in RoleDefinitionService.DiscoverRoles(_testDir))
        {
            foreach (var target in DydoLinkTargets(File.ReadAllText(CompileSkill(role.Name))))
            {
                Assert.StartsWith(climb, target);

                var document = target[climb.Length..].Replace('/', Path.DirectorySeparatorChar);
                var resolved = Path.Combine(repositoryDydo, document);
                Assert.True(File.Exists(resolved),
                    $"{role.Name}: compiled link '{target}' names no document ({resolved})");
                checkedLinks++;
            }
        }

        Assert.True(checkedLinks > 0, "no compiled dydo link was checked; this fixture would pass vacuously");
    }

    // A relative link into the knowledge tree, not an absolute URL that merely contains the word.
    private static IEnumerable<string> DydoLinkTargets(string skill) =>
        System.Text.RegularExpressions.Regex.Matches(skill, @"\]\(([^)\s]+)\)")
            .Select(match => match.Groups[1].Value)
            .Where(target => target.Contains("/dydo/", StringComparison.Ordinal)
                && !target.Contains("://", StringComparison.Ordinal));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.csproj")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the DynaDocs repository root.");
    }

    // DR 045 section 11 retires the Waypoint ontology from the vocabulary, and nothing else in
    // the navigation wording: the same DR calls the Issue Captain the hat a top-level session
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
