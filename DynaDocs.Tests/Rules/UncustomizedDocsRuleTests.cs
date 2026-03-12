namespace DynaDocs.Tests.Rules;

using DynaDocs.Models;
using DynaDocs.Rules;
using Xunit;

public class UncustomizedDocsRuleTests
{
    private readonly UncustomizedDocsRule _rule = new();

    [Fact]
    public void Properties_AreSet()
    {
        Assert.False(string.IsNullOrEmpty(_rule.Description));
    }

    [Fact]
    public void Validate_WarnsAboutUncustomizedAbout()
    {
        var doc = CreateDoc("understand/about.md",
            "# About\n\n[Describe the project in 2-3 sentences]");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Equal(ViolationSeverity.Warning, violations[0].Severity);
        Assert.Contains("About.md", violations[0].Message);
    }

    [Fact]
    public void Validate_AcceptsCustomizedAbout()
    {
        var doc = CreateDoc("understand/about.md",
            "# About\n\nThis is a real project description.");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_WarnsAboutUncustomizedArchitecture()
    {
        var doc = CreateDoc("understand/architecture.md",
            "# Architecture\n\n**Fill this in.**");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Equal(ViolationSeverity.Warning, violations[0].Severity);
        Assert.Contains("Architecture.md", violations[0].Message);
    }

    [Fact]
    public void Validate_AcceptsCustomizedArchitecture()
    {
        var doc = CreateDoc("understand/architecture.md",
            "# Architecture\n\nReal architecture content here.");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_IgnoresUnrelatedFiles()
    {
        var doc = CreateDoc("guides/how-to.md",
            "# Guide\n\n[Describe the project in 2-3 sentences]\n**Fill this in.**");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    private static DocFile CreateDoc(string relativePath, string content)
    {
        return new DocFile
        {
            FilePath = $"/base/{relativePath}",
            RelativePath = relativePath,
            FileName = Path.GetFileName(relativePath),
            Content = content
        };
    }
}
