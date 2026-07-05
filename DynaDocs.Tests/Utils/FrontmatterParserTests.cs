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
    public void ParseFields_SixDashRunOnOneLine_NotAValidBlock_ReturnsNull()
    {
        // "------" is a single line: its closing "---" is not line-anchored (it sits on the opening line),
        // so line-anchored reads correctly reject it rather than parsing a phantom empty block from the
        // "---" SUBSTRING — the exact truncation the read-side anchoring fixes (finding 4).
        const string content = "------\n\n# Body";

        Assert.Null(FrontmatterParser.ParseFields(content));
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
    public void ParseFields_ValueContainingTripleDash_ReadBackFully_NotTruncated()
    {
        // Finding 4 (read side): a value containing a "---" substring must be read in full, and later keys
        // must not be lost — the un-anchored read truncated the block at the first "---" inside the value.
        const string content = "---\ntitle: a --- b\nstatus: open\n---\n\n# Body";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Equal("a --- b", fields!["title"]);
        Assert.Equal("open", fields["status"]); // not lost by mid-value truncation
    }

    [Fact]
    public void ParseFields_ValueContainingTripleDash_CRLF_ReadBackFully()
    {
        // CRLF-safe: the same anchoring holds for Windows line endings.
        const string content = "---\r\ntitle: a --- b\r\nstatus: open\r\n---\r\n\r\n# Body";

        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Equal("a --- b", fields!["title"]);
        Assert.Equal("open", fields["status"]);
    }

    [Fact]
    public void StripFrontmatter_ValueContainingTripleDash_BodyStartsAfterRealDelimiter()
    {
        // Finding 4 (read side): StripFrontmatter must anchor the close to a line, or a "---" inside a value
        // makes the body start mid-value.
        const string content = "---\ntitle: a --- b\n---\n\n# Body\n";

        Assert.Equal("# Body\n", FrontmatterParser.StripFrontmatter(content));
    }

    [Fact]
    public void UpsertField_EmptyFrontmatterBlock_DoesNotGlueKeyOntoOpeningDelimiter()
    {
        // Finding 4 (UpsertField): a file whose frontmatter block is EMPTY ("---\n---\n"). The insert must land
        // on its own line — the old back-scan reached index 0 and glued the key onto the opening delimiter
        // ("---needs-human: true"), corrupting the file.
        const string content = "---\n---\n\n# Body\n";

        var updated = FrontmatterParser.UpsertField(content, "needs-human", "true");

        Assert.DoesNotContain("---needs-human", updated);
        var fields = FrontmatterParser.ParseFields(updated);
        Assert.NotNull(fields);
        Assert.Equal("true", fields!["needs-human"]);
        Assert.Contains("# Body", updated);
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

    [Theory]
    [InlineData("---\narea: backend\n---  \n\n# Title")]   // trailing spaces
    [InlineData("---\narea: backend\n---\t\n\n# Title")]   // trailing tab
    [InlineData("---\narea: backend\n---   ")]             // trailing spaces at end-of-content
    public void ParseFields_ClosingDelimiterWithTrailingWhitespace_StillParsed(string content)
    {
        // Finding 7: a closing "---" line carrying trailing whitespace ("---  \n") must still close the block —
        // several pre-anchoring readers accepted it, so the anchored close must too, or a previously-parseable
        // file silently degrades to frontmatter-less.
        var fields = FrontmatterParser.ParseFields(content);

        Assert.NotNull(fields);
        Assert.Equal("backend", fields!["area"]);
    }

    [Fact]
    public void StripFrontmatter_ClosingDelimiterWithTrailingWhitespace_ReturnsBody()
    {
        // Read side stays consistent: trailing whitespace on the closing delimiter does not strand the body.
        const string content = "---\narea: backend\n---  \n\n# Title\n\nBody.";

        Assert.Equal("# Title\n\nBody.", FrontmatterParser.StripFrontmatter(content));
    }

    [Fact]
    public void UpsertField_ClosingDelimiterWithTrailingWhitespace_WritesConsistently()
    {
        // Write side stays consistent with the read side (finding 7): a closing delimiter with trailing
        // whitespace is recognised, so the field is upserted rather than the file being treated as un-fronted.
        const string content = "---\nname: t\nstatus: open\n---  \n\n# Body\n";

        var updated = FrontmatterParser.UpsertField(content, "status", "closed");

        var fields = FrontmatterParser.ParseFields(updated);
        Assert.Equal("closed", fields!["status"]);
        Assert.Equal("t", fields["name"]);
        Assert.Contains("# Body", updated);
    }
}
