namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Utils;

[Collection("Integration")]
public class CheckCommandTests : IDisposable
{
    private readonly string _root;
    private readonly string _original;

    public CheckCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dydo-check-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _original = Environment.CurrentDirectory;
        Environment.CurrentDirectory = _root;
    }

    [Fact]
    public void Check_MissingRequiredScanExclusionFailsInsteadOfReportingSuccess()
    {
        File.WriteAllText(Path.Combine(_root, "dydo.json"),
            "{\"version\":1,\"structure\":{\"root\":\"dydo\"},\"scanExclude\":[\"agents/\"]}");
        Directory.CreateDirectory(Path.Combine(_root, "dydo"));

        var (code, stdout, stderr) = ConsoleCapture.All(() => CheckCommand.Create().Parse("").Invoke());

        Assert.NotEqual(0, code);
        Assert.Contains("_system/.local/", stderr);
        Assert.DoesNotContain("All checks passed", stdout);
    }

    private const string ValidConfig =
        """{"version":1,"structure":{"root":"dydo"},"scanExclude":["_system/.local/","agents/"]}""";
    private const string StaleAuditEntryConfig =
        """{"version":1,"structure":{"root":"dydo"},"scanExclude":["_system/.local/","_system/audit/","agents/"]}""";
    private const string MissingExclusionConfig =
        """{"version":1,"structure":{"root":"dydo"},"scanExclude":["agents/"]}""";
    private const string MissingExclusionError = "scanExclude is missing required entry";

    private string CreateProject(string name, string config)
    {
        var project = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.Combine(project, "dydo", "guides"));
        File.WriteAllText(Path.Combine(project, "dydo.json"), config);
        File.WriteAllText(Path.Combine(project, "dydo", "index.md"), "# Index\n");
        return project;
    }

    private static (int Code, string Stdout, string Stderr) Check(params string[] args) =>
        ConsoleCapture.All(() => CheckCommand.Create().Parse(args).Invoke());

    [Fact]
    public void Check_ExplicitRoot_ReadsConfigAndDocsFromThatProjectNotCwd()
    {
        Environment.CurrentDirectory = CreateProject("here", MissingExclusionConfig);
        var other = CreateProject("other", ValidConfig);

        var (code, stdout, stderr) = Check("--root", other);

        Assert.NotEqual(2, code);
        Assert.Contains($"Root: {other}", stdout);
        Assert.Contains($"Checking {Path.Combine(other, "dydo")}...", stdout);
        Assert.DoesNotContain(MissingExclusionError, stderr);
    }

    [Fact]
    public void Check_RootInsideAnotherProjectsSubfolder_RootsAtItsDydoJson()
    {
        Environment.CurrentDirectory = CreateProject("here", ValidConfig);
        var other = CreateProject("other", MissingExclusionConfig);

        var (code, stdout, stderr) = Check("--root", Path.Combine(other, "dydo", "guides"));

        Assert.Equal(1, code);
        Assert.Contains($"Root: {other}", stdout);
        Assert.Contains($"Checking {Path.Combine(other, "dydo")}...", stdout);
        Assert.Contains(MissingExclusionError, stderr);
    }

    [Fact]
    public void Check_RootGivenAFileInsideAnotherProject_ResolvesToThatProjectsRoot()
    {
        Environment.CurrentDirectory = CreateProject("here", ValidConfig);
        var other = CreateProject("other", MissingExclusionConfig);

        var (code, stdout, stderr) = Check("--root", Path.Combine(other, "dydo", "index.md"));

        Assert.Equal(1, code);
        Assert.Contains($"Root: {other}", stdout);
        Assert.Contains(MissingExclusionError, stderr);
    }

    [Fact]
    public void Check_RootInNoDydoProject_IsToolError()
    {
        var nowhere = Path.Combine(_root, "nowhere");
        Directory.CreateDirectory(nowhere);

        var (code, stdout, stderr) = Check("--root", nowhere);

        Assert.Equal(2, code);
        Assert.Contains($"No dydo project found at {nowhere}", stderr);
        Assert.DoesNotContain("Checking", stdout);
    }

    [Fact]
    public void Check_RootThatDoesNotExist_IsToolError()
    {
        var missing = Path.Combine(_root, "missing");

        var (code, _, stderr) = Check("--root", missing);

        Assert.Equal(2, code);
        Assert.Contains($"No dydo project found at {missing}", stderr);
    }

    [Fact]
    public void Check_WithoutRoot_ChecksCwdProjectAndPrintsNoRootLine()
    {
        var here = CreateProject("here", MissingExclusionConfig);
        CreateProject("other", ValidConfig);
        Environment.CurrentDirectory = here;

        var (code, stdout, stderr) = Check();

        Assert.Equal(1, code);
        Assert.DoesNotContain("Root:", stdout);
        Assert.Contains($"Checking {Path.Combine(here, "dydo")}...", stdout);
        Assert.Contains(MissingExclusionError, stderr);
    }

    [Fact]
    public void Check_WithoutRoot_StillRefusesAPathInAnotherProject()
    {
        Environment.CurrentDirectory = CreateProject("here", ValidConfig);
        var other = CreateProject("other", ValidConfig);
        var otherDocs = Path.Combine(other, "dydo");

        var (code, stdout, stderr) = Check(otherDocs);

        Assert.Equal(2, code);
        Assert.Contains($"Path is outside the docs tree: {otherDocs}", stderr);
        Assert.DoesNotContain("Root:", stdout);
    }

    [Fact]
    public void Check_ConfigStillCarryingStaleAuditEntry_PassesTheScanExcludeInvariant()
    {
        var project = CreateProject("stale", StaleAuditEntryConfig);
        File.WriteAllText(Path.Combine(project, "dydo", "index.md"),
            "---\narea: general\ntype: hub\n---\n\n# Index\n\nA valid hub.\n");
        Environment.CurrentDirectory = project;

        var (code, _, stderr) = Check();

        Assert.Equal(ExitCodes.Success, code);
        Assert.DoesNotContain("scanExclude", stderr);
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _original;
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }
}
