namespace DynaDocs.Tests.Models;

using DynaDocs.Models;

public class DocFileTests
{
    [Theory]
    [InlineData("_index.md", true)]
    [InlineData("_INDEX.md", true)]
    [InlineData("_Index.MD", true)]
    [InlineData("_INDEX.MD", true)]
    [InlineData("index.md", false)]
    [InlineData("other.md", false)]
    public void IsHubFile_IsCaseInsensitive(string fileName, bool expected)
    {
        var doc = CreateDoc(fileName);

        Assert.Equal(expected, doc.IsHubFile);
    }

    [Theory]
    [InlineData("index.md", true)]
    [InlineData("INDEX.md", true)]
    [InlineData("Index.MD", true)]
    [InlineData("INDEX.MD", true)]
    [InlineData("_index.md", false)]
    [InlineData("other.md", false)]
    public void IsIndexFile_IsCaseInsensitive(string fileName, bool expected)
    {
        var doc = CreateDoc(fileName);

        Assert.Equal(expected, doc.IsIndexFile);
    }

    private static DocFile CreateDoc(string fileName)
    {
        return new DocFile
        {
            FilePath = $"/base/{fileName}",
            RelativePath = fileName,
            FileName = fileName,
            Content = "# Test"
        };
    }
}
