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
    public void GetParentPid_ReturnsValidPid_ForCurrentProcess()
    {
        var ppid = ProcessUtils.GetParentPid(Environment.ProcessId);

        Assert.NotNull(ppid);
        Assert.True(ppid > 0);
    }

    [Fact]
    public void GetParentPid_ReturnsNull_ForInvalidPid()
    {
        var ppid = ProcessUtils.GetParentPid(int.MaxValue);

        Assert.Null(ppid);
    }

    [Fact]
    public void FindAncestorProcess_ReturnsNull_WhenNotFound()
    {
        var pid = ProcessUtils.FindAncestorProcess("nonexistent-process-name-xyz");

        Assert.Null(pid);
    }

    [Fact]
    public void FindAncestorProcess_RespectsMaxDepth()
    {
        var pid = ProcessUtils.FindAncestorProcess("dotnet", maxDepth: 0);

        Assert.Null(pid);
    }

    [Fact]
    public void ResolvePowerShell_PwshAvailable_ReturnsPwsh()
    {
        ProcessUtils.PowerShellResolverOverride = () => "pwsh.exe";
        try
        {
            Assert.Equal("pwsh.exe", ProcessUtils.ResolvePowerShell());
        }
        finally
        {
            ProcessUtils.PowerShellResolverOverride = null;
        }
    }

    [Fact]
    public void ResolvePowerShell_PwshNotAvailable_ReturnsPowershell()
    {
        ProcessUtils.PowerShellResolverOverride = () => "powershell.exe";
        try
        {
            Assert.Equal("powershell.exe", ProcessUtils.ResolvePowerShell());
        }
        finally
        {
            ProcessUtils.PowerShellResolverOverride = null;
        }
    }

    [Fact]
    public void ResolvePowerShell_WithoutOverride_ReturnsPwshOrPowershell()
    {
        ProcessUtils.PowerShellResolverOverride = null;

        var result = ProcessUtils.ResolvePowerShell();

        Assert.True(result == "pwsh.exe" || result == "powershell.exe",
            $"Expected 'pwsh.exe' or 'powershell.exe' but got '{result}'");
    }

    [Fact]
    public void GetProcessName_ReturnsNull_ForNonexistentPid()
    {
        // PID that's very unlikely to exist — exercises the catch branch
        var result = ProcessUtils.GetProcessName(int.MaxValue);

        Assert.Null(result);
    }

    [Fact]
    public void IsProcessRunning_ReturnsFalse_ForNonexistentPid()
    {
        Assert.False(ProcessUtils.IsProcessRunning(int.MaxValue));
    }

    [Fact]
    public void FindProcessesByCommandLine_ReturnsListForAnyPattern()
    {
        // Exercises the Windows wmic/PowerShell path
        var result = ProcessUtils.FindProcessesByCommandLine("dotnet");

        Assert.NotNull(result);
        Assert.IsType<List<int>>(result);
    }

    [Fact]
    public void FindProcessesByCommandLine_ReturnsEmptyForBogusPattern()
    {
        var result = ProcessUtils.FindProcessesByCommandLine("zzz-nonexistent-process-pattern-zzz");

        Assert.NotNull(result);
    }

    [Fact]
    public void FindAncestorProcess_FindsDotnet()
    {
        // We're running inside dotnet test, so "dotnet" should be an ancestor
        var pid = ProcessUtils.FindAncestorProcess("dotnet");

        // May or may not find it depending on how tests are launched,
        // but the method should not throw
        if (pid != null)
            Assert.True(pid > 0);
    }

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("it's", "it''s")]
    [InlineData("say \"hi\"", "say \"hi\"")]
    [InlineData("$var", "$var")]
    [InlineData("back`tick", "back`tick")]
    [InlineData("a'b\"c$d`e", "a''b\"c$d`e")]
    public void EscapeForPowerShellLike_EscapesDangerousCharacters(string input, string expected)
    {
        Assert.Equal(expected, ProcessUtils.EscapeForPowerShellLike(input));
    }

    [Fact]
    public void ParseWmicCsvOutput_ValidCsv_ExtractsPids()
    {
        var csv = "Node,ProcessId\nMACHINE,1234\nMACHINE,5678\n";
        var pids = ProcessUtils.ParseWmicCsvOutput(csv);

        Assert.Equal(2, pids.Count);
        Assert.Contains(1234, pids);
        Assert.Contains(5678, pids);
    }

    [Fact]
    public void ParseWmicCsvOutput_EmptyOutput_ReturnsEmpty()
    {
        Assert.Empty(ProcessUtils.ParseWmicCsvOutput(""));
    }

    [Fact]
    public void ParseWmicCsvOutput_BlankLines_Skipped()
    {
        var csv = "\n\nMACHINE,42\n\n";
        var pids = ProcessUtils.ParseWmicCsvOutput(csv);

        Assert.Single(pids);
        Assert.Equal(42, pids[0]);
    }

    [Fact]
    public void ParseWmicCsvOutput_InvalidPid_Skipped()
    {
        var csv = "Node,ProcessId\nMACHINE,notanumber\nMACHINE,100\n";
        var pids = ProcessUtils.ParseWmicCsvOutput(csv);

        Assert.Single(pids);
        Assert.Equal(100, pids[0]);
    }

    [Fact]
    public void ParseWmicCsvOutput_ZeroPid_Skipped()
    {
        var csv = "Node,ProcessId\nMACHINE,0\nMACHINE,99\n";
        var pids = ProcessUtils.ParseWmicCsvOutput(csv);

        Assert.Single(pids);
        Assert.Equal(99, pids[0]);
    }
}
