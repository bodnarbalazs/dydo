namespace DynaDocs.Tests.Workflow;

public sealed class ReleaseWorkflowTests
{
    [Fact]
    public void ReleaseWorkflow_GatesEveryPublishPathOnTheBuildMatrix()
    {
        var workflow = File.ReadAllText(RepositoryFile(".github", "workflows", "release.yml"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.DoesNotContain("newline-fidelity:", workflow);
        Assert.DoesNotContain("DynaDocs.Tests/Sync/Notion", workflow);
        Assert.Contains("build:", workflow);
        Assert.Contains("release:\n    needs: build", workflow);
        Assert.Contains("nuget:\n    needs: build", workflow);
        Assert.Contains("npm:\n    needs: release", workflow);
    }

    private static string RepositoryFile(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException("Could not locate the repository workflow.");
    }
}
