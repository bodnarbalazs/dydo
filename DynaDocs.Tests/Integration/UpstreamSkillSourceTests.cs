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

    [Fact]
    public void MattDerivedTemplates_ExistInBothSourceLocations_WithNeutralInvocationMetadata()
    {
        var root = FindRepositoryRoot();

        foreach (var (skill, invocation) in ExpectedInvocation)
        {
            var shippedPath = Path.Combine(root, "Templates", $"skill-{skill}.template.md");
            var installedPath = Path.Combine(
                root, "dydo", "_system", "templates", $"skill-{skill}.template.md");

            Assert.True(File.Exists(shippedPath), $"Missing shipped source for {skill}");
            Assert.True(File.Exists(installedPath), $"Missing installed source for {skill}");

            var shipped = Normalize(File.ReadAllText(shippedPath));
            var installed = Normalize(File.ReadAllText(installedPath));

            Assert.Equal(shipped, installed);
            Assert.Contains($"mode: {skill}\n", shipped);
            Assert.Contains($"invocation: {invocation}\n", shipped);
            Assert.Contains("emit: skill\n", shipped);
            Assert.Contains("mattpocock/skills", shipped);
            Assert.Contains(UpstreamCommit, shipped);
            Assert.Contains("(MIT)", shipped);
        }
    }

    [Fact]
    public void GrillMe_IsAnExplicitAliasForTheGeneratedGrillingSkill()
    {
        var source = ReadTemplate("grill-me");

        Assert.Contains("invocation: explicit", source);
        Assert.Contains("separately generated `grilling` skill", source);
        Assert.Contains("human confirms shared understanding", source);
    }

    [Fact]
    public void Wayfinder_UsesLinearProjectMap_WithoutInventedWaypointOntology()
    {
        var source = ReadTemplate("wayfinder");

        Assert.Contains("Linear Project is the canonical map", source);
        Assert.Contains("native dependency relations", source);
        Assert.Contains("Assignment is the claim", source);
        Assert.Contains("Fog of war", source);
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
