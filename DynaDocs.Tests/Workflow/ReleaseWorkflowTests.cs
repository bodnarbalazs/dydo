namespace DynaDocs.Tests.Workflow;

public sealed class ReleaseWorkflowTests
{
    [Fact]
    public void ReleaseWorkflow_GatesEveryPublishPathOnTheLfAndCrlfMatrix()
    {
        var workflow = File.ReadAllText(RepositoryFile(".github", "workflows", "release.yml"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("newline-fidelity:", workflow);
        Assert.Contains("os: ubuntu-latest", workflow);
        Assert.Contains("os: windows-latest", workflow);
        Assert.Contains("autocrlf: \"false\"", workflow);
        Assert.Contains("autocrlf: \"true\"", workflow);
        Assert.Contains("$expectedCrLf = '${{ matrix.autocrlf }}' -eq 'true'", workflow);
        Assert.Contains("Remove-Item -LiteralPath 'DynaDocs.Tests/Sync/Notion/Fixtures/slice-11-sanitized.md' -Force", workflow);
        Assert.Contains("git checkout-index -f -- 'DynaDocs.Tests/Sync/Notion/Fixtures/slice-11-sanitized.md'", workflow);
        Assert.Contains("dotnet restore DynaDocs.Tests/DynaDocs.Tests.csproj", workflow);
        Assert.Contains("dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter", workflow);
        Assert.Contains("FullyQualifiedName~ReleaseWorkflowTests", workflow);
        Assert.Contains("build:\n    needs: newline-fidelity", workflow);
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
