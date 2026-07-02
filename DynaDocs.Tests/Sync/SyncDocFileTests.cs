// @test-tier: 2
namespace DynaDocs.Tests.Sync;

using DynaDocs.Models;
using DynaDocs.Sync;

public class SyncDocFileTests
{
    [Fact]
    public void Parse_PreservesFrontmatterOrder()
    {
        const string content = "---\nstatus: open\npriority: high\nassignee: kim\n---\n\n# Title\n\nBody text.";

        var doc = SyncDocFile.Parse(content, "my-task", "tasks/my-task.md");

        Assert.Equal(["status", "priority", "assignee"], doc.Fields.Select(f => f.Key));
        Assert.Equal("kim", doc.GetField("assignee"));
        Assert.Equal("my-task", doc.LocalId);
    }

    [Fact]
    public void Parse_KeepsBodyIntact()
    {
        const string content = "---\nstatus: open\n---\n\n# Heading\n\n- item one\n- item two\n";

        var doc = SyncDocFile.Parse(content, "t", "tasks/t.md");

        Assert.Equal("# Heading\n\n- item one\n- item two\n", doc.Body);
    }

    [Fact]
    public void Parse_ValueWithColon_Preserved()
    {
        const string content = "---\nlink: https://example.com/x\n---\n\nbody";
        var doc = SyncDocFile.Parse(content, "t", "tasks/t.md");
        Assert.Equal("https://example.com/x", doc.GetField("link"));
    }

    [Fact]
    public void Parse_NoFrontmatter_AllBody()
    {
        var doc = SyncDocFile.Parse("# Just a body\n", "t", "tasks/t.md");
        Assert.Empty(doc.Fields);
        Assert.Equal("# Just a body\n", doc.Body);
    }

    [Fact]
    public void Render_RoundTrips_OrderAndBody()
    {
        const string content = "---\nstatus: open\npriority: high\n---\n\n# Title\n\nLine.";
        var doc = SyncDocFile.Parse(content, "t", "tasks/t.md");

        var rendered = SyncDocFile.Render(doc);
        var reparsed = SyncDocFile.Parse(rendered, "t", "tasks/t.md");

        Assert.Equal(doc.Fields.Select(f => (f.Key, f.Value)), reparsed.Fields.Select(f => (f.Key, f.Value)));
        Assert.Equal(doc.Body, reparsed.Body);
    }

    [Fact]
    public void WriteThenRead_RoundTripsFromDisk()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dydo-syncdoc-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(dir, "t.md");
            var doc = new SyncDoc
            {
                LocalId = "t",
                Fields = [new SyncField { Key = "status", Value = "open" }],
                Body = "# T\n\nbody",
                SourcePath = "tasks/t.md",
            };

            SyncDocFile.Write(path, doc);
            var read = SyncDocFile.Read(path, "t", "tasks/t.md");

            Assert.Equal("open", read.GetField("status"));
            Assert.Equal("# T\n\nbody", read.Body);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Parse_OpenDelimiterButNoClose_AllBody()
    {
        // A leading '---' with no closing delimiter is not valid frontmatter: keep the whole
        // content as body rather than swallowing it.
        var doc = SyncDocFile.Parse("---\nstatus: open\nstill body, no close", "t", "tasks/t.md");
        Assert.Empty(doc.Fields);
        Assert.Equal("---\nstatus: open\nstill body, no close", doc.Body);
    }

    [Fact]
    public void Parse_CarriageReturns_Normalized()
    {
        const string content = "---\r\nstatus: open\r\n---\r\n\r\nbody line";
        var doc = SyncDocFile.Parse(content, "t", "tasks/t.md");
        Assert.Equal("open", doc.GetField("status"));
        Assert.Equal("body line", doc.Body);
    }

    [Fact]
    public void Render_ExternalValueWithNewlinesAndDelimiter_CannotForgeKeysOrInjectBody()
    {
        // An externally-authored field value carrying newlines + a `---` must not escape its frontmatter
        // line: no forged sibling key, no injected body — and it round-trips back as the original value.
        const string malicious = "ship v2\nstatus: active\n---\n# injected";
        var doc = new SyncDoc
        {
            LocalId = "t",
            Fields = [new SyncField { Key = "goal", Value = malicious }],
            Body = "real body",
            SourcePath = "tasks/t.md",
        };

        var reparsed = SyncDocFile.Parse(SyncDocFile.Render(doc), "t", "tasks/t.md");

        Assert.Single(reparsed.Fields);                    // only 'goal' — no forged 'status'
        Assert.Null(reparsed.GetField("status"));
        Assert.Equal(malicious, reparsed.GetField("goal")); // original single value, byte-for-byte
        Assert.Equal("real body", reparsed.Body);          // body not hijacked by the injected '---'
    }

