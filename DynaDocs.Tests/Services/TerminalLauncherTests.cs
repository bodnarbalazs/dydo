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
        Assert.Equal("-NoExit -Command \"claude 'Adele --inbox'\"", args);
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
        Assert.Contains("cd '/home/user/project' && claude", args);
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
        Assert.Contains("cd '/Users/dev/project' && claude", args);
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
