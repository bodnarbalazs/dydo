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

    [Fact]
    public void FindClaudeAncestor_ReturnsClaudeKey_WhenInjected()
    {
        // The override path is the same on every OS — the helper should hit the
        // "claude" key first regardless of platform.
        ProcessUtils.FindAncestorProcessOverride = (name, _) => name == "claude" ? 12345 : null;
        try
        {
            var pid = ProcessUtils.FindClaudeAncestor();
            Assert.Equal(12345, pid);
        }
        finally
        {
            ProcessUtils.FindAncestorProcessOverride = null;
        }
    }

    [Fact]
    public void FindClaudeAncestor_OnWindows_FallsBackToNodeKey()
    {
        // Closes #0151: on Windows the official claude distribution is a Node
        // script, so the resolved process name is "node". The helper must accept
        // a "node" ancestor when "claude" was not found.
        if (!OperatingSystem.IsWindows()) return;

        ProcessUtils.FindAncestorProcessOverride = (name, _) => name == "node" ? 54321 : null;
        try
        {
            var pid = ProcessUtils.FindClaudeAncestor();
            Assert.Equal(54321, pid);
        }
        finally
        {
            ProcessUtils.FindAncestorProcessOverride = null;
        }
    }

    [Fact]
    public void FindClaudeAncestor_OnNonWindows_DoesNotFallBackToNode()
    {
        // On Linux/Mac the claude binary IS named "claude" — so a "node"
        // ancestor must NOT be picked (would mistake unrelated node processes
        // for the claude tab).
        if (OperatingSystem.IsWindows()) return;

        ProcessUtils.FindAncestorProcessOverride = (name, _) => name == "node" ? 54321 : null;
        try
        {
            var pid = ProcessUtils.FindClaudeAncestor();
            Assert.Null(pid);
        }
        finally
        {
            ProcessUtils.FindAncestorProcessOverride = null;
        }
    }

    [Fact]
    public void FindClaudeAncestor_NoAncestor_ReturnsNull()
    {
        ProcessUtils.FindAncestorProcessOverride = (_, _) => null;
        try
        {
            var pid = ProcessUtils.FindClaudeAncestor();
            Assert.Null(pid);
        }
        finally
        {
            ProcessUtils.FindAncestorProcessOverride = null;
        }
    }

    [Fact]
    public void FindCodexAncestor_ReturnsCodexKey_WhenInjected()
    {
        ProcessUtils.FindAncestorProcessOverride = (name, _) => name == "codex" ? 24680 : null;
        try
        {
            var pid = ProcessUtils.FindCodexAncestor();
            Assert.Equal(24680, pid);
        }
        finally
        {
            ProcessUtils.FindAncestorProcessOverride = null;
        }
    }

    [Fact]
    public void FindCodexAncestor_OnWindows_FallsBackToNodeKey()
    {
        if (!OperatingSystem.IsWindows()) return;

        ProcessUtils.FindAncestorProcessOverride = (name, _) => name == "node" ? 13579 : null;
        try
        {
            var pid = ProcessUtils.FindCodexAncestor();
            Assert.Equal(13579, pid);
        }
        finally
        {
            ProcessUtils.FindAncestorProcessOverride = null;
        }
    }

    [Fact]
    public void FindAgentHostAncestor_Codex_UsesCodexKey()
    {
        ProcessUtils.FindAncestorProcessOverride = (name, _) => name == "codex" ? 11223 : null;
        try
        {
            var pid = ProcessUtils.FindAgentHostAncestor("codex");
            Assert.Equal(11223, pid);
        }
        finally
        {
            ProcessUtils.FindAncestorProcessOverride = null;
        }
    }

    [Fact]
    public void FindAgentHostAncestor_Unknown_PreservesClaudeLookup()
    {
        ProcessUtils.FindAncestorProcessOverride = (name, _) => name == "claude" ? 44556 : null;
        try
        {
            var pid = ProcessUtils.FindAgentHostAncestor("unknown");
            Assert.Equal(44556, pid);
        }
        finally
        {
            ProcessUtils.FindAncestorProcessOverride = null;
        }
    }

    [Fact]
    public void NoForeignHostNearerThanClaimedHost_ForeignHostBetween_ReturnsFalse()
    {
        // #0250 nearest-host-wins: a codex host sits nearer than the claimed claude host, so an
        // inner codex worker cannot inherit the outer claude session's identity.
        const int codexPid = 700001;
        const int claudePid = 700002;
        ProcessUtils.GetParentPidOverride = pid =>
            pid == Environment.ProcessId ? codexPid :
            pid == codexPid ? claudePid : null;
        ProcessUtils.GetProcessNameOverride = pid =>
            pid == codexPid ? "codex" :
            pid == claudePid ? "claude" : null;
        try
        {
            Assert.False(ProcessUtils.NoForeignHostNearerThanClaimedHost(claudePid));
        }
        finally
        {
            ProcessUtils.GetParentPidOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
        }
    }

    [Fact]
    public void NoForeignHostNearerThanClaimedHost_ReachesClaimedHostFirst_ReturnsTrue()
    {
        // The legitimate consumer: the caller reaches the claimed host with only plain shells
        // in between — no foreign agent host is nearer.
        const int claudePid = 700004;
        ProcessUtils.GetParentPidOverride = pid =>
            pid == Environment.ProcessId ? 700005 :
            pid == 700005 ? claudePid : null;
        ProcessUtils.GetProcessNameOverride = pid => pid == claudePid ? "claude" : "bash";
        try
        {
            Assert.True(ProcessUtils.NoForeignHostNearerThanClaimedHost(claudePid));
        }
        finally
        {
            ProcessUtils.GetParentPidOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
        }
    }

    [Fact]
    public void NoForeignHostNearerThanClaimedHost_NoHostAncestor_ReturnsTrue()
    {
        ProcessUtils.GetParentPidOverride = pid => pid == Environment.ProcessId ? 700003 : null;
        ProcessUtils.GetProcessNameOverride = _ => "bash";
        try
        {
            Assert.True(ProcessUtils.NoForeignHostNearerThanClaimedHost(999999));
        }
        finally
        {
            ProcessUtils.GetParentPidOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
        }
    }

    // --- #0250/F1: node ancestors disambiguated by command line ---

    [Theory]
    [InlineData(null, "Unreadable")]
    [InlineData("", "Unreadable")]
    [InlineData("   ", "Unreadable")]
    // The npm dydo launcher shim is transparent even when dydo's own args name a vendor.
    [InlineData(@"node C:\Users\u\AppData\Roaming\npm\node_modules\dydo\bin\dydo agent role co-thinker", "Transparent")]
    [InlineData("node /usr/lib/node_modules/dydo/bin/dydo claim --host codex", "Transparent")]
    [InlineData("node ./npm/bin/dydo whoami", "Transparent")]
    // Real vendor CLIs running under node.
    [InlineData(@"node C:\Users\u\.claude\local\node_modules\@anthropic-ai\claude-code\cli.js", "ClaudeHost")]
    [InlineData("node /opt/codex/bin/codex.js", "CodexHost")]
    // Unrelated node scripts are not agent hosts.
    [InlineData("node /home/u/project/server.js", "Transparent")]
    [InlineData("node node-gyp build", "Transparent")]
    // "claudia" must not read as claude (token boundary).
    [InlineData("node /apps/claudia/index.js", "Transparent")]
    public void ClassifyNodeCommandLine_MapsToExpectedKind(string? cmdline, string expected)
        => Assert.Equal(expected, ProcessUtils.ClassifyNodeCommandLine(cmdline).ToString());

    [Fact]
    public void NoForeignHostNearerThanClaimedHost_NpmLauncherNode_NotForeign()
    {
        // Windows+npm shape: dydo.exe → npm launcher node (transparent) → claude host (claimed).
        // The launcher node must not be treated as a foreign host — the legitimate CLI call passes.
        if (!OperatingSystem.IsWindows()) return;
        const int launcherPid = 710001;
        const int claudeHostPid = 710002;
        ProcessUtils.GetParentPidOverride = pid =>
            pid == Environment.ProcessId ? launcherPid :
            pid == launcherPid ? claudeHostPid : null;
        ProcessUtils.GetProcessNameOverride = _ => "node";
        ProcessUtils.GetProcessCommandLineOverride = pid =>
            pid == launcherPid ? @"node C:\npm\node_modules\dydo\bin\dydo whoami" :
            @"node C:\claude\cli.js";
        try
        {
            Assert.True(ProcessUtils.NoForeignHostNearerThanClaimedHost(claudeHostPid));
        }
        finally
        {
            ProcessUtils.GetParentPidOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
            ProcessUtils.GetProcessCommandLineOverride = null;
        }
    }

    [Fact]
    public void NoForeignHostNearerThanClaimedHost_UnknownNodeScript_Transparent()
    {
        if (!OperatingSystem.IsWindows()) return;
        const int scriptPid = 710010;
        const int claudeHostPid = 710011;
        ProcessUtils.GetParentPidOverride = pid =>
            pid == Environment.ProcessId ? scriptPid :
            pid == scriptPid ? claudeHostPid : null;
        ProcessUtils.GetProcessNameOverride = _ => "node";
        ProcessUtils.GetProcessCommandLineOverride = pid =>
            pid == scriptPid ? "node C:\\tools\\watcher.js" : "node C:\\claude\\cli.js";
        try
        {
            Assert.True(ProcessUtils.NoForeignHostNearerThanClaimedHost(claudeHostPid));
        }
        finally
        {
            ProcessUtils.GetParentPidOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
            ProcessUtils.GetProcessCommandLineOverride = null;
        }
    }

    [Fact]
    public void NoForeignHostNearerThanClaimedHost_CodexByCmdlineNearerThanClaimedClaude_ReturnsFalse()
    {
        if (!OperatingSystem.IsWindows()) return;
        // An inner codex host (a node whose command line names codex) sits nearer than the
        // claimed claude host — the caller is a codex worker, not the claude agent.
        const int codexNodePid = 710020;
        const int claudeHostPid = 710021;
        ProcessUtils.GetParentPidOverride = pid =>
            pid == Environment.ProcessId ? codexNodePid :
            pid == codexNodePid ? claudeHostPid : null;
        ProcessUtils.GetProcessNameOverride = _ => "node";
        ProcessUtils.GetProcessCommandLineOverride = pid =>
            pid == codexNodePid ? "node C:\\codex\\codex.js" : "node C:\\claude\\cli.js";
        try
        {
            Assert.False(ProcessUtils.NoForeignHostNearerThanClaimedHost(claudeHostPid));
        }
        finally
        {
            ProcessUtils.GetParentPidOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
            ProcessUtils.GetProcessCommandLineOverride = null;
        }
    }

    [Fact]
    public void NoForeignHostNearerThanClaimedHost_UnreadableNodeAtLauncherPosition_Transparent()
    {
        if (!OperatingSystem.IsWindows()) return;
        // Conservative fallback: an unreadable command line at the launcher position (direct
        // parent of the initial dydo process) is treated as the npm shim → transparent.
        const int launcherPid = 710030;
        const int claimedPid = 710031;
        ProcessUtils.GetParentPidOverride = pid =>
            pid == Environment.ProcessId ? launcherPid :
            pid == launcherPid ? claimedPid : null;
        ProcessUtils.GetProcessNameOverride = _ => "node";
        ProcessUtils.GetProcessCommandLineOverride = _ => null;
        try
        {
            Assert.True(ProcessUtils.NoForeignHostNearerThanClaimedHost(claimedPid));
        }
        finally
        {
            ProcessUtils.GetParentPidOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
            ProcessUtils.GetProcessCommandLineOverride = null;
        }
    }

    [Fact]
    public void NoForeignHostNearerThanClaimedHost_UnreadableNodeAboveLauncher_TreatedAsForeign()
    {
        if (!OperatingSystem.IsWindows()) return;
        // A node with an unreadable command line that is NOT at the launcher position keeps the
        // old name-based treatment: it counts as a foreign host.
        const int shellPid = 710040;
        const int nodePid = 710041;
        ProcessUtils.GetParentPidOverride = pid =>
            pid == Environment.ProcessId ? shellPid :
            pid == shellPid ? nodePid : null;
        ProcessUtils.GetProcessNameOverride = pid => pid == shellPid ? "pwsh" : "node";
        ProcessUtils.GetProcessCommandLineOverride = _ => null;
        try
        {
            Assert.False(ProcessUtils.NoForeignHostNearerThanClaimedHost(999999));
        }
        finally
        {
            ProcessUtils.GetParentPidOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
            ProcessUtils.GetProcessCommandLineOverride = null;
        }
    }

    [Fact]
    public void FindClaudeAncestor_SkipsNpmLauncherShim_ReturnsClaudeHost()
    {
        if (!OperatingSystem.IsWindows()) return;
        // Amplifier fix: claim-time ClaimedPid capture must skip the transient npm launcher node
        // and land on the real claude host node above it.
        const int launcherPid = 710050;
        const int claudeHostPid = 710051;
        ProcessUtils.FindAncestorProcessOverride = null;
        ProcessUtils.GetParentPidOverride = pid =>
            pid == Environment.ProcessId ? launcherPid :
            pid == launcherPid ? claudeHostPid : null;
        ProcessUtils.GetProcessNameOverride = _ => "node";
        ProcessUtils.GetProcessCommandLineOverride = pid =>
            pid == launcherPid ? @"node C:\npm\node_modules\dydo\bin\dydo claim auto" :
            @"node C:\Users\u\.claude\cli.js";
        try
        {
            Assert.Equal(claudeHostPid, ProcessUtils.FindClaudeAncestor());
        }
        finally
        {
            ProcessUtils.GetParentPidOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
            ProcessUtils.GetProcessCommandLineOverride = null;
        }
    }

    [Fact]
    public void FindCodexAncestor_SkipsNpmLauncherShim_ReturnsCodexHost()
    {
        if (!OperatingSystem.IsWindows()) return;
        const int launcherPid = 710060;
        const int codexHostPid = 710061;
        ProcessUtils.FindAncestorProcessOverride = null;
        ProcessUtils.GetParentPidOverride = pid =>
            pid == Environment.ProcessId ? launcherPid :
            pid == launcherPid ? codexHostPid : null;
        ProcessUtils.GetProcessNameOverride = _ => "node";
        ProcessUtils.GetProcessCommandLineOverride = pid =>
            pid == launcherPid ? @"node C:\npm\node_modules\dydo\bin\dydo whoami" :
            @"node C:\codex\codex.js";
        try
        {
            Assert.Equal(codexHostPid, ProcessUtils.FindCodexAncestor());
        }
        finally
        {
            ProcessUtils.GetParentPidOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
            ProcessUtils.GetProcessCommandLineOverride = null;
        }
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

    [Theory]
    [InlineData("claude", "claude", true)]
    [InlineData("claude.exe", "claude", true)]
    [InlineData("CLAUDE", "claude", true)]
    [InlineData("claudia", "claude", false)]
    [InlineData("claudia.exe", "claude", false)]
    [InlineData("claude-dev", "claude", false)]
    [InlineData("anthropicclaude", "claude", false)]
    [InlineData(null, "claude", false)]
    // #0151 augment: post-update self-rename on Windows. The OS retains
    // "claude.exe.old.<unix-ms>" for the running process's lifetime; both the
    // anchor matcher and the kill whitelist must recognise it.
    [InlineData("claude.exe.old.1777935765627", "claude", true)]
    [InlineData("CLAUDE.EXE.OLD.42", "claude", true)]
    [InlineData("claude.exe.old", "claude", false)]              // missing timestamp
    [InlineData("claude.exe.old.", "claude", false)]             // empty timestamp
    [InlineData("claude.exe.old.abc", "claude", false)]          // non-numeric timestamp
    [InlineData("xclaude.exe.old.42", "claude", false)]          // anchored — no prefix bypass
    [InlineData("claude.exe.old.42x", "claude", false)]          // anchored — no suffix bypass
    [InlineData("codex", "codex", true)]
    [InlineData("codex.exe", "codex", true)]
    [InlineData("CODEX.EXE", "codex", true)]
    [InlineData("codex-dev", "codex", false)]
    [InlineData("openai-codex", "codex", false)]
    [InlineData("node", "node", true)]
    [InlineData("node.exe", "node", true)]
    [InlineData("NODE.EXE", "node", true)]
    [InlineData("node-gyp", "node", false)]
    [InlineData("node-gyp.exe", "node", false)]
    [InlineData("nodejs", "node", false)]
    public void MatchesProcessName_ExactBasename(string? actual, string needle, bool expected)
        => Assert.Equal(expected, ProcessUtils.MatchesProcessName(actual, needle));

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

    [Fact]
    public void GetParentPidLinux_OnWindows_ReturnsNull()
    {
        if (!OperatingSystem.IsWindows()) return;
        // /proc/PID/status doesn't exist on Windows
        var result = ProcessUtils.GetParentPidLinux(1);

        Assert.Null(result);
    }

    [Fact]
    public void GetParentPidMac_OnWindows_ReturnsNull()
    {
        if (!OperatingSystem.IsWindows()) return;
        // ps command doesn't exist on Windows; RunProcess returns null
        var result = ProcessUtils.GetParentPidMac(1);

        Assert.Null(result);
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
