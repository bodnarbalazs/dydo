namespace DynaDocs.Tests.Steps;

using Xunit.Sdk;

public class CliScenarioCleanupTests(CliProcessProbe probe) : IClassFixture<CliProcessProbe>
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Timeout_ConfirmsDescendantsAfterStreamsAndParentComplete(bool finishesTermination)
    {
        if (!OperatingSystem.IsLinux())
            return;
        var scenario = new CliScenario();
        var terminationReads = 0;
        var hold = 0;
        string? captured = null;
        string ReadStat(string path)
        {
            var pidPath = Path.Combine(scenario.DirectoryPath, "hold.pid");
            if (hold == 0 && File.Exists(pidPath))
                hold = int.Parse(File.ReadAllText(pidPath));
            if (path != $"/proc/{hold}/stat")
                return File.ReadAllText(path);
            if (captured is null)
                return captured = File.ReadAllText(path);
            terminationReads++;
            if (finishesTermination && terminationReads > 1)
                return File.ReadAllText(path);

            // Hold the same identity at the observed EOF/reap-before-final-exit interleaving.
            // The real tree is killed; only the proc observation is controlled.
            var end = captured.LastIndexOf(')');
            var fields = captured[(end + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            fields[0] = finishesTermination ? "R" : "T";
            fields[6] = (ulong.Parse(fields[6]) | 4).ToString(); // PF_EXITING cannot imply termination.
            fields[17] = "1";
            fields[20] = "0";
            fields[21] = "0";
            return captured[..(end + 1)] + " " + string.Join(' ', fields);
        }

        var run = scenario.RunProcessAsync(probe.Command(scenario.DirectoryPath, "tree", scenario.DirectoryPath),
            TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1), ReadStat);
        try
        {
            if (finishesTermination)
            {
                var error = await Assert.ThrowsAsync<TimeoutException>(() => run);
                Assert.True(terminationReads >= 2, "Cleanup must observe the exiting identity before confirming its final termination.");
                Assert.Contains("parent ready", error.Message);
                Assert.Contains("parent stderr", error.Message);
                Assert.True(ProcessLiveness.HasTerminated(hold));
            }
            else
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => run);
                Assert.IsAssignableFrom<OperationCanceledException>(error.InnerException);
                Assert.Contains(scenario.DirectoryPath, error.Message);
                Assert.ThrowsAny<XunitException>(scenario.Cleanup);
                Assert.True(Directory.Exists(scenario.DirectoryPath));
            }
            Assert.True(terminationReads > 0, "EOF and direct-parent reap alone cannot confirm descendant termination.");
            var parent = int.Parse(File.ReadAllText(Path.Combine(scenario.DirectoryPath, "tree.pid")));
            Assert.False(Directory.Exists($"/proc/{parent}"), "Direct parent must already be reaped.");
            if (finishesTermination)
            {
                scenario.Cleanup();
                Assert.False(Directory.Exists(scenario.DirectoryPath));
            }
        }
        finally
        {
            try { await run; }
            catch (TimeoutException) { }
            catch (InvalidOperationException) { }
            if (hold != 0)
                await ProcessLiveness.WaitForTerminationAsync(hold);
            if (Directory.Exists(scenario.DirectoryPath))
                Directory.Delete(scenario.DirectoryPath, recursive: true);
        }
    }
}
