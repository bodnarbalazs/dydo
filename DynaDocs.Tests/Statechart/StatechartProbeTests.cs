namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;

public class StatechartProbeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task InstalledNativeProbePreservesBothOutputChannelsAndChildExitAfterGo(int status)
    {
        var binary = Environment.GetEnvironmentVariable("STATECHART_TEST_BINARY");
        Assert.True(binary is not null && File.Exists(binary), "STATECHART_TEST_BINARY is required; no source fallback.");
        var start = new ProcessStartInfo(binary!) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "__statechart-probe", Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                "/d", "/c", "echo statechart-probe-out& echo statechart-probe-err 1>&2& exit /b " + status })
            start.ArgumentList.Add(argument);
        using var child = Process.Start(start)!;
        var errors = child.StandardError.ReadToEndAsync();
        Assert.Equal("ready:1", await child.StandardOutput.ReadLineAsync());
        await child.StandardInput.WriteLineAsync("go:1");
        await child.StandardInput.FlushAsync();
        var output = child.StandardOutput.ReadToEndAsync();
        await child.WaitForExitAsync();
        Assert.Equal(status, child.ExitCode);
        Assert.Contains("statechart-probe-out", await output);
        Assert.Contains("statechart-probe-err", await errors);
    }
}
