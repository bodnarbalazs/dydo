namespace DynaDocs.Tests.Workflow;

public sealed class ReleaseWorkflowTests
{
    private const string AllowedTagGuard = "github.event_name == 'push' && github.ref == 'refs/tags/v3.5.1'";
    private const string NpmPublishRun = "npm publish --access public --provenance --tag latest";
    private const string PythonVersionFile = "DynaDocs.Tests/coverage/.python-version";
    private const string AssurancePython = ".static-gates/python";
    private const string AssurancePythonExecutable = AssurancePython + "/Scripts/python.exe";
    private const string ViewerInstallStep = "Install viewer dependencies";
    private const string CoverageEvidenceStep = "Upload coverage evidence";
    private const string CoverageRestore = "pnpm -C DynaDocs.Tests/coverage install --frozen-lockfile";
    private const string ValidationCacheLocks = "\n            DynaDocs.Tests/coverage/pnpm-lock.yaml\n            viewer/pnpm-lock.yaml";
    private const string ValidationCacheKey = "cache-dependency-path: |" + ValidationCacheLocks;
    private const string StatechartProvisionStep = "Provision statechart prerequisites";
    private const string StatechartTestStep = "Windows statechart tests";
    private const string StatechartGuardStep = "Require every Windows statechart test to run";
    private const string StatechartTap = "${{ runner.temp }}/statechart-windows.tap";
    private const string StatechartTestRun = "node --disable-warning=ExperimentalWarning --test-reporter=spec --test-reporter-destination=stdout " +
        "--test-reporter=tap --test-reporter-destination=" + StatechartTap + " test/windows.test.ts";

