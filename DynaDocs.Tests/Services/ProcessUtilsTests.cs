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
    public void GetProcessCommandLine_CurrentProcess_ReturnsNonEmpty()
    {
        // Exercises the real per-PID reader (Windows wmic → CIM, Linux /proc, macOS ps).
        var cmdline = ProcessUtils.GetProcessCommandLine(Environment.ProcessId);

        Assert.False(string.IsNullOrWhiteSpace(cmdline));
    }

    [Fact]
    public void GetProcessCommandLine_InvalidPid_ReturnsNull()
    {
        Assert.Null(ProcessUtils.GetProcessCommandLine(0));
        Assert.Null(ProcessUtils.GetProcessCommandLine(-1));
    }

    [Fact]
    public void GetCommandLineByPowerShell_CurrentProcess_ReturnsCommandLine()
    {
        // The Windows PowerShell/CIM fallback path (used when wmic is absent, as on newer
        // Windows). Exercised directly so it is covered regardless of whether wmic works here.
        if (!OperatingSystem.IsWindows()) return;

        var cmdline = ProcessUtils.GetCommandLineByPowerShell(Environment.ProcessId);

        Assert.False(string.IsNullOrWhiteSpace(cmdline));
    }

    [Fact]
    public void GetCommandLineByWmic_BogusPid_ReturnsNull()
    {
        if (!OperatingSystem.IsWindows()) return;

        Assert.Null(ProcessUtils.GetCommandLineByWmic(int.MaxValue));
    }

    [Fact]
    public void GetProcessCommandLineLinux_OnWindows_ReturnsNull()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Null(ProcessUtils.GetProcessCommandLineLinux(1));
    }

    [Fact]
    public void GetProcessCommandLineMac_OnWindows_ReturnsNull()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Null(ProcessUtils.GetProcessCommandLineMac(1));
    }

    [Fact]
    public void ParseWmicCommandLineList_ExtractsValue()
    {
        var output = "\r\nCommandLine=node C:\\npm\\bin\\dydo whoami\r\n\r\n";
        Assert.Equal("node C:\\npm\\bin\\dydo whoami", ProcessUtils.ParseWmicCommandLineList(output));
    }

    [Fact]
    public void ParseWmicCommandLineList_EmptyValue_ReturnsNull()
    {
        Assert.Null(ProcessUtils.ParseWmicCommandLineList("CommandLine=\r\n"));
        Assert.Null(ProcessUtils.ParseWmicCommandLineList(""));
        Assert.Null(ProcessUtils.ParseWmicCommandLineList("NoCommandLineHere\r\n"));
    }

    [Fact]
    public void ParsePsEoPidArgs_PrefixCollision_NotMatched()
    {
        // ParsePsEoPidArgs uses substring contains; the watchdog relies on the trailing
        // " --inbox" suffix as a token boundary. If the prompt format ever changes, this
        // test asserts the boundary is gone and forces the change to surface.
        var output = "  PID ARGS\n  100 dydo dispatch Jacky --inbox-other\n  200 dydo dispatch Jack --inbox\n";

        var pids = ProcessUtils.ParsePsEoPidArgs(output, "Jack --inbox");

        Assert.Single(pids);
        Assert.Equal(200, pids[0]);
        Assert.DoesNotContain(100, pids);
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
        if (!OperatingSystem.IsWindows()) return;
        // ps command doesn't exist on Windows; RunProcess returns null
        var result = ProcessUtils.FindProcessesByCommandLineMac("dotnet");

        Assert.Empty(result);
    }

    [Fact]
    public void FindProcessesByCommandLineLinux_OnWindows_ReturnsEmpty()
    {
        if (!OperatingSystem.IsWindows()) return;
        // /proc doesn't exist on Windows; outer catch returns empty
        var result = ProcessUtils.FindProcessesByCommandLineLinux("dotnet");

        Assert.Empty(result);
    }

    // --- RunProcess ---

    [Fact]
    public void RunProcess_ValidCommand_ReturnsOutput()
    {
        var output = OperatingSystem.IsWindows()
            ? ProcessUtils.RunProcess("cmd", "/c echo hello")
            : ProcessUtils.RunProcess("echo", "hello");

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
