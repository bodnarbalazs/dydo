namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class ProcessUtilsTests
{
    [Fact]
    public void IsProcessRunning_ReturnsTrue_ForCurrentProcess()
    {
        var result = ProcessUtils.IsProcessRunning(Environment.ProcessId);

        Assert.True(result);
    }

    [Fact]
    public void IsProcessRunning_ReturnsFalse_ForInvalidPid()
    {
        Assert.False(ProcessUtils.IsProcessRunning(-1));
        Assert.False(ProcessUtils.IsProcessRunning(0));
    }

    [Fact]
    public void GetProcessName_ReturnsName_ForCurrentProcess()
    {
        var name = ProcessUtils.GetProcessName(Environment.ProcessId);

        Assert.NotNull(name);
        Assert.NotEmpty(name);
    }

    [Fact]
    public void GetProcessName_ReturnsNull_ForInvalidPid()
    {
        var result = ProcessUtils.GetProcessName(-1);

        Assert.Null(result);
    }
}
