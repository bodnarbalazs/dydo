namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Notion.Dtos;

public class NotionRichTextTests
{
    [Fact]
    public void Of_ShortContent_IsASingleRun()
    {
        var runs = NotionRichText.Of("hello");
        Assert.Single(runs);
        Assert.Equal("hello", runs[0].Text!.Content);
    }

    [Fact]
    public void Of_EmptyContent_IsASingleEmptyRun()
    {
        var runs = NotionRichText.Of("");
        Assert.Single(runs);
        Assert.Equal("", runs[0].Text!.Content);
    }

    [Fact]
    public void Of_OverLimitContent_SplitsIntoRunsUnderTheCap_AndRoundTrips()
    {
        // Notion rejects a rich-text run longer than 2000 chars with a 400 that aborts the reconcile
        // (observed on a 2065-char issue paragraph). The runs must each be within the cap and concatenate
        // back to the original.
        var content = new string('a', 2000) + new string('b', 65);

        var runs = NotionRichText.Of(content);

        Assert.Equal(2, runs.Count);
        Assert.All(runs, r => Assert.True(r.Text!.Content.Length <= 2000));
        Assert.Equal(content, NotionRichText.Flatten(runs));
    }

    [Fact]
    public void Of_DoesNotSplitASurrogatePairAcrossRuns()
    {
        // 😀 is a surrogate pair (2 UTF-16 units). With exactly 1999 filler chars before it, a naive split
        // at 2000 would cut the pair in half, emitting invalid text. It must move whole to the next run.
        var content = new string('a', 1999) + "\U0001F600" + new string('b', 100);

        var runs = NotionRichText.Of(content);

        Assert.All(runs, r => Assert.False(char.IsHighSurrogate(r.Text!.Content[^1]))); // no run ends mid-pair
        Assert.Equal(content, NotionRichText.Flatten(runs));
    }
}
