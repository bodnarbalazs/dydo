namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Notion;

/// <summary>The DR 035 §3 dialect normalizer, split into a comparison form (<see cref="DocsMarkdownNormalizer.Normalize"/>)
/// and a structure-preserving persist form (<see cref="DocsMarkdownNormalizer.CleanForPersist"/>). The corpus
/// spans the drift-prone constructs the fidelity work targets: blank-line/heading spacing, fenced code with a
/// language, ordered + nested lists, inline formatting, a table, an <c>&lt;unknown/&gt;</c> construct, and
/// Notion's expiring pre-signed URLs.</summary>
public class DocsMarkdownNormalizerTests
{
    public static IEnumerable<object[]> Corpus =>
    [
        ["blank+heading", "# Arch\n\nbody one.\n\n## Sub\n\nmore."],
        ["fenced-code", "```csharp\nvar x = 1;\n```"],
        ["ordered-list", "1. one\n2. two\n3. three"],
        ["nested-list", "- a\n  - b\n  - c\n- d"],
        ["inline", "Some **bold**, *italic*, `code`, and [a link](https://x.test)."],
        ["table", "| A | B |\n|---|---|\n| 1 | 2 |"],
        ["unknown-tag", "<unknown foo=\"bar\" />\n\ntext after."],
    ];

    [Theory]
    [MemberData(nameof(Corpus))]
    public void Normalize_IsIdempotent(string _, string md)
    {
        var once = DocsMarkdownNormalizer.Normalize(md);
        Assert.Equal(once, DocsMarkdownNormalizer.Normalize(once));
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void CleanForPersist_IsIdempotent(string _, string md)
    {
        var once = DocsMarkdownNormalizer.CleanForPersist(md);
        Assert.Equal(once, DocsMarkdownNormalizer.CleanForPersist(once));
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void CleanForPersist_PreservesStructure_NeverFlattens(string label, string md)
    {
        // CleanForPersist is what may be written back to a canonical file, so it must never destroy structure.
        // A table in particular must survive verbatim (the comparison Normalize deliberately flattens it, which
        // is why the two operations are distinct — regression guard for the table-destruction bug).
        var cleaned = DocsMarkdownNormalizer.CleanForPersist(md);
        Assert.Equal(md.Replace("\r\n", "\n"), cleaned);
        if (label == "table")
            Assert.Contains("| A | B |", cleaned);
    }

    [Fact]
    public void Normalize_CollapsesLineEndingAndTrailingWhitespaceDrift()
    {
        // CRLF vs LF and trailing spaces are pure dialect drift — they must not register as an edit.
        Assert.Equal(
            DocsMarkdownNormalizer.Normalize("line one\n\nline two"),
            DocsMarkdownNormalizer.Normalize("line one  \r\n\r\nline two"));
    }

    [Fact]
    public void Normalize_CollapsesTrailingBlankLines()
    {
        Assert.Equal(
            DocsMarkdownNormalizer.Normalize("body"),
            DocsMarkdownNormalizer.Normalize("body\n\n\n"));
    }

    [Fact]
    public void Normalize_StripsExpiringUrlSignature_SoAFreshSignatureIsNotAnEdit()
    {
        // The same image re-reads with a fresh pre-signed signature every tick; two differently-signed reads of
        // the same file must normalize equal, and the volatile signing query must be gone.
        var a = "![i](https://f.notion.so/x.png?X-Amz-Signature=AAA&X-Amz-Expires=900)";
        var b = "![i](https://f.notion.so/x.png?X-Amz-Signature=ZZZ&X-Amz-Expires=100)";
        Assert.Equal(DocsMarkdownNormalizer.Normalize(a), DocsMarkdownNormalizer.Normalize(b));
        Assert.DoesNotContain("X-Amz-Signature", DocsMarkdownNormalizer.Normalize(a));
    }

    [Fact]
    public void CleanForPersist_StripsExpiringUrlSignature_NeverPersistsIt()
    {
        var signed = "![i](https://f.notion.so/x.png?X-Amz-Signature=AAA&X-Amz-Expires=900)";
        var cleaned = DocsMarkdownNormalizer.CleanForPersist(signed);
        Assert.DoesNotContain("X-Amz-Signature", cleaned);
    }

    [Theory]
    [InlineData("A webhook URL: [hook](https://api.test/callback?signature=abc123&event=push).")]
    [InlineData("A CDN asset: [file](https://cdn.test/a.pdf?expires=3600&region=eu).")]
    public void CleanForPersist_LeavesLegitimateSignatureAndExpiresParams_Untouched(string body)
    {
        // A generic ?signature=/?expires= is NOT a Notion pre-signed URL — only the X-Amz- SigV4 prefix identifies
        // one. Stripping these would corrupt legitimate URLs a doc body carries, so they must survive verbatim.
        Assert.Equal(body, DocsMarkdownNormalizer.CleanForPersist(body));
    }

    [Fact]
    public void CleanForPersist_StripsPreSignedUrl_ButKeepsAFollowingStableParam()
    {
        // The old [^)\s"']* tail ate everything after the first volatile param, losing a stable param that followed
        // it. A stripped X-Amz- param must NOT swallow a trailing &page=2 — it survives, repromoted to the query head.
        var signed = "![i](https://f.notion.so/x.png?X-Amz-Signature=abc&page=2)";
        var cleaned = DocsMarkdownNormalizer.CleanForPersist(signed);
        Assert.DoesNotContain("X-Amz-Signature", cleaned);
        Assert.Contains("page=2", cleaned);
        Assert.Equal("![i](https://f.notion.so/x.png?page=2)", cleaned);
    }

    [Fact]
    public void Empty_NormalizesToEmpty_OnBothOperations()
    {
        Assert.Equal("", DocsMarkdownNormalizer.Normalize(""));
        Assert.Equal("", DocsMarkdownNormalizer.CleanForPersist(""));
    }
}
