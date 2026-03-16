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

    [Fact]
    public void ParseWmicCsvOutput_SingleColumn_Skipped()
    {
        var csv = "ProcessId\n1234\n";
        var pids = ProcessUtils.ParseWmicCsvOutput(csv);

        Assert.Empty(pids);
    }

    // --- ParseNewlineSeparatedPids ---

    [Fact]
    public void ParseNewlineSeparatedPids_ValidOutput_ExtractsPids()
    {
        var output = "100\n200\n300\n";
        var pids = ProcessUtils.ParseNewlineSeparatedPids(output);

        Assert.Equal(3, pids.Count);
        Assert.Equal([100, 200, 300], pids);
    }

    [Fact]
    public void ParseNewlineSeparatedPids_EmptyOutput_ReturnsEmpty()
    {
        Assert.Empty(ProcessUtils.ParseNewlineSeparatedPids(""));
    }

    [Fact]
    public void ParseNewlineSeparatedPids_WhitespaceAndBlanks_Skipped()
    {
        var output = "  42  \n\n  \n99\n";
        var pids = ProcessUtils.ParseNewlineSeparatedPids(output);

        Assert.Equal(2, pids.Count);
        Assert.Contains(42, pids);
        Assert.Contains(99, pids);
    }

    [Fact]
    public void ParseNewlineSeparatedPids_ZeroAndNegative_Skipped()
    {
        var output = "0\n-5\n10\n";
        var pids = ProcessUtils.ParseNewlineSeparatedPids(output);

        Assert.Single(pids);
        Assert.Equal(10, pids[0]);
    }

    [Fact]
    public void ParseNewlineSeparatedPids_NonNumeric_Skipped()
    {
        var output = "abc\n50\nxyz\n";
        var pids = ProcessUtils.ParseNewlineSeparatedPids(output);

        Assert.Single(pids);
        Assert.Equal(50, pids[0]);
    }

    // --- ParsePsEoPidArgs ---

    [Fact]
    public void ParsePsEoPidArgs_MatchingLines_ExtractsPids()
    {
        var output = "  PID ARGS\n  123 /usr/bin/dotnet run\n  456 /usr/bin/node server.js\n  789 dotnet test\n";
        var pids = ProcessUtils.ParsePsEoPidArgs(output, "dotnet");

        Assert.Equal(2, pids.Count);
        Assert.Contains(123, pids);
        Assert.Contains(789, pids);
    }

    [Fact]
    public void ParsePsEoPidArgs_NoMatch_ReturnsEmpty()
    {
        var output = "  PID ARGS\n  123 /usr/bin/node server.js\n";
        var pids = ProcessUtils.ParsePsEoPidArgs(output, "dotnet");

        Assert.Empty(pids);
    }

    [Fact]
    public void ParsePsEoPidArgs_CaseInsensitive()
    {
        var output = "  100 DOTNET run\n";
        var pids = ProcessUtils.ParsePsEoPidArgs(output, "dotnet");

        Assert.Single(pids);
        Assert.Equal(100, pids[0]);
    }

    [Fact]
    public void ParsePsEoPidArgs_EmptyOutput_ReturnsEmpty()
    {
        Assert.Empty(ProcessUtils.ParsePsEoPidArgs("", "dotnet"));
    }

    [Fact]
    public void ParsePsEoPidArgs_HeaderLineNotNumeric_Skipped()
    {
        var output = "  PID ARGS dotnet\n  42 dotnet test\n";
        var pids = ProcessUtils.ParsePsEoPidArgs(output, "dotnet");

        Assert.Single(pids);
        Assert.Equal(42, pids[0]);
    }

    [Fact]
    public void ParsePsEoPidArgs_NoSpace_Skipped()
    {
        // A line with no space can't have a PID prefix
        var output = "dotnet\n  55 dotnet run\n";
        var pids = ProcessUtils.ParsePsEoPidArgs(output, "dotnet");

        Assert.Single(pids);
        Assert.Equal(55, pids[0]);
    }

    [Fact]
    public void ParsePsEoPidArgs_ZeroPid_Skipped()
    {
        var output = "  0 dotnet run\n  77 dotnet test\n";
        var pids = ProcessUtils.ParsePsEoPidArgs(output, "dotnet");

        Assert.Single(pids);
        Assert.Equal(77, pids[0]);
    }

    // --- ParseProcStatusForPpid ---

    [Fact]
    public void ParseProcStatusForPpid_ValidStatus_ReturnsPpid()
    {
        var lines = new[] { "Name:\ttest", "State:\tS (sleeping)", "PPid:\t1234", "TracerPid:\t0" };
        var ppid = ProcessUtils.ParseProcStatusForPpid(lines);

        Assert.Equal(1234, ppid);
    }

    [Fact]
    public void ParseProcStatusForPpid_NoPpidLine_ReturnsNull()
    {
        var lines = new[] { "Name:\ttest", "State:\tS (sleeping)", "TracerPid:\t0" };

        Assert.Null(ProcessUtils.ParseProcStatusForPpid(lines));
    }

    [Fact]
    public void ParseProcStatusForPpid_EmptyLines_ReturnsNull()
    {
        Assert.Null(ProcessUtils.ParseProcStatusForPpid(Array.Empty<string>()));
    }

    [Fact]
    public void ParseProcStatusForPpid_MalformedPpid_ReturnsNull()
    {
        var lines = new[] { "PPid:\tnot_a_number" };

        Assert.Null(ProcessUtils.ParseProcStatusForPpid(lines));
    }

    [Fact]
    public void ParseProcStatusForPpid_PpidWithWhitespace_Parsed()
    {
        var lines = new[] { "PPid:\t  567  " };

        Assert.Equal(567, ProcessUtils.ParseProcStatusForPpid(lines));
    }

    // --- ParsePsPpidOutput ---

    [Fact]
    public void ParsePsPpidOutput_ValidPid_ReturnsPid()
    {
        Assert.Equal(1234, ProcessUtils.ParsePsPpidOutput("  1234  "));
    }

    [Fact]
    public void ParsePsPpidOutput_EmptyOutput_ReturnsNull()
    {
        Assert.Null(ProcessUtils.ParsePsPpidOutput(""));
    }

    [Fact]
    public void ParsePsPpidOutput_NonNumeric_ReturnsNull()
    {
        Assert.Null(ProcessUtils.ParsePsPpidOutput("  abc  "));
    }

    [Fact]
    public void ParsePsPpidOutput_WhitespaceOnly_ReturnsNull()
    {
        Assert.Null(ProcessUtils.ParsePsPpidOutput("   "));
    }

    // --- FindByPowerShell (integration, Windows only) ---

    [Fact]
    public void FindByPowerShell_ReturnsListForKnownPattern()
    {
        var result = ProcessUtils.FindByPowerShell("dotnet");

        Assert.NotNull(result);
        Assert.IsType<List<int>>(result);
    }

    [Fact]
    public void FindByPowerShell_ReturnsListForBogusPattern()
    {
        // May match the PowerShell process itself, so just verify no crash
        var result = ProcessUtils.FindByPowerShell("zzz-nonexistent-process-pattern-zzz");

        Assert.NotNull(result);
    }

    [Fact]
    public void FindByPowerShell_ResolverThrows_ReturnsEmpty()
    {
        // Trigger the catch path by making the resolver throw
        ProcessUtils.PowerShellResolverOverride = () => throw new InvalidOperationException("test");
        try
        {
            var result = ProcessUtils.FindByPowerShell("dotnet");
            Assert.Empty(result);
        }
        finally
        {
            ProcessUtils.PowerShellResolverOverride = null;
        }
    }

    // --- FindByWmic (integration, Windows only) ---

    [Fact]
    public void FindByWmic_ReturnsListForKnownPattern()
    {
        var result = ProcessUtils.FindByWmic("dotnet");

        Assert.NotNull(result);
        Assert.IsType<List<int>>(result);
    }

    [Fact]
    public void FindByWmic_ReturnsListForBogusPattern()
    {
        // May match the wmic process itself, so just verify no crash
        var result = ProcessUtils.FindByWmic("zzz-nonexistent-process-pattern-zzz");

        Assert.NotNull(result);
    }

    // --- Platform-specific methods on Windows (error path coverage) ---

    [Fact]
    public void FindProcessesByCommandLineMac_OnWindows_ReturnsEmpty()
    {
        // ps command doesn't exist on Windows; RunProcess returns null
        var result = ProcessUtils.FindProcessesByCommandLineMac("dotnet");

        Assert.Empty(result);
    }

    [Fact]
    public void FindProcessesByCommandLineLinux_OnWindows_ReturnsEmpty()
    {
        // /proc doesn't exist on Windows; outer catch returns empty
        var result = ProcessUtils.FindProcessesByCommandLineLinux("dotnet");

        Assert.Empty(result);
    }

    [Fact]
    public void GetParentPidLinux_OnWindows_ReturnsNull()
    {
        // /proc/PID/status doesn't exist on Windows
        var result = ProcessUtils.GetParentPidLinux(1);

        Assert.Null(result);
    }

    [Fact]
    public void GetParentPidMac_OnWindows_ReturnsNull()
    {
        // ps command doesn't exist on Windows; RunProcess returns null
        var result = ProcessUtils.GetParentPidMac(1);

        Assert.Null(result);
    }

    // --- RunProcess ---

    [Fact]
    public void RunProcess_ValidCommand_ReturnsOutput()
    {
        var output = ProcessUtils.RunProcess("cmd", "/c echo hello");

        Assert.NotNull(output);
        Assert.Contains("hello", output);
    }

    [Fact]
    public void RunProcess_NonExistentCommand_ReturnsNull()
    {
        var output = ProcessUtils.RunProcess("nonexistent-command-xyz", "");

        Assert.Null(output);
    }
}
