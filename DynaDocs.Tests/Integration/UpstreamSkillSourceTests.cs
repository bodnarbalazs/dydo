namespace DynaDocs.Tests.Integration;

public class UpstreamSkillSourceTests
{
    private const string UpstreamCommit = "6654f6b60cd9d5be8b54c6fafe44346dabeb3b76";

    private static readonly IReadOnlyDictionary<string, string> ExpectedInvocation =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["wayfinder"] = "explicit",
            ["grilling"] = "automatic",
            ["grill-me"] = "explicit",
            ["bro"] = "explicit",
            ["writing-for-agents"] = "automatic"
        };

    // The invocation metadata is a routing contract, not prose: an explicit-only skill that
    // compiles as model-invocable will be selected behind the human's back. Shipped-vs-installed
    // parity for these files lives in InstalledTemplateParityTests.
    [Fact]
    public void MattDerivedTemplates_CarryTheirInvocationMetadataAndAttribution()
    {
        foreach (var (skill, invocation) in ExpectedInvocation)
        {
            var source = ReadTemplate(skill);

            Assert.Contains($"mode: {skill}\n", source);
            Assert.Contains($"invocation: {invocation}\n", source);
            Assert.Contains("emit: skill\n", source);
            Assert.Contains("mattpocock/skills", source);
            Assert.Contains(UpstreamCommit, source);
            Assert.Contains("(MIT)", source);
        }
    }

    // grill-me is the human's explicit entry to the grilling method; it must stay explicit-only
    // and must point at the method rather than restating it.
    [Fact]
    public void GrillMe_IsAnExplicitEntryPointThatDefersToTheGrillingSkill()
    {
        var source = ReadTemplate("grill-me");

        Assert.Contains("invocation: explicit\n", source);
        Assert.Contains("grilling", source);
        Assert.True(NonBlankLines(source) < NonBlankLines(ReadTemplate("grilling")),
            "grill-me must stay thinner than the method it defers to");
    }

    // DR 045 section 11 retires the Waypoint ontology from the vocabulary; the rebuilt wayfinder
    // navigates the Linear Project itself.
    [Fact]
    public void Wayfinder_CarriesNoRetiredWaypointOntology()
    {
        var source = ReadTemplate("wayfinder");

        Assert.Contains("invocation: explicit\n", source);
        Assert.DoesNotContain("Waypoint", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Notices_AreIdenticalAndPackageMetadataIncludesThem()
    {
        var root = FindRepositoryRoot();
        var rootNotice = Normalize(File.ReadAllText(Path.Combine(root, "THIRD-PARTY-NOTICES.md")));
        var npmNotice = Normalize(File.ReadAllText(
            Path.Combine(root, "npm", "THIRD-PARTY-NOTICES.md")));

        Assert.Equal(rootNotice, npmNotice);
        Assert.Contains("Copyright (c) 2026 Matt Pocock", rootNotice);
        Assert.Contains(UpstreamCommit, rootNotice);
        Assert.Contains("https://github.com/mattpocock/skills", rootNotice);
        Assert.Contains("THE SOFTWARE IS PROVIDED \"AS IS\"", rootNotice);

        var project = Normalize(File.ReadAllText(Path.Combine(root, "DynaDocs.csproj")));
        Assert.Contains(
            "<None Include=\"THIRD-PARTY-NOTICES.md\" Pack=\"true\" PackagePath=\"\" />",
            project);

        var npmPackage = Normalize(File.ReadAllText(Path.Combine(root, "npm", "package.json")));
        Assert.Contains("\"THIRD-PARTY-NOTICES.md\"", npmPackage);
    }

    private static string ReadTemplate(string skill) => Normalize(File.ReadAllText(Path.Combine(
        FindRepositoryRoot(), "Templates", $"skill-{skill}.template.md")));

    private static int NonBlankLines(string value) =>
        value.Split('\n').Count(line => !string.IsNullOrWhiteSpace(line));

    private static string Normalize(string value) => value.Replace("\r\n", "\n");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.sln")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Templates")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the DynaDocs repository root.");
    }
}
