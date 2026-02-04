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

    #region Folder Name Validation

    [Fact]
    public void Validate_AcceptsKebabCaseFolderPath()
    {
        var doc = CreateDoc("test.md", "my-folder/sub-folder/test.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_RejectsPascalCaseFolder()
    {
        var doc = CreateDoc("test.md", "MyFolder/test.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("Folder name", violations[0].Message);
        Assert.Contains("MyFolder", violations[0].Message);
    }

    [Fact]
    public void Validate_RejectsMultipleInvalidFolders()
    {
        var doc = CreateDoc("test.md", "BadFolder/AnotherBad/test.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Equal(2, violations.Count);
    }

    [Fact]
    public void Validate_AcceptsSingleLetterFolders()
    {
        var doc = CreateDoc("test.md", "a/b/test.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_AcceptsNumbersInFolderNames()
    {
        var doc = CreateDoc("test.md", "api-v2/test.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_ReportsFileAndFolderViolations()
    {
        var doc = CreateDoc("BadFile.md", "BadFolder/BadFile.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Equal(2, violations.Count);
    }

    #endregion

    #region Exclusions

    [Fact]
    public void Validate_SkipsTemplateFiles()
    {
        var doc = CreateDoc("agent-workflow.template.md", "_system/templates/agent-workflow.template.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsAgentWorkspaceFiles()
    {
        var doc = CreateDoc("workflow.md", "agents/Adele/workflow.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsPascalCaseAgentFolders()
    {
        var doc = CreateDoc("code-writer.md", "agents/Adele/modes/code-writer.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_DoesNotSkipNonAgentPascalCaseFolders()
    {
        var doc = CreateDoc("test.md", "BadFolder/test.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("BadFolder", violations[0].Message);
    }

    #endregion

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
