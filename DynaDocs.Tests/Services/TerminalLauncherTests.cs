namespace DynaDocs.Tests.Services;

using System.ComponentModel;
using System.Diagnostics;
using DynaDocs.Services;
using Xunit;

public class TerminalLauncherTests
{
    #region Argument Generation Tests

    [Theory]
    [InlineData("Adele")]
    [InlineData("Brian")]
    [InlineData("Charlie")]
    public void GetWindowsArguments_ContainsAgentAndInboxFlag(string agentName)
    {
        var args = TerminalLauncher.GetWindowsArguments(agentName);

        // Should contain the agent name and --inbox flag
        // Expected: -NoExit -Command "claude 'AgentName --inbox'"
        Assert.Contains(agentName, args);
        Assert.Contains("--inbox", args);
        Assert.Contains("-NoExit", args);
    }

    [Theory]
    [InlineData("gnome-terminal", "Adele")]
    [InlineData("konsole", "Brian")]
    [InlineData("xfce4-terminal", "Charlie")]
    [InlineData("alacritty", "Dexter")]
    [InlineData("kitty", "Emma")]
    [InlineData("wezterm", "Frank")]
    [InlineData("tilix", "Grace")]
    [InlineData("foot", "Henry")]
    [InlineData("xterm", "Iris")]
    public void GetLinuxArguments_ContainsAgentAndInboxFlag(string terminal, string agentName)
    {
        var args = TerminalLauncher.GetLinuxArguments(terminal, agentName);

        // Should contain the agent name and --inbox flag
        // Expected: claude 'AgentName --inbox'
        Assert.Contains(agentName, args);
        Assert.Contains("--inbox", args);
    }

