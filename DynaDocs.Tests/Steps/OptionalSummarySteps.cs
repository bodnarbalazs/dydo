namespace DynaDocs.Tests.Steps;

using Reqnroll;
using System.Text.RegularExpressions;

[Binding]
public class OptionalSummarySteps(CliScenario scenario)
{
    private const string Frontmatter = "---\narea: general\ntype: reference\n---\n\n";
    private string DocumentPath => Path.Combine(scenario.DirectoryPath, "dydo", "document.md");
    private byte[] _beforeFix = [];

    [Given("a documentation tree whose frontmatter, navigation and links are valid")]
    public void ValidTree()
    {
        File.WriteAllText(Path.Combine(scenario.DirectoryPath, "dydo.json"),
            """{"version":1,"structure":{"root":"dydo"},"scanExclude":["_system/.local/","_system/audit/","agents/"]}""");
        Directory.CreateDirectory(Path.GetDirectoryName(DocumentPath)!);
        File.WriteAllText(Path.Combine(scenario.DirectoryPath, "dydo", "index.md"),
            "---\narea: general\ntype: hub\n---\n\n# Index\n\n[Document](./document.md)\n[Off-limits](./files-off-limits.md)\n");
        File.WriteAllText(Path.Combine(scenario.DirectoryPath, "dydo", "files-off-limits.md"),
            "---\ntype: config\n---\n\n# Files Off-Limits\n\n## Default Patterns\n\n```\n.env*\n```\n");
    }

    [Given("a document with an H1 title followed directly by an H2 section")]
    public void Section() => File.WriteAllText(DocumentPath, Frontmatter + "# Document\n\n## Details\n\nUseful content.");

    [Given("a document with an H1 title followed directly by a list")]
    public void List() => File.WriteAllText(DocumentPath, Frontmatter + "# Document\n\n- First item\n- Second item");

    [Given("a document containing valid frontmatter and an H1 title only")]
    public void TitleOnly() => File.WriteAllText(DocumentPath, Frontmatter + "# Document");

    [Given("a document with an H1 title followed by {string}")]
    public void OpeningProse(string prose) => File.WriteAllText(DocumentPath, Frontmatter + "# Document\n\n" + prose);

    [Given("a document with valid frontmatter and an H2 section but no H1 title")]
    public void MissingTitle() => File.WriteAllText(DocumentPath, Frontmatter + "## Details\n\nUseful content.");

    [Given("that section links to {string} which does not exist")]
    public void BrokenLink(string target) => File.AppendAllText(DocumentPath, $"\n\n[Missing]({target})");

    [Given("a document with an H1 title and no frontmatter or opening summary")]
    public void MissingFrontmatter() => File.WriteAllText(DocumentPath, "# Document\n\n## Details");

    [When("I check the documentation")]
    public Task Check() => scenario.RunAsync("check");

    [When("I fix the documentation")]
    public async Task Fix()
    {
        _beforeFix = File.ReadAllBytes(DocumentPath);
        await scenario.RunAsync("fix");
        scenario.Result.AssertSuccess();
    }

    [Then("the document has no validation errors or warnings")]
    [Then("the documentation has no validation errors or warnings")]
    public void ValidDocumentation() => AssertValidDocumentation(scenario.Result);

    internal static void AssertValidDocumentation(CliResult result)
    {
        result.AssertSuccess();
        Assert.Empty(result.Stderr);
        var lines = result.Stdout.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("Checking ", lines[0]);
        Assert.Matches(@"^Found 0 errors, 0 warnings in [1-9][0-9]* files\.$", lines[1]);
        Assert.Equal("All checks passed.", lines[2]);
    }

    [Then("checking fails with {string} for that document")]
    public void CheckFailure(string message)
    {
        Assert.Equal(1, scenario.Result.ExitCode);
        var lines = scenario.Result.Stdout.Replace("\r\n", "\n").Split('\n');
        var document = Array.FindIndex(lines, line => line.Trim() == "document.md");
        Assert.True(document >= 0, scenario.Result.Stdout);
        var violations = lines.Skip(document + 1).TakeWhile(line => line.StartsWith("    - ", StringComparison.Ordinal));
        Assert.Contains(violations, line => Regex.Replace(line[6..], @"^Line [1-9][0-9]*: ", "") == message);
    }

    [Then("no manual repair asks for a summary paragraph")]
    public void NoSummaryRepair() => Assert.DoesNotContain("summary", scenario.Result.Stdout, StringComparison.OrdinalIgnoreCase);

    [Then("that document's bytes are unchanged")]
    public void UnchangedDocument() => Assert.Equal(_beforeFix, File.ReadAllBytes(DocumentPath));

    [Then("the manual repairs include {string} for that document")]
    public void ManualRepair(string repair) => Assert.True(
        scenario.Result.Stdout.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Any(line => Regex.IsMatch(line, $@"^  \S document\.md - {Regex.Escape(repair)}$")), scenario.Result.Stdout);
}
