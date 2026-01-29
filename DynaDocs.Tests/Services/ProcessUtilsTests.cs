namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class ProcessUtilsTests
{
    [Fact]
    public void GetParentProcessId_ReturnsValidPid_ForCurrentProcess()
    {
        var parentPid = ProcessUtils.GetParentProcessId();

        Assert.True(parentPid > 0, "Should return valid parent PID");
        Assert.NotEqual(Environment.ProcessId, parentPid);
    }

    [Fact]
    public void GetParentProcessId_ReturnsNegativeOne_ForInvalidPid()
    {
        var result = ProcessUtils.GetParentProcessId(-1);

        Assert.Equal(-1, result);
    }

    [Fact]
    public void GetParentProcessId_ReturnsNegativeOne_ForNonExistentProcess()
    {
        var result = ProcessUtils.GetParentProcessId(999999999);

        Assert.Equal(-1, result);
    }

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

    [Fact]
    public void GetProcessAncestors_ReturnsParentPid()
    {
        var (terminalPid, claudePid) = ProcessUtils.GetProcessAncestors();

        // The direct parent (claudePid) should be valid when running tests
        Assert.True(claudePid > 0, "Should get valid parent PID");
    }
}
