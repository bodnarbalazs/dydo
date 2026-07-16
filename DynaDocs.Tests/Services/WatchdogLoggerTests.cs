namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

/// <summary>
/// The retained watchdog log surface after the 2.1.0 strip (DR-041): the one surviving event
/// (model_cap_restored, emitted by ModelCapService.RestoreExpired) plus the shared append and
/// size-based rotation.
/// </summary>
[Collection("Integration")]
public class WatchdogLoggerTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _logPath;

    public WatchdogLoggerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-wdlog-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _logPath = Path.Combine(_testDir, "watchdog.log");
        WatchdogLogger.LogPathOverride = _logPath;
    }

    public void Dispose()
    {
        WatchdogLogger.LogPathOverride = null;
        WatchdogLogger.MaxBytesOverride = null;
        WatchdogLogger.MaxRotationsOverride = null;
        if (Directory.Exists(_testDir))
            try { Directory.Delete(_testDir, true); } catch (IOException) { }
    }

    [Fact]
    public void LogModelCapRestore_AppendsJsonLine()
    {
        WatchdogLogger.LogModelCapRestore(_testDir, "claude-fable-5", "claude-sonnet-5");

        var content = File.ReadAllText(_logPath);
        Assert.Contains("\"event\":\"model_cap_restored\"", content);
        Assert.Contains("claude-fable-5", content);
    }

    [Fact]
    public void Write_UnderMaxBytes_DoesNotRotate()
    {
        WatchdogLogger.LogModelCapRestore(_testDir, "m1", "fallback");
        WatchdogLogger.LogModelCapRestore(_testDir, "m2", "fallback");

        Assert.False(File.Exists(_logPath + ".1"), "log under the size cap must not rotate");
        Assert.Equal(2, File.ReadAllLines(_logPath).Length);
    }

    [Fact]
    public void Write_OverMaxBytes_RotatesThroughNumberedBackups()
    {
        // A 1-byte cap forces every non-empty log to rotate. Four writes walk the full rotation
        // ladder: create → move to .1 → move .1 to .2 → delete oldest (.2) then re-shift.
        WatchdogLogger.MaxBytesOverride = 1;
        WatchdogLogger.MaxRotationsOverride = 2;

        for (var i = 0; i < 4; i++)
            WatchdogLogger.LogModelCapRestore(_testDir, $"m{i}", "fallback");

        Assert.True(File.Exists(_logPath), "current log must exist after rotation");
        Assert.True(File.Exists(_logPath + ".1"), "first rotation backup must exist");
        Assert.True(File.Exists(_logPath + ".2"), "second rotation backup must exist");
        Assert.False(File.Exists(_logPath + ".3"), "rotation must be capped at MaxRotations");
    }
}
