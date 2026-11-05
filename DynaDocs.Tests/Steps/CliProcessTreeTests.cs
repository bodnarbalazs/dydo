namespace DynaDocs.Tests.Steps;

public class CliProcessTreeTests
{
    [Theory]
    [InlineData("Z")]
    [InlineData("missing")]
    [InlineData("reused")]
    public async Task CapturedIdentity_ConfirmsOnlyItsOwnTermination(string outcome)
    {
        if (!OperatingSystem.IsLinux())
            return;
        var captured = false;
        string Read(string path)
        {
            if (path == "/proc/self/stat")
                return "mounted";
            if (path == "/proc/1/stat")
                return Stat(1, 0, "S", 10);
            if (path != $"/proc/{Environment.ProcessId}/stat" || captured && outcome == "missing")
                throw new FileNotFoundException();
            return Stat(Environment.ProcessId, 1, captured && outcome == "Z" ? "Z" : "S",
                captured && outcome == "reused" ? 12UL : 11UL);
        }
        var tree = new CliProcessTree(Read);
        tree.Capture(1, CancellationToken.None);
        captured = true;
        await tree.WaitForTerminationAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("R")]
    [InlineData("T")]
    [InlineData("?")]
    [InlineData("unreadable")]
    [InlineData("mount-missing")]
    public async Task CapturedIdentity_LiveOrUnverifiableNeverConfirmsCleanup(string outcome)
    {
        if (!OperatingSystem.IsLinux())
            return;
        using var cleanup = new CancellationTokenSource();
        var captured = false;
        string Read(string path)
        {
            if (path == "/proc/self/stat")
                return captured && outcome == "mount-missing" ? throw new DirectoryNotFoundException() : "mounted";
            if (path == "/proc/1/stat")
                return Stat(1, 0, "S", 10);
            if (path != $"/proc/{Environment.ProcessId}/stat")
                throw new FileNotFoundException();
            if (!captured)
                return Stat(Environment.ProcessId, 1, "S", 11);
            if (outcome == "unreadable")
                throw new UnauthorizedAccessException();
            cleanup.Cancel();
            return Stat(Environment.ProcessId, 1, outcome, 11);
        }
        var tree = new CliProcessTree(Read);
        tree.Capture(1, cleanup.Token);
        captured = true;
        var error = await Record.ExceptionAsync(() => tree.WaitForTerminationAsync(cleanup.Token));
        Assert.NotNull(error);
        if (outcome is "R" or "T")
            Assert.IsAssignableFrom<OperationCanceledException>(error);
        else if (outcome == "?")
            Assert.IsType<InvalidDataException>(error);
        else if (outcome == "unreadable")
            Assert.IsType<UnauthorizedAccessException>(error);
        else
            Assert.IsType<DirectoryNotFoundException>(error);
    }

    private static string Stat(int pid, int parent, string state, ulong start) =>
        $"{pid} (name with ) parentheses) {state} {parent} " + string.Join(' ', Enumerable.Repeat("1", 17)) + $" {start}";
}
