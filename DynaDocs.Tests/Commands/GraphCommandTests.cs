namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;

[Collection("Integration")]
public class GraphCommandTests : IDisposable
{
    private readonly string _root;
    private readonly string _original;

    public GraphCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dydo-graph-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _original = Environment.CurrentDirectory;
        Environment.CurrentDirectory = _root;
    }

    private string CreateOtherProject()
    {
        var project = Path.Combine(_root, "other");
        Directory.CreateDirectory(Path.Combine(project, "dydo", "guides"));
        File.WriteAllText(Path.Combine(project, "dydo.json"), """{"version":1,"structure":{"root":"dydo"}}""");
        File.WriteAllText(Path.Combine(project, "dydo", "index.md"), "# Index\n\n[Setup](./guides/setup.md)\n");
        File.WriteAllText(Path.Combine(project, "dydo", "guides", "setup.md"), "# Setup\n");
        return project;
    }

    private static (int Code, string Stdout, string Stderr) Graph(params string[] args) =>
        ConsoleCapture.All(() => GraphCommand.Create().Parse(args).Invoke());

    [Fact]
    public void Graph_RootInsideAnotherProject_GraphsThatProjectsDocs()
    {
        var other = CreateOtherProject();

        var (code, stdout, _) = Graph("guides/setup.md", "--incoming", "--root", Path.Combine(other, "dydo", "guides"));

        Assert.Equal(0, code);
        Assert.Contains($"Root: {other}", stdout);
        Assert.Contains("index.md", stdout);
    }

    [Fact]
    public void GraphStats_Root_GraphsThatProjectsDocs()
    {
        var other = CreateOtherProject();

        var (code, stdout, _) = Graph("stats", "--root", other);

        Assert.Equal(0, code);
        Assert.Contains($"Root: {other}", stdout);
        Assert.Contains("setup.md", stdout);
    }

    [Fact]
    public void Graph_RootInNoDydoProject_IsToolError()
    {
        var (code, _, stderr) = Graph("stats", "--root", _root);

        Assert.Equal(2, code);
        Assert.Contains($"No dydo project found at {_root}", stderr);
    }

    [Fact]
    public void Graph_WithoutRoot_UsesCwdAndPrintsNoRootLine()
    {
        CreateOtherProject();

        var (code, stdout, stderr) = Graph("stats");

        Assert.Equal(2, code);
        Assert.DoesNotContain("Root:", stdout);
        Assert.Contains("Could not find docs folder", stderr);
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _original;
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }
}
