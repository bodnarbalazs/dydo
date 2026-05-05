namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class FrontmatterTypesServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public FrontmatterTypesServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "fmt-types-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "_system"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public void GetValidTypes_FileMissing_ReturnsBaseline()
    {
        var service = new FrontmatterTypesService(_tempRoot);

        var types = service.GetValidTypes();

        Assert.Equal((IEnumerable<string>)Frontmatter.ValidTypes, types);
        Assert.Contains("inquisition", types);
    }

    [Fact]
    public void GetValidTypes_EmptyArray_ReturnsBaseline()
    {
        WriteTypes("[]");
        var service = new FrontmatterTypesService(_tempRoot);

        var types = service.GetValidTypes();

        Assert.Equal((IEnumerable<string>)Frontmatter.ValidTypes, types);
    }

    [Fact]
    public void GetValidTypes_WithCustomEntry_MergedWithBaseline()
    {
        WriteTypes("[\"custom-type\"]");
        var service = new FrontmatterTypesService(_tempRoot);

        var types = service.GetValidTypes();

        foreach (var baseline in Frontmatter.ValidTypes)
            Assert.Contains(baseline, types);
        Assert.Contains("custom-type", types);
    }

    [Fact]
    public void GetValidTypes_DuplicateOfBaseline_NotDuplicated()
    {
        WriteTypes("[\"hub\", \"custom\"]");
        var service = new FrontmatterTypesService(_tempRoot);

        var types = service.GetValidTypes();

        Assert.Single(types, t => t == "hub");
        Assert.Contains("custom", types);
    }

    [Fact]
    public void GetValidTypes_Malformed_ReturnsBaseline()
    {
        WriteTypes("not json {");
        var service = new FrontmatterTypesService(_tempRoot);

        var types = service.GetValidTypes();

        Assert.Equal((IEnumerable<string>)Frontmatter.ValidTypes, types);
    }

    [Fact]
    public void GetValidTypes_WithCommentAndTrailingComma_ParsesSuccessfully()
    {
        WriteTypes("// leading comment\n[\"custom\",]");
        var service = new FrontmatterTypesService(_tempRoot);

        var types = service.GetValidTypes();

        Assert.Contains("custom", types);
    }

    [Fact]
    public void GetValidTypes_CalledTwice_LoadsOnce()
    {
        WriteTypes("[\"a\"]");
        var service = new FrontmatterTypesService(_tempRoot);

        var first = service.GetValidTypes();

        // Mutate the file: if the service re-read on second call, results would change.
        WriteTypes("[\"b\"]");
        var second = service.GetValidTypes();

        Assert.Same(first, second);
        Assert.Contains("a", second);
        Assert.DoesNotContain("b", second);
    }

    private void WriteTypes(string json)
    {
        File.WriteAllText(Path.Combine(_tempRoot, "_system", "types.json"), json);
    }
}
