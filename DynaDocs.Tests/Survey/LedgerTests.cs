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
        Assert.Equal("Toolchain | DYD-504 (+ DYD-519 merge) | fixing", rows[0].Text);
        Assert.Equal("DYD-3 star item", rows[2].Text);
    }

    [Fact]
    public void Keys_AreEveryIdentifierInTheRow_OnceEach_InOrder()
    {
        var row = Ledger.Parse(
            "## Ledger\r\n| Lane |\r\n| -- |\r\n| DYD-2 → [LC-1389](https://linear.app/x/issue/LC-1389/x), dyd-9, DYD-2, 2026-11-04, PR-x |\r\n")!.Single();

        Assert.Equal(["DYD-2", "LC-1389"], row.Keys);
    }

    [Fact]
    public void ATableWithoutEdgePipes_IsRows_AndItsSeparatorRowIsNot()
    {
        var rows = Ledger.Parse("""
            ## Ledger
            Lane | Ticket | State
            --- | --- | ---
            Core | DYD-1 | running
            Old | DYD-2 | waiting
            """)!;

        Assert.Equal([["DYD-1"], ["DYD-2"]], rows.Select(row => row.Keys));
        Assert.Equal("Core | DYD-1 | running", rows[0].Text);
    }

    [Fact]
    public void FencedCode_HoldsNoRow_AndItsHeadingDoesNotEndTheLedger()
    {
        var rows = Ledger.Parse("""
            ## Ledger
            ```
            - DYD-1 is an example, not a lane
            ## DYD-2 is no heading
            ```
            - DYD-3 lane
            ~~~
            | DYD-4 | not a row |
            ~~~
            - DYD-5 lane
            ## Next
            - DYD-6 after the ledger
            """)!;

        Assert.Equal([["DYD-3"], ["DYD-5"]], rows.Select(row => row.Keys));
    }

    [Fact]
    public void AListItem_KeepsItsContinuationLines()
    {
        var rows = Ledger.Parse("""
            ## Ledger
            - DYD-1 lane,
              continued by DYD-2
            lazily continued by DYD-3
            - DYD-4 next item
            """)!;

        Assert.Equal([["DYD-1", "DYD-2", "DYD-3"], ["DYD-4"]], rows.Select(row => row.Keys));
        Assert.Equal("DYD-1 lane, continued by DYD-2 lazily continued by DYD-3", rows[0].Text);
    }

    [Fact]
    public void ARowsText_IsWhatItShows_WithoutLinkTargetsOrBareUrls()
    {
        var rows = Ledger.Parse("""
            ## Ledger
            - [DYD-1](https://linear.app/x/issue/DYD-1/inquisition-completed) waits, see https://example.com/done and <https://x.test/merged>
            - [the core lane](https://linear.app/x/issue/DYD-7/core) and DYD-8
            """)!;

        Assert.Equal("DYD-1 waits, see and", rows[0].Text);
        Assert.Equal("the core lane and DYD-8", rows[1].Text);
        Assert.Equal(["DYD-7", "DYD-8"], rows[1].Keys);
    }

    [Fact]
    public void ARowsText_KeepsEmphasisCodeAndEntities_DropsHtmlTags_AndRowsInAQuoteCount()
    {
        var rows = Ledger.Parse(
            "## Ledger\n- **DYD-1** _waits_ on `DYD-2` &amp; <span>review</span>\\\n  more\n> - DYD-3 quoted lane\n")!;

        Assert.Equal("DYD-1 waits on DYD-2 & review more", rows[0].Text);
        Assert.Equal(["DYD-1", "DYD-2"], rows[0].Keys);
        Assert.Equal(["DYD-3"], rows[1].Keys);
    }
}
