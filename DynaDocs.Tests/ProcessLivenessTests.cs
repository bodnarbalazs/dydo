namespace DynaDocs.Tests;

using System.Diagnostics;

public class ProcessLivenessTests
{
    [Fact]
    public void CurrentProcess_IsLive()
    {
        Assert.False(ProcessLiveness.HasTerminated(Environment.ProcessId));
    }

    [Fact]
    public async Task LinuxZombie_IsTerminatedBeforeItsParentReapsIt_WhileParentRemainsLive()
    {
        if (!OperatingSystem.IsLinux())
            return;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("PYTHON") ?? "python3")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("""
            import os, sys
            pid = os.fork()
            if pid == 0:
                os._exit(0)
            print(pid, flush=True)
            sys.stdin.readline()
            os.waitpid(pid, 0)
            print('reaped', flush=True)
            sys.stdin.readline()
            """);
        using var parent = Process.Start(start)!;
        try
        {
            var pid = int.Parse((await parent.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)))!);
            await ProcessLiveness.WaitForTerminationAsync(pid);
            Assert.Contains("State:\tZ (zombie)", File.ReadAllText($"/proc/{pid}/status"));
            Assert.True(ProcessLiveness.HasTerminated(pid));
            Assert.False(ProcessLiveness.HasTerminated(parent.Id));

            await parent.StandardInput.WriteLineAsync();
            await parent.StandardInput.FlushAsync();
            Assert.Equal("reaped", await parent.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(Directory.Exists($"/proc/{pid}"));
            Assert.True(ProcessLiveness.HasTerminated(pid));
            Assert.False(ProcessLiveness.HasTerminated(parent.Id));
        }
        finally
        {
            parent.StandardInput.Close();
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(0, parent.ExitCode);
        Assert.Empty(await parent.StandardError.ReadToEndAsync());
    }

    [Theory]
    [InlineData("R")]
    [InlineData("S")]
    [InlineData("D")]
    [InlineData("T")]
    [InlineData("t")]
    public void LinuxRunningOrStoppedProcess_IsLive(string state)
    {
        var stat = $"42 (name with ) parentheses) {state} " + string.Join(' ', Enumerable.Repeat("1", 19));
        Assert.False(ProcessLiveness.HasTerminatedOnLinux(42, _ => stat));
    }

    [Theory]
    [InlineData("")]
    [InlineData("42 (probe) Z")]
    [InlineData("42 (probe) ? 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1")]
    [InlineData("43 (probe) Z 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1")]
    [InlineData("42 (probe) Z 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 invalid")]
    public void LinuxUnknownOrMalformedStat_FailsClosed(string stat)
    {
        Assert.Throws<InvalidDataException>(() => ProcessLiveness.HasTerminatedOnLinux(42, _ => stat));
    }

    [Fact]
    public void LinuxUnreadableProc_FailsClosed()
    {
        Assert.Throws<UnauthorizedAccessException>(() => ProcessLiveness.HasTerminatedOnLinux(42,
            path => path == "/proc/self/stat" ? "mounted" : throw new UnauthorizedAccessException()));
        Assert.Throws<IOException>(() => ProcessLiveness.HasTerminatedOnLinux(42,
            path => path == "/proc/self/stat" ? "mounted" : throw new IOException()));
        Assert.Throws<DirectoryNotFoundException>(() => ProcessLiveness.HasTerminatedOnLinux(42,
            _ => throw new DirectoryNotFoundException()));
    }

    [Fact]
    public void LinuxMissingPid_IsTerminated()
    {
        Assert.True(ProcessLiveness.HasTerminatedOnLinux(42,
            path => path == "/proc/self/stat" ? "mounted" : throw new FileNotFoundException()));
        Assert.True(ProcessLiveness.HasTerminatedOnLinux(42,
            path => path == "/proc/self/stat" ? "mounted" : throw new DirectoryNotFoundException()));
    }
}