    [Fact]
    public void CiWorkflow_RunsNativeCancellationOnSupportedLinuxAndWindowsWithPinnedPython()
    {
        var workflow = File.ReadAllText(RepositoryFile(".github", "workflows", "ci.yml"));
        AssertCancellationJob(ActiveJobs(workflow)["assurance-cancellation"]);
        Assert.Equal("3.14.4\n", PythonRuntimePin().Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ubuntu-24.04", "ubuntu-latest")]
    [InlineData("ubuntu-24.04", "ubuntu-26.04")]
    [InlineData("[ubuntu-24.04, windows-latest]", "[ubuntu-24.04]")]
    [InlineData("runs-on: ${{ matrix.os }}", "runs-on: ubuntu-24.04")]
    [InlineData("python-version-file:", "python-version:")]
    [InlineData("DynaDocs.Tests/coverage/.python-version", "wrong/.python-version")]
    [InlineData("run: python -m unittest", "# run: python -m unittest")]
    [InlineData("test_windows_cancellation ", "")]
    [InlineData("test_windows_job ", "")]
    [InlineData(" test_gate_adapter.CoverageCancellationTests", "")]
    public void CiWorkflow_RejectsUnsupportedRunnersOrMissingNativeCancellationProof(string original, string replacement)
    {
        var workflow = File.ReadAllText(RepositoryFile(".github", "workflows", "ci.yml"));
        var job = ActiveJobs(workflow)["assurance-cancellation"];
        AssertCancellationJob(job);
        Assert.Contains(original, job);
        var invalid = ActiveText(job.Replace(original, replacement, StringComparison.Ordinal));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertCancellationJob(invalid));
    }

    // Each feature-tip merge commit gets a hosted verdict. A newer push to the same feature branch
    // cancels its superseded run; master/main pushes and pull requests get a group of their own run,
    // so they are never cancelled, and no run cancels another ref's run. Only a first attempt joins
    // the branch group: after push B supersedes A, a rerun of A (run_attempt > 1) takes its run's
    // own group, so it neither cancels tip B nor is cancelled by it.
    [Fact]
    public void CiWorkflow_RunsOnFeatureTipPushesAndCancelsOnlyASupersededRunOnTheSameFeatureBranch()
    {
        var workflow = File.ReadAllText(RepositoryFile(".github", "workflows", "ci.yml"));

        Assert.Equal("on:\n  push:\n    branches: [master, main, 'feature/**']\n  pull_request:", TopLevelBlock(workflow, "on"));
        Assert.Equal(
            "concurrency:\n" +
            "  group: ci-${{ github.event_name == 'push' && startsWith(github.ref, 'refs/heads/feature/') && github.run_attempt == 1 && github.ref || github.run_id }}\n" +
            "  cancel-in-progress: true",
            TopLevelBlock(workflow, "concurrency"));
        Assert.DoesNotContain("concurrency:", TopLevelBlock(workflow, "jobs"));
    }

    // Push A, push B (cancels A), rerun A: the rerun's attempt is 2, so its group is its run_id and
    // tip B's group ci-refs/heads/feature/<slug> is never shared with it.
    [Fact]
    public void CiWorkflow_RerunOfASupersededFeatureRunTakesItsOwnConcurrencyGroup()
    {
        var group = TopLevelBlock(File.ReadAllText(RepositoryFile(".github", "workflows", "ci.yml")), "concurrency")
            .Split('\n')[1];
        var branchArm = group[..group.IndexOf("&& github.ref ||", StringComparison.Ordinal)];

        Assert.Contains("github.run_attempt == 1", branchArm);
        Assert.EndsWith("&& github.ref || github.run_id }}", group);
    }

    [Fact]
    public void ReleaseWorkflow_UsesPinnedPythonOnlyOnSupportedWindowsValidation()
    {
        var pythonJobs = ActiveJobs(Workflow())
            .Where(job => job.Value.Contains("actions/setup-python@", StringComparison.Ordinal)).ToArray();
        var validation = Assert.Single(pythonJobs);
        Assert.Equal("validation", validation.Key);
        Assert.Equal("windows-latest", JobField(validation.Value, "runs-on"));
        AssertValidationPythonContract(Workflow(), PythonRuntimePin());
    }

    private static void AssertCancellationJob(string job)
    {
        Assert.Equal("${{ matrix.os }}", JobField(job, "runs-on"));
        Assert.Equal("[ubuntu-24.04, windows-latest]", StepField(job, "os"));
        Assert.Equal("false", StepField(job, "fail-fast"));
        Assert.Null(StepField(job, "exclude"));
        Assert.Null(JobField(job, "if"));
        Assert.Null(JobField(job, "continue-on-error"));

        var setup = JobStep(job, "Setup Python");
        Assert.Equal("actions/setup-python@v5", StepField(setup, "uses"));
        Assert.Equal(PythonVersionFile, StepField(setup, "python-version-file"));
        Assert.Null(StepField(setup, "python-version"));
        Assert.Null(StepField(setup, "if"));
        var regression = JobStep(job, "Native cancellation regression");
        Assert.Equal("DynaDocs.Tests/coverage/tests", StepField(regression, "PYTHONPATH"));
        Assert.Equal("python -m unittest -v test_windows_cancellation test_windows_job test_gate_adapter.CoverageCancellationTests",
            StepField(regression, "run"));
        Assert.Null(StepField(regression, "if"));
        Assert.Null(StepField(regression, "continue-on-error"));
        Assert.True(StepIndex(job, "Setup Python") < StepIndex(job, "Native cancellation regression"));
    }

    // DYD-622: the statechart runtime's Windows paths run only on Windows, so a hosted Windows leg runs
    // windows.test.ts with real TLC and fails when a test skips or none runs.
    [Fact]
    public void CiWorkflow_RunsEveryWindowsStatechartTestWithThePinnedPrerequisites() =>
        AssertStatechartWindowsJob(ActiveJobs(CiWorkflow())["statechart-windows"]);

    [Theory]
    [InlineData("runs-on: windows-latest", "runs-on: ubuntu-24.04")]
    [InlineData("    runs-on: windows-latest", "    if: github.event_name == 'push'\n    runs-on: windows-latest")]
    [InlineData("    timeout-minutes: 20", "    timeout-minutes: 20\n    continue-on-error: true")]
    [InlineData("scripts install --frozen-lockfile", "scripts install")]
    [InlineData("run: ./.github/workflows/provision-statechart.ps1", "run: echo unprovisioned")]
    [InlineData("TLA2TOOLS: ${{ steps.statechart.outputs.tla2tools }}", "")]
    [InlineData("JAVA: ${{ steps.statechart.outputs.java }}", "")]
    [InlineData("STATECHART_PELE: ${{ steps.statechart.outputs.pele }}", "")]
    [InlineData("test/windows.test.ts", "test/chart.test.ts")]
    [InlineData("--disable-warning=ExperimentalWarning --test-reporter=spec", "--disable-warning=ExperimentalWarning --test --test-reporter=spec")]
    [InlineData(" --test-reporter=tap --test-reporter-destination=${{ runner.temp }}/statechart-windows.tap", "")]
    [InlineData("run: ./.github/workflows/assert-node-tests-ran.ps1", "# run: ./.github/workflows/assert-node-tests-ran.ps1")]
    [InlineData(" -MinimumTests 6", " -MinimumTests 2")]
    [InlineData(" -MinimumTests 6", "")]
    [InlineData("      - name: " + StatechartGuardStep, "      - name: " + StatechartGuardStep + "\n        if: false")]
    [InlineData("      - name: " + StatechartGuardStep, "      - name: " + StatechartGuardStep + "\n        continue-on-error: true")]
    [InlineData("      - name: " + StatechartTestStep, "      - name: " + StatechartTestStep + "\n        continue-on-error: true")]
    public void CiWorkflow_RejectsAWindowsStatechartLegThatCanSkipOrPassVacuously(string original, string replacement)
    {
        var job = ActiveJobs(CiWorkflow())["statechart-windows"];
        AssertStatechartWindowsJob(job);
        Assert.Contains(original, job);
        var invalid = ActiveText(job.Replace(original, replacement, StringComparison.Ordinal));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertStatechartWindowsJob(invalid));
    }

