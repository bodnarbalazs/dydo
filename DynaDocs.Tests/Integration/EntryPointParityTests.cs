namespace DynaDocs.Tests.Integration;

using DynaDocs.Services;

public class EntryPointParityTests
{
    [Fact]
    public void RepositoryEntryPoints_MatchTheSharedTemplate()
    {
        var repositoryRoot = FindRepositoryRoot();
        var expected = TemplateGenerator.GenerateEntryPointMd("DynaDocs");

        Assert.Equal(Normalize(expected), Normalize(File.ReadAllText(Path.Combine(repositoryRoot, "AGENTS.md"))));
        Assert.Equal(Normalize(expected), Normalize(File.ReadAllText(Path.Combine(repositoryRoot, "CLAUDE.md"))));
    }

    // The entry point is read at the start of every session on both hosts, so its budget is the
    // contract: short enough to be read in full, and pointing at dydo/index.md for everything
    // else. Its wording belongs to whoever authors it; only the shape is pinned here.
    [Fact]
    public void SharedTemplate_StaysWithinTheMinimalEntryContract()
    {
        var content = TemplateGenerator.ReadBuiltInTemplate("entry-point.template.md");
        var nonBlankLines = content.Replace("\r\n", "\n").Split('\n')
            .Count(line => !string.IsNullOrWhiteSpace(line));

        Assert.Contains("{{PROJECT_NAME}}", content);
        Assert.Contains("(dydo/index.md)", content);
        Assert.True(nonBlankLines <= 25,
            $"the entry point must stay readable in one glance; it has {nonBlankLines} non-blank lines");
    }

    private static string Normalize(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
