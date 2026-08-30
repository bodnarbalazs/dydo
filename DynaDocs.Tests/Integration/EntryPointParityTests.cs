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

    [Fact]
    public void SharedTemplate_ContainsOnlyTheMinimalEntryContract()
    {
        var content = TemplateGenerator.ReadBuiltInTemplate("entry-point.template.md");

        Assert.Contains("{{PROJECT_NAME}}", content);
        Assert.Contains("(dydo/index.md)", content);
        Assert.Contains("Linear", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("live work", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Git", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("durable", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("authored", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compiled", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("generated output", content, StringComparison.OrdinalIgnoreCase);

        foreach (var forbidden in new[]
        {
            "memory",
            "kaizen",
            "self-improvement",
            "glossary",
            "Claude",
            "Codex",
            "most likely defines",
        })
        {
            Assert.DoesNotContain(forbidden, content, StringComparison.OrdinalIgnoreCase);
        }
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
