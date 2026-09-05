namespace DynaDocs.Tests.Rules;

using DynaDocs.Models;
using DynaDocs.Rules;
using Xunit;

public class TitleRuleTests
{
    private readonly TitleRule _rule = new();

    [Fact]
    public void Properties_AreSet()
    {
        Assert.Equal("Title", _rule.Name);
        Assert.False(string.IsNullOrEmpty(_rule.Description));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("(One-line summary)")]
    [InlineData("A useful navigation description.")]
    public void Validate_AcceptsTitle_RegardlessOfOptionalSummary(string? summary)
    {
        var doc = CreateDoc(title: "My Document", summary: summary);

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "Some content")]
    [InlineData("", "Some content")]
    public void Validate_RejectsMissingTitle(string? title, string? summary)
    {
        var doc = CreateDoc(title, summary);

        var violations = _rule.Validate(doc, [], "/base").ToList();

        var violation = Assert.Single(violations);
        Assert.Equal(ViolationSeverity.Error, violation.Severity);
        Assert.Equal("Missing title (# heading)", violation.Message);
    }

    [Theory]
    [InlineData("_system/template-additions/extra-foo.md")]
    [InlineData("_system/template-additions/skill-implementer.template.md")]
    public void Validate_SkipsTemplateAdditionWithNoTitle(string relativePath)
    {
        var doc = CreateDoc(title: null, summary: null, relativePath: relativePath);

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    private static DocFile CreateDoc(string? title, string? summary, string relativePath = "test.md")
    {
        return new DocFile
        {
            FilePath = $"/base/{relativePath}",
            RelativePath = relativePath,
            FileName = Path.GetFileName(relativePath),
            Content = "# Test",
            Title = title,
            SummaryParagraph = summary
        };
    }
}
