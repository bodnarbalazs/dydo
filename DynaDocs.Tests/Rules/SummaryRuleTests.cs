namespace DynaDocs.Tests.Rules;

using DynaDocs.Models;
using DynaDocs.Rules;
using Xunit;

public class SummaryRuleTests
{
    private readonly SummaryRule _rule = new();

    [Fact]
    public void Properties_AreSet()
    {
        Assert.False(string.IsNullOrEmpty(_rule.Description));
    }

    [Fact]
    public void Validate_AcceptsDocWithTitleAndSummary()
    {
        var doc = CreateDoc(title: "My Document", summary: "This is the summary paragraph.");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_RejectsMissingTitle()
    {
        var doc = CreateDoc(title: null, summary: "Some content");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Equal(ViolationSeverity.Error, violations[0].Severity);
        Assert.Contains("Missing title", violations[0].Message);
    }

    [Fact]
    public void Validate_WarnsMissingSummary()
    {
        var doc = CreateDoc(title: "My Title", summary: null);

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Equal(ViolationSeverity.Warning, violations[0].Severity);
        Assert.Contains("summary", violations[0].Message.ToLower());
    }

    [Fact]
    public void Validate_WarnsWhitespaceSummary()
    {
        var doc = CreateDoc(title: "My Title", summary: "   ");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Equal(ViolationSeverity.Warning, violations[0].Severity);
    }

    [Fact]
    public void Validate_StopsAfterMissingTitle()
    {
        // If title is missing, don't also complain about summary
        var doc = CreateDoc(title: null, summary: null);

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("title", violations[0].Message.ToLower());
    }

    [Fact]
    public void Validate_SkipsTemplateAdditionWithNoTitle()
    {
        var doc = CreateDoc(
            title: null,
            summary: null,
            relativePath: "_system/template-additions/extra-foo.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsTemplateFileWithNoTitle()
    {
        var doc = CreateDoc(
            title: null,
            summary: null,
            relativePath: "_system/templates/mode-code-writer.template.md");

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
