namespace DynaDocs.Tests.Workflow;

public sealed class ReleaseWorkflowTests
{
    private const string AllowedTagGuard = "github.event_name == 'push' && github.ref == 'refs/tags/v3.1.0'";
    private const string NpmPublishRun = "npm publish --access public --provenance --tag latest";
    private const string PythonVersionFile = "DynaDocs.Tests/coverage/.python-version";
    private const string AssurancePython = "dydo/_system/.local/static-gates/python";
    private const string AssurancePythonExecutable = AssurancePython + "/Scripts/python.exe";
    private const string ViewerInstallStep = "Install viewer dependencies";
    private const string CoverageEvidenceStep = "Upload coverage evidence";

    [Fact]
    public void ReleaseWorkflow_ValidatesTheBuildBeforeEveryPublicationAction()
    {
        var workflow = Workflow();
        var jobs = ActiveJobs(workflow);

        Assert.Contains("workflow_dispatch:", ActiveText(workflow));
        Assert.Equal("  push:\n    tags:\n      - 'v*'", ActivePush(workflow));
        Assert.Equal(5, CountOccurrences(jobs["build"], "rid:"));

        var validation = jobs["validation"];
        Assert.Contains("runs-on: windows-latest", validation);
        Assert.Contains("fetch-depth: 0", validation);
        Assert.Contains("actions/setup-python", validation);
        AssertValidationPythonContract(workflow, PythonRuntimePin());
        AssertValidationAssuranceToolchains(workflow);
        Assert.Contains("dotnet build DynaDocs.sln -c Release --warnaserror", validation);
        Assert.Contains("dotnet run --project DynaDocs.csproj -c Release --no-build -- check", validation);
        Assert.DoesNotContain("gate mutation", ActiveText(workflow), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("v3.0.0-beta.3", ActiveText(workflow), StringComparison.Ordinal);
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
    public void ReleaseWorkflow_RejectsInlineFloatingOrMalformedPythonPins()
    {
        var workflow = Workflow();
        var versionFileField = $"python-version-file: '{PythonVersionFile}'";

        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationPythonContract(
            workflow.Replace(versionFileField, "python-version: '3.12.10'", StringComparison.Ordinal), PythonRuntimePin()));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationPythonContract(
            workflow.Replace(versionFileField, "python-version: '3.12'", StringComparison.Ordinal), PythonRuntimePin()));
        foreach (var pin in new[] { "", "3.12", "3.12.x", ">=3.12", "3.12.10\n3.12.11\n", " 3.12.10\n" })
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationPythonContract(workflow, pin));
    }

    [Fact]
    public void ReleaseWorkflow_RejectsHostedOrIncompleteAssuranceToolchains()
    {
        var workflow = Workflow();
        var localPip = $"{AssurancePythonExecutable} -m pip install -r DynaDocs.Tests/coverage/requirements.lock";
        var localAdapter = $"{AssurancePythonExecutable} DynaDocs.Tests/coverage/run_tests.py";
        var localCoverage = $"{AssurancePythonExecutable} DynaDocs.Tests/coverage/gap_check.py --force-run";

        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace("- name: Create Python assurance environment", "- name: Removed Python assurance environment", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace($"python -m venv {AssurancePython}", $"# python -m venv {AssurancePython}", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace("- name: Restore AltCover", "- name: Removed AltCover restore", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace("dotnet tool restore --tool-manifest .config/dotnet-tools.json", "# dotnet tool restore --tool-manifest .config/dotnet-tools.json", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace(localPip, "python -m pip install -r DynaDocs.Tests/coverage/requirements.lock", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace(localAdapter, "python DynaDocs.Tests/coverage/run_tests.py", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace(localCoverage, "python DynaDocs.Tests/coverage/gap_check.py --force-run", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace("dotnet-version: '10.0.300'", "dotnet-version: '10.0.x'", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace("node-version: '22.13.0'", "node-version: '22'", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace("DOTNET_INSTALL_DIR: ${{ runner.temp }}/dydo-assurance-dotnet", "REMOVED_DOTNET_INSTALL_DIR: ${{ runner.temp }}/dydo-assurance-dotnet", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace("dotnet tool restore --tool-manifest .config/dotnet-tools.json", "dotnet tool restore --tool-manifest .config/wrong-tools.json", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace("dotnet tool restore --tool-manifest .config/dotnet-tools.json", "dotnet tool restore", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            SwapSteps(workflow, "Install Node assurance toolchain", "Run isolated test adapter")));
    }

    [Fact]
    public void ReleaseWorkflow_RejectsValidationWithoutTheViewerToolchain()
    {
        var workflow = Workflow();
        var validation = ActiveJobs(workflow)["validation"];

        AssertValidationViewerToolchain(validation);
        foreach (var step in new[] { "Setup pnpm", ViewerInstallStep })
            AssertViewerToolchainRejected(validation.Replace(JobStep(validation, step) + "\n", "", StringComparison.Ordinal));
        AssertViewerToolchainRejected(validation.Replace("version: 11", "version: 10", StringComparison.Ordinal));
        AssertViewerToolchainRejected(validation.Replace("uses: pnpm/action-setup@v4", "uses: pnpm/action-setup@v3", StringComparison.Ordinal));
        AssertViewerToolchainRejected(validation.Replace("cache: pnpm", "cache: npm", StringComparison.Ordinal));
        AssertViewerToolchainRejected(validation.Replace("cache-dependency-path: viewer/pnpm-lock.yaml", "cache-dependency-path: pnpm-lock.yaml", StringComparison.Ordinal));
        AssertViewerToolchainRejected(validation.Replace("pnpm -C viewer install --frozen-lockfile", "pnpm -C viewer install", StringComparison.Ordinal));
        AssertViewerToolchainRejected(Swap(validation, JobStep(validation, "Setup pnpm"), JobStep(validation, "Setup Node.js")));
        AssertViewerToolchainRejected(Swap(validation, JobStep(validation, ViewerInstallStep), JobStep(validation, "Run coverage gate")));
    }

    [Fact]
    public void ReleaseWorkflow_UploadsTheCoverageEvidenceEvenWhenValidationFails()
    {
        var validation = ActiveJobs(Workflow())["validation"];
        var upload = JobStep(validation, CoverageEvidenceStep);

        Assert.Equal("actions/upload-artifact@v4", StepField(upload, "uses"));
        Assert.Equal("${{ always() }}", StepField(upload, "if"));
        Assert.Equal("validation-coverage-evidence", StepField(upload, "name"));
        Assert.Contains("\n            DynaDocs.Tests/coverage/results/**\n", upload + "\n");
        Assert.Contains("\n            DynaDocs.Tests/TestResults/**\n", upload + "\n");
        Assert.True(StepIndex(validation, "Run coverage gate") < StepIndex(validation, CoverageEvidenceStep),
            "The coverage evidence upload must follow the coverage gate it records.");
    }

    private static void AssertViewerToolchainRejected(string validation) =>
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationViewerToolchain(validation));

    [Fact]
    public void ReleaseWorkflow_RejectsCommentedGuardsMissingValidationAndRoguePublishers()
    {
        var workflow = Workflow();

        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => Assert.Equal(
            "  push:\n    tags:\n      - 'v*'",
            ActivePush(workflow.Replace("    tags:", "    branches:\n      - release\n    tags:", StringComparison.Ordinal))));
        AssertRejected(workflow.Replace($"if: ${{{{ {AllowedTagGuard} }}}}", $"# if: ${{{{ {AllowedTagGuard} }}}}", StringComparison.Ordinal));
        AssertRejected(workflow.Replace(AllowedTagGuard, "github.event_name == 'push' && github.ref == 'refs/heads/release'", StringComparison.Ordinal));
        AssertRejected(workflow.Replace("needs: [build, validation]", "needs: build", StringComparison.Ordinal));
        AssertRejected(workflow.Replace("needs: [build, validation]", "needs: build # needs: [build, validation]", StringComparison.Ordinal));
        AssertRejected(workflow + "\n  rogue:\n    runs-on: ubuntu-latest\n    steps:\n      - run: npm publish --access public\n");
        AssertRejected(workflow.Replace("prerelease: false", "prerelease: true", StringComparison.Ordinal));
        AssertRejected(workflow.Replace(NpmPublishRun, "npm publish --access public --provenance --tag beta", StringComparison.Ordinal));
        AssertRejected(workflow
            .Replace(NpmPublishRun, "npm publish --access public --provenance --tag wrong", StringComparison.Ordinal)
            .Replace("- name: Publish to npm", $"- name: Publish to npm # {NpmPublishRun}", StringComparison.Ordinal));
    }

    [Fact]
    public void ReleaseWorkflow_PublishesStableOnLatest()
    {
        var jobs = ActiveJobs(Workflow());
        var release = jobs["release"];

        Assert.Contains("- name: Create stable release", release);
        Assert.Contains("prerelease: false", release);
        Assert.DoesNotContain("prerelease: true", release);
        Assert.Equal(NpmPublishRun, StepField(JobStep(jobs["npm"], "Publish to npm"), "run"));
    }

    private static void AssertRejected(string workflow) => Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFailClosedPublicationGraph(workflow));

    private static void AssertValidationPythonContract(string workflow, string pin)
    {
        var setup = JobStep(ActiveJobs(workflow)["validation"], "Setup Python");
        Assert.Equal("actions/setup-python@v5", StepField(setup, "uses"));
        Assert.Equal($"'{PythonVersionFile}'", StepField(setup, "python-version-file"));
        Assert.Null(StepField(setup, "python-version"));
        Assert.Equal("3.12.10\n", pin.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.True(Version.TryParse(pin.TrimEnd('\r', '\n'), out var version));
        Assert.Equal(3, version.Major);
        Assert.Equal(12, version.Minor);
        Assert.True(version.Build >= 0);
    }

    private static void AssertValidationAssuranceToolchains(string workflow)
    {
        var validation = ActiveJobs(workflow)["validation"];
        var setupDotnet = JobStep(validation, "Setup .NET");
        var setupNode = JobStep(validation, "Setup Node.js");
        Assert.Equal("actions/setup-dotnet@v4", StepField(setupDotnet, "uses"));
        Assert.Equal("${{ runner.temp }}/dydo-assurance-dotnet", StepField(setupDotnet, "DOTNET_INSTALL_DIR"));
        Assert.Equal("'10.0.300'", StepField(setupDotnet, "dotnet-version"));
        Assert.Equal("actions/setup-node@v4", StepField(setupNode, "uses"));
        Assert.Equal("'22.13.0'", StepField(setupNode, "node-version"));
        Assert.Equal($"python -m venv {AssurancePython}", StepField(JobStep(validation, "Create Python assurance environment"), "run"));
        Assert.Equal($"{AssurancePythonExecutable} -m pip install -r DynaDocs.Tests/coverage/requirements.lock", StepField(JobStep(validation, "Install Python assurance toolchain"), "run"));
        Assert.Equal("dotnet tool restore --tool-manifest .config/dotnet-tools.json", StepField(JobStep(validation, "Restore AltCover"), "run"));
        Assert.Equal("dotnet restore DynaDocs.Tests/coverage/metrics/GateMetrics.csproj --locked-mode", StepField(JobStep(validation, "Restore coverage metrics"), "run"));
        Assert.Equal("npm ci", StepField(JobStep(validation, "Install Node assurance toolchain"), "run"));
        Assert.Equal("DynaDocs.Tests/coverage", StepField(JobStep(validation, "Install Node assurance toolchain"), "working-directory"));
        Assert.Equal($"{AssurancePythonExecutable} DynaDocs.Tests/coverage/run_tests.py", StepField(JobStep(validation, "Run isolated test adapter"), "run"));
        Assert.Equal($"{AssurancePythonExecutable} DynaDocs.Tests/coverage/gap_check.py --force-run", StepField(JobStep(validation, "Run coverage gate"), "run"));
        Assert.Equal("if ((dotnet --version) -ne '10.0.300') { throw 'Expected .NET SDK 10.0.300.' }", StepField(JobStep(validation, "Verify .NET SDK"), "run"));

        var adapter = validation.IndexOf("- name: Run isolated test adapter", StringComparison.Ordinal);
        foreach (var name in new[] { "Create Python assurance environment", "Install Python assurance toolchain", "Restore AltCover", "Restore coverage metrics", "Install Node assurance toolchain" })
            Assert.True(validation.IndexOf($"- name: {name}", StringComparison.Ordinal) < adapter, $"{name} must precede the isolated test adapter.");
        AssertValidationViewerToolchain(validation);
    }

    // The viewer gap_check stack runs its test, static and coverage rows through the viewer's own
    // pnpm scripts, so the coverage gate needs pnpm and the viewer's locked dependencies.
    private static void AssertValidationViewerToolchain(string validation)
    {
        var setupPnpm = JobStep(validation, "Setup pnpm");
        var setupNode = JobStep(validation, "Setup Node.js");
        Assert.Equal("pnpm/action-setup@v4", StepField(setupPnpm, "uses"));
        Assert.Equal("11", StepField(setupPnpm, "version"));
        Assert.Equal("pnpm", StepField(setupNode, "cache"));
        Assert.Equal("viewer/pnpm-lock.yaml", StepField(setupNode, "cache-dependency-path"));
        Assert.Equal("pnpm -C viewer install --frozen-lockfile", StepField(JobStep(validation, ViewerInstallStep), "run"));

        Assert.True(StepIndex(validation, "Setup pnpm") < StepIndex(validation, "Setup Node.js"), "Setup pnpm must precede Setup Node.js, whose pnpm cache needs it.");
        Assert.True(StepIndex(validation, "Setup Node.js") < StepIndex(validation, ViewerInstallStep), "Setup Node.js must precede the viewer install.");
        Assert.True(StepIndex(validation, ViewerInstallStep) < StepIndex(validation, "Run coverage gate"), "The viewer install must precede the coverage gate.");
    }

    private static int StepIndex(string job, string name) => job.IndexOf($"      - name: {name}\n", StringComparison.Ordinal);

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

        AssertReleaseStep(jobs["release"], "Create stable release", "false");
        Assert.Equal(NpmPublishRun, StepField(JobStep(jobs["npm"], "Publish to npm"), "run"));
    }

    private static string PublicationAction(string job) => job switch
    {
        "release" => "softprops/action-gh-release@",
        "nuget" => "dotnet nuget push",
        _ => "npm publish"
    };

    private static void AssertReleaseStep(string release, string name, string prerelease)
    {
        var step = JobStep(release, name);
        Assert.Null(StepField(step, "if"));
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

    private static string SwapSteps(string workflow, string first, string second)
    {
        var active = ActiveText(workflow);
        var validation = ActiveJobs(active)["validation"];
        return Swap(active, JobStep(validation, first), JobStep(validation, second));
    }

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

    private static string PythonRuntimePin() => File.ReadAllText(RepositoryFile("DynaDocs.Tests", "coverage", ".python-version"));

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
