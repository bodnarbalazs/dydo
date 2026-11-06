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

    // A backslash outside a -C tree stays bash's (DYD-739 review): an escaped quote is text inside
    // its quotes, and a backslash-newline joins two lines into one command, so no route is invented.
    [Theory]
    [InlineData("git commit -m \"Document \\\"git fetch && gh pr merge 12\\\" usage\"")]
    [InlineData("git commit -m \"don't \\\"x; gh repo edit --default-branch y\\\"\"")]
    [InlineData("printf \"example: \\\"; gh pr merge 5; \\\"\\n\"")]
    [InlineData("echo \\\ngh pr merge 5")]
    [InlineData("echo \\; git push origin main")]
    public void BashLine_BackslashInTextOrAContinuation_TakesNoRoute(string line)
    {
        Assert.Empty(PrCallScanner.FindMergeRoutes(line, ShellDialect.Posix));
        Assert.Empty(PrCallScanner.FindMergeRoutesInArgv(["bash", "-lc", line]));
    }

    [Fact]
    public void BashLine_PushContinuedOnTheNextLine_TakesOnlyTheBashRoute()
    {
        const string line = "git push \\\n  -u origin work";
        MergeRoute[] expected = [new(MergeRouteKind.Push, "git push -u origin work", "work")];

        Assert.Equal(expected, PrCallScanner.FindMergeRoutes(line, ShellDialect.Posix));
        Assert.Equal(expected, PrCallScanner.FindMergeRoutesInArgv(["bash", "-lc", line]));
    }

    // Only a push bash runs takes a tree as written: a -C push inside escaped quotes is echo's text.
    [Fact]
    public void BashLine_WindowsTreePushInsideEscapedQuotes_TakesNoRoute()
    {
        var routes = PrCallScanner.FindMergeRoutes($"echo \"\\\"; git -C {WindowsTree} push origin main; \\\"\"", ShellDialect.Posix);

        Assert.Empty(routes);
    }

    [Fact]
    public void PowerShellLine_WindowsTree_IsReadAsWritten()
    {
        var routes = PrCallScanner.FindMergeRoutes($"git -C {WindowsTree} push", ShellDialect.PowerShell);

        Assert.Equal(WindowsTree, Assert.Single(routes).Directory);
    }
}
