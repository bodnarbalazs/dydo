using System.Diagnostics;
using Reqnroll;

namespace DynaDocs.Tests.Steps;

[Binding]
public sealed class AssuranceAdoptionSteps
{
    private static readonly Lazy<(int Exit, string Output)> Probe = new(RunProbe);

    [Given(@"^a configured ""([^""]+)"" fixture adapter that completes with native exit (\d+)$")]
    public void GivenGateFixture(string _, int __) => AssertProbe();

    [Given(@"^a configured test fixture command that normally returns native exit (\d+)$")]
    public void GivenTestFixture(int _) => AssertProbe();

    [Given(@"^three ordered independent ""([^""]+)"" adapters returning native exits 1, 2 and 0$")]
    public void GivenIndependentFixtures(string _) => AssertProbe();

    [Given(@"^an ordered ""([^""]+)"" adapter that starts a long-lived owned child and then cancels it$")]
    public void GivenInterruptedFixture(string _) => AssertProbe();

    [Given(@"^the adapter produces its declared evidence and completes owned cleanup before returning$")]
    [Given(@"^the facade itself receives no interruption$")]
    [Given(@"^the adapter waits for that child to stop and removes its owned scratch before returning 130$")]
    [Given(@"^a later independent adapter would write a dispatch marker$")]
    public void GivenContractCondition() => AssertProbe();

    [When(@"^I invoke that gate through each canonical and derived testing facade$")]
    [When(@"^I invoke the test through each canonical and derived testing facade$")]
    public void WhenInvoked() => AssertProbe();

    [Then(@"^its result row has state ""([^""]+)"", childExit (\d+) and resultExit (\d+)$")]
    public void ThenResultRow(string _, int __, int ___) => AssertProbe();

    [Then(@"^its aggregate result and process exit are both (\d+)$")]
    [Then(@"^the aggregate result and process exit are both (\d+)$")]
    public void ThenAggregate(int _) => AssertProbe();

    [Then(@"^the published result retains schema 1 and the existing artifact shape$")]
    [Then(@"^all three adapters execute once in manifest order$")]
    [Then(@"^their ordered row states are failed, invalid and passed with unchanged child exits$")]
    [Then(@"^the interrupted row is published only after the owned child has stopped and scratch is absent$")]
    [Then(@"^the later adapter does not execute and its dispatch marker is absent$")]
    [Then(@"^the interrupted row retains childExit 130 and resultExit 130$")]
    public void ThenContractObserved() => AssertProbe();

    private static void AssertProbe()
    {
        var result = Probe.Value;
        Assert.True(result.Exit == 0, result.Output);
    }

    private static (int, string) RunProbe()
    {
        var root = RepositoryRoot();
        var interpreter = Environment.GetEnvironmentVariable("PYTHON")
            ?? (OperatingSystem.IsWindows() ? "py" : "python3");
        var start = new ProcessStartInfo(interpreter)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["PYTHON"] = interpreter;
        start.ArgumentList.Add("-m");
        start.ArgumentList.Add("unittest");
        start.ArgumentList.Add("discover");
        start.ArgumentList.Add("-s");
        start.ArgumentList.Add("DynaDocs.Tests/coverage/tests");
        start.ArgumentList.Add("-p");
        start.ArgumentList.Add("test_assurance_adoption.py");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                || File.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;
        throw new InvalidOperationException("Cannot locate repository root");
    }
}
