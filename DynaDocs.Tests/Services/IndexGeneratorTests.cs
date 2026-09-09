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
    }

    [Fact]
    public void Generate_StartsWithHubFrontmatter()
    {
        var generator = new IndexGenerator();
        var result = generator.Generate([], "/test").Replace("\r\n", "\n");

        Assert.StartsWith("---\narea: general\ntype: hub\n---\n", result);
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
    public void Generate_ShowsSectionLinkWhenAuthoredSectionExists()
    {
        // Arrange
        var generator = new IndexGenerator();
        var docs = new List<DocFile>
        {
            new DocFile
            {
                FilePath = "/test/understand/_understand.md",
                RelativePath = "understand/_understand.md",
                FileName = "_understand.md",
                Content = "# Understanding"
            }
        };
        var basePath = "/test";

        // Act
        var result = generator.Generate(docs, basePath);

        // Assert
        Assert.Contains("[Understanding the Platform](./understand/_understand.md)", result);
    }

    [Fact]
    public void Generate_ShowsFolderNotFoundWhenSectionPageMissing()
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
