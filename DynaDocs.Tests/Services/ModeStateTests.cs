namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public sealed class ModeStateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dydo-mode-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_root, "mode.json");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void MissingFile_ReadsEveryFlagAtItsDefault() =>
        Assert.Equal([KeyValuePair.Create("overnight", "off")], new ModeState(_root).Read());

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{"overnight":"maybe"}""")]
    [InlineData("null")]
    public void UnusableFile_ReadsTheDefault(string content)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(FilePath, content);

        Assert.Equal("off", new ModeState(_root).Read().Single().Value);
    }

    [Fact]
    public void Set_PersistsForTheNextReader()
    {
        new ModeState(_root).Set("overnight", "on");

        Assert.Equal("on", new ModeState(_root).Read().Single().Value);
        Assert.Contains("\"overnight\": \"on\"", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Set_OverUnreadableFile_RewritesIt()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(FilePath, "{ not json");

        new ModeState(_root).Set("overnight", "on");

        Assert.Equal("on", new ModeState(_root).Read().Single().Value);
    }

    [Fact]
    public void EveryFlagDefaultsToItsFirstValue() =>
        Assert.Equal(["off", "on"], ModeState.Flags["overnight"]);
}
