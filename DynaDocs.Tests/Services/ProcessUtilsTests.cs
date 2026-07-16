namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

[Collection("ProcessUtils")]
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
    public void IsProcessRunning_ReturnsFalse_ForNonexistentPid()
    {
        Assert.False(ProcessUtils.IsProcessRunning(int.MaxValue));
    }
}
