namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class IndexGeneratorTests
{
    [Fact]
    public void Generate_ContainsDynaDocsbranding()
    {
        // Arrange
        var generator = new IndexGenerator();
        var docs = new List<DocFile>();
        var basePath = "/test";

        // Act
        var result = generator.Generate(docs, basePath);

        // Assert
        Assert.Contains("DynaDocs", result);
        Assert.DoesNotContain("LC Documentation", result);
        Assert.DoesNotContain("LC documentation", result);
    }

    [Fact]
    public void Generate_ContainsNavigationSection()
    {
        // Arrange
        var generator = new IndexGenerator();
        var docs = new List<DocFile>();
        var basePath = "/test";

        // Act
        var result = generator.Generate(docs, basePath);

        // Assert
        Assert.Contains("## How to Navigate", result);
        Assert.Contains("## Documentation Sections", result);
    }

    [Fact]
    public void Generate_ShowsHubLinkWhenHubExists()
    {
        // Arrange
        var generator = new IndexGenerator();
        var docs = new List<DocFile>
        {
            new DocFile
            {
                FilePath = "/test/understand/_index.md",
                RelativePath = "understand/_index.md",
                FileName = "_index.md",
                Content = "# Understanding"
            }
        };
        var basePath = "/test";

        // Act
        var result = generator.Generate(docs, basePath);

        // Assert
        Assert.Contains("[Understanding the Platform](./understand/_index.md)", result);
    }

    [Fact]
    public void Generate_ShowsFolderNotFoundWhenHubMissing()
    {
        // Arrange
        var generator = new IndexGenerator();
        var docs = new List<DocFile>();
        var basePath = "/test";

        // Act
        var result = generator.Generate(docs, basePath);

        // Assert
        Assert.Contains("*understand/ folder not found*", result);
    }
}
