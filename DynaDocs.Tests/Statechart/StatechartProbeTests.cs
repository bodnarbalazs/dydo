namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;
using DynaDocs.Services.Statechart;

public class StatechartProbeTests
{
    [Fact]
    public async Task ProbeOutputIsReadUpTo64KiBAndRefusedBeyond()
    {
        Assert.Equal(65536, (await StatechartProbe.Bounded(new StringReader(new string('x', 65536)), CancellationToken.None)).Length);
        await Assert.ThrowsAsync<IOException>(() => StatechartProbe.Bounded(new StringReader(new string('x', 65537)), CancellationToken.None));
    }

    [SkippableTheory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task InstalledNativeProbePreservesBothOutputChannelsAndChildExitAfterGo(int status)
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "This probe fixture launches cmd.exe.");
        var start = StatechartTestBinary.Start();
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
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
