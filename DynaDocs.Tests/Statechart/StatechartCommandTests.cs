namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;
using System.Text.Json;
using DynaDocs.Commands;

public class StatechartCommandTests
{
    [Fact]
    public void DoctorIsAnExplicitOfflineOperationWithoutAnApprovalFlag()
    {
        var command = StatechartCommand.Create();
        Assert.Equal("statechart", command.Name);
        Assert.Contains(command.Subcommands, child => child.Name == "doctor");
        Assert.DoesNotContain(command.Options, option => option.Name.Contains("approved", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InstalledDoctorExtractsPayloadAndNeverClaimsChartReadinessOrInstallsPrerequisites()
    {
        using var scope = new StatechartTestDirectory();
        var binary = Environment.GetEnvironmentVariable("STATECHART_TEST_BINARY");
        Assert.True(!string.IsNullOrEmpty(binary) && File.Exists(binary), "STATECHART_TEST_BINARY is required; no source fallback.");
        var start = new ProcessStartInfo(binary!)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("statechart");
        start.ArgumentList.Add("doctor");
        foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("DYDO_", StringComparison.Ordinal)).ToArray())
            start.Environment.Remove(name);
        start.Environment["DYDO_STATECHART_ASSET_ROOT"] = Path.Combine(scope.Path, "assets");
        start.Environment["DYDO_STATECHART_DEPENDENCY_ROOT"] = Path.Combine(scope.Path, "dependencies");
        start.Environment["DYDO_STATECHART_STATE_ROOT"] = Path.Combine(scope.Path, "state");
        start.Environment.Remove("JAVA");
        start.Environment.Remove("TLA2TOOLS");
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync();
        var errors = child.StandardError.ReadToEndAsync();
        await child.WaitForExitAsync();
        Assert.Equal("", await errors);
        Assert.Equal(2, child.ExitCode);
        using var json = JsonDocument.Parse(await output);
        Assert.False(json.RootElement.GetProperty("ready").GetBoolean());
        Assert.Equal("diagnostic", json.RootElement.GetProperty("state").GetString());
        Assert.True(Directory.Exists(json.RootElement.GetProperty("payload").GetString()));
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "dependencies")));
        Assert.All(Directory.GetFiles(Path.Combine(scope.Path, "state"), "scratch-*.journal"), journal =>
            Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(journal)));
    }
}
