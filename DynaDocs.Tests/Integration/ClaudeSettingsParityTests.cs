namespace DynaDocs.Tests.Integration;

// The repository's own Claude wiring is what `dydo init claude` writes for a fresh project, so
// Claude agents working on DynaDocs run the same guard hook every adopter gets.
[Collection("Integration")]
public class ClaudeSettingsParityTests : IntegrationTestBase
{
    private const string SettingsPath = ".claude/settings.json";

    [Fact]
    public async Task RepositoryClaudeSettings_MatchFreshInitOutput()
    {
        (await InitProjectAsync("claude")).AssertSuccess();
        var committedPath = Path.Combine(RepositoryRoot(), SettingsPath);

        Assert.True(File.Exists(committedPath), $"the repository must commit {SettingsPath}");
        Assert.Equal(Normalize(ReadFile(SettingsPath)), Normalize(File.ReadAllText(committedPath)));
    }

    private static string Normalize(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal);
}
