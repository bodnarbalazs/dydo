namespace DynaDocs.Tests.Steps;

using Reqnroll;
using Xunit.Sdk;

public class CheckAssertionTests
{
    private const string About = "About.md is not customized. Consider updating it.";
    private const string Architecture = "Architecture.md is not customized. Consider updating it.";
    // Complete native `check` report; the disposable project's absolute prefix is immaterial.
    private const string Report = "Checking scratch/dydo...\n\nWARNINGS:\n  understand/about.md\n    - " + About
        + "\n  understand/architecture.md\n    - " + Architecture
        + "\n\nFound 0 errors, 2 warnings in 30 files.\n\nFound warnings (no errors).\n";
    private const string Clean = "Checking scratch/dydo...\n\nFound 0 errors, 0 warnings in 30 files.\n\nAll checks passed.\n";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WarningReport_AcceptsNativeLineEndingsAndPathSeparators(bool windows)
    {
        var report = windows ? Report.Replace("/", "\\").Replace("\n", "\r\n") : Report;
        FreshInstallationSteps.AssertOnboardingWarnings(new(0, report, ""), ExpectedWarnings());
    }

    [Theory]
    [InlineData("understand/about.md", "understand/wrong.md")]
    [InlineData("understand/architecture.md", "architecture.md")]
    [InlineData(About, "Wrong about warning")]
    [InlineData(Architecture, "Wrong architecture warning")]
    [InlineData(About, About + " ")]
    public void WarningReport_RejectsWrongObservedTuple(string before, string after) =>
        RejectWarning(new(0, Report.Replace(before, after), ""));

    [Theory]
    [InlineData("document", 0)]
    [InlineData("document", 1)]
    [InlineData("warning", 0)]
    [InlineData("warning", 1)]
    public void WarningReport_ConsumesEveryExpectedValue(string column, int row)
    {
        var expected = ExpectedWarnings();
        expected.Rows[row][column] = "changed";
        Assert.ThrowsAny<XunitException>(() => FreshInstallationSteps.AssertOnboardingWarnings(new(0, Report, ""), expected));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(2)]
    public void WarningReport_RejectsMissingExtraAndDuplicateExpectedRows(int count)
    {
        var expected = new Table("document", "warning");
        for (var row = 0; row < count; row++)
            expected.AddRow("understand/about.md", About);
        Assert.ThrowsAny<XunitException>(() => FreshInstallationSteps.AssertOnboardingWarnings(new(0, Report, ""), expected));
    }

    [Theory]
    [InlineData("    - " + About + "\n", "")]
    [InlineData("    - " + About + "\n", "    - " + About + "\n    - " + About + "\n")]
    [InlineData("\n\nFound 0", "\n  unexpected.md\n    - Extra warning\n\nFound 0")]
    [InlineData("WARNINGS:\n", "WARNINGS:\n    - Unattached warning\n")]
    [InlineData("WARNINGS:\n", "WARNINGS:\n  empty-group.md\n")]
    [InlineData("  understand/about.md", "   understand/about.md")]
    [InlineData("  understand/about.md", "malformed group")]
    [InlineData("30 files.", "0 files.")]
    [InlineData("WARNINGS:", "WARNINGS:\nWARNINGS:")]
    [InlineData("Found warnings (no errors).", "Found warnings (no errors).\n  outside.md")]
    [InlineData("Found warnings (no errors).", "Found warnings (no errors).\nFound warnings (no errors).")]
    [InlineData("\n\nFound 0", "\nFound 0")]
    [InlineData("Found warnings (no errors).", "Found warnings (no errors).\nERRORS:")]
    [InlineData("Found warnings (no errors).", "Found warnings (no errors).\nFound 1 errors, 2 warnings in 30 files.")]
    public void WarningReport_RejectsMalformedOrIncompleteReports(string before, string after) =>
        RejectWarning(new(0, Report.Replace(before, after), ""));

    [Theory]
    [InlineData(1, "")]
    [InlineData(0, "unexpected error")]
    public void WarningReport_RejectsFailureEvenWithExpectedWarnings(int exit, string stderr) =>
        RejectWarning(new(exit, Report, stderr));

    [Theory]
    [InlineData("Found 0 errors, 0 warnings in 0 files.\nAll checks passed.", "")]
    [InlineData("No docs folder found.\nAll checks passed.", "")]
    [InlineData("prefix Found 0 errors, 0 warnings in 30 files.\nAll checks passed.", "")]
    [InlineData(Clean, "ERRORS:")]
    [InlineData(Clean + "Found 1 errors, 0 warnings in 30 files.\n", "")]
    [InlineData(Clean + Clean, "")]
    [InlineData(Clean + "WARNINGS:\n  document.md\n    - Warning\n", "")]
    public void CleanReport_RejectsVacuousMalformedOrContradictorySuccess(string stdout, string stderr) =>
        Assert.ThrowsAny<XunitException>(() => OptionalSummarySteps.AssertValidDocumentation(new(0, stdout, stderr)));

    [Fact]
    public void CleanReport_AcceptsCompleteNativeReport() =>
        OptionalSummarySteps.AssertValidDocumentation(new(0, Clean, ""));

    [Fact]
    public void CommandSuccess_RejectsFailureDespiteRetainedFiles()
    {
        var scenario = new CliScenario();
        try
        {
            new FreshInstallationSteps(scenario).UserFile("user-notes.txt", "My project notes.");
            Assert.ThrowsAny<XunitException>(() => new CliResult(1, "Templates remain present.", "update failed").AssertSuccess());
            new FreshInstallationSteps(scenario).PreservedUserFile("user-notes.txt", "My project notes.");
        }
        finally { scenario.Cleanup(); }
    }

    internal static Table ExpectedWarnings()
    {
        var table = new Table("document", "warning");
        table.AddRow("understand/about.md", About);
        table.AddRow("understand/architecture.md", Architecture);
        return table;
    }

    private static void RejectWarning(CliResult result) =>
        Assert.ThrowsAny<XunitException>(() => FreshInstallationSteps.AssertOnboardingWarnings(result, ExpectedWarnings()));
}
