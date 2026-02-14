namespace DynaDocs.Tests.Rules;

using DynaDocs.Models;
using DynaDocs.Rules;
using Xunit;

public class FrontmatterRuleTests
{
    private readonly FrontmatterRule _rule = new();

    [Fact]
    public void Validate_AcceptsValidFrontmatter()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "backend",
            Type = "guide"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_RejectsMissingFrontmatter()
    {
        var doc = CreateDocWithFrontmatter(null);

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("Missing frontmatter", violations[0].Message);
    }

    [Fact]
    public void Validate_RejectsMissingArea()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Type = "guide"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Contains(violations, v => v.Message.Contains("area"));
    }

    [Fact]
    public void Validate_RejectsMissingType()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "backend"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Contains(violations, v => v.Message.Contains("type"));
    }

    [Fact]
    public void Validate_RejectsInvalidArea()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "invalid-area",
            Type = "guide"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("Invalid area", violations[0].Message);
    }

    [Fact]
    public void Validate_RejectsInvalidType()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "backend",
            Type = "invalid-type"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("Invalid type", violations[0].Message);
    }

    [Fact]
    public void Validate_RequiresStatusForDecisions()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "platform",
            Type = "decision",
            Date = "2025-01-15"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Contains(violations, v => v.Message.Contains("status"));
    }

    [Fact]
    public void Validate_RequiresDateForDecisions()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "platform",
            Type = "decision",
            Status = "accepted"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Contains(violations, v => v.Message.Contains("date"));
    }

    [Fact]
    public void Validate_RequiresDateForChangelog()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "general",
            Type = "changelog"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Contains(violations, v => v.Message.Contains("date"));
    }

    #region New Valid Areas and Types

    [Fact]
    public void Validate_AcceptsUnderstandArea()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "understand",
            Type = "concept"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_AcceptsGuidesArea()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "guides",
            Type = "guide"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_AcceptsContextType()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "general",
            Type = "context"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_AcceptsReferenceArea()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "reference",
            Type = "reference"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_AcceptsProjectArea()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "project",
            Type = "hub"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    #endregion

    #region Exclusions

    [Fact]
    public void Validate_SkipsAgentWorkspaceFiles()
    {
        var doc = CreateDocWithFrontmatter(null, "agents/Adele/workflow.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsNestedAgentWorkspaceFiles()
    {
        var doc = CreateDocWithFrontmatter(null, "agents/Brian/modes/code-writer.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsFilesOffLimits()
    {
        var doc = CreateDocWithFrontmatter(null, "files-off-limits.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsFilesOffLimitsInSubfolder()
    {
        var doc = CreateDocWithFrontmatter(null, "dydo/files-off-limits.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsTemplateFiles()
    {
        var doc = CreateDocWithFrontmatter(null, "_system/templates/mode-code-writer.template.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_DoesNotSkipNonAgentFiles()
    {
        var doc = CreateDocWithFrontmatter(null, "guides/how-to.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("Missing frontmatter", violations[0].Message);
    }

    #endregion

    #region Valid Decision

    [Fact]
    public void Validate_AcceptsValidDecision()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "platform",
            Type = "decision",
            Status = "accepted",
            Date = "2025-01-15"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_RejectsInvalidStatus()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "platform",
            Type = "decision",
            Status = "invalid-status",
            Date = "2025-01-15"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("Invalid status", violations[0].Message);
    }

    #endregion

    #region Task File Validation

    [Fact]
    public void Validate_TaskFile_WithArea_NoType_Passes()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "backend"
        }, "project/tasks/my-feature.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_TaskFile_WithoutArea_Fails()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter(), "project/tasks/my-feature.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("area", violations[0].Message);
    }

    [Fact]
    public void Validate_TaskFile_InvalidArea_Fails()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "invalid-area"
        }, "project/tasks/my-feature.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("Invalid area", violations[0].Message);
    }

    [Fact]
    public void Validate_TaskMetaFile_StillRequiresType()
    {
        // Meta files (starting with _) in tasks folder should go through normal validation
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "project"
        }, "project/tasks/_tasks.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Contains(violations, v => v.Message.Contains("type"));
    }

    [Fact]
    public void Validate_TaskFile_MissingFrontmatter_Fails()
    {
        var doc = CreateDocWithFrontmatter(null, "project/tasks/no-frontmatter.md");

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("Missing frontmatter", violations[0].Message);
    }

    #endregion

    #region Folder Meta Type

    [Fact]
    public void Validate_AcceptsFolderMetaType()
    {
        var doc = CreateDocWithFrontmatter(new Frontmatter
        {
            Area = "guides",
            Type = "folder-meta"
        });

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    #endregion

    private static DocFile CreateDocWithFrontmatter(Frontmatter? frontmatter, string relativePath = "test.md")
    {
        var fileName = Path.GetFileName(relativePath);
        return new DocFile
        {
            FilePath = $"/base/{relativePath}",
            RelativePath = relativePath,
            FileName = fileName,
            Content = "# Test",
            Frontmatter = frontmatter,
            HasFrontmatter = frontmatter != null
        };
    }
}
