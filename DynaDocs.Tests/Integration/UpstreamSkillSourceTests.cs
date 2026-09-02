namespace DynaDocs.Tests.Integration;

public class UpstreamSkillSourceTests
{
    private const string UpstreamCommit = "6654f6b60cd9d5be8b54c6fafe44346dabeb3b76";

    private static readonly string[] MattDerivedSkills =
        ["wayfinder", "grilling", "grill-me", "bro", "writing-for-agents"];

    // DR 045 section 9's explicit-only list, narrowed to the skills this file covers. Every other
    // invocation value belongs to the source that authors it, so it is validated, not pinned.
    private static readonly string[] ExplicitOnlySkills = ["grill-me", "bro"];

    // The invocation metadata is a routing contract, not prose: a skill with a missing or invalid
    // value routes by accident. Which model-invoked skills exist is the taxonomy's business; that
    // the human-only ones stay human-only is this test's. Shipped-vs-installed parity lives in
    // InstalledTemplateParityTests, and the metadata-to-compiled-policy correspondence in
    // CodexSyncArtifactsE2ETests.
    [Fact]
    public void MattDerivedTemplates_CarryValidInvocationMetadataAndAttribution()
    {
        foreach (var skill in MattDerivedSkills)
        {
            var source = ReadTemplate(skill);
            var invocation = InvocationValue(source);

            Assert.Contains($"name: {skill}\n", source);
            Assert.Contains("emit: skill\n", source);
            Assert.True(invocation is "explicit" or "automatic",
                $"{skill}: invocation must be 'explicit' or 'automatic', was '{invocation}'");
            if (ExplicitOnlySkills.Contains(skill))
                Assert.Equal("explicit", invocation);
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
    // navigates the Linear Project itself. Its invocation value is section 9's to set, not this
    // test's to freeze.
    [Fact]
    public void Wayfinder_CarriesNoRetiredWaypointOntology()
    {
        var source = ReadTemplate("wayfinder");

        Assert.Contains("name: wayfinder\n", source);
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

    private static string InvocationValue(string source) =>
        source.Split('\n')
            .Single(line => line.StartsWith("invocation:", StringComparison.Ordinal))
            ["invocation:".Length..]
            .Trim();

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
