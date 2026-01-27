namespace DynaDocs.Tests.Rules;

using DynaDocs.Models;
using DynaDocs.Rules;
using Xunit;

public class NamingRuleTests
{
    private readonly NamingRule _rule = new();

    [Fact]
    public void Validate_AcceptsKebabCaseFilename()
    {
        var doc = CreateDoc("my-document.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_RejectsSpacesInFilename()
    {
        var doc = CreateDoc("My Document.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("kebab-case", violations[0].Message);
    }

    [Fact]
    public void Validate_RejectsPascalCaseFilename()
    {
        var doc = CreateDoc("MyDocument.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Equal(ViolationSeverity.Error, violations[0].Severity);
    }

    [Fact]
    public void Validate_AcceptsUnderscoreIndexFile()
    {
        var doc = CreateDoc("_index.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_AcceptsClaudeMdException()
    {
        var doc = CreateDoc("CLAUDE.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SuggestsFix()
    {
        var doc = CreateDoc("My Document.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.True(violations[0].IsAutoFixable);
        Assert.Equal("my-document.md", violations[0].SuggestedFix);
    }

    private static DocFile CreateDoc(string fileName, string relativePath = "")
    {
        if (string.IsNullOrEmpty(relativePath))
            relativePath = fileName;

        return new DocFile
        {
            FilePath = $"/base/{relativePath}",
            RelativePath = relativePath,
            FileName = fileName,
            Content = "# Test"
        };
    }
}
