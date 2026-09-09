namespace DynaDocs.Tests.Workflow;

public sealed class ReleaseWorkflowTests
{
    [Fact]
    public void ReleaseWorkflow_ValidatesBeforeItPublishes()
    {
        var workflow = Workflow();

        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Equal(5, CountOccurrences(Job("build"), "rid:"));

        var validation = Job("validation");
        Assert.Contains("runs-on: ubuntu-latest", validation);
        Assert.Contains("actions/setup-python", validation);
        Assert.Contains("actions/setup-dotnet", validation);
        Assert.Contains("actions/setup-node", validation);
        Assert.Contains("python DynaDocs.Tests/coverage/run_tests.py", validation);
        Assert.Contains("dotnet build DynaDocs.csproj -c Release -warnaserror", validation);
        Assert.Contains("dydo check", validation);
        Assert.Contains("python DynaDocs.Tests/coverage/gap_check.py --force-run", validation);
        Assert.Contains("python DynaDocs.Tests/coverage/gap_check.py gate mutation --since 2e31b1d0915529926a79224424c18620ee8003e1", validation);

        AssertPublishJobNeeds("release", "build", "validation");
        AssertPublishJobNeeds("nuget", "build", "validation");
        AssertPublishJobNeeds("npm", "build", "validation", "release");
        Assert.DoesNotContain("continue-on-error:", validation);

        var jobPreamble = workflow[..workflow.IndexOf("\njobs:\n", StringComparison.Ordinal)];
        Assert.DoesNotContain("permissions:", jobPreamble);
        Assert.Contains("permissions:\n      contents: read", Job("build"));
        Assert.Contains("permissions:\n      contents: read", validation);
        Assert.Contains("permissions:\n      contents: write", Job("release"));
        Assert.DoesNotContain("id-token:", Job("release"));
        Assert.Contains("permissions:\n      contents: read\n      id-token: write", Job("nuget"));
        Assert.Contains("permissions:\n      contents: read\n      id-token: write", Job("npm"));
    }

    [Fact]
    public void ReleaseWorkflow_PublishesOnlyExplicitAllowedTagsWithTheRightChannels()
    {
        var workflow = Workflow();
        var allowedTagGuard = "github.event_name == 'push' && (github.ref == 'refs/tags/v3.0.0-beta.3' || github.ref == 'refs/tags/v3.0.0')";

        foreach (var job in new[] { "release", "nuget", "npm" })
            Assert.Contains($"if: ${{{{ {allowedTagGuard} }}}}", Job(job));

        var release = Job("release");
        Assert.Contains("- name: Create beta release", release);
        Assert.Contains("prerelease: true", release);
        Assert.Contains("- name: Create stable release", release);
        Assert.Contains("prerelease: false", release);

        var npm = Job("npm");
        Assert.Contains("npm publish --access public --provenance --tag ${{ github.ref == 'refs/tags/v3.0.0-beta.3' && 'beta' || 'latest' }}", npm);
        Assert.DoesNotContain("npm publish --access public --provenance\n", npm);
    }

    private static void AssertPublishJobNeeds(string job, params string[] dependencies)
    {
        var body = Job(job);
        var needs = dependencies.Length == 1
            ? $"needs: {dependencies[0]}"
            : $"needs: [{string.Join(", ", dependencies)}]";

        Assert.Contains(needs, body);
    }

    private static string Workflow() => File.ReadAllText(RepositoryFile(".github", "workflows", "release.yml"))
        .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string Job(string name)
    {
        var workflow = Workflow();
        var start = workflow.IndexOf($"  {name}:\n", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not locate job '{name}'.");

        var next = workflow.IndexOf("\n  build:\n", start + 1, StringComparison.Ordinal);
        foreach (var candidate in new[] { "validation", "release", "nuget", "npm" })
        {
            var candidateStart = workflow.IndexOf($"\n  {candidate}:\n", start + 1, StringComparison.Ordinal);
            if (candidateStart >= 0 && (next < 0 || candidateStart < next))
                next = candidateStart;
        }
        return workflow[start..(next < 0 ? workflow.Length : next)];
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = 0; (index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
            count++;
        return count;
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
