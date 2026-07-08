namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Notion;

/// <summary>The DR 035 §5 fidelity suite: a corpus of docs mirroring the constructs the NAMED real docs
/// actually carry — thematic breaks (<c>---</c>) mid-body (architecture, edge), an HTML comment block
/// (coding-standards, edge), real pipe tables (architecture, table), fenced code with a language, ordered +
/// nested lists, inline formatting, an <c>&lt;unknown/&gt;</c>-triggering construct, and a query-bearing URL —
/// is pushed through the native-markdown-endpoint path and asserted to CONVERGE: the repo is byte-unchanged
/// after a sync (no phantom conflicts, the root cause of issue 0235), a re-run is idempotent, and
/// <see cref="DocsMarkdownNormalizer.CleanForPersist"/> is byte-safe over the realistic bodies (its
/// signature-stripping regex must leave a legitimate query untouched while stripping a pre-signed one). The
/// fake echoes markdown losslessly; the Notion-flavored dialect drift only a live board exhibits is Charlie's
/// live smoke, not simulated here.</summary>
public class DocsFidelityTests : IDisposable
{
    private readonly string _root;
    private readonly string _dydoRoot;

    // A minimal spine model (its one object type's dir is excluded from the mirror) — the loader requires at
    // least one object type; the corpus lives entirely outside project/campaigns so nothing is excluded.
    private const string SpineModel = """
        { "objects": [ { "type": "Campaign", "dir": "project/campaigns", "notionTitle": "Campaigns",
          "properties": { "title": { "type": "title" } } } ] }
        """;

    // Bodies mirror the real docs' drift-prone constructs (see the type summary): thematic breaks, HTML comment
    // blocks, real pipe tables, fenced code with a language, ordered + nested lists, inline formatting, an
    // <unknown/> block, and both a legitimate query-bearing URL and (in the CleanForPersist test) a pre-signed one.
    // None carry a trailing newline (the raw-literal terminator drops it), matching a dydo doc body on disk.
    private static readonly (string Rel, string Body)[] Corpus =
    [
        // A real pipe table AND a thematic break (--- mid-body), as architecture.md's Guard System section carries.
        ("understand/architecture.md",
            "# Architecture\n\nAn overview of the guard's staged access model.\n\n| Stage | Condition | Can Do |\n|-------|-----------|--------|\n| 0 | No identity | Read bootstrap only |\n| 1 | Claimed, no role | Read own mode files |\n| 2 | Claimed + role | Read everything |\n\n---\n\n## Stack\n\n- .NET 10\n- Markdig"),
        // A legitimate query-bearing URL — CleanForPersist's signature regex must NOT strip a normal query.
        ("understand/about.md",
            "# About\n\nParagraph one links to [the guide](https://example.test/docs?tab=guide&v=2).\n\nParagraph two."),
        // An HTML comment block and fenced code with a language, as coding-standards.md carries.
        ("guides/coding-standards.md",
            "# Standards\n\nUse fenced code:\n\n```csharp\nvar x = 1;\n```\n\n<!--\nAdd stack-specific standards as the project grows:\n- guides/backend/_index.md\n-->\n\nDone."),
        ("guides/testing-strategy.md",
            "# Testing\n\nRun the suite:\n\n```bash\npython run_tests.py\n```\n\nThen check coverage."),
        ("guides/how-to-use-docs.md",
            "# How To\n\n1. First\n2. Second\n   - nested a\n   - nested b\n3. Third"),
        ("project/changelog/2-0-1.md",
            "# 2.0.1\n\nChanges:\n\n1. Added markdown sync\n   - reads via GET\n   - writes via PATCH\n2. Retired the converter for docs\n3. Fixed phantom conflicts"),
        ("reference/dydo-commands.md",
            "# Commands\n\nRun **dydo sync** or *dydo init*; see [docs](https://example.test/ref?cmd=sync)."),
        ("glossary.md",
            "# Glossary\n\n**Agent** — a worker. `dydo` — the CLI."),
        ("reference/table.md",
            "# Table\n\n| Command | Effect |\n|---------|--------|\n| init | scaffold |\n| sync | mirror |"),
        // An <unknown/> block bracketed by an HTML comment and a thematic break — every construct at once.
        ("reference/edge.md",
            "# Edge\n\n<!-- editor note: leave this block as-is -->\n\n<unknown block=\"x\" />\n\n---\n\nText after an unsupported construct."),
    ];