    [Fact]
    public void Render_ValueWithQuotesBackslashAndCarriageReturn_RoundTripsExactly()
    {
        // Exercise every escape path: an embedded quote, a backslash, and a CR — all must survive
        // the double-quoted encode/decode byte-for-byte.
        const string value = "a \"quoted\" b \\ c\rd\ne";
        var doc = new SyncDoc
        {
            LocalId = "t",
            Fields = [new SyncField { Key = "k", Value = value }],
            Body = "body",
            SourcePath = "tasks/t.md",
        };

        var reparsed = SyncDocFile.Parse(SyncDocFile.Render(doc), "t", "tasks/t.md");

        Assert.Equal(value, reparsed.GetField("k"));
    }

    [Fact]
    public void Render_ValueStartingWithQuote_IsQuoted_AndRoundTrips()
    {
        // A value that merely starts with a double quote is quoted so read-back is unambiguous.
        const string value = "\"already quoted\"";
        var doc = new SyncDoc
        {
            LocalId = "t",
            Fields = [new SyncField { Key = "k", Value = value }],
            Body = "body",
            SourcePath = "tasks/t.md",
        };

        var reparsed = SyncDocFile.Parse(SyncDocFile.Render(doc), "t", "tasks/t.md");

        Assert.Equal(value, reparsed.GetField("k"));
    }

    [Fact]
    public void Parse_MalformedUnterminatedQuotedKey_LineSkipped()
    {
        // A frontmatter line that opens a quote but never closes it is not a valid field — skip it
        // rather than mis-splitting on a stray colon.
        var doc = SyncDocFile.Parse("---\n\"oops no close\n---\n\nbody", "t", "tasks/t.md");

        Assert.Empty(doc.Fields);
        Assert.Equal("body", doc.Body);
    }

    [Fact]
    public void Render_KeyContainingColon_RoundTripsExactly()
    {
        // A Notion property name may contain a colon (e.g. "a:b"). Unquoted it would render as `a:b: v`
        // and reparse with the wrong split; quoting the key keeps SeparatorColon/Decode reading it back whole.
        var doc = new SyncDoc
        {
            LocalId = "t",
            Fields = [new SyncField { Key = "a:b", Value = "v" }],
            Body = "body",
            SourcePath = "tasks/t.md",
        };

        var reparsed = SyncDocFile.Parse(SyncDocFile.Render(doc), "t", "tasks/t.md");

        Assert.Single(reparsed.Fields);
        Assert.Equal("a:b", reparsed.Fields[0].Key);
        Assert.Equal("v", reparsed.Fields[0].Value);
    }

    [Fact]
    public void Parse_HandAuthoredQuotedValue_NotUnquoted()
    {
        // A value a human wrapped in quotes is not something Encode would emit for `active`, so Decode must
        // pass it through verbatim — never silently strip the quotes into `active`.
        var doc = SyncDocFile.Parse("---\nstatus: \"active\"\n---\n\nbody", "t", "tasks/t.md");

        Assert.Equal("\"active\"", doc.GetField("status"));
    }

    [Theory]
    [InlineData("\"a\"b\"")]   // a raw inner quote — Encode always escapes these
    [InlineData("\"abc\\\"")]  // a trailing lone backslash (the closing quote is actually escaped)
    [InlineData("\"a\\xb\"")]  // an escape sequence Encode never emits
    public void Parse_QuotedTokenNotEncodeOutput_PassedThroughVerbatim(string raw)
    {
        // A token that is wrapped in quotes but is not well-formed Encode output must not be unescaped —
        // it is passed through byte-for-byte rather than mangled.
        var doc = SyncDocFile.Parse($"---\nk: {raw}\n---\n\nbody", "t", "tasks/t.md");

        Assert.Equal(raw, doc.GetField("k"));
    }

    [Fact]
    public void Render_ExternalKeyWithNewline_CannotForgeSiblingKey()
    {
        // A field KEY can be externally authored too; a newline in it must not forge a sibling line.
        const string maliciousKey = "goal\nstatus: active";
        var doc = new SyncDoc
        {
            LocalId = "t",
            Fields = [new SyncField { Key = maliciousKey, Value = "v" }],
            Body = "body",
            SourcePath = "tasks/t.md",
        };

        var reparsed = SyncDocFile.Parse(SyncDocFile.Render(doc), "t", "tasks/t.md");

        Assert.Single(reparsed.Fields);
        Assert.Null(reparsed.GetField("status"));
        Assert.Equal("v", reparsed.GetField(maliciousKey)); // key round-trips whole, still one field
    }
}
