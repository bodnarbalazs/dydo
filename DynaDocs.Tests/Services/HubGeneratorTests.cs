namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class HubGeneratorTests
{
    [Fact]
    public void GenerateHub_NonChangelogFolder_PrefersDocTitleOverFilename()
    {
        var doc = MakeDoc(
            relativePath: "guides/testing-strategy.md",
            fileName: "testing-strategy.md",
            title: "Testing Strategy — Three-Tier System");

        var hub = HubGenerator.GenerateHub(
            relativeFolderPath: "guides",
            docsInFolder: [doc],
            subfolderHubs: [],
            allDocs: [doc]);

        Assert.Contains("[Testing Strategy — Three-Tier System](./testing-strategy.md)", hub);
        Assert.DoesNotContain("[Testing Strategy](./testing-strategy.md)", hub);
    }

    [Fact]
    public void GenerateHub_TitleWithLinkLiteral_SwapsBracketsSoEntryLinkParses()
    {
        var doc = MakeDoc(
            relativePath: "project/decisions/anchor-bug.md",
            fileName: "anchor-bug.md",
            title: "Anchor-only links [label](#section) produce broken-link errors");

        var hub = HubGenerator.GenerateHub(
            relativeFolderPath: "project/decisions",
            docsInFolder: [doc],
            subfolderHubs: [],
            allDocs: [doc]);

        Assert.Contains("[Anchor-only links (label)(#section) produce broken-link errors](./anchor-bug.md)", hub);
        Assert.DoesNotContain("[label](#section)", hub);
    }

    [Fact]
    public void EscapeLinkLiterals_TitleWithoutLinkShape_PassesThroughUntouched()
    {
        Assert.Equal("Fix the [VERIFY] markers in migration", HubGenerator.EscapeLinkLiterals("Fix the [VERIFY] markers in migration"));
    }

    [Fact]
    public void GenerateHub_NonChangelogFolder_FallsBackToKebabWhenTitleNull()
    {
        var doc = MakeDoc(
            relativePath: "guides/coding-standards.md",
            fileName: "coding-standards.md",
            title: null);

        var hub = HubGenerator.GenerateHub(
            relativeFolderPath: "guides",
            docsInFolder: [doc],
            subfolderHubs: [],
            allDocs: [doc]);

        Assert.Contains("[Coding Standards](./coding-standards.md)", hub);
    }

    [Fact]
    public void GenerateHub_ChangelogFolder_UsesKebabLabelEvenWhenTitleSet()
    {
        var doc = MakeDoc(
            relativePath: "project/changelog/2026/2026-05-04/auto-resume-smoke-v140.md",
            fileName: "auto-resume-smoke-v140.md",
            title: "Task: auto-resume-smoke-v140");

        var hub = HubGenerator.GenerateHub(
            relativeFolderPath: "project/changelog/2026/2026-05-04",
            docsInFolder: [doc],
            subfolderHubs: [],
            allDocs: [doc]);

        Assert.Contains("[Auto Resume Smoke V140](./auto-resume-smoke-v140.md)", hub);
        Assert.DoesNotContain("Task: auto-resume-smoke-v140", hub);
    }

    [Fact]
    public void GenerateHub_ChangelogFolder_OmitsSummaryFromEntries()
    {
        var doc = MakeDoc(
            relativePath: "project/changelog/2026/2026-05-04/fix-wait-race.md",
            fileName: "fix-wait-race.md",
            title: "Task: fix-wait-race",
            summary: "Review commit b33a171 for fix-wait-race (#0147). Lots more verbose context.");

        var hub = HubGenerator.GenerateHub(
            relativeFolderPath: "project/changelog/2026/2026-05-04",
            docsInFolder: [doc],
            subfolderHubs: [],
            allDocs: [doc]);

        Assert.Contains("- [Fix Wait Race](./fix-wait-race.md)", hub);
        Assert.DoesNotContain("Review commit", hub);
        Assert.DoesNotContain("- [Fix Wait Race](./fix-wait-race.md) -", hub);
    }

    [Fact]
    public void GenerateHub_NonChangelogFolder_KeepsSummaryWhenPresent()
    {
        var doc = MakeDoc(
            relativePath: "guides/coding-standards.md",
            fileName: "coding-standards.md",
            title: "Coding Standards",
            summary: "Rules and conventions for writing code in this project.");

        var hub = HubGenerator.GenerateHub(
            relativeFolderPath: "guides",
            docsInFolder: [doc],
            subfolderHubs: [],
            allDocs: [doc]);

        Assert.Contains("- [Coding Standards](./coding-standards.md) - Rules and conventions for writing code in this project.", hub);
    }

    [Fact]
    public void GenerateHub_ChangelogFolder_BackslashPathStillUsesKebabLabel()
    {
        var doc = MakeDoc(
            relativePath: @"project\changelog\2026\2026-05-04\fix-wait-race.md",
            fileName: "fix-wait-race.md",
            title: "Task: fix-wait-race");

        var hub = HubGenerator.GenerateHub(
            relativeFolderPath: @"project\changelog\2026\2026-05-04",
            docsInFolder: [doc],
            subfolderHubs: [],
            allDocs: [doc]);

        Assert.Contains("[Fix Wait Race](./fix-wait-race.md)", hub);
        Assert.DoesNotContain("Task: fix-wait-race", hub);
    }

    [Fact]
    public void GenerateHub_ProjectFolder_HasNoRetiredWorkProse()
    {
        var hub = HubGenerator.GenerateHub(
            relativeFolderPath: "project",
            docsInFolder: [],
            subfolderHubs: [],
            allDocs: []);

        Assert.DoesNotContain("## Tasks", hub);
        Assert.DoesNotContain("transient", hub);
    }

    [Fact]
    public void GenerateHub_NonProjectFolder_DoesNotIncludeRetiredWorkProse()
    {
        var hub = HubGenerator.GenerateHub(
            relativeFolderPath: "guides",
            docsInFolder: [],
            subfolderHubs: [],
            allDocs: []);

        Assert.DoesNotContain("## Tasks", hub);
    }

    [Fact]
    public void GenerateAllHubs_ProducesGenericProjectSubfolderHub()
    {
        var decision = MakeDoc(
            relativePath: "project/decisions/example.md",
            fileName: "example.md",
            title: "Example");

        var hubs = HubGenerator.GenerateAllHubs("/base", [decision]);

        Assert.True(hubs.ContainsKey("project/decisions/_index.md"));
    }

    [Fact]
    public void GenerateHub_SummaryWithDydo30_PreservesDottedVersion()
    {
        const string summary = "Identity and verification evidence for provisioning the reviewed dydo 3.0 work graph in Linear.";

        Assert.Equal(summary, GenerateSummary(summary));
    }

    [Fact]
    public void GenerateHub_SummaryWithDotNet10_UsesFirstCompleteSentence()
    {
        const string summary = "DynaDocs is a .NET 10 CLI that authors and validates durable project knowledge, compiles native agent methods, and enforces universal guard rules. Linear is outside the runtime boundary and remains the sole owner of live project-management state.";

        Assert.Equal(
            "DynaDocs is a .NET 10 CLI that authors and validates durable project knowledge, compiles native agent methods, and enforces universal guard rules.",
            GenerateSummary(summary));
    }

    [Fact]
    public void GenerateHub_SummaryWithSkillTemplateFilename_PreservesInlineCodeDelimiters()
    {
        const string summary = "The skill template is the role: `dydo sync` discovers `skill-<name>.template.md` sources and compiles their methodology into native skills and, for worker roles, spawnable agent definitions. Role methods receive Linear Issue/Project context from the host; they do not create a repository work hierarchy.";

        Assert.Equal(
            "The skill template is the role: `dydo sync` discovers `skill-<name>.template.md` sources and compiles their methodology into native skills and,...",
            GenerateSummary(summary));
    }

    [Fact]
    public void GenerateHub_TerminalPunctuationInsideInlineCode_IsIgnored()
    {
        const string summary = "Use `mode. template? name!` safely. More detail follows.";

        Assert.Equal("Use `mode. template? name!` safely.", GenerateSummary(summary));
    }

    [Fact]
    public void GenerateHub_SummaryWith1xAnd2x_PreservesBothDottedVersions()
    {
        const string summary = "A self-contained, ordered procedure for an AI coding agent to migrate an existing project's dydo workspace from the 1.x generation to the current 2.x generation. Execute it **in the target project** (the repo being migrated), not in the dydo source repo.";

        Assert.Equal(
            "A self-contained, ordered procedure for an AI coding agent to migrate an existing project's dydo workspace from the 1.x generation to the current...",
            GenerateSummary(summary));
    }

    [Theory]
    [InlineData("Deployment succeeded! More detail follows.", "Deployment succeeded!")]
    [InlineData("Deployment succeeded? More detail follows.", "Deployment succeeded?")]
    public void GenerateHub_SummaryWithAlternateTerminator_UsesFirstCompleteSentence(
        string summary,
        string expected)
    {
        Assert.Equal(expected, GenerateSummary(summary));
    }

    [Fact]
    public void GenerateHub_SummaryWithPunctuationAndClosingMarkerCluster_PreservesEntireRun()
    {
        const string summary = "Deployment succeeded?!\")** More detail follows.";

        Assert.Equal("Deployment succeeded?!\")**", GenerateSummary(summary));
    }

    [Fact]
    public void GenerateHub_SentenceEndingAt150Characters_ReturnsCompleteSentence()
    {
        var summary = new string('a', 149) + ". More detail follows.";

        Assert.Equal(new string('a', 149) + ".", GenerateSummary(summary));
    }

    [Fact]
    public void GenerateHub_SummaryAtMost150CharactersWithoutTerminator_ReturnsWholeText()
    {
        var summary = new string('a', 150);

        Assert.Equal(summary, GenerateSummary(summary));
    }

    [Fact]
    public void GenerateHub_OverlongFallbackIntersectingInlineCode_MovesBeforeCodeSpan()
    {
        var summary = "Safe `complete.code` prefix " + new string('a', 90) + " `" + new string('x', 80) + ".template.md` trailing text";

        Assert.Equal(
            "Safe `complete.code` prefix " + new string('a', 90) + "...",
            GenerateSummary(summary));
    }

    [Fact]
    public void GenerateHub_OverlongFallbackWithoutWhitespace_HardCutsAt147Characters()
    {
        var summary = new string('a', 200);

        Assert.Equal(new string('a', 147) + "...", GenerateSummary(summary));
    }

    private static string GenerateSummary(string summary)
    {
        const string prefix = "- [Example](./example.md) - ";
        var doc = MakeDoc(
            relativePath: "guides/example.md",
            fileName: "example.md",
            title: "Example",
            summary: summary);

        var hub = HubGenerator.GenerateHub(
            relativeFolderPath: "guides",
            docsInFolder: [doc],
            subfolderHubs: [],
            allDocs: [doc]);

        var entry = hub.Split(Environment.NewLine).Single(line => line.StartsWith(prefix, StringComparison.Ordinal));
        return entry[prefix.Length..];
    }

    private static DocFile MakeDoc(string relativePath, string fileName, string? title, string? summary = null)
    {
        return new DocFile
        {
            FilePath = relativePath,
            RelativePath = relativePath,
            FileName = fileName,
            Content = "",
            Title = title,
            SummaryParagraph = summary
        };
    }
}
