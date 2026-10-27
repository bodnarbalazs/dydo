namespace DynaDocs.Tests;

public class PreservedTextTests
{
    // A core.autocrlf=true checkout turns every inline fixture into CRLF text.
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void AssertLinesKeptInOrder_LineGainingACommaPassesWithEitherLineEnding(string newline)
    {
        var original = string.Join(newline, "{", "  \"a\": true", "}");
        var result = string.Join(newline, "{", "  \"a\": true,", "  \"b\": 1", "}");

        PreservedText.AssertLinesKeptInOrder(original, result);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void AssertLinesKeptInOrder_ChangedLineFailsWithEitherLineEnding(string newline)
    {
        var original = string.Join(newline, "{", "  \"a\": true", "}");
        var result = string.Join(newline, "{", "  \"a\": false", "}");

        Assert.ThrowsAny<Exception>(() => PreservedText.AssertLinesKeptInOrder(original, result));
    }
}