    [Fact]
    public void GetLinuxArguments_UnknownTerminal_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            TerminalLauncher.GetLinuxArguments("unknown-terminal", "Adele"));
    }

    [Theory]
    [InlineData("Adele")]
    [InlineData("Brian")]
    [InlineData("Charlie")]
    public void GetMacArguments_ContainsAgentAndInboxFlag(string agentName)
    {
        var args = TerminalLauncher.GetMacArguments(agentName);

        // Should contain the agent name and --inbox flag
        Assert.Contains(agentName, args);
        Assert.Contains("--inbox", args);
    }

    [Theory]
    [InlineData("Agent With Space")]
    public void GetWindowsArguments_HandlesSpecialCharacters(string agentName)
    {
        // Should not throw - quoting handles spaces
        var args = TerminalLauncher.GetWindowsArguments(agentName);
        Assert.Contains(agentName, args);
    }

    [Fact]
    public void GetWindowsArguments_EscapesSingleQuotesInAgentName()
    {
        var args = TerminalLauncher.GetWindowsArguments("Agent'Quote");
        // Single quotes inside the prompt must be doubled for PowerShell
        Assert.Contains("Agent''Quote", args);
    }

    [Fact]
    public void GetWindowsArguments_UsesSingleQuotesAroundPrompt()
    {
        var args = TerminalLauncher.GetWindowsArguments("Adele");
        // The prompt must be wrapped in single quotes so PowerShell treats it as one argument
        Assert.Contains("claude 'Adele --inbox'", args);
    }

    [Fact]
    public void GetClaudePrompt_FormatsCorrectly()
    {
        var prompt = TerminalLauncher.GetClaudePrompt("Adele");

        Assert.Equal("Adele --inbox", prompt);
    }

    [Fact]
    public void GetClaudeCommand_FormatsCorrectly()
    {
        var command = TerminalLauncher.GetClaudeCommand("Adele");

        Assert.Equal("claude \"Adele --inbox\"", command);
    }

    [Fact]
    public void GetWindowsArguments_ExactFormat()
    {
        var args = TerminalLauncher.GetWindowsArguments("Adele");
        Assert.Equal("-NoExit -Command \"Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; claude 'Adele --inbox'\"", args);
    }

    [Theory]
    [InlineData("gnome-terminal", "Adele")]
    [InlineData("konsole", "Brian")]
    [InlineData("xfce4-terminal", "Charlie")]
    [InlineData("alacritty", "Dexter")]
    [InlineData("kitty", "Emma")]
    [InlineData("wezterm", "Frank")]
    [InlineData("tilix", "Grace")]
    [InlineData("foot", "Henry")]
    [InlineData("xterm", "Iris")]
    public void GetLinuxArguments_PromptIsInsideSingleQuotes(string terminal, string agentName)
    {
        var args = TerminalLauncher.GetLinuxArguments(terminal, agentName);
        Assert.Contains($"'{agentName} --inbox'", args);
    }

    [Fact]
    public void GetMacArguments_PromptIsQuotedAsUnit()
    {
        var args = TerminalLauncher.GetMacArguments("Adele");
        // In the AppleScript string, the prompt is wrapped in escaped double quotes
        Assert.Contains("Adele --inbox", args);
        // Verify it's inside quotes (escaped for AppleScript)
        Assert.Contains("\\\"Adele --inbox\\\"", args);
    }

    [Fact]
    public void GetWindowsArguments_InboxIsNotStandaloneToken()
    {
        var args = TerminalLauncher.GetWindowsArguments("Adele");
        var tokens = args.Split(' ');
        Assert.DoesNotContain("--inbox", tokens);
        Assert.DoesNotContain("\"--inbox\"", tokens);
    }

    [Theory]
    [InlineData("gnome-terminal")]
    [InlineData("konsole")]
    [InlineData("alacritty")]
    [InlineData("kitty")]
    [InlineData("foot")]
    [InlineData("xterm")]
    public void GetLinuxArguments_ContainsCdPrefix_WhenWorkingDirectoryProvided(string terminal)
    {
        var args = TerminalLauncher.GetLinuxArguments(terminal, "Adele", "/home/user/project");
        Assert.Contains("cd '/home/user/project' && unset CLAUDECODE; claude", args);
    }

    [Theory]
    [InlineData("gnome-terminal")]
    [InlineData("konsole")]
    public void GetLinuxArguments_NoCdPrefix_WhenNoWorkingDirectory(string terminal)
    {
        var args = TerminalLauncher.GetLinuxArguments(terminal, "Adele");
        Assert.DoesNotContain("cd ", args);
    }

    [Fact]
    public void GetMacArguments_ContainsCdPrefix_WhenWorkingDirectoryProvided()
    {
        var args = TerminalLauncher.GetMacArguments("Adele", "/Users/dev/project");
        Assert.Contains("cd '/Users/dev/project' && unset CLAUDECODE; claude", args);
    }

    [Fact]
    public void GetMacArguments_NoCdPrefix_WhenNoWorkingDirectory()
    {
        var args = TerminalLauncher.GetMacArguments("Adele");
        Assert.DoesNotContain("cd ", args);
    }

    [Fact]
    public void GetLinuxArguments_Throws_WhenPathContainsSingleQuote()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            TerminalLauncher.GetLinuxArguments("gnome-terminal", "Adele", "/home/user/it's-a-project"));

        Assert.Contains("single quote", ex.Message);
    }

    [Fact]
    public void GetMacArguments_Throws_WhenPathContainsSingleQuote()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            TerminalLauncher.GetMacArguments("Adele", "/Users/dev/it's-a-project"));

        Assert.Contains("single quote", ex.Message);
    }

    [Fact]
    public void TryLaunchTerminals_RethrowsArgumentException_ForInvalidPath()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        Assert.Throws<ArgumentException>(() =>
            launcher.TryLaunchTerminals(TerminalLauncher.LinuxTerminals, "Adele", "/home/it's-broken"));
    }

    [Fact]
    public void GetWindowsArguments_ClearsClaudeCodeEnvVar()
    {
        var args = TerminalLauncher.GetWindowsArguments("Adele");
        Assert.Contains("Remove-Item Env:CLAUDECODE", args);
        Assert.True(args.IndexOf("Remove-Item Env:CLAUDECODE") < args.IndexOf("claude"));
    }

    [Theory]
    [InlineData("gnome-terminal")]
    [InlineData("konsole")]
    [InlineData("xfce4-terminal")]
    [InlineData("alacritty")]
    [InlineData("kitty")]
    [InlineData("wezterm")]
    [InlineData("tilix")]
    [InlineData("foot")]
    [InlineData("xterm")]
    public void GetLinuxArguments_ClearsClaudeCodeEnvVar(string terminal)
    {
        var args = TerminalLauncher.GetLinuxArguments(terminal, "Adele");
        Assert.Contains("unset CLAUDECODE", args);
        Assert.True(args.IndexOf("unset CLAUDECODE") < args.IndexOf("claude"));
    }

    [Fact]
    public void GetMacArguments_ClearsClaudeCodeEnvVar()
    {
        var args = TerminalLauncher.GetMacArguments("Adele");
        Assert.Contains("unset CLAUDECODE", args);
        Assert.True(args.IndexOf("unset CLAUDECODE") < args.IndexOf("claude"));
    }

    #endregion

    #region Tab Mode Argument Tests

    [Theory]
    [InlineData("gnome-terminal", "--tab")]
    [InlineData("konsole", "--new-tab")]
    [InlineData("xfce4-terminal", "--tab")]
    public void GetLinuxArguments_TabMode_ContainsTabFlag(string terminal, string expectedFlag)
    {
        var args = TerminalLauncher.GetLinuxArguments(terminal, "Adele", useTab: true);
        Assert.Contains(expectedFlag, args);
        Assert.Contains("Adele", args);
        Assert.Contains("--inbox", args);
    }

    [Theory]
    [InlineData("alacritty")]
    [InlineData("kitty")]
    [InlineData("wezterm")]
    [InlineData("tilix")]
    [InlineData("foot")]
    [InlineData("xterm")]
    public void GetLinuxArguments_TabMode_FallsBackToWindow_WhenNoTabSupport(string terminal)
    {
        var tabArgs = TerminalLauncher.GetLinuxArguments(terminal, "Adele", useTab: true);
        var windowArgs = TerminalLauncher.GetLinuxArguments(terminal, "Adele", useTab: false);

        // Without tab support, tab mode should produce the same args as window mode
        Assert.Equal(windowArgs, tabArgs);
    }

    #endregion

    #region Behavior Tests with IProcessStarter

    [Fact]
    public void LaunchWindows_TriesWindowsTerminalFirst()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele");

        // Should try wt (Windows Terminal) first
        Assert.Single(recorder.Started);
        Assert.Equal("wt", recorder.Started[0].FileName);
    }

    [Fact]
    public void LaunchWindows_FallsBackToPowerShell_WhenWtFails()
    {
        var recorder = new RecordingProcessStarter();
        recorder.FailOnFileName("wt"); // Simulate wt not found
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele");

        // Should have tried wt first, then fallen back to powershell
        Assert.Equal(2, recorder.Started.Count);
        Assert.Equal("wt", recorder.Started[0].FileName);
        Assert.Equal("powershell", recorder.Started[1].FileName);
    }

    [Fact]
    public void LaunchWindows_PowerShellArgs_ContainNoExit()
    {
        var recorder = new RecordingProcessStarter();
        recorder.FailOnFileName("wt"); // Force fallback to PowerShell
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele");

        var psCall = recorder.Started.First(p => p.FileName == "powershell");
        Assert.Contains("-NoExit", psCall.Arguments);
    }

    [Fact]
    public void LaunchWindows_PowerShellArgs_ContainAgentName()
    {
        var recorder = new RecordingProcessStarter();
        recorder.FailOnFileName("wt");
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele");

        var psCall = recorder.Started.First(p => p.FileName == "powershell");
        Assert.Contains("Adele", psCall.Arguments);
        Assert.Contains("--inbox", psCall.Arguments);
    }

    [Fact]
    public void LaunchWindows_WtArgs_EscapesSemicolonsForWt()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele");

        var wtCall = recorder.Started.First(p => p.FileName == "wt");
        // wt uses ';' as subcommand separator, so literal semicolons must be escaped as '\;'
        Assert.Contains("\\;", wtCall.Arguments);
    }

    [Fact]
    public void LaunchWindows_SetsWorkingDirectory_WhenProvided()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);
        var projectRoot = "/home/user/my-project";

        launcher.LaunchWindows("Adele", projectRoot);

        Assert.Equal(projectRoot, recorder.Started[0].WorkingDirectory);
    }

    [Fact]
    public void LaunchWindows_PowerShellFallback_SetsWorkingDirectory()
    {
        var recorder = new RecordingProcessStarter();
        recorder.FailOnFileName("wt");
        var launcher = new TerminalLauncher(recorder);
        var projectRoot = "/home/user/my-project";

        launcher.LaunchWindows("Adele", projectRoot);

        var psCall = recorder.Started.First(p => p.FileName == "powershell");
        Assert.Equal(projectRoot, psCall.WorkingDirectory);
    }

    [Fact]
    public void LaunchWindows_WtArgs_ContainStartingDirectory()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);
        var projectRoot = "/home/user/my-project";

        launcher.LaunchWindows("Adele", projectRoot);

        Assert.Contains("--startingDirectory", recorder.Started[0].Arguments);
        Assert.Contains(projectRoot, recorder.Started[0].Arguments);
    }

    [Fact]
    public void LaunchWindows_WtArgs_OmitStartingDirectory_WhenNoWorkingDirectory()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele");

        Assert.DoesNotContain("--startingDirectory", recorder.Started[0].Arguments);
    }

    [Fact]
    public void LaunchWindows_WtArgs_HandlesPathsWithSpaces()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele", @"C:\Users\Some User\My Projects\app");

        // Path is wrapped in double quotes inside wt args
        Assert.Contains(@"--startingDirectory ""C:\Users\Some User\My Projects\app""", recorder.Started[0].Arguments);
    }

    [Fact]
    public void GetLinuxArguments_HandlesPathsWithSpaces()
    {
        var args = TerminalLauncher.GetLinuxArguments("gnome-terminal", "Adele", "/home/some user/my project");

        // Single quotes handle spaces in bash
        Assert.Contains("cd '/home/some user/my project'", args);
    }

    [Fact]
    public void GetMacArguments_HandlesPathsWithSpaces()
    {
        var args = TerminalLauncher.GetMacArguments("Adele", "/Users/Some User/My Project");

        Assert.Contains("cd '/Users/Some User/My Project'", args);
    }

    [Fact]
    public void LaunchMac_UsesOsascript()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchMac("Adele");

        Assert.Single(recorder.Started);
        Assert.Equal("osascript", recorder.Started[0].FileName);
    }

    [Fact]
    public void LaunchMac_DoesNotUseShellExecute()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchMac("Adele");

        Assert.False(recorder.Started[0].UseShellExecute);
    }

    [Fact]
    public void LaunchMac_PassesScriptViaArgumentList()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchMac("Adele");

        var psi = recorder.Started[0];
        Assert.Equal(2, psi.ArgumentList.Count);
        Assert.Equal("-e", psi.ArgumentList[0]);
        Assert.Contains("tell app \"Terminal\" to do script", psi.ArgumentList[1]);
        Assert.Contains("Adele --inbox", psi.ArgumentList[1]);
    }

    [Fact]
    public void LaunchMac_ScriptContainsEscapedQuotesAroundPrompt()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchMac("Adele");

        var script = recorder.Started[0].ArgumentList[1];
        // AppleScript escaped quotes wrap the prompt so Terminal runs: claude "Adele --inbox"
        Assert.Contains("\\\"Adele --inbox\\\"", script);
    }

    [Fact]
    public void LaunchMac_ScriptContainsCdPrefix_WhenWorkingDirectoryProvided()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchMac("Adele", "/Users/dev/project");

        var script = recorder.Started[0].ArgumentList[1];
        Assert.Contains("cd '/Users/dev/project' &&", script);
    }

    [Fact]
    public void LaunchMac_ScriptNoCdPrefix_WhenNoWorkingDirectory()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchMac("Adele");

        var script = recorder.Started[0].ArgumentList[1];
        Assert.DoesNotContain("cd ", script);
    }

    [Fact]
    public void LaunchMac_ScriptClearsClaudeCodeEnvVar()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchMac("Adele");

        var script = recorder.Started[0].ArgumentList[1];
        Assert.Contains("unset CLAUDECODE", script);
        Assert.True(script.IndexOf("unset CLAUDECODE") < script.IndexOf("claude"));
    }

    [Fact]
    public void LaunchMac_Throws_WhenPathContainsSingleQuote()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        Assert.Throws<ArgumentException>(() =>
            launcher.LaunchMac("Adele", "/Users/dev/it's-a-project"));
    }

    [Fact]
    public void TryLaunchTerminals_DoesNotUseShellExecute()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.TryLaunchTerminals(TerminalLauncher.LinuxTerminals, "Adele");

        // UseShellExecute must be false on Unix; 'true' routes through xdg-open/open
        // which cannot launch terminal emulators.
        Assert.False(recorder.Started[0].UseShellExecute);
    }

    [Fact]
    public void TryLaunchTerminals_StopsOnFirstSuccess()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        var result = launcher.TryLaunchTerminals(TerminalLauncher.LinuxTerminals, "Adele");

        Assert.True(result);
        // Should only try one terminal (the first one that succeeds)
        Assert.Single(recorder.Started);
        Assert.Equal("gnome-terminal", recorder.Started[0].FileName);
    }

    [Fact]
    public void TryLaunchTerminals_TriesNextOnFailure()
    {
        var recorder = new RecordingProcessStarter();
        recorder.FailOnFileName("gnome-terminal");
        var launcher = new TerminalLauncher(recorder);

        var result = launcher.TryLaunchTerminals(TerminalLauncher.LinuxTerminals, "Adele");

        Assert.True(result);
        // Should have tried gnome-terminal first, then konsole
        Assert.Equal(2, recorder.Started.Count);
        Assert.Equal("gnome-terminal", recorder.Started[0].FileName);
        Assert.Equal("konsole", recorder.Started[1].FileName);
    }

    [Fact]
    public void TryLaunchTerminals_ReturnsFalse_WhenAllFail()
    {
        var recorder = new RecordingProcessStarter { FailAll = true };
        var launcher = new TerminalLauncher(recorder);

        var result = launcher.TryLaunchTerminals(TerminalLauncher.LinuxTerminals, "Adele");

        Assert.False(result);
        // Should have tried all terminals
        Assert.Equal(TerminalLauncher.LinuxTerminals.Length, recorder.Started.Count);
    }

    [Fact]
    public void TryLaunchTerminals_ArgumentsContainAgentName()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.TryLaunchTerminals(TerminalLauncher.LinuxTerminals, "Brian");

        Assert.Contains("Brian", recorder.Started[0].Arguments);
        Assert.Contains("--inbox", recorder.Started[0].Arguments);
    }

    [Fact]
    public void TryLaunchTerminals_ArgumentsContainCdPrefix_WhenWorkingDirectoryProvided()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);
        var projectRoot = "/home/user/my-project";

        launcher.TryLaunchTerminals(TerminalLauncher.LinuxTerminals, "Adele", projectRoot);

        Assert.Contains($"cd '{projectRoot}'", recorder.Started[0].Arguments);
    }

    [Fact]
    public void TryLaunchTerminals_NoCdPrefix_WhenNoWorkingDirectory()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.TryLaunchTerminals(TerminalLauncher.LinuxTerminals, "Adele");

        Assert.DoesNotContain("cd ", recorder.Started[0].Arguments);
    }

    #endregion

    #region Tab Mode Behavior Tests

    [Fact]
    public void LaunchWindows_TabMode_AddsWindowTarget()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele", useTab: true);

        var wtCall = recorder.Started.First(p => p.FileName == "wt");
        Assert.StartsWith("-w 0 new-tab", wtCall.Arguments);
    }

    [Fact]
    public void LaunchWindows_WindowMode_NoWindowTarget()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele", useTab: false);

        var wtCall = recorder.Started.First(p => p.FileName == "wt");
        Assert.DoesNotContain("-w 0", wtCall.Arguments);
        Assert.StartsWith("new-window ", wtCall.Arguments);
    }

    [Fact]
    public void LaunchMac_TabMode_WithITerm_UsesITermTabCreation()
    {
        var recorder = new RecordingProcessStarter();
        var detector = new TestTerminalDetector();
        detector.SetAvailable("iTerm");
        var launcher = new TerminalLauncher(recorder, detector);

        launcher.LaunchMac("Adele", useTab: true);

        var script = recorder.Started[0].ArgumentList[1];
        Assert.Contains("tell application \"iTerm\"", script);
        Assert.Contains("create tab with default profile", script);
        Assert.Contains("write text", script);
        Assert.Contains("Adele --inbox", script);
    }

    [Fact]
    public void LaunchMac_TabMode_WithITerm_CreatesWindowWhenNoneExist()
    {
        var recorder = new RecordingProcessStarter();
        var detector = new TestTerminalDetector();
        detector.SetAvailable("iTerm");
        var launcher = new TerminalLauncher(recorder, detector);

        launcher.LaunchMac("Adele", useTab: true);

        var script = recorder.Started[0].ArgumentList[1];
        Assert.Contains("create window with default profile", script);
        Assert.Contains("count of windows", script);
    }

    [Fact]
    public void LaunchMac_TabMode_WithoutITerm_FallsBackToNewWindow()
    {
        var recorder = new RecordingProcessStarter();
        var detector = new TestTerminalDetector(); // iTerm not available
        var launcher = new TerminalLauncher(recorder, detector);

        launcher.LaunchMac("Adele", useTab: true);

        var script = recorder.Started[0].ArgumentList[1];
        // Falls back to Terminal.app new window (no "in front window", no iTerm)
        Assert.Contains("tell app \"Terminal\" to do script", script);
        Assert.DoesNotContain("in front window", script);
        Assert.DoesNotContain("iTerm", script);
    }

    [Fact]
    public void LaunchMac_TabMode_WithoutITerm_PrintsInfoMessage()
    {
        var recorder = new RecordingProcessStarter();
        var detector = new TestTerminalDetector();
        var launcher = new TerminalLauncher(recorder, detector);

        var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            launcher.LaunchMac("Adele", useTab: true);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }

        var message = output.ToString();
        Assert.Contains("Terminal.app does not support tab creation via scripting", message);
        Assert.Contains("iTerm2", message);
    }

    [Fact]
    public void LaunchMac_WindowMode_DoesNotUseITerm_EvenWhenAvailable()
    {
        var recorder = new RecordingProcessStarter();
        var detector = new TestTerminalDetector();
        detector.SetAvailable("iTerm");
        var launcher = new TerminalLauncher(recorder, detector);

        launcher.LaunchMac("Adele", useTab: false);

        var script = recorder.Started[0].ArgumentList[1];
        Assert.Contains("tell app \"Terminal\" to do script", script);
        Assert.DoesNotContain("iTerm", script);
    }

    [Fact]
    public void LaunchMac_WindowMode_DoesNotPrintInfoMessage()
    {
        var recorder = new RecordingProcessStarter();
        var detector = new TestTerminalDetector();
        var launcher = new TerminalLauncher(recorder, detector);

        var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            launcher.LaunchMac("Adele", useTab: false);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }

        Assert.Empty(output.ToString());
    }

    [Fact]
    public void TryLaunchTerminals_TabMode_UsesTabArguments_WhenAvailable()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        // gnome-terminal supports tabs
        launcher.TryLaunchTerminals(TerminalLauncher.LinuxTerminals, "Adele", useTab: true);

        Assert.Contains("--tab", recorder.Started[0].Arguments);
    }

    [Fact]
    public void LaunchWindows_TabMode_UsesNewTabSubcommand()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele", useTab: true);

        var wtCall = recorder.Started.First(p => p.FileName == "wt");
        Assert.Contains("new-tab", wtCall.Arguments);
        Assert.DoesNotContain("new-window", wtCall.Arguments);
    }

    [Fact]
    public void LaunchWindows_WindowMode_UsesNewWindowSubcommand()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele", useTab: false);

        var wtCall = recorder.Started.First(p => p.FileName == "wt");
        Assert.Contains("new-window", wtCall.Arguments);
        Assert.DoesNotContain("new-tab", wtCall.Arguments);
    }

    [Fact]
    public void LaunchWindows_DefaultMode_UsesNewWindowSubcommand()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele");

        var wtCall = recorder.Started.First(p => p.FileName == "wt");
        Assert.Contains("new-window", wtCall.Arguments);
        Assert.DoesNotContain("new-tab", wtCall.Arguments);
    }

    [Fact]
    public void LaunchWindows_WindowMode_WithWorkingDirectory_UsesNewWindow()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele", "/home/user/project", useTab: false);

        var wtCall = recorder.Started.First(p => p.FileName == "wt");
        Assert.Contains("new-window", wtCall.Arguments);
        Assert.Contains("--startingDirectory", wtCall.Arguments);
    }

    [Fact]
    public void LaunchWindows_TabMode_WithWorkingDirectory_CombinesWindowTargetAndStartingDirectory()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele", "/home/user/project", useTab: true);

        var wtCall = recorder.Started.First(p => p.FileName == "wt");
        Assert.Contains("-w 0", wtCall.Arguments);
        Assert.Contains("--startingDirectory", wtCall.Arguments);
    }

    [Fact]
    public void LaunchMac_TabMode_WithITerm_WithWorkingDirectory_ContainsCdPrefix()
    {
        var recorder = new RecordingProcessStarter();
        var detector = new TestTerminalDetector();
        detector.SetAvailable("iTerm");
        var launcher = new TerminalLauncher(recorder, detector);

        launcher.LaunchMac("Adele", "/Users/dev/project", useTab: true);

        var script = recorder.Started[0].ArgumentList[1];
        Assert.Contains("cd '/Users/dev/project' &&", script);
    }

    [Fact]
    public void LaunchMac_TabMode_WithoutITerm_WithWorkingDirectory_ContainsCdPrefix()
    {
        var recorder = new RecordingProcessStarter();
        var detector = new TestTerminalDetector();
        var launcher = new TerminalLauncher(recorder, detector);

        launcher.LaunchMac("Adele", "/Users/dev/project", useTab: true);

        var script = recorder.Started[0].ArgumentList[1];
        Assert.Contains("cd '/Users/dev/project' &&", script);
    }

    [Fact]
    public void LaunchMac_TabMode_WithITerm_ClearsClaudeCodeEnvVar()
    {
        var recorder = new RecordingProcessStarter();
        var detector = new TestTerminalDetector();
        detector.SetAvailable("iTerm");
        var launcher = new TerminalLauncher(recorder, detector);

        launcher.LaunchMac("Adele", useTab: true);

        var script = recorder.Started[0].ArgumentList[1];
        Assert.Contains("unset CLAUDECODE", script);
        Assert.True(script.IndexOf("unset CLAUDECODE") < script.IndexOf("claude"));
    }

    [Fact]
    public void LaunchMac_TabMode_WithoutITerm_ClearsClaudeCodeEnvVar()
    {
        var recorder = new RecordingProcessStarter();
        var detector = new TestTerminalDetector();
        var launcher = new TerminalLauncher(recorder, detector);

        launcher.LaunchMac("Adele", useTab: true);

        var script = recorder.Started[0].ArgumentList[1];
        Assert.Contains("unset CLAUDECODE", script);
        Assert.True(script.IndexOf("unset CLAUDECODE") < script.IndexOf("claude"));
    }

    [Fact]
    public void LaunchMac_TabMode_WithITerm_AutoClose_ContainsStatusCheck()
    {
        var recorder = new RecordingProcessStarter();
        var detector = new TestTerminalDetector();
        detector.SetAvailable("iTerm");
        var launcher = new TerminalLauncher(recorder, detector);

        launcher.LaunchMac("Adele", useTab: true, autoClose: true);

        var script = recorder.Started[0].ArgumentList[1];
        Assert.Contains("dydo agent status Adele", script);
        Assert.Contains("exit 0", script);
    }

    [Fact]
    public void GetITermTabScript_ContainsExpectedStructure()
    {
        var script = TerminalLauncher.GetITermTabScript("unset CLAUDECODE; claude \\\"Adele --inbox\\\"", "");

        Assert.Contains("tell application \"iTerm\"", script);
        Assert.Contains("create tab with default profile", script);
        Assert.Contains("create window with default profile", script);
        Assert.Contains("tell current session", script);
        Assert.Contains("write text", script);
        Assert.Contains("activate", script);
        Assert.Contains("end tell", script);
    }

    [Theory]
    [InlineData("gnome-terminal")]
    [InlineData("konsole")]
    [InlineData("xfce4-terminal")]
    public void GetLinuxArguments_TabMode_WithWorkingDirectory_ContainsCdPrefix(string terminal)
    {
        var args = TerminalLauncher.GetLinuxArguments(terminal, "Adele", "/home/user/project", useTab: true);
        Assert.Contains("cd '/home/user/project' &&", args);
    }

    #endregion

    #region Auto-Close Argument Tests

    [Fact]
    public void GetWindowsArguments_AutoClose_ContainsStatusCheck()
    {
        var args = TerminalLauncher.GetWindowsArguments("Adele", autoClose: true);
        Assert.Contains("dydo agent status Adele", args);
        Assert.Contains("-match 'free'", args);
        Assert.Contains("exit 0", args);
    }

    [Fact]
    public void GetWindowsArguments_AutoClose_OmitsNoExit()
    {
        var args = TerminalLauncher.GetWindowsArguments("Adele", autoClose: true);
        Assert.DoesNotContain("-NoExit", args);
    }

    [Fact]
    public void GetWindowsArguments_NoAutoClose_ContainsNoExit()
    {
        var args = TerminalLauncher.GetWindowsArguments("Adele", autoClose: false);
        Assert.Contains("-NoExit", args);
    }

    [Fact]
    public void GetWindowsArguments_NoAutoClose_NoStatusCheck()
    {
        var args = TerminalLauncher.GetWindowsArguments("Adele", autoClose: false);
        Assert.DoesNotContain("dydo agent status", args);
        Assert.DoesNotContain("exit 0", args);
    }

    [Theory]
    [InlineData("gnome-terminal")]
    [InlineData("konsole")]
    [InlineData("xfce4-terminal")]
    [InlineData("alacritty")]
    [InlineData("kitty")]
    [InlineData("wezterm")]
    [InlineData("tilix")]
    [InlineData("foot")]
    [InlineData("xterm")]
    public void GetLinuxArguments_AutoClose_ContainsStatusCheck(string terminal)
    {
        var args = TerminalLauncher.GetLinuxArguments(terminal, "Adele", autoClose: true);
        Assert.Contains("dydo agent status Adele", args);
        Assert.Contains("exit 0", args);
    }

    [Theory]
    [InlineData("gnome-terminal")]
    [InlineData("konsole")]
    [InlineData("xfce4-terminal")]
    [InlineData("alacritty")]
    [InlineData("kitty")]
    [InlineData("wezterm")]
    [InlineData("tilix")]
    [InlineData("foot")]
    [InlineData("xterm")]
    public void GetLinuxArguments_AutoClose_StillContainsExecBashFallback(string terminal)
    {
        var args = TerminalLauncher.GetLinuxArguments(terminal, "Adele", autoClose: true);
        Assert.Contains("exec bash", args);
    }

    [Theory]
    [InlineData("gnome-terminal")]
    [InlineData("konsole")]
    [InlineData("xfce4-terminal")]
    public void GetLinuxArguments_AutoClose_TabMode_ContainsStatusCheck(string terminal)
    {
        var args = TerminalLauncher.GetLinuxArguments(terminal, "Adele", useTab: true, autoClose: true);
        Assert.Contains("dydo agent status Adele", args);
        Assert.Contains("exit 0", args);
    }

    [Fact]
    public void GetMacArguments_AutoClose_ContainsStatusCheck()
    {
        var args = TerminalLauncher.GetMacArguments("Adele", autoClose: true);
        Assert.Contains("dydo agent status Adele", args);
        Assert.Contains("exit 0", args);
    }

    [Fact]
    public void LaunchWindows_AutoClose_WtArgs_ContainStatusCheck()
    {
        var recorder = new RecordingProcessStarter();
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele", autoClose: true);

        var wtCall = recorder.Started.First(p => p.FileName == "wt");
        Assert.Contains("dydo agent status Adele", wtCall.Arguments);
    }

    [Fact]
    public void LaunchWindows_AutoClose_PowerShellFallback_ContainStatusCheck()
    {
        var recorder = new RecordingProcessStarter();
        recorder.FailOnFileName("wt");
        var launcher = new TerminalLauncher(recorder);

        launcher.LaunchWindows("Adele", autoClose: true);

        var psCall = recorder.Started.First(p => p.FileName == "powershell");
        Assert.Contains("dydo agent status Adele", psCall.Arguments);
    }

    #endregion
}

/// <summary>
/// Test helper that controls which terminal applications are reported as available.
/// </summary>
public class TestTerminalDetector : ITerminalDetector
{
    private readonly HashSet<string> _available = [];

    public void SetAvailable(string appName) => _available.Add(appName);

    public bool IsAvailable(string appName) => _available.Contains(appName);
}

/// <summary>
/// Test helper that records process start attempts without actually starting processes.
/// </summary>
public class RecordingProcessStarter : IProcessStarter
{
    public List<ProcessStartInfo> Started { get; } = [];
    private readonly HashSet<string> _failingFileNames = [];
    public bool FailAll { get; set; }

    public void FailOnFileName(string fileName)
    {
        _failingFileNames.Add(fileName);
    }

    public void Start(ProcessStartInfo psi)
    {
        Started.Add(psi);

        if (FailAll || _failingFileNames.Contains(psi.FileName))
        {
            throw new Win32Exception("File not found (simulated)");
        }
    }
}
