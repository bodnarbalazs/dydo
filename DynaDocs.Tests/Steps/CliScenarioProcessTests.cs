namespace DynaDocs.Tests.Steps;

using System.Diagnostics;
using System.ComponentModel;
using System.Text.Json;
using Xunit.Sdk;

public class CliScenarioProcessTests(CliProcessProbe probe) : IClassFixture<CliProcessProbe>, IDisposable
{
    private readonly CliScenario _scenario = new();

    [Fact]
    public async Task Process_DrainsBothLargeStreamsWithoutDeadlock()
    {
        var result = await Run("flood");
        result.AssertSuccess();
        Assert.Equal(new string('O', 1_300_000), result.Stdout);
        Assert.Equal(new string('E', 1_300_000), result.Stderr);
    }

    [Fact]
    public async Task Process_PreservesNonzeroExitAndBothDiagnosticStreams()
    {
        var result = await Run("failure");
        Assert.Equal(23, result.ExitCode);
        var error = Assert.ThrowsAny<XunitException>(result.AssertSuccess);
        Assert.Contains("retained stdout", error.Message);
        Assert.Contains("retained stderr", error.Message);
    }

    [Fact]
    public async Task Process_PreservesArgumentBoundaries()
    {
        string[] arguments = ["", "with spaces", "quoted\"argument", "semi;colon", "λ/é"];
        var result = await Run(["arguments", .. arguments]);
        result.AssertSuccess();
        Assert.Equal(arguments, JsonSerializer.Deserialize<string[]>(result.Stdout));
    }

    [Fact]
    public async Task MissingExecutable_IsAnExplicitLaunchFailure()
    {
        var start = probe.Command(_scenario.DirectoryPath, "failure");
        start.FileName = Path.Combine(_scenario.DirectoryPath, "missing-executable");
        await Assert.ThrowsAsync<Win32Exception>(() => _scenario.RunProcessAsync(
            start, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)));
        _scenario.Cleanup();
        Assert.False(Directory.Exists(_scenario.DirectoryPath));
    }

    [Fact]
    public async Task Timeout_TerminatesAndReapsParentAndDescendant()
    {
        var error = await Assert.ThrowsAsync<TimeoutException>(() => _scenario.RunProcessAsync(
            probe.Command(_scenario.DirectoryPath, "tree", _scenario.DirectoryPath),
            TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5)));
        Assert.Contains("parent ready", error.Message);
        Assert.Contains("parent stderr", error.Message);
        Assert.True(Exited("tree"));
        Assert.True(Exited("hold"));
    }

    [Fact]
    public async Task OrphanedPipe_ReportsUnconfirmedCleanupAndRetainsScratch()
    {
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _scenario.RunProcessAsync(
                probe.Command(_scenario.DirectoryPath, "orphan", _scenario.DirectoryPath),
                TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)));
            Assert.Contains(_scenario.DirectoryPath, error.Message);
            Assert.Contains("termination", error.Message);
            Assert.True(Exited("orphan"));
            Assert.False(Exited("hold"));
            Assert.ThrowsAny<XunitException>(_scenario.Cleanup);
            Assert.True(Directory.Exists(_scenario.DirectoryPath));
        }
        finally
        {
            await StopDescendant();
        }
    }

    private Task<CliResult> Run(params string[] arguments) => _scenario.RunProcessAsync(
        probe.Command(_scenario.DirectoryPath, arguments), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5));

    private bool Exited(string name)
    {
        var pid = int.Parse(File.ReadAllText(Path.Combine(_scenario.DirectoryPath, name + ".pid")));
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.HasExited;
        }
        catch (ArgumentException) { return true; }
    }

    private async Task StopDescendant()
    {
        var path = Path.Combine(_scenario.DirectoryPath, "hold.pid");
        if (!File.Exists(path) || Exited("hold"))
            return;
        using var child = Process.GetProcessById(int.Parse(File.ReadAllText(path)));
        child.Kill(entireProcessTree: true);
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(child.HasExited);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_scenario.DirectoryPath))
            return;
        Assert.True(!File.Exists(Path.Combine(_scenario.DirectoryPath, "hold.pid")) || Exited("hold"),
            $"Probe child remains alive; retained {_scenario.DirectoryPath}");
        Directory.Delete(_scenario.DirectoryPath, recursive: true);
    }
}
