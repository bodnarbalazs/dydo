namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;

public class StatechartPrerequisitesTests
{
    [Theory]
    [InlineData("node", "v22.17.1", false)]
    [InlineData("node", "v22.18.0", true)]
    [InlineData("node", "v23.5.0", false)]
    [InlineData("node", "v23.6.0", true)]
    [InlineData("node", "v24.0.0", true)]
    [InlineData("bun", "1.1.4", false)]
    [InlineData("bun", "1.1.5", true)]
    [InlineData("deno", "deno 2.0.0", true)]
    [InlineData("java", "openjdk version \"11.0.16.1\"", true)]
    [InlineData("java", "java version \"1.8.0\"", false)]
    [InlineData("node", "unrecognized", false)]
    public void RuntimeCompatibilityPreservesExistingBoundaries(string runtime, string version, bool expected)
    {
        Assert.Equal(expected, StatechartPrerequisites.Compatible(runtime, version));
    }

    [Fact]
    public void InvalidExplicitOverridesCannotFallBackToOtherCandidates()
    {
        using var scope = new StatechartTestDirectory();
        Assert.Throws<IOException>(() => StatechartPrerequisites.Java(scope.Path, "./missing", Environment.GetEnvironmentVariable("PATH")));
        File.WriteAllText(Path.Combine(scope.Path, "fake.jar"), "changed");
        Assert.Throws<IOException>(() => StatechartPrerequisites.Tlc(scope.Path, "fake.jar"));
        Assert.Null(StatechartPrerequisites.Tlc(scope.Path, null));
    }

    [Fact]
    public void CallerRelativeJavaAndBareJavaUseTheOriginalDirectoryAndPath()
    {
        using var scope = new StatechartTestDirectory();
        var name = OperatingSystem.IsWindows() ? "java.exe" : "java";
        var file = Path.Combine(scope.Path, name);
        File.WriteAllText(file, "test candidate; never executed");
        Assert.Equal(file, StatechartPrerequisites.Java(scope.Path, "./" + name, ""));
        Assert.Equal(file, StatechartPrerequisites.Java(scope.Path, "java", scope.Path));
        Assert.Null(StatechartPrerequisites.Find("java", "relative-path-is-not-a-candidate"));
    }
}
