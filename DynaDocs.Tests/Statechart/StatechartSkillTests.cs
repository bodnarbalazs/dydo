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
            ["release"] = "3.5.1", ["payload"] = change == "payload" ? "other" : assets.Digest, ["lock"] = assets.LockDigest };
        if (change != "missing") File.WriteAllText(Path.Combine(skill, ".statechart-skill.json"), identity.ToJsonString());
        if (change == "source") File.AppendAllText(Path.Combine(skill, "SKILL.md"), "\nlocal edit");
        if (change == "valid") Assert.Equal(skill, StatechartSkill.Validate(skill, assets));
        else Assert.Throws<IOException>(() => StatechartSkill.Validate(skill, assets));
        if (change == "source") Assert.EndsWith("local edit", File.ReadAllText(Path.Combine(skill, "SKILL.md")));
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
}
