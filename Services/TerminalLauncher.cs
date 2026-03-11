namespace DynaDocs.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;

public class TerminalLauncher
{
    private readonly IProcessStarter _processStarter;
    private readonly ITerminalDetector _terminalDetector;

    public record TerminalConfig(
        string FileName,
        Func<string, string?, string> GetArguments,
        Func<string, string?, string>? GetTabArguments = null);

    public TerminalLauncher(IProcessStarter? processStarter = null, ITerminalDetector? terminalDetector = null)
    {
        _processStarter = processStarter ?? new DefaultProcessStarter();
        _terminalDetector = terminalDetector ?? new DefaultTerminalDetector();
    }

    public static string GetClaudePrompt(string agentName)
    {
        return $"{agentName} --inbox";
    }

    public static string GetClaudeCommand(string agentName)
    {
        var prompt = GetClaudePrompt(agentName);
        return $"claude \"{prompt}\"";
    }

    public static string GenerateWorktreeId(string agentName) =>
        $"{agentName}-{DateTime.UtcNow:yyyyMMddHHmmss}";

    public static readonly TerminalConfig[] LinuxTerminals =
    [
        new("gnome-terminal",
            (agentName, wd) => $"-- bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\"",
            (agentName, wd) => $"--tab -- bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\""),
        new("konsole",
            (agentName, wd) => $"-e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\"",
            (agentName, wd) => $"--new-tab -e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\""),
        new("xfce4-terminal",
            (agentName, wd) => $"-e \"bash -c \\\"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\\\"\"",
            (agentName, wd) => $"--tab -e \"bash -c \\\"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\\\"\""),
        new("alacritty", (agentName, wd) => $"-e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\""),
        new("kitty", (agentName, wd) => $"bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\""),
        new("wezterm", (agentName, wd) => $"start -- bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\""),
        new("tilix", (agentName, wd) => $"-e \"bash -c \\\"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\\\"\""),
        new("foot", (agentName, wd) => $"bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\""),
        new("xterm", (agentName, wd) => $"-e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\""),
    ];

    public static readonly TerminalConfig[] MacTerminals =
    [
        new("osascript", (agentName, wd) =>
            $"-e 'tell app \"Terminal\" to do script \"{CdPrefix(wd)}unset CLAUDECODE; claude \\\"{agentName} --inbox\\\"\"'"),
    ];

    internal static string WorktreeSetupScript(string worktreeId) =>
        $"mkdir -p .dydo/worktrees && git worktree prune && git worktree add .dydo/worktrees/{worktreeId} -b worktree/{worktreeId} && cd .dydo/worktrees/{worktreeId} && ";

    internal static string WorktreeCleanupScript(string worktreeId) =>
        $"cd ../../.. && git worktree remove .dydo/worktrees/{worktreeId} --force";

    internal static string CdPrefix(string? workingDirectory)
    {
        if (workingDirectory == null)
            return "";

        if (workingDirectory.Contains('\''))
            throw new ArgumentException(
                $"Project path contains a single quote and cannot be safely used in a shell cd command: {workingDirectory}");

        return $"cd '{workingDirectory}' && ";
    }

    public static string GetWindowsArguments(string agentName, bool autoClose = false, string? worktreeId = null, string? windowName = null)
        => WindowsTerminalLauncher.GetArguments(agentName, autoClose, worktreeId, windowName);

    public static string GetLinuxArguments(string terminalName, string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null)
        => LinuxTerminalLauncher.GetArguments(terminalName, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName);

    public static string GetMacArguments(string agentName, string? workingDirectory = null, bool autoClose = false, string? worktreeId = null, string? windowName = null)
        => MacTerminalLauncher.GetArguments(agentName, workingDirectory, autoClose, worktreeId, windowName);

    public static string GetITermTabScript(string shellCommand, string postCheck)
        => MacTerminalLauncher.GetITermTabScript(shellCommand, postCheck);

    public static IProcessStarter? ProcessStarterOverride { get; set; }

    public static void LaunchNewTerminal(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null)
    {
        new TerminalLauncher(ProcessStarterOverride).Launch(agentName, workingDirectory, useTab, autoClose, worktreeId, windowName);
    }

    public void Launch(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                WindowsTerminalLauncher.Launch(_processStarter, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                MacTerminalLauncher.Launch(_processStarter, _terminalDetector, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName);
            else if (!LinuxTerminalLauncher.TryLaunch(_processStarter, LinuxTerminals, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName))
                throw new InvalidOperationException("No terminal found");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARN: Could not launch terminal: {ex.Message}");
            Console.WriteLine($"Please manually open a new terminal and run:");
            Console.WriteLine($"  {GetClaudeCommand(agentName)}");
        }
    }

    public void LaunchWindows(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null)
        => WindowsTerminalLauncher.Launch(_processStarter, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName);

    public void LaunchMac(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null)
        => MacTerminalLauncher.Launch(_processStarter, _terminalDetector, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName);

    public bool TryLaunchTerminals(TerminalConfig[] terminals, string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null)
        => LinuxTerminalLauncher.TryLaunch(_processStarter, terminals, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName);

    private class DefaultProcessStarter : IProcessStarter
    {
        public void Start(ProcessStartInfo psi) => Process.Start(psi);
    }

    private class DefaultTerminalDetector : ITerminalDetector
    {
        public bool IsAvailable(string appName)
        {
            return Directory.Exists($"/Applications/{appName}.app");
        }
    }
}
