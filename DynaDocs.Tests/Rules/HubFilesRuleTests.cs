namespace DynaDocs.Tests.Rules;

using DynaDocs.Models;
using DynaDocs.Rules;
using Xunit;

public class HubFilesRuleTests
{
    private readonly HubFilesRule _rule = new();

    [Fact]
    public void ValidateFolder_AcceptsFolderWithIndex()
    {
        var docs = new List<DocFile>
        {
            CreateDoc("guides/_index.md"),
            CreateDoc("guides/how-to.md")
        };

        var violations = _rule.ValidateFolder("/base/guides", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFolder_RejectsFolderWithoutIndex()
    {
        var docs = new List<DocFile>
        {
            CreateDoc("guides/how-to.md"),
            CreateDoc("guides/reference.md")
        };

        var violations = _rule.ValidateFolder("/base/guides", docs, "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("_index.md", violations[0].Message);
    }

    [Fact]
    public void ValidateFolder_SkipsEmptyFolders()
    {
        var docs = new List<DocFile>();

        var violations = _rule.ValidateFolder("/base/empty", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFolder_SkipsRootFolder()
    {
        var docs = new List<DocFile>
        {
            CreateDoc("readme.md")
        };

        var violations = _rule.ValidateFolder("/base", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFolder_OnlyChecksDocsInTargetFolder()
    {
        // Docs in other folders shouldn't matter
        var docs = new List<DocFile>
        {
            CreateDoc("other/_index.md"),
            CreateDoc("guides/how-to.md")
        };

        var violations = _rule.ValidateFolder("/base/guides", docs, "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("guides/", violations[0].FilePath);
    }

    [Fact]
    public void ValidateFolder_HandlesNestedFolders()
    {
        var docs = new List<DocFile>
        {
            CreateDoc("guides/backend/_index.md"),
            CreateDoc("guides/backend/api.md")
        };

        var violations = _rule.ValidateFolder("/base/guides/backend", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFolder_ReportsNestedFolderWithoutIndex()
    {
        var docs = new List<DocFile>
        {
            CreateDoc("guides/backend/api.md")
        };

        var violations = _rule.ValidateFolder("/base/guides/backend", docs, "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("guides/backend/", violations[0].FilePath);
    }

    [Fact]
    public void ValidateFolder_CanAutoFix()
    {
        Assert.True(_rule.CanAutoFix);
    }

    #region Exclusions

    [Fact]
    public void ValidateFolder_SkipsSystemFolders()
    {
        var docs = new List<DocFile>
        {
            CreateDoc("_system/templates/agent-workflow.template.md")
        };

        var violations = _rule.ValidateFolder("/base/_system/templates", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFolder_SkipsAgentWorkspaces()
    {
        var docs = new List<DocFile>
        {
            CreateDoc("agents/Adele/modes/code-writer.md")
        };

        var violations = _rule.ValidateFolder("/base/agents/Adele/modes", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFolder_SkipsAgentRootFolder()
    {
        var docs = new List<DocFile>
        {
            CreateDoc("agents/Adele/workflow.md")
        };

        var violations = _rule.ValidateFolder("/base/agents/Adele", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFolder_SkipsAgentsFolder()
    {
        var docs = new List<DocFile>
        {
            CreateDoc("agents/Adele/workflow.md"),
            CreateDoc("agents/Brian/workflow.md")
        };

        var violations = _rule.ValidateFolder("/base/agents", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    #endregion

    private static DocFile CreateDoc(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        return new DocFile
        {
            FilePath = $"/base/{relativePath}",
            RelativePath = relativePath,
            FileName = fileName,
            Content = "# Test"
        };
    }
}
