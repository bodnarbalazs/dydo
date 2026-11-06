namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

/// <summary>
/// A git -C tree written with backslashes, as a Windows path is (DYD-739). Bash drops an unquoted
/// backslash, so its reading of <c>C:\Users\me\wt</c> names <c>C:Usersmewt</c>; the line as written
/// names the tree the agent meant. A bash line's push takes the tree of each reading, so the guard
/// refuses it when either is on the default branch.
/// </summary>
public class PrCallScannerBackslashTreeTests
{
    private const string WindowsTree = @"C:\Users\me\wt";

    private static List<string?> Trees(List<MergeRoute> routes) =>
        [.. routes.Select(route => route.Directory).Order(StringComparer.Ordinal)];

    [Fact]
    public void CodexBashEnvelope_PushThroughAWindowsTree_TakesTheTreeAsWritten()
    {
        var routes = PrCallScanner.FindMergeRoutesInArgv(["bash", "-lc", $"git -C {WindowsTree} push"]);

        Assert.Contains(WindowsTree, Trees(routes));
    }

    [Theory]
    [InlineData($"git -C {WindowsTree} push")]
    [InlineData($"git -C {WindowsTree} push origin HEAD")]
    [InlineData($"git status && git -C {WindowsTree} push")]
    [InlineData($"echo \"$(git -C {WindowsTree} push)\"")]
    [InlineData($"bash -c 'git -C {WindowsTree} push'")]
    public void BashLine_PushThroughAWindowsTree_TakesBothReadingsTrees(string line)
    {
        var routes = PrCallScanner.FindMergeRoutes(line, ShellDialect.Posix);

        Assert.Equal(["C:Usersmewt", WindowsTree], Trees(routes));
        Assert.All(routes, route => Assert.Equal(MergeRouteKind.Push, route.Kind));
    }

    // Where bash keeps the backslashes, both readings name one tree, and the route is not doubled.
    [Fact]
    public void BashLine_QuotedWindowsTree_TakesThatTreeOnce()
    {
        var routes = PrCallScanner.FindMergeRoutes($"git -C '{WindowsTree}' push", ShellDialect.Posix);

        Assert.Equal(WindowsTree, Assert.Single(routes).Directory);
    }

    // The bash reading stays: an escaped space still names the tree bash would enter.
    [Fact]
    public void BashLine_EscapedSpaceInTheTree_KeepsTheBashTree()
    {
        var routes = PrCallScanner.FindMergeRoutes(@"git -C my\ tree push origin HEAD", ShellDialect.Posix);

        Assert.Contains("my tree", Trees(routes));
    }

    [Fact]
    public void PowerShellLine_WindowsTree_IsReadAsWritten()
    {
        var routes = PrCallScanner.FindMergeRoutes($"git -C {WindowsTree} push", ShellDialect.PowerShell);

        Assert.Equal(WindowsTree, Assert.Single(routes).Directory);
    }
}