    // A Notion read-back image URL is pre-signed and expires — CleanForPersist must strip the volatile signing
    // query (never persist a dead signature) while keeping the stable path and surrounding content intact.
    private const string PreSignedUrlBody =
        "# Diagram\n\n![architecture](https://files.notion.so/secure/abc.png?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Signature=deadbeef)\n\nCaption text.";

    public DocsFidelityTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dydo-fidelity-" + Guid.NewGuid().ToString("N")[..8]);
        _dydoRoot = Path.Combine(_root, "dydo");
        foreach (var (rel, body) in Corpus)
            Seed(rel, $"---\ntitle: {Path.GetFileNameWithoutExtension(rel)}\n---\n\n{body}");
        WriteModel(SpineModel);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private void Seed(string rel, string content)
    {
        var full = Path.Combine(_dydoRoot, rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private void WriteModel(string json)
    {
        var path = Path.Combine(_dydoRoot, "_system", "sync-model.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private string DocPath(string rel) => Path.Combine(_dydoRoot, rel.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void Sync_LeavesRepoByteUnchanged_AndIsIdempotent_NoPhantomConflicts()
    {
        var before = Corpus.ToDictionary(c => c.Rel, c => File.ReadAllText(DocPath(c.Rel)));

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        // The acceptance (issue 0235): pushing the corpus never rewrites a single canonical file.
        foreach (var (rel, original) in before)
            Assert.Equal(original, File.ReadAllText(DocPath(rel)));

        // A second identical tick is a pure no-op: no body re-pushed and still byte-unchanged.
        client.MarkdownUpdates.Clear();
        var output = new StringWriter();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, output);

        Assert.Empty(client.MarkdownUpdates);
        Assert.DoesNotContain("conflict", output.ToString());
        foreach (var (rel, original) in before)
            Assert.Equal(original, File.ReadAllText(DocPath(rel)));
    }

    [Fact]
    public void Sync_PushesBody_ThatReadsBackConvergent_PerCorpusDoc()
    {
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        // For each corpus doc, the body read back through the markdown endpoint converges with the original
        // (DR 035 §5). A table in particular must round-trip verbatim, not be flattened.
        foreach (var (rel, body) in Corpus)
        {
            var pageId = PageIdFor(client, rel);
            var readBack = client.GetPageMarkdown(pageId);
            Assert.Equal(DocsMarkdownNormalizer.Normalize(body), DocsMarkdownNormalizer.Normalize(readBack));
            if (rel.EndsWith("table.md"))
                Assert.Contains("| Command | Effect |", readBack);
        }
    }

    [Fact]
    public void CleanForPersist_IsByteSafe_OverRealisticContent_ButStripsPreSignedUrls()
    {
        // The persist-side clean (applied to a Notion-read body before it can land on a canonical file) must be
        // byte-safe over realistic content: tables, thematic breaks, HTML comments, fenced code, nested lists,
        // and a LEGITIMATE query URL all survive verbatim — the signature-stripping regex (line 41-42) must not
        // over-match a normal ?tab=…&v=… query. The corpus bodies use \n and carry no trailing newline, so
        // CleanForPersist is an identity over them.
        foreach (var (rel, body) in Corpus)
            Assert.Equal(body, DocsMarkdownNormalizer.CleanForPersist(body));

        // The one case it MUST rewrite: a pre-signed, expiring URL's signing query is stripped, its stable path
        // and the surrounding markdown kept — never persist a dead signature (DR 035 caveat).
        var cleaned = DocsMarkdownNormalizer.CleanForPersist(PreSignedUrlBody);
        Assert.DoesNotContain("X-Amz-", cleaned);
        Assert.DoesNotContain("deadbeef", cleaned);
        Assert.Contains("https://files.notion.so/secure/abc.png", cleaned);
        Assert.Contains("Caption text.", cleaned);
    }

    /// <summary>Resolve a corpus doc's Notion page id by walking the managed tree from the "Docs" root, matching
    /// the page whose stored markdown body equals the doc's pushed body.</summary>
    private string PageIdFor(FakeNotionClient client, string rel)
    {
        var title = Path.GetFileNameWithoutExtension(rel);
        string? Find(string parent)
        {
            foreach (var child in client.GetChildPages(parent))
            {
                if (child.Title == title)
                    return child.Id;
                if (Find(child.Id) is { } found)
                    return found;
            }
            return null;
        }
        var root = client.GetChildPages("workspace").Single(p => p.Title == "Docs").Id;
        return Find(root) ?? throw new InvalidOperationException($"no page for {rel}");
    }
}