    [Fact]
    public void CiWorkflow_RejectsAWindowsStatechartGuardBeforeItsTests()
    {
        var job = ActiveJobs(CiWorkflow())["statechart-windows"];
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertStatechartWindowsJob(
            Swap(job, JobStep(job, StatechartTestStep), JobStep(job, StatechartGuardStep))));
    }

    // Both Windows statechart consumers provision through one script, so their pins cannot drift apart.
    [Theory]
    [InlineData("ci.yml", "statechart-windows", StatechartTestStep)]
    [InlineData("ci.yml", "statechart-coverage", "Statechart suite and platform coverage gate")]
    [InlineData("release.yml", "validation", "Run coverage gate")]
    public void Workflows_ProvisionStatechartPrerequisitesThroughTheSharedScript(string name, string jobName, string consumer)
    {
        var workflow = ActiveText(File.ReadAllText(RepositoryFile(".github", "workflows", name)));
        var job = ActiveJobs(workflow)[jobName];

        AssertStatechartProvisioning(job, consumer);
        Assert.DoesNotContain("dependency-lock.json", workflow);
        Assert.DoesNotContain("Get-FileHash", workflow);
    }

    [Fact]
    public void StatechartProvisioning_ChecksEveryDownloadAgainstItsDependencyLockPin()
    {
        var script = File.ReadAllText(RepositoryFile(".github", "workflows", "provision-statechart.ps1"));

        foreach (var check in new[]
        {
            "Get-Content Resources/Statechart/dependency-lock.json -Raw",
            "Where-Object rid -eq 'win-x64'",
            "(Get-FileHash $jar -Algorithm SHA256).Hash -ne $lock.tlc.sha256) { throw",
            "(Get-FileHash $archive -Algorithm SHA256).Hash -ne $jre.sha256) { throw",
            "$integrity -cne $lock.pele.integrity) { throw",
            "\"tla2tools=$jar\" >> $env:GITHUB_OUTPUT",
            "\"java=$($java[0].FullName)\" >> $env:GITHUB_OUTPUT",
            "\"pele=$entry\" >> $env:GITHUB_OUTPUT",
        })
            Assert.Contains(check, script);
    }

    private static void AssertStatechartWindowsJob(string job)
    {
        Assert.Equal("windows-latest", JobField(job, "runs-on"));
        Assert.Null(JobField(job, "strategy"));
        Assert.Null(JobField(job, "if"));
        Assert.Null(JobField(job, "continue-on-error"));
        Assert.Equal("pnpm -C skills/engineering/statechart/scripts install --frozen-lockfile",
            StepField(JobStep(job, "Install statechart test dependencies"), "run"));
        AssertStatechartProvisioning(job, StatechartTestStep);

        var tests = JobStep(job, StatechartTestStep);
        var guard = JobStep(job, StatechartGuardStep);
        Assert.Equal("skills/engineering/statechart/scripts", StepField(tests, "working-directory"));
        Assert.Equal(StatechartTestRun, StepField(tests, "run"));
        Assert.Equal("pwsh", StepField(guard, "shell"));
        Assert.Equal($"./.github/workflows/assert-node-tests-ran.ps1 -Tap '{StatechartTap}' -MinimumTests 6", StepField(guard, "run"));
        foreach (var step in new[] { tests, guard })
        {
            Assert.Null(StepField(step, "if"));
            Assert.Null(StepField(step, "continue-on-error"));
        }
        Assert.True(StepIndex(job, "Install statechart test dependencies") < StepIndex(job, StatechartTestStep));
        Assert.True(StepIndex(job, StatechartTestStep) < StepIndex(job, StatechartGuardStep), "The guard must read the report its tests wrote.");
    }

    private static void AssertStatechartProvisioning(string job, string consumer)
    {
        var provision = JobStep(job, StatechartProvisionStep);
        Assert.Equal("statechart", StepField(provision, "id"));
        Assert.Equal("pwsh", StepField(provision, "shell"));
        Assert.Equal("./.github/workflows/provision-statechart.ps1", StepField(provision, "run"));
        Assert.Null(StepField(provision, "if"));
        Assert.Null(StepField(provision, "continue-on-error"));

        var consuming = JobStep(job, consumer);
        Assert.Equal("${{ steps.statechart.outputs.tla2tools }}", StepField(consuming, "TLA2TOOLS"));
        Assert.Equal("${{ steps.statechart.outputs.java }}", StepField(consuming, "JAVA"));
        Assert.Equal("${{ steps.statechart.outputs.pele }}", StepField(consuming, "STATECHART_PELE"));
        Assert.True(StepIndex(job, StatechartProvisionStep) < StepIndex(job, consumer), "Provisioning must precede its consumer.");
    }

    private static string CiWorkflow() => File.ReadAllText(RepositoryFile(".github", "workflows", "ci.yml"));

    [Fact]
    public void RuntimePin_RequiresThePatchedAotCompilerAndRuntime()
    {
        var project = System.Xml.Linq.XDocument.Load(RepositoryFile("DynaDocs.csproj"));
        Assert.Equal("10.0.14", Assert.Single(project.Descendants("RuntimeFrameworkVersion")).Value);
        Assert.Equal("true", Assert.Single(project.Descendants("PublishAot")).Value);
        Assert.Equal("true", Assert.Single(project.Descendants("PackAsTool")).Value);
        Assert.Equal("LatestMinor", Assert.Single(project.Descendants("RollForward")).Value);
    }

    [Theory]
    [InlineData("ci.yml", "python-coverage statechart-coverage dotnet-coverage dotnet-coverage-union guard-windows build full-stack")]
    [InlineData("release.yml", "build validation nuget")]
    public void Workflows_ProvisionPatchedRuntimeBeforeDotnetConsumers(string name, string expectedJobs)
    {
        var workflow = File.ReadAllText(RepositoryFile(".github", "workflows", name));
        var jobs = ActiveJobs(workflow).Where(job => job.Value.Contains("actions/setup-dotnet@", StringComparison.Ordinal)).ToArray();
        Assert.Equal(expectedJobs.Split(' '), jobs.Select(job => job.Key));
        Assert.All(jobs.Select(job => job.Value), AssertPatchedRuntimeProvisioning);
    }

    [Fact]
    public void Workflows_RejectMissingMisorderedOrUnpinnedRuntimeProvisioning()
    {
        var job = ActiveJobs(Workflow())["validation"];
        var setup = JobStep(job, "Setup .NET");
        var provision = JobStep(job, "Provision patched .NET runtime");
        foreach (var invalid in new[]
        {
            job.Replace(provision, "", StringComparison.Ordinal),
            Swap(job, setup, provision),
            job.Replace("shell: pwsh", "shell: bash", StringComparison.Ordinal),
            job.Replace("run: ./.github/workflows/provision-dotnet-runtime.ps1", "# run: ./.github/workflows/provision-dotnet-runtime.ps1", StringComparison.Ordinal),
            job.Replace("dotnet-version: '10.0.303'", "dotnet-version: '10.0.x'", StringComparison.Ordinal),
            job.Replace("DOTNET_INSTALL_DIR:", "REMOVED_DOTNET_INSTALL_DIR:", StringComparison.Ordinal),
            job.Replace(provision, "", StringComparison.Ordinal) + "\n" + provision
        })
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertPatchedRuntimeProvisioning(ActiveText(invalid)));
    }

    private static void AssertPatchedRuntimeProvisioning(string job)
    {
        var setup = JobStep(job, "Setup .NET");
        var provision = JobStep(job, "Provision patched .NET runtime");
        Assert.Equal("'10.0.303'", StepField(setup, "dotnet-version"));
        Assert.Equal("${{ runner.temp }}/dydo-assurance-dotnet", StepField(setup, "DOTNET_INSTALL_DIR"));
        Assert.Equal("pwsh", StepField(provision, "shell"));
        Assert.Equal("./.github/workflows/provision-dotnet-runtime.ps1", StepField(provision, "run"));
        Assert.Null(StepField(provision, "if"));
        Assert.Null(StepField(provision, "continue-on-error"));
        var following = job[(StepIndex(job, "Setup .NET") + setup.Length)..].TrimStart();
        Assert.StartsWith("- name: Provision patched .NET runtime\n", following);
    }

    [Fact]
    public void Workflows_UseTheExactInstalledNodeAndDotnetPins()
    {
        foreach (var name in new[] { "ci.yml", "release.yml" })
        {
            var workflow = File.ReadAllText(RepositoryFile(".github", "workflows", name));
            foreach (var (field, expected) in new[] { ("node-version", "26.13.0"), ("dotnet-version", "10.0.303") })
            {
                var pins = ActiveText(workflow).Split('\n').Select(line => line.Trim())
                    .Where(line => line.StartsWith(field + ":", StringComparison.Ordinal)).ToArray();
                Assert.NotEmpty(pins);
                Assert.All(pins, pin => Assert.Equal($"{field}: '{expected}'", pin));
            }
        }
    }

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
            workflow.Replace(versionFileField, "python-version: '3.14.4'", StringComparison.Ordinal), PythonRuntimePin()));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationPythonContract(
            workflow.Replace(versionFileField, "python-version: '3.14'", StringComparison.Ordinal), PythonRuntimePin()));
        foreach (var pin in new[] { "", "3.14", "3.14.x", ">=3.14", "3.14.4\n3.14.5\n", " 3.14.4\n" })
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
            workflow.Replace("dotnet-version: '10.0.303'", "dotnet-version: '10.0.x'", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace("node-version: '26.13.0'", "node-version: '22'", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace("DOTNET_INSTALL_DIR: ${{ runner.temp }}/dydo-assurance-dotnet", "REMOVED_DOTNET_INSTALL_DIR: ${{ runner.temp }}/dydo-assurance-dotnet", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace("dotnet tool restore --tool-manifest .config/dotnet-tools.json", "dotnet tool restore --tool-manifest .config/wrong-tools.json", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace("dotnet tool restore --tool-manifest .config/dotnet-tools.json", "dotnet tool restore", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            SwapSteps(workflow, "Install Node assurance toolchain", "Run isolated test adapter")));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace(CoverageRestore, "npm ci --prefix DynaDocs.Tests/coverage", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            workflow.Replace(CoverageRestore, "pnpm -C DynaDocs.Tests/coverage install", StringComparison.Ordinal)));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertValidationAssuranceToolchains(
            SwapSteps(workflow, "Setup pnpm", "Install Node assurance toolchain")));
    }

    // DYD-553: every CI job that restores the coverage package, on each of its Linux and Windows
    // legs, restores its pnpm lock frozen, with pnpm set up before Node.js caches its store.
    [Theory]
    [InlineData("python-coverage")]
    [InlineData("statechart-coverage")]
    [InlineData("statechart-coverage-union")]
    [InlineData("dotnet-coverage")]
    [InlineData("assurance-cancellation")]
    public void CiWorkflow_RestoresTheCoveragePackageFromItsPnpmLock(string name)
    {
        var job = ActiveJobs(File.ReadAllText(RepositoryFile(".github", "workflows", "ci.yml")))[name];

        AssertCoverageRestore(job);
        foreach (var invalid in new[]
        {
            job.Replace(CoverageRestore, "npm ci --prefix DynaDocs.Tests/coverage", StringComparison.Ordinal),
            job.Replace(CoverageRestore, "pnpm -C DynaDocs.Tests/coverage install", StringComparison.Ordinal),
            job.Replace("version: 11", "version: 10", StringComparison.Ordinal),
            job.Replace("cache: pnpm", "cache: npm", StringComparison.Ordinal),
            job.Replace("cache-dependency-path: DynaDocs.Tests/coverage/pnpm-lock.yaml", "cache-dependency-path: DynaDocs.Tests/coverage/package-lock.json", StringComparison.Ordinal),
            job.Replace(JobStep(job, "Setup pnpm") + "\n", "", StringComparison.Ordinal),
            Swap(job, JobStep(job, "Setup pnpm"), JobStep(job, "Setup Node.js")),
            Swap(job, JobStep(job, "Setup Node.js"), JobStep(job, "Install locked coverage dependencies")),
        })
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertCoverageRestore(invalid));
    }

    [Theory]
    [InlineData("ci.yml")]
    [InlineData("release.yml")]
    public void Workflows_NeverRestoreWithNpm(string workflow)
    {
        var text = ActiveText(File.ReadAllText(RepositoryFile(".github", "workflows", workflow)));

        Assert.DoesNotContain("npm ci", text);
        Assert.DoesNotContain("package-lock.json", text);
    }

    private static void AssertCoverageRestore(string job)
    {
        var setupPnpm = JobStep(job, "Setup pnpm");
        var setupNode = JobStep(job, "Setup Node.js");
        Assert.Equal("pnpm/action-setup@v4", StepField(setupPnpm, "uses"));
        Assert.Equal("11", StepField(setupPnpm, "version"));
        Assert.Equal("'26.13.0'", StepField(setupNode, "node-version"));
        Assert.Equal("pnpm", StepField(setupNode, "cache"));
        Assert.Equal("DynaDocs.Tests/coverage/pnpm-lock.yaml", StepField(setupNode, "cache-dependency-path"));
        Assert.Equal(CoverageRestore, StepField(JobStep(job, "Install locked coverage dependencies"), "run"));
        Assert.True(StepIndex(job, "Setup pnpm") < StepIndex(job, "Setup Node.js"), "Setup pnpm must precede Setup Node.js, whose pnpm cache needs it.");
        Assert.True(StepIndex(job, "Setup Node.js") < StepIndex(job, "Install locked coverage dependencies"), "Setup Node.js must precede the coverage restore.");
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
        foreach (var oneLock in new[] { "viewer/pnpm-lock.yaml", "DynaDocs.Tests/coverage/pnpm-lock.yaml" })
            AssertViewerToolchainRejected(validation.Replace(ValidationCacheKey, "cache-dependency-path: " + oneLock, StringComparison.Ordinal));
        AssertViewerToolchainRejected(validation.Replace(ValidationCacheLocks, "\n            viewer/pnpm-lock.yaml\n            pnpm-lock.yaml", StringComparison.Ordinal));
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
        Assert.Equal("3.14.4\n", pin.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.True(Version.TryParse(pin.TrimEnd('\r', '\n'), out var version));
        Assert.Equal(3, version.Major);
        Assert.Equal(14, version.Minor);
        Assert.True(version.Build >= 0);
    }

    private static void AssertValidationAssuranceToolchains(string workflow)
    {
        var validation = ActiveJobs(workflow)["validation"];
        var setupDotnet = JobStep(validation, "Setup .NET");
        var setupNode = JobStep(validation, "Setup Node.js");
        Assert.Equal("actions/setup-dotnet@v4", StepField(setupDotnet, "uses"));
        Assert.Equal("${{ runner.temp }}/dydo-assurance-dotnet", StepField(setupDotnet, "DOTNET_INSTALL_DIR"));
        Assert.Equal("'10.0.303'", StepField(setupDotnet, "dotnet-version"));
        Assert.Equal("actions/setup-node@v4", StepField(setupNode, "uses"));
        Assert.Equal("'26.13.0'", StepField(setupNode, "node-version"));
        Assert.Equal($"python -m venv {AssurancePython}", StepField(JobStep(validation, "Create Python assurance environment"), "run"));
        Assert.Equal($"{AssurancePythonExecutable} -m pip install -r DynaDocs.Tests/coverage/requirements.lock", StepField(JobStep(validation, "Install Python assurance toolchain"), "run"));
        Assert.Equal("dotnet tool restore --tool-manifest .config/dotnet-tools.json", StepField(JobStep(validation, "Restore AltCover"), "run"));
        Assert.Equal("dotnet restore DynaDocs.Tests/coverage/metrics/GateMetrics.csproj --locked-mode", StepField(JobStep(validation, "Restore coverage metrics"), "run"));
        Assert.Equal(CoverageRestore, StepField(JobStep(validation, "Install Node assurance toolchain"), "run"));
        Assert.Null(StepField(JobStep(validation, "Install Node assurance toolchain"), "working-directory"));
        Assert.True(StepIndex(validation, "Setup pnpm") < StepIndex(validation, "Install Node assurance toolchain"), "Setup pnpm must precede the coverage restore.");
        Assert.Equal($"{AssurancePythonExecutable} DynaDocs.Tests/coverage/run_tests.py", StepField(JobStep(validation, "Run isolated test adapter"), "run"));
        Assert.Equal($"{AssurancePythonExecutable} DynaDocs.Tests/coverage/gap_check.py --force-run", StepField(JobStep(validation, "Run coverage gate"), "run"));
        Assert.Equal("if ((dotnet --version) -ne '10.0.303') { throw 'Expected .NET SDK 10.0.303.' }", StepField(JobStep(validation, "Verify .NET SDK"), "run"));

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
        // The job restores both pnpm locks, so the store cache is keyed on both.
        Assert.Contains(ValidationCacheKey + "\n", setupNode + "\n", StringComparison.Ordinal);
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

    private static string TopLevelBlock(string workflow, string key)
    {
        var lines = ActiveText(workflow).Split('\n');
        var start = Array.IndexOf(lines, $"{key}:");
        Assert.True(start >= 0, $"Missing active top-level {key} block.");

        var end = start + 1;
        while (end < lines.Length && lines[end].StartsWith(' '))
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

    internal static string RepositoryFile(params string[] parts)
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
