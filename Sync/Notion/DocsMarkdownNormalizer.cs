namespace DynaDocs.Sync.Notion;

using System.Text.RegularExpressions;
using Markdig;

/// <summary>
/// Reconciles the docs mirror's two markdown dialects (DR 035 §3). Local docs are CommonMark; Notion's native
/// Markdown Content API echoes back Notion-flavored markdown (its own spacing/marker choices, HTML tables,
/// callout/toggle syntax, <c>&lt;unknown/&gt;</c> tags for unsupported blocks) plus pre-signed, expiring
/// image/file URLs — a well-defined dialect difference, not the arbitrary lossy noise the retired block
/// converter produced. Two operations serve two distinct needs, and conflating them corrupts data:
/// <list type="bullet">
/// <item><see cref="Normalize"/> — a canonical form used ONLY to COMPARE bodies (the 3-way merge's change
/// detection). Full Markdig canonicalization collapses dialect + whitespace so a no-op tick stays a no-op and
/// only a genuine Notion edit registers, retiring the phantom-conflict corruption class (issue 0235) at its
/// root. It is deterministic and content-sensitive, so it is safe to compare with — but it MUST NOT produce
/// content written to a file: Markdig's normalize renderer flattens a pipe table to its bare cell text, which
/// is fine for a compare but would destroy the table on disk.</item>
/// <item><see cref="CleanForPersist"/> — a structure-preserving clean applied to a body READ from Notion before
/// it can be persisted to a canonical repo file: line-ending normalization and stripping the volatile signing
/// query from expiring URLs (never persist an expiring URL, DR 035 caveat), leaving tables, lists, and every
/// other construct intact.</item>
/// </list>
/// Markdig is already a dependency and is AOT/trimmer-friendly (xoofx guidance) — no reflection. The dialect
/// mapping starts minimal (whitespace/heading canonicalization) and is meant to grow empirically off the
/// fidelity corpus (DR 035 §3): HTML-table and callout/toggle convergence are revealed by the live smoke, not
/// simulated by the exact-echo fake, and are a scheduled follow-on.
/// </summary>
public static class DocsMarkdownNormalizer
{
    // Pipe/grid tables are parsed so a table's cells survive as stable text under the comparison Normalize (a
    // table renders identically on both sides); the set is meant to grow empirically off the fidelity corpus.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseGridTables()
        .Build();

    // Notion read-back image/file URLs are S3 pre-signed (SigV4) and expire (DR 035 caveat): the same file re-reads
    // with a fresh signature every tick, which would otherwise register as a body edit and churn — or, worse, persist
    // a dead signed URL as canonical. The `X-Amz-` prefix uniquely identifies SigV4 signing params, so strip ONLY
    // those: a generic `?signature=`/`?expires=` (a webhook or CDN URL a doc body legitimately carries) is NOT a
    // Notion pre-signed URL and must be left intact. Each param's value stops at `&`, so a stable param following a
    // volatile one is never eaten — a trailing param that becomes the new first param is repromoted from `&` to `?`.
    private static readonly Regex AmzTrailingParam =
        new(@"&X-Amz-[^=&]*=[^&)\s""']*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AmzLeadingParamThenMore =
        new(@"\?X-Amz-[^=&]*=[^&)\s""']*&", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AmzLeadingParam =
        new(@"\?X-Amz-[^=&]*=[^&)\s""']*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Strip every X-Amz- signing param while keeping the URL valid: remove trailing (`&`-led) ones first, so at most
    // one `?`-led param survives; if a stable param still follows it, collapse the leading param to `?`; otherwise
    // drop the sole leading param and its `?` outright.
    private static string StripSigningQuery(string s) =>
        AmzLeadingParam.Replace(
            AmzLeadingParamThenMore.Replace(
                AmzTrailingParam.Replace(s, ""), "?"), "");

    /// <summary>Canonical form for COMPARISON only — see the type summary. Never write the result to a file.</summary>
    public static string Normalize(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "";
        var lf = StripSigningQuery(LineEndings(markdown));
        return LineEndings(Markdown.Normalize(lf, pipeline: Pipeline)).TrimEnd('\n');
    }

    /// <summary>Structure-preserving clean for a Notion-read body that may be PERSISTED — see the type summary.</summary>
    public static string CleanForPersist(string markdown) =>
        string.IsNullOrEmpty(markdown) ? "" : StripSigningQuery(LineEndings(markdown)).TrimEnd('\n');

    private static string LineEndings(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n");
}
