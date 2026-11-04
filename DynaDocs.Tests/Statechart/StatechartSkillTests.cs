namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;
using System.Text.Json.Nodes;

public class StatechartSkillTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("payload")]
    [InlineData("protocol")]
    [InlineData("source")]
    [InlineData("missing")]
    public void InstalledSkillMustMatchActualEmbeddedSourcesAndReleaseProtocolPayload(string change)
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var payload = assets.Extract(scope.Path);
        var skill = Path.Combine(payload, "skills", "engineering", "statechart");
        var identity = new JsonObject { ["schema"] = 1, ["protocol"] = change == "protocol" ? 2 : 1,
            ["release"] = assets.Release, ["payload"] = change == "payload" ? "other" : assets.Digest, ["lock"] = assets.LockDigest };
        if (change != "missing") File.WriteAllText(Path.Combine(skill, ".statechart-skill.json"), identity.ToJsonString());
        if (change == "source") File.AppendAllText(Path.Combine(skill, "SKILL.md"), "\nlocal edit");
        if (change == "valid") Assert.Equal(skill, StatechartSkill.Validate(skill, assets));
        else Assert.Throws<IOException>(() => StatechartSkill.Validate(skill, assets));
        if (change == "source") Assert.EndsWith("local edit", File.ReadAllText(Path.Combine(skill, "SKILL.md")));
    }

    [Fact]
    public void AnUnchangedSkillFromAnEarlierReleaseIsReplacedWithThisRelease()
    {
        using var scope = new StatechartTestDirectory();
        var (project, payload, skill) = Installed(scope.Path);
        FromAnEarlierRelease(skill, inventory: true);
        Assert.Equal(skill, StatechartSkill.Place(project, payload, new StatechartAssets()));
        Assert.Equal(skill, StatechartSkill.Validate(skill, new StatechartAssets()));
        Assert.DoesNotContain("Earlier release wording", File.ReadAllText(Path.Combine(skill, "SKILL.md")));
        Assert.Equal(["statechart"], Directory.GetFileSystemEntries(Path.GetDirectoryName(skill)!).Select(Path.GetFileName));
    }

    [Fact]
    public void LocalEditsToAnEarlierReleaseAreRefusedUntouchedWithTheRemedy()
    {
        using var scope = new StatechartTestDirectory();
        var (project, payload, skill) = Installed(scope.Path);
        FromAnEarlierRelease(skill, inventory: true);
        File.AppendAllText(Path.Combine(skill, "SKILL.md"), "\nlocal edit");
        var receipt = File.ReadAllBytes(Path.Combine(skill, ".statechart-skill.json"));
        var error = Assert.Throws<IOException>(() => StatechartSkill.Place(project, payload, new StatechartAssets()));
        Assert.Contains("SKILL.md", error.Message);
        Assert.Contains("aside", error.Message);
        Assert.EndsWith("local edit", File.ReadAllText(Path.Combine(skill, "SKILL.md")));
        Assert.Equal(receipt, File.ReadAllBytes(Path.Combine(skill, ".statechart-skill.json")));
    }

    // Receipts written before the inventory existed record no files, so only this release's exact bytes are safe to keep.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnEarlierReceiptWithoutAnInventoryIsReplacedOnlyWhenItsFilesAreThisRelease(bool thisReleaseBytes)
    {
        using var scope = new StatechartTestDirectory();
        var (project, payload, skill) = Installed(scope.Path);
        if (thisReleaseBytes) Restamp(skill, inventory: false);
        else FromAnEarlierRelease(skill, inventory: false);
        if (thisReleaseBytes)
        {
            StatechartSkill.Place(project, payload, new StatechartAssets());
            Assert.Equal(skill, StatechartSkill.Validate(skill, new StatechartAssets()));
            return;
        }
        Assert.Throws<IOException>(() => StatechartSkill.Place(project, payload, new StatechartAssets()));
        Assert.EndsWith("Earlier release wording.", File.ReadAllText(Path.Combine(skill, "SKILL.md")));
    }

    [Fact]
    public void AChartInvocationOnAnUnchangedEarlierSkillNamesTheInstallSkillCommand()
    {
        using var scope = new StatechartTestDirectory();
        var (project, _, skill) = Installed(scope.Path);
        FromAnEarlierRelease(skill, inventory: true);
        var error = Assert.Throws<IOException>(() => StatechartSkill.Validate(skill, new StatechartAssets()));
        Assert.Contains("dydo statechart install-skill --root " + project, error.Message);
        Assert.EndsWith("Earlier release wording.", File.ReadAllText(Path.Combine(skill, "SKILL.md")));
    }

    [Fact]
    public async Task ForeignCanonicalSkillIsPreservedBeforeProjectionOrPrerequisiteLaunch()
    {
        using var scope = new StatechartTestDirectory();
        var project = Path.Combine(scope.Path, "project");
        var skill = Path.Combine(project, "skills", "engineering", "statechart");
        Directory.CreateDirectory(skill);
        var sentinel = Path.Combine(skill, "SKILL.md");
        File.WriteAllText(sentinel, "human-owned");
        var home = new StatechartHome(Path.Combine(scope.Path, "assets"), Path.Combine(scope.Path, "deps"), Path.Combine(scope.Path, "state"));
        await Assert.ThrowsAsync<IOException>(() => StatechartSkill.InstallAsync(project, home, new StatechartAssets(),
            "win-x64", TextWriter.Null, CancellationToken.None));
        Assert.Equal("human-owned", File.ReadAllText(sentinel));
        Assert.False(Directory.Exists(Path.Combine(project, ".agents")));
        Assert.False(Directory.Exists(home.Dependencies));
    }

    private static (string Project, string Payload, string Skill) Installed(string root)
    {
        var assets = new StatechartAssets();
        var payload = assets.Extract(Path.Combine(root, "assets"));
        var project = Path.Combine(root, "project");
        Directory.CreateDirectory(project);
        return (project, payload, StatechartSkill.Place(project, payload, assets));
    }

    // The skill as an earlier dydo release left it: other SKILL.md bytes under a receipt naming that release.
    private static void FromAnEarlierRelease(string skill, bool inventory)
    {
        File.AppendAllText(Path.Combine(skill, "SKILL.md"), "\nEarlier release wording.");
        Restamp(skill, inventory);
    }

    private static void Restamp(string skill, bool inventory)
    {
        var receipt = new JsonObject { ["schema"] = 1, ["protocol"] = 1, ["release"] = "0.0.1",
            ["payload"] = "earlier-payload", ["lock"] = "earlier-lock" };
        if (inventory) receipt["files"] = StatechartInventory.Capture(skill, ".statechart-skill.json");
        File.WriteAllText(Path.Combine(skill, ".statechart-skill.json"), receipt.ToJsonString());
    }
}
