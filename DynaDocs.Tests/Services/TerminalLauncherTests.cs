namespace DynaDocs.Tests.Services;

using DynaDocs.Services;
using Xunit;

public class TerminalLauncherTests
{
    [Theory]
    [InlineData("Adele")]
    [InlineData("Brian")]
    [InlineData("Charlie")]
    public void GetWindowsArguments_ContainsQuotedPrompt(string agentName)
    {
        var args = TerminalLauncher.GetWindowsArguments(agentName);

        // Should contain properly escaped quotes for PowerShell
        // Expected: -Command "claude ""--inbox Adele"""
        Assert.Contains($"\"\"--inbox {agentName}\"\"", args);
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
    public void GetLinuxArguments_ContainsQuotedPrompt(string terminal, string agentName)
    {
        var args = TerminalLauncher.GetLinuxArguments(terminal, agentName);

        // Should contain single-quoted prompt for bash
        // Expected: claude '--inbox AgentName'
        Assert.Contains($"'--inbox {agentName}'", args);
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
    public void GetMacArguments_ContainsQuotedPrompt(string agentName)
    {
        var args = TerminalLauncher.GetMacArguments(agentName);

        // Should contain escaped quotes for AppleScript
        // Expected: \\\"--inbox AgentName\\\"
        Assert.Contains($"\\\"--inbox {agentName}\\\"", args);
    }

    [Theory]
    [InlineData("Agent With Space")]
    [InlineData("Agent'Quote")]
    public void GetWindowsArguments_HandlesSpecialCharacters(string agentName)
    {
        // Should not throw - quoting handles spaces
        var args = TerminalLauncher.GetWindowsArguments(agentName);
        Assert.Contains(agentName, args);
    }
}
