namespace DynaDocs.Tests.Workflow;

public sealed class ReleaseWorkflowTests
{
    private const string AllowedTagGuard = "github.event_name == 'push' && (github.ref == 'refs/tags/v3.0.0-beta.3' || github.ref == 'refs/tags/v3.0.0')";
    private const string NpmPublishRun = "npm publish --access public --provenance --tag ${{ github.ref == 'refs/tags/v3.0.0-beta.3' && 'beta' || 'latest' }}";

    [Fact]
    public void ReleaseWorkflow_ValidatesTheBuildBeforeEveryPublicationAction()
    {
        var workflow = Workflow();
        var jobs = ActiveJobs(workflow);

        Assert.Contains("workflow_dispatch:", ActiveText(workflow));
        Assert.Equal("  push:\n    branches:\n      - feature/dydo-3-consolidation\n    tags:\n      - 'v*'", ActivePush(workflow));
        Assert.Equal(5, CountOccurrences(jobs["build"], "rid:"));

        var validation = jobs["validation"];
        Assert.Contains("runs-on: ubuntu-latest", validation);
        Assert.Contains("fetch-depth: 0", validation);
        Assert.Contains("actions/setup-python", validation);
        Assert.Contains("actions/setup-dotnet", validation);
        Assert.Contains("actions/setup-node", validation);
        Assert.Contains("python DynaDocs.Tests/coverage/run_tests.py", validation);
        Assert.Contains("dotnet build DynaDocs.sln -c Release --warnaserror", validation);
        Assert.Contains("dotnet run --project DynaDocs.csproj -c Release --no-build -- check", validation);
        Assert.Contains("python DynaDocs.Tests/coverage/gap_check.py --force-run", validation);
        Assert.Contains("python DynaDocs.Tests/coverage/gap_check.py gate mutation --since 2e31b1d0915529926a79224424c18620ee8003e1", validation);
        Assert.Contains("python -m pip install -r DynaDocs.Tests/coverage/requirements.lock", validation);
        Assert.Contains("run: npm ci\n        working-directory: DynaDocs.Tests/coverage", validation);
        Assert.DoesNotContain("continue-on-error:", validation);

        AssertFailClosedPublicationGraph(workflow);

        var preamble = ActiveText(workflow)[..ActiveText(workflow).IndexOf("\njobs:\n", StringComparison.Ordinal)];
        Assert.DoesNotContain("permissions:", preamble);
        Assert.Contains("permissions:\n      contents: read", jobs["build"]);
        Assert.Contains("permissions:\n      contents: read", validation);
        Assert.Contains("permissions:\n      contents: write", jobs["release"]);
        Assert.DoesNotContain("id-token:", jobs["release"]);
        Assert.Contains("permissions:\n      contents: read\n      id-token: write", jobs["nuget"]);
        Assert.Contains("permissions:\n      contents: read\n      id-token: write", jobs["npm"]);
    }

