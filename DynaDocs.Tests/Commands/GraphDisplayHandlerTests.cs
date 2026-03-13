namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;

public class GraphDisplayHandlerTests
{
    private class FakeDocGraph : IDocGraph
    {
        public List<(string Doc, int LineNumber)> IncomingLinks { get; set; } = [];
        public List<(string Doc, int Degree)> WithinDegreeLinks { get; set; } = [];
        public List<(string Doc, int IncomingCount)> StatsData { get; set; } = [];

        public void Build(List<DocFile> docs, string basePath) { }
        public List<(string Doc, int LineNumber)> GetIncoming(string docPath) => IncomingLinks;
        public List<(string Doc, int Degree)> GetWithinDegree(string docPath, int maxDegree) => WithinDegreeLinks;
        public bool HasDoc(string docPath) => true;
        public List<(string Doc, int IncomingCount)> GetStats() => StatsData;
    }

    #region ShowIncoming

    [Fact]
    public void ShowIncoming_WithLinks_PrintsEachLink()
    {
        var graph = new FakeDocGraph
        {
            IncomingLinks = [("docs/a.md", 5), ("docs/b.md", 12)]
        };
        var output = CaptureOutput(() => GraphDisplayHandler.ShowIncoming(graph, "docs/target.md", "target.md"));

        Assert.Contains("docs/a.md:5", output);
        Assert.Contains("docs/b.md:12", output);
        Assert.Contains("2 docs link here", output);
    }

    [Fact]
    public void ShowIncoming_NoLinks_PrintsNone()
    {
        var graph = new FakeDocGraph();
        var output = CaptureOutput(() => GraphDisplayHandler.ShowIncoming(graph, "docs/target.md", "target.md"));

        Assert.Contains("(none)", output);
    }

    #endregion

    #region ShowDegree

    [Fact]
    public void ShowDegree_WithLinks_PrintsGroupedByDegree()
    {
        var graph = new FakeDocGraph
        {
            WithinDegreeLinks = [("docs/a.md", 1), ("docs/b.md", 2)]
        };
        var output = CaptureOutput(() => GraphDisplayHandler.ShowDegree(graph, "docs/target.md", "target.md", 2));

        Assert.Contains("[degree 1]", output);
        Assert.Contains("[degree 2]", output);
        Assert.Contains("docs/a.md", output);
        Assert.Contains("docs/b.md", output);
        Assert.Contains("2 docs within 2 hops", output);
    }

    [Fact]
    public void ShowDegree_NoLinks_PrintsNoOutgoing()
    {
        var graph = new FakeDocGraph();
        var output = CaptureOutput(() => GraphDisplayHandler.ShowDegree(graph, "docs/target.md", "target.md", 2));

        Assert.Contains("(no outgoing links)", output);
    }

    #endregion

    #region ShowCombined

    [Fact]
    public void ShowCombined_WithLinks_PrintsGroupedByDegree()
    {
        var graph = new FakeDocGraph
        {
            WithinDegreeLinks = [("docs/a.md", 1), ("docs/b.md", 1)]
        };
        var output = CaptureOutput(() => GraphDisplayHandler.ShowCombined(graph, "docs/target.md", 2));

        Assert.Contains("[degree 1]", output);
        Assert.Contains("2 docs", output);
    }

    [Fact]
    public void ShowCombined_NoLinks_PrintsNone()
    {
        var graph = new FakeDocGraph();
        var output = CaptureOutput(() => GraphDisplayHandler.ShowCombined(graph, "docs/target.md", 2));

        Assert.Contains("(none)", output);
    }

    #endregion

    #region ShowStats

    [Fact]
    public void ShowStats_PrintsRankedDocuments()
    {
        var graph = new FakeDocGraph
        {
            StatsData = [("docs/popular.md", 10), ("docs/other.md", 3)]
        };
        var output = CaptureOutput(() => GraphDisplayHandler.ShowStats(graph, 5));

        Assert.Contains("popular.md", output);
        Assert.Contains("other.md", output);
        Assert.Contains("13 internal links", output);
    }

    #endregion

    private static string CaptureOutput(Action action)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
