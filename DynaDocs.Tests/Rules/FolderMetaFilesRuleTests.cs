namespace DynaDocs.Tests.Rules;

using DynaDocs.Models;
using DynaDocs.Rules;
using Xunit;

public class FolderMetaFilesRuleTests
{
    private readonly FolderMetaFilesRule _rule = new();

    [Fact]
    public void ValidateFolder_AcceptsFolderWithMetaFile()
    {
        var docs = new List<DocFile>
        {
            CreateDoc("guides/api/_api.md"),
            CreateDoc("guides/api/endpoints.md")
        };

        var violations = _rule.ValidateFolder("/base/guides/api", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFolder_RejectsFolderWithoutMetaFile()
    {
        var docs = new List<DocFile>
        {
            CreateDoc("guides/api/endpoints.md"),
            CreateDoc("guides/api/models.md")
        };

        var violations = _rule.ValidateFolder("/base/guides/api", docs, "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("_api.md", violations[0].Message);
    }

    [Fact]
    public void ValidateFolder_SkipsTopLevelMainFolders()
    {
        // guides/ itself should not require a meta file
        var docs = new List<DocFile>
        {
            CreateDoc("guides/_index.md"),
            CreateDoc("guides/some-guide.md")
        };

        var violations = _rule.ValidateFolder("/base/guides", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFolder_SkipsRootFolder()
    {
        var docs = new List<DocFile>
        {
            CreateDoc("index.md")
        };

        var violations = _rule.ValidateFolder("/base", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFolder_SkipsNestedFolders()
    {
        // project/tasks/subtask/ should not require a meta file (not a direct child of main folder)
        var docs = new List<DocFile>
        {
            CreateDoc("project/tasks/subtask/task1.md")
        };

        var violations = _rule.ValidateFolder("/base/project/tasks/subtask", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFolder_SkipsHiddenFolders()
    {
        var docs = new List<DocFile>
        {
            CreateDoc("guides/.obsidian/config.md")
        };

        var violations = _rule.ValidateFolder("/base/guides/.obsidian", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFolder_SkipsEmptyFolders()
    {
        var docs = new List<DocFile>();

        var violations = _rule.ValidateFolder("/base/guides/empty", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFolder_CorrectMetaFileName()
    {
        // guides/getting-started/ needs _getting-started.md, not _meta.md
        var docs = new List<DocFile>
        {
            CreateDoc("guides/getting-started/_meta.md"),  // Wrong name
            CreateDoc("guides/getting-started/install.md")
        };

        var violations = _rule.ValidateFolder("/base/guides/getting-started", docs, "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("_getting-started.md", violations[0].Message);
    }

    [Fact]
    public void ValidateFolder_CanAutoFix()
    {
        Assert.True(_rule.CanAutoFix);
    }

    [Theory]
    [InlineData("guides")]
    [InlineData("project")]
    [InlineData("reference")]
    [InlineData("understand")]
    public void ValidateFolder_ValidatesAllMainFolders(string mainFolder)
    {
        var docs = new List<DocFile>
        {
            CreateDoc($"{mainFolder}/subfolder/doc.md")
        };

        var violations = _rule.ValidateFolder($"/base/{mainFolder}/subfolder", docs, "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("_subfolder.md", violations[0].Message);
    }

    [Fact]
    public void ValidateFolder_SkipsNonMainFolders()
    {
        // _system/templates/ should not require meta files
        var docs = new List<DocFile>
        {
            CreateDoc("_system/templates/some-template.md")
        };

        var violations = _rule.ValidateFolder("/base/_system/templates", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFolder_SkipsAgentFolders()
    {
        // agents/Adele/ should not require meta files
        var docs = new List<DocFile>
        {
            CreateDoc("agents/Adele/workflow.md")
        };

        var violations = _rule.ValidateFolder("/base/agents/Adele", docs, "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateFolder_OnlyChecksDocsInTargetFolder()
    {
        // Meta file in different folder shouldn't satisfy this folder's requirement
        var docs = new List<DocFile>
        {
            CreateDoc("guides/other/_api.md"),
            CreateDoc("guides/api/endpoints.md")
        };

        var violations = _rule.ValidateFolder("/base/guides/api", docs, "/base").ToList();

        Assert.Single(violations);
    }

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
