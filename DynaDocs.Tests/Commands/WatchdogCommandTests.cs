namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Services;

[Collection("Integration")]
public class WatchdogCommandTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _originalDir;

    public WatchdogCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-watchdogcmd-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _originalDir = Environment.CurrentDirectory;

        // Set up minimal dydo structure so PathUtils.FindDydoRoot works
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"),
            """{"version":1,"structure":{"root":"dydo","tasks":"project/tasks","issues":"project/issues"},"paths":{"source":[],"tests":[],"pathSets":null},"agents":{"pool":[],"assignments":{}},"integrations":{"claude":false},"dispatch":{"launchInTab":false,"autoClose":false},"tasks":{"autoCompactInterval":20},"frameworkHashes":{}}""");
        var localDir = Path.Combine(_testDir, "dydo", "_system", ".local");
        Directory.CreateDirectory(localDir);

        // Prevent real process spawning
        WatchdogService.StartProcessOverride = _ => null;
        Environment.CurrentDirectory = _testDir;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDir;
        WatchdogService.StartProcessOverride = null;
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch (IOException) { }
    }

    [Fact]
    public void Create_ReturnsCommandWithSubcommands()
    {
        var command = WatchdogCommand.Create();

        Assert.Equal("watchdog", command.Name);
        Assert.Equal(3, command.Subcommands.Count);
        Assert.Contains(command.Subcommands, c => c.Name == "start");
        Assert.Contains(command.Subcommands, c => c.Name == "stop");
        Assert.Contains(command.Subcommands, c => c.Name == "run");
    }

    [Fact]
    public void Create_RunSubcommandIsHidden()
    {
        var command = WatchdogCommand.Create();
        var runCmd = command.Subcommands.First(c => c.Name == "run");

        Assert.True(runCmd.Hidden);
    }

    [Fact]
    public void Start_WhenNotRunning_PrintsStarted()
    {
        // Override to return a quick-exiting process so EnsureRunning returns true
        WatchdogService.StartProcessOverride = _ =>
        {
            var psi = OperatingSystem.IsWindows()
                ? new System.Diagnostics.ProcessStartInfo("cmd", "/c echo ok")
                : new System.Diagnostics.ProcessStartInfo("echo", "ok");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            return System.Diagnostics.Process.Start(psi);
        };

        var (output, exitCode) = RunSubcommand("start");

        Assert.Equal(0, exitCode);
        Assert.Contains("Watchdog started.", output);
    }

    [Fact]
    public void Start_WhenAlreadyRunning_PrintsAlreadyRunning()
    {
        // Write a PID file with a running process so EnsureRunning returns false
        using var dummy = StartDummyProcess();
        WritePidFile(dummy.Id);

        var (output, exitCode) = RunSubcommand("start");

        Assert.Equal(0, exitCode);
        Assert.Contains("Watchdog is already running.", output);

        dummy.Kill();
    }

    [Fact]
    public void Stop_WhenNotRunning_PrintsAlreadyStopped()
    {
        // No PID file → Stop returns false
        var (output, exitCode) = RunSubcommand("stop");

        Assert.Equal(0, exitCode);
        Assert.Contains("Watchdog is already stopped.", output);
    }

    [Fact]
    public void Stop_WhenRunning_PrintsStopped()
    {
        using var dummy = StartDummyProcess();
        WritePidFile(dummy.Id);

        var (output, exitCode) = RunSubcommand("stop");

        Assert.Equal(0, exitCode);
        Assert.Contains("Watchdog stopped.", output);
    }

    private static (string output, int exitCode) RunSubcommand(string subcommand)
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var command = WatchdogCommand.Create();
            var exitCode = command.Parse(subcommand).Invoke();
            return (writer.ToString(), exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private void WritePidFile(int pid)
    {
        var dydoRoot = Path.Combine(_testDir, "dydo");
        var pidFile = WatchdogService.GetPidFilePath(dydoRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(pidFile)!);
        File.WriteAllText(pidFile, pid.ToString());
    }

    private static System.Diagnostics.Process StartDummyProcess()
    {
        var psi = new System.Diagnostics.ProcessStartInfo("ping",
            OperatingSystem.IsWindows() ? "-n 600 127.0.0.1" : "-c 600 127.0.0.1")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        };
        return System.Diagnostics.Process.Start(psi)!;
    }
}