    [Fact]
    public void ReleaseWorkflow_RejectsCommentedGuardsMissingValidationAndRoguePublishers()
    {
        var workflow = Workflow();

        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => Assert.Equal(
            "  push:\n    branches:\n      - feature/dydo-3-consolidation\n    tags:\n      - 'v*'",
            ActivePush(workflow.Replace("      - feature/dydo-3-consolidation", "      - feature/dydo-3-consolidation\n      - release", StringComparison.Ordinal))));
        AssertRejected(workflow.Replace($"if: ${{{{ {AllowedTagGuard} }}}}", $"# if: ${{{{ {AllowedTagGuard} }}}}", StringComparison.Ordinal));
        AssertRejected(workflow.Replace(AllowedTagGuard, "github.event_name == 'push' && github.ref == 'refs/heads/feature/dydo-3-consolidation'", StringComparison.Ordinal));
        AssertRejected(workflow.Replace("needs: [build, validation]", "needs: build", StringComparison.Ordinal));
        AssertRejected(workflow.Replace("needs: [build, validation]", "needs: build # needs: [build, validation]", StringComparison.Ordinal));
        AssertRejected(workflow + "\n  rogue:\n    runs-on: ubuntu-latest\n    steps:\n      - run: npm publish --access public\n");
        AssertRejected(Swap(workflow, "prerelease: true", "prerelease: false"));
        AssertRejected(Swap(workflow, "if: github.ref == 'refs/tags/v3.0.0-beta.3'", "if: github.ref == 'refs/tags/v3.0.0'"));
        AssertRejected(Swap(workflow, "'beta'", "'latest'"));
        AssertRejected(workflow
            .Replace(NpmPublishRun, "npm publish --access public --provenance --tag wrong", StringComparison.Ordinal)
            .Replace("- name: Publish to npm", $"- name: Publish to npm # {NpmPublishRun}", StringComparison.Ordinal));
    }

    [Fact]
    public void ReleaseWorkflow_UsesExplicitBetaAndStableChannels()
    {
        var jobs = ActiveJobs(Workflow());
        var release = jobs["release"];

        Assert.Contains("- name: Create beta release", release);
        Assert.Contains("prerelease: true", release);
        Assert.Contains("- name: Create stable release", release);
        Assert.Contains("prerelease: false", release);
        Assert.Equal(NpmPublishRun, StepField(JobStep(jobs["npm"], "Publish to npm"), "run"));
    }

    private static void AssertRejected(string workflow) => Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFailClosedPublicationGraph(workflow));

    private static void AssertFailClosedPublicationGraph(string workflow)
    {
        var jobs = ActiveJobs(workflow);
        var expectedNeeds = new Dictionary<string, string>
        {
            ["release"] = "needs: [build, validation]",
            ["nuget"] = "needs: [build, validation]",
            ["npm"] = "needs: [build, validation, release]",
        };
        var publicationActions = new[] { "softprops/action-gh-release@", "dotnet nuget push", "npm publish" };

        foreach (var (name, body) in jobs)
        {
            if (!publicationActions.Any(body.Contains))
                continue;

            Assert.True(expectedNeeds.TryGetValue(name, out var needs), $"Unexpected publication job '{name}'.");
            Assert.Equal($"${{{{ {AllowedTagGuard} }}}}", JobField(body, "if"));
            Assert.Equal(needs[7..], JobField(body, "needs"));
        }

        foreach (var (name, needs) in expectedNeeds)
        {
            Assert.True(jobs.TryGetValue(name, out var body), $"Missing publication job '{name}'.");
            Assert.Equal($"${{{{ {AllowedTagGuard} }}}}", JobField(body, "if"));
            Assert.Equal(needs[7..], JobField(body, "needs"));
            Assert.Contains(PublicationAction(name), body);
        }

        AssertReleaseStep(jobs["release"], "Create beta release", "github.ref == 'refs/tags/v3.0.0-beta.3'", "true");
        AssertReleaseStep(jobs["release"], "Create stable release", "github.ref == 'refs/tags/v3.0.0'", "false");
        Assert.Equal(NpmPublishRun, StepField(JobStep(jobs["npm"], "Publish to npm"), "run"));
    }

    private static string PublicationAction(string job) => job switch
    {
        "release" => "softprops/action-gh-release@",
        "nuget" => "dotnet nuget push",
        _ => "npm publish"
    };

    private static void AssertReleaseStep(string release, string name, string guard, string prerelease)
    {
        var step = JobStep(release, name);
        Assert.Equal(guard, StepField(step, "if"));
        Assert.Equal("softprops/action-gh-release@v2", StepField(step, "uses"));
        Assert.Equal(prerelease, StepField(step, "prerelease"));
    }

    private static string JobStep(string job, string name)
    {
        var lines = job.Split('\n');
        var start = Array.FindIndex(lines, line => line == $"      - name: {name}");
        Assert.True(start >= 0, $"Missing release step '{name}'.");

        var end = start + 1;
        while (end < lines.Length && !lines[end].StartsWith("      - ", StringComparison.Ordinal))
            end++;
        return string.Join('\n', lines[start..end]);
    }

    private static string? StepField(string step, string field)
    {
        foreach (var line in step.Split('\n'))
        {
            var trimmed = line.TrimStart();
            var prefix = $"{field}:";
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
                return trimmed[prefix.Length..].Trim();
        }
        return null;
    }

    private static string Swap(string text, string first, string second) => text
        .Replace(first, "__temporary_swap__", StringComparison.Ordinal)
        .Replace(second, first, StringComparison.Ordinal)
        .Replace("__temporary_swap__", second, StringComparison.Ordinal);

    private static string? JobField(string body, string field)
    {
        foreach (var line in body.Split('\n'))
        {
            var prefix = $"    {field}:";
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return line[prefix.Length..].Trim();
        }
        return null;
    }

    private static Dictionary<string, string> ActiveJobs(string workflow)
    {
        var lines = ActiveText(workflow).Split('\n');
        var jobs = new Dictionary<string, string>();
        string? name = null;
        var body = new List<string>();

        foreach (var line in lines)
        {
            if (line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("    ", StringComparison.Ordinal) && line.EndsWith(':'))
            {
                if (name is not null)
                    jobs.Add(name, string.Join('\n', body));
                name = line[2..^1];
                body.Clear();
            }
            else if (name is not null)
                body.Add(line);
        }

        if (name is not null)
            jobs.Add(name, string.Join('\n', body));
        return jobs;
    }

    private static string ActivePush(string workflow)
    {
        var lines = ActiveText(workflow).Split('\n');
        var start = Array.IndexOf(lines, "  push:");
        Assert.True(start >= 0, "Missing active push trigger.");

        var end = start + 1;
        while (end < lines.Length &&
               (!lines[end].StartsWith("  ", StringComparison.Ordinal) ||
                lines[end].StartsWith("    ", StringComparison.Ordinal)))
            end++;

        return string.Join('\n', lines[start..end]);
    }

    private static string ActiveText(string workflow) => string.Join('\n', workflow
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Split('\n')
        .Select(StripInlineComment)
        .Where(line => !string.IsNullOrWhiteSpace(line)));

    private static string StripInlineComment(string line)
    {
        char? quote = null;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (quote is null && (character == '\'' || character == '\"'))
                quote = character;
            else if (quote == character)
                quote = null;
            else if (quote is null && character == '#')
                return line[..index].TrimEnd();
        }
        return line;
    }

    private static string Workflow() => File.ReadAllText(RepositoryFile(".github", "workflows", "release.yml"));

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
