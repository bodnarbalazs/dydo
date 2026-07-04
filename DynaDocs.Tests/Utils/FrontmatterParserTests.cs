// @test-tier: 2
namespace DynaDocs.Tests.Utils;

using DynaDocs.Utils;

public class FrontmatterParserTests
{
    [Fact]
    public void ParseFields_StandardFrontmatter_ReturnsAllFields()
    {
        const string content = "---\narea: backend\ntype: issue\nstatus: open\n---\n\n# Title";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Equal("backend", fields!["area"]);
        Assert.Equal("issue", fields["type"]);
        Assert.Equal("open", fields["status"]);
    }

    [Fact]
    public void ParseFields_ValueContainingColons_CapturesFullValue()
    {
        const string content = "---\nreceived: 2026-03-19T10:00:00Z\n---\n";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Equal("2026-03-19T10:00:00Z", fields!["received"]);
    }

    [Fact]
    public void ParseFields_HyphenatedKeys_ParsedCorrectly()
    {
        const string content = "---\nmust-read: true\nauto-close: false\ndispatched-by: Alice\n---\n";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Equal("true", fields!["must-read"]);
        Assert.Equal("false", fields["auto-close"]);
        Assert.Equal("Alice", fields["dispatched-by"]);
    }

    [Fact]
    public void ParseFields_NoFrontmatter_ReturnsNull()
    {
        const string content = "# Just a heading\n\nSome content.";

        Assert.Null(FrontmatterParser.ParseFields(content));
    }

    [Fact]
    public void ParseFields_OnlyOpeningDelimiter_ReturnsNull()
    {
        const string content = "---\nkey: value\nNo closing delimiter";

        Assert.Null(FrontmatterParser.ParseFields(content));
    }

    [Fact]
    public void ParseFields_EmptyFrontmatter_ReturnsEmptyDictionary()
    {
        const string content = "------\n\n# Body";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Empty(fields!);
    }

    [Fact]
    public void ParseFields_EmptyFrontmatterWithNewlines_ReturnsEmptyDictionary()
    {
        const string content = "---\n\n---\n\n# Body";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Empty(fields!);
    }

    [Fact]
    public void ParseFields_LinesWithoutColons_AreSkipped()
    {
        const string content = "---\narea: backend\njust a line\nstatus: open\n---\n";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Equal(2, fields!.Count);
        Assert.Equal("backend", fields["area"]);
        Assert.Equal("open", fields["status"]);
    }

    [Fact]
    public void ParseFields_WhitespaceAroundKeyValue_IsTrimmed()
    {
        const string content = "---\n  area  :  backend  \n---\n";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Equal("backend", fields!["area"]);
    }

    [Fact]
    public void ParseFields_EmptyValue_ReturnsEmptyString()
    {
        const string content = "---\nkey:\n---\n";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Equal("", fields!["key"]);
    }

    [Fact]
    public void ParseFields_DuplicateKeys_LastWins()
    {
        const string content = "---\nkey: first\nkey: second\n---\n";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Equal("second", fields!["key"]);
    }

    [Fact]
    public void ParseFields_CarriageReturnLineEndings_HandledCorrectly()
    {
        const string content = "---\r\narea: backend\r\ntype: issue\r\n---\r\n";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Equal("backend", fields!["area"]);
        Assert.Equal("issue", fields["type"]);
    }

    [Fact]
    public void ParseFields_PreservesKeyCasing()
    {
        const string content = "---\nFrom: Alice\nTYPE: message\n---\n";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.True(fields!.ContainsKey("From"));
        Assert.True(fields.ContainsKey("TYPE"));
    }

    [Fact]
    public void ParseFields_EmptyKeyBeforeColon_IsSkipped()
    {
        const string content = "---\n: orphan-value\narea: backend\n---\n";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Single(fields!);
        Assert.Equal("backend", fields["area"]);
    }

    [Fact]
    public void StripFrontmatter_WithFrontmatter_ReturnsBody()
    {
        const string content = "---\narea: backend\n---\n\n# Title\n\nBody text.";

        var body = FrontmatterParser.StripFrontmatter(content);

        Assert.Equal("# Title\n\nBody text.", body);
    }

    [Fact]
    public void StripFrontmatter_WithoutFrontmatter_ReturnsOriginal()
    {
        const string content = "# Just a heading";

        Assert.Equal(content, FrontmatterParser.StripFrontmatter(content));
    }

    [Fact]
    public void StripFrontmatter_OnlyOpeningDelimiter_ReturnsOriginal()
    {
        const string content = "---\nkey: value\nNo closing";

        Assert.Equal(content, FrontmatterParser.StripFrontmatter(content));
    }

