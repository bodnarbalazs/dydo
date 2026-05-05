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
