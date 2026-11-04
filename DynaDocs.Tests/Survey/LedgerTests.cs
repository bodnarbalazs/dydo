namespace DynaDocs.Tests.Survey;

using DynaDocs.Services.Survey;

public class LedgerTests
{
    [Fact]
    public void NoHeadingContainingLedger_IsNoLedger()
    {
        Assert.Null(Ledger.Parse("## Lanes\n- DYD-1 lane\n### Lane ledger\n- DYD-2 lane\n"));
    }

    [Fact]
    public void TheSection_RunsFromTheFirstLedgerHeading_ToTheNextLevelTwoHeading()
    {
        var rows = Ledger.Parse("""
            ## Goal
            - DYD-1 before the ledger
            ## Lane LEDGER — 2026-11-04 16:20 UTC (supersedes the table below)
            - DYD-2 first
            ### Detail
            - DYD-3 under a level-three heading
            ## Ledger, older
            - DYD-4 in the second ledger
            """)!;

        Assert.Equal([["DYD-2"], ["DYD-3"]], rows.Select(row => row.Keys));
    }

    [Fact]
    public void Rows_AreTableRowsAndListItems_ThatNameAnIssue()
    {
        var rows = Ledger.Parse("""
            ## Lane ledger
            Paragraph text naming DYD-1 is not a row.

            | Lane | Order | State |
            | -- | -- | -- |
            | Toolchain | [DYD-504](https://linear.app/x/issue/DYD-504/modernize) (+ DYD-519 merge) | fixing |
            - DYD-2 dash item
            * DYD-3 star item
              + DYD-4 nested plus item
            1. DYD-5 numbered item
            2) DYD-6 numbered item
            -DYD-7 is no list item
            """)!;

        Assert.Equal(
            [["DYD-504", "DYD-519"], ["DYD-2"], ["DYD-3"], ["DYD-4"], ["DYD-5"], ["DYD-6"]],
            rows.Select(row => row.Keys));
        Assert.Equal("| Toolchain | [DYD-504](https://linear.app/x/issue/DYD-504/modernize) (+ DYD-519 merge) | fixing |", rows[0].Text);
    }

    [Fact]
    public void Keys_AreEveryIdentifierInTheRow_OnceEach_InOrder()
    {
        var row = Ledger.Parse(
            "## Ledger\r\n| DYD-2 → [LC-1389](https://linear.app/x/issue/LC-1389/x), dyd-9, DYD-2, 2026-11-04, PR-x |\r\n")!.Single();

        Assert.Equal(["DYD-2", "LC-1389"], row.Keys);
    }
}