    [Fact]
    public void StripFrontmatter_EmptyBody_ReturnsEmpty()
    {
        const string content = "---\nkey: value\n---";

        Assert.Equal("", FrontmatterParser.StripFrontmatter(content));
    }

    [Fact]
    public void ParseFields_PathListValue_CapturedAsWholeString()
    {
        const string content = "---\nwritable-paths: Commands/**, Services/**\n---\n";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Equal("Commands/**, Services/**", fields!["writable-paths"]);
    }

    [Fact]
    public void ParseFields_JsonLikeValue_CapturedCorrectly()
    {
        const string content = "---\ntask-role-history: { \"task1\": [\"code-writer\"] }\n---\n";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Equal("{ \"task1\": [\"code-writer\"] }", fields!["task-role-history"]);
    }

    [Fact]
    public void UpsertField_ExistingKey_RewritesInPlace_PreservingOtherLinesAndBody()
    {
        const string content = "---\nname: t\nneeds-human: false\nstatus: pending\n---\n\n# Task: t\n";

        var updated = FrontmatterParser.UpsertField(content, "needs-human", "true");

        var fields = FrontmatterParser.ParseFields(updated);
        Assert.Equal("true", fields!["needs-human"]);
        Assert.Equal("t", fields["name"]);
        Assert.Equal("pending", fields["status"]);
        Assert.Contains("# Task: t", updated);
    }

    [Fact]
    public void UpsertField_MissingKey_AppendsInsideFrontmatterBlock()
    {
        const string content = "---\nname: t\nstatus: pending\n---\n\n# Body\n";

        var updated = FrontmatterParser.UpsertField(content, "needs-human", "true");

        var fields = FrontmatterParser.ParseFields(updated);
        Assert.Equal("true", fields!["needs-human"]);
        Assert.Equal("pending", fields["status"]);
        Assert.Contains("# Body", updated);
    }

    [Fact]
    public void UpsertField_NoFrontmatter_ReturnsContentUnchanged()
    {
        const string content = "# Just a heading\n";
        Assert.Equal(content, FrontmatterParser.UpsertField(content, "needs-human", "true"));
    }

    [Fact]
    public void UpsertField_ValueContainingTripleDash_DoesNotCorruptFile()
    {
        // A "---" sequence INSIDE a frontmatter value must not be mistaken for the closing delimiter, or the
        // write would truncate mid-value and corrupt the file.
        const string content = "---\ntitle: a --- b\nstatus: open\n---\n\n# Body\n";

        var updated = FrontmatterParser.UpsertField(content, "status", "closed");

        Assert.Contains("title: a --- b", updated); // the value is preserved verbatim
        Assert.Contains("status: closed", updated);
        Assert.DoesNotContain("status: open", updated);
        Assert.Contains("# Body", updated);
    }

    [Fact]
    public void UpsertField_BodyContainingTripleDash_PreservesBodyAndUpdatesField()
    {
        // A "---" horizontal rule in the body sits after the closing delimiter and must be left untouched.
        const string content = "---\nname: t\nstatus: open\n---\n\nSome text\n\n---\n\nmore text\n";

        var updated = FrontmatterParser.UpsertField(content, "status", "closed");

        var fields = FrontmatterParser.ParseFields(updated);
        Assert.Equal("closed", fields!["status"]);
        Assert.Equal("t", fields["name"]);
        Assert.Contains("Some text", updated);
        Assert.Contains("\n---\n", updated);    // the body rule survives
        Assert.Contains("more text", updated);
    }

    [Fact]
    public void UpsertField_NoLineAnchoredClose_ReturnsContentUnchanged()
    {
        // An opening delimiter whose only "---" lives inside a value (no closing line) cannot be safely
        // upserted — return the content unchanged rather than corrupt it.
        const string content = "---\nkey: a---b";
        Assert.Equal(content, FrontmatterParser.UpsertField(content, "status", "closed"));
    }

    [Fact]
    public void UpsertField_LineStartingWithTripleDashButNotDelimiter_SkippedNotTreatedAsClose()
    {
        // A line that starts with "---" but continues with more text is NOT the closing delimiter; the scan
        // must skip past it to the real close rather than truncate the block there.
        const string content = "---\nname: t\n--- not a delimiter\nstatus: open\n---\n\n# Body\n";

        var updated = FrontmatterParser.UpsertField(content, "status", "closed");

        Assert.Contains("--- not a delimiter", updated);
        Assert.Contains("status: closed", updated);
        Assert.DoesNotContain("status: open", updated);
        Assert.Contains("# Body", updated);
    }
}
