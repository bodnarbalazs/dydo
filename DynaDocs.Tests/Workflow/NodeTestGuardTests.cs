namespace DynaDocs.Tests.Workflow;

using System.ComponentModel;
using System.Diagnostics;

// DYD-622: node exits 0 when a test skips, so the hosted Windows statechart leg trusts this guard to
// read the TAP summary. These tests run the committed guard under pwsh against reports shaped like
// node's own. pwsh is found on PATH. Without it they skip locally and fail on a GitHub Actions runner,
// where every dotnet test leg already runs its own pwsh steps.
public sealed class NodeTestGuardTests : IDisposable
{
    private const int MinimumTests = 6;
    private const string MissingPwsh = "pwsh is not on PATH, so the node test guard cannot run.";
    private readonly string _directory = Directory.CreateTempSubdirectory("dydo-node-guard-").FullName;

    public void Dispose() => TestDirectory.Delete(_directory);

    [SkippableFact]
    public void Guard_AcceptsAReportInWhichEveryTestRanAndPassed()
    {
        var (exitCode, output) = RunGuard(Report(tests: 6, pass: 6));

        Assert.True(exitCode == 0, output);
        Assert.Contains("Every test ran and passed: tests=6 pass=6 fail=0 cancelled=0 skipped=0 todo=0", output);
    }

    [SkippableFact]
    public void Guard_RejectsAMissingReport()
    {
        var (exitCode, output) = RunGuard(Path.Combine(_directory, "absent.tap"));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("No test ran: there is no TAP report at", output);
    }

    [SkippableTheory]
    [InlineData("TAP version 13\nok 1 - case 1\n1..1\n", "tests, pass, fail, cancelled, skipped, todo")]
    [InlineData("TAP version 13\n1..6\n# tests 6\n# pass 6\n# fail 0\n# cancelled 0\n# skipped 0\n", "todo")]
    public void Guard_RejectsAReportWithoutEverySummaryCounter(string report, string missing)
    {
        var (exitCode, output) = RunGuard(Write(report));

        Assert.NotEqual(0, exitCode);
        Assert.Contains($"The TAP report has no summary for: {missing}.", output);
    }

    [SkippableTheory]
    [InlineData(0, 0, 0, 0, 0, 0, "Fewer than 6 tests ran")]
    [InlineData(2, 2, 0, 0, 0, 0, "Fewer than 6 tests ran")]
    [InlineData(6, 5, 0, 0, 1, 0, "A test skipped instead of running")]
    [InlineData(6, 5, 0, 0, 0, 1, "A test skipped instead of running")]
    [InlineData(6, 5, 1, 0, 0, 0, "Not every test passed")]
    [InlineData(6, 5, 0, 1, 0, 0, "Not every test passed")]
    public void Guard_RejectsAReportThatProvesLessThanEveryRequiredTestPassing(
        int tests, int pass, int fail, int cancelled, int skipped, int todo, string reason)
    {
        var (exitCode, output) = RunGuard(Report(tests, pass, fail, cancelled, skipped, todo));

        Assert.NotEqual(0, exitCode);
        Assert.Contains($"{reason}: tests={tests} pass={pass} fail={fail} cancelled={cancelled} skipped={skipped} todo={todo}", output);
    }

    // node's TAP reporter ends each file with these counters after its test points.
    private string Report(int tests, int pass, int fail = 0, int cancelled = 0, int skipped = 0, int todo = 0)
    {
        var points = Enumerable.Range(1, tests).Select(index => $"# Subtest: case {index}\nok {index} - case {index}\n");
        return Write($"TAP version 13\n{string.Concat(points)}1..{tests}\n# tests {tests}\n# suites 0\n# pass {pass}\n" +
            $"# fail {fail}\n# cancelled {cancelled}\n# skipped {skipped}\n# todo {todo}\n# duration_ms 12.5\n");
    }

    private string Write(string report)
    {
        var path = Path.Combine(_directory, "report.tap");
        File.WriteAllText(path, report);
        return path;
    }

    private static (int ExitCode, string Output) RunGuard(string tap)
    {
        var guard = ReleaseWorkflowTests.RepositoryFile(".github", "workflows", "assert-node-tests-ran.ps1");
        // pwsh's own error view wraps a thrown message at the console width, so print the message whole.
        var command = $"try {{ & {Quoted(guard)} -Tap {Quoted(tap)} -MinimumTests {MinimumTests} }} " +
            "catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }";
        var start = new ProcessStartInfo("pwsh", ["-NoProfile", "-NonInteractive", "-Command", command])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        Process process;
        try
        {
            process = Process.Start(start)!;
        }
        catch (Win32Exception error)
        {
            Skip.IfNot(Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true", MissingPwsh);
            throw new InvalidOperationException(MissingPwsh + " A GitHub Actions runner must provide it.", error);
        }
        using (process)
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, stdout.Result + stderr);
        }
    }

    private static string Quoted(string path) => "'" + path.Replace("'", "''", StringComparison.Ordinal) + "'";
}
