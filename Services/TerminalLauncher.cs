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

    public static string GenerateWorktreeId(string taskName, string? parentWorktreeId = null)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(taskName, @"^[a-zA-Z0-9_.\-]+$"))
            throw new ArgumentException($"Task name contains unsafe characters (allowed: a-zA-Z0-9_.-): {taskName}", nameof(taskName));
        if (taskName.Contains(".+."))
            throw new ArgumentException($"Task name cannot contain '.+.' sequence (reserved for branch encoding): {taskName}", nameof(taskName));
        return parentWorktreeId != null ? $"{parentWorktreeId}/{taskName}" : taskName;
    }

    public static string WorktreeIdToBranchSuffix(string worktreeId) =>
        worktreeId.Replace("/", ".+.");

    public static string BranchSuffixToWorktreeId(string suffix) =>
        suffix.Replace(".+.", "/");

    public static readonly TerminalConfig[] LinuxTerminals =
    [
        new("gnome-terminal",
            (agentName, wd) => $"-- bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\"",
            (agentName, wd) => $"--tab -- bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\""),
        new("konsole",
            (agentName, wd) => $"-e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\"",
            (agentName, wd) => $"--new-tab -e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\""),
        new("xfce4-terminal",
            (agentName, wd) => $"-e \"bash -c \\\"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\\\"\"",
            (agentName, wd) => $"--tab -e \"bash -c \\\"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\\\"\""),
        new("alacritty", (agentName, wd) => $"-e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\""),
        new("kitty", (agentName, wd) => $"bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\""),
        new("wezterm", (agentName, wd) => $"start -- bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\""),
        new("tilix", (agentName, wd) => $"-e \"bash -c \\\"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\\\"\""),
        new("foot", (agentName, wd) => $"bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\""),
        new("xterm", (agentName, wd) => $"-e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\""),
    ];

    public static readonly TerminalConfig[] MacTerminals =
    [
        new("osascript", (agentName, wd) =>
            $"-e 'tell app \"Terminal\" to do script \"{CdPrefix(wd)}unset CLAUDECODE; claude \\\"{agentName} --inbox\\\"\"'"),
    ];

    internal static string WorktreeSetupScript(string worktreeId, string? mainProjectRoot = null)
    {
        // Worktree is already created by DispatchService.CreateGitWorktree() before terminal launch.
        // This script only cd's into it and sets up symlinks.
        if (mainProjectRoot != null)
        {
            var escapedRoot = mainProjectRoot.Replace("'", "'\\''");
            return $"cd '{escapedRoot}/dydo/_system/.local/worktrees/{worktreeId}' && " +
                   $"rm -rf dydo/agents && ln -s '{escapedRoot}/dydo/agents' dydo/agents && " +
                   $"rm -rf dydo/_system/roles && ln -s '{escapedRoot}/dydo/_system/roles' dydo/_system/roles && " +
                   $"mkdir -p '{escapedRoot}/dydo/project/issues' && rm -rf dydo/project/issues && ln -s '{escapedRoot}/dydo/project/issues' dydo/project/issues && " +
                   $"mkdir -p '{escapedRoot}/dydo/project/inquisitions' && rm -rf dydo/project/inquisitions && ln -s '{escapedRoot}/dydo/project/inquisitions' dydo/project/inquisitions && " +
                   $"(dydo worktree init-settings --main-root '{escapedRoot}' || echo 'WARNING: init-settings failed' >&2) && sleep 1 && ";
        }

        return $"_wt_root=\"$(pwd)\" && " +
               $"cd dydo/_system/.local/worktrees/{worktreeId} && " +
               $"rm -rf dydo/agents && ln -s \"$_wt_root/dydo/agents\" dydo/agents && " +
               $"rm -rf dydo/_system/roles && ln -s \"$_wt_root/dydo/_system/roles\" dydo/_system/roles && " +
               $"mkdir -p \"$_wt_root/dydo/project/issues\" && rm -rf dydo/project/issues && ln -s \"$_wt_root/dydo/project/issues\" dydo/project/issues && " +
               $"mkdir -p \"$_wt_root/dydo/project/inquisitions\" && rm -rf dydo/project/inquisitions && ln -s \"$_wt_root/dydo/project/inquisitions\" dydo/project/inquisitions && " +
               $"(dydo worktree init-settings --main-root \"$_wt_root\" || echo 'WARNING: init-settings failed' >&2) && sleep 1 && ";
    }

    internal static string WorktreeInitSettingsScript(string? mainProjectRoot)
    {
        if (mainProjectRoot == null) return "";
        var escapedRoot = mainProjectRoot.Replace("'", "'\\''");
        return $"(dydo worktree init-settings --main-root '{escapedRoot}' || echo 'WARNING: init-settings failed' >&2) && sleep 1 && ";
    }

    internal static string WorktreeInheritedSetupScript(string? mainProjectRoot, string? workingDirectory)
    {
        if (mainProjectRoot == null) return "";
        var escapedRoot = mainProjectRoot.Replace("'", "'\\''");
        var cdPrefix = workingDirectory != null
            ? $"cd '{workingDirectory.Replace("'", "'\\''")}' && "
            : "";
        return $"{cdPrefix}(dydo worktree init-settings --main-root '{escapedRoot}' || echo 'WARNING: init-settings failed' >&2) && sleep 1 && ";
    }

    internal static string WorktreeCleanupScript(string worktreeId, string agentName) =>
        $"dydo worktree cleanup {worktreeId} --agent {agentName}";

    internal static string CdPrefix(string? workingDirectory)
    {
        if (workingDirectory == null)
            return "";

        if (workingDirectory.Contains('\''))
            throw new ArgumentException(
                $"Project path contains a single quote and cannot be safely used in a shell cd command: {workingDirectory}");

        return $"cd '{workingDirectory}' && ";
    }

    public static string GetWindowsArguments(string agentName, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null, string? workingDirectory = null)
        => WindowsTerminalLauncher.GetArguments(agentName, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, workingDirectory);

    public static string GetLinuxArguments(string terminalName, string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
        => LinuxTerminalLauncher.GetArguments(terminalName, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot);

    public static string GetMacArguments(string agentName, string? workingDirectory = null, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
        => MacTerminalLauncher.GetArguments(agentName, workingDirectory, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot);

    public static string GetITermTabScript(string shellCommand, string postCheck)
        => MacTerminalLauncher.GetITermTabScript(shellCommand, postCheck);

    public static IProcessStarter? ProcessStarterOverride { get; set; }

    public static int LaunchNewTerminal(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
    {
        return new TerminalLauncher(ProcessStarterOverride).Launch(agentName, workingDirectory, useTab, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot);
    }

    public int Launch(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return WindowsTerminalLauncher.Launch(_processStarter, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return MacTerminalLauncher.Launch(_processStarter, _terminalDetector, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot);

            var pid = LinuxTerminalLauncher.TryLaunch(_processStarter, LinuxTerminals, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot);
            if (pid == 0)
                throw new InvalidOperationException("No terminal found");
            return pid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARN: Could not launch terminal: {ex.Message}");
            Console.WriteLine($"Please manually open a new terminal and run:");
            Console.WriteLine($"  {GetClaudeCommand(agentName)}");
            return 0;
        }
    }

    public int LaunchWindows(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
        => WindowsTerminalLauncher.Launch(_processStarter, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot);

    public int LaunchMac(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
        => MacTerminalLauncher.Launch(_processStarter, _terminalDetector, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot);

    public int TryLaunchTerminals(TerminalConfig[] terminals, string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
        => LinuxTerminalLauncher.TryLaunch(_processStarter, terminals, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot);

    private class DefaultProcessStarter : IProcessStarter
    {
        public int Start(ProcessStartInfo psi)
        {
            var process = Process.Start(psi);
            if (process == null) return 0;
            try { return process.Id; }
            catch { return 0; }
        }
    }

    private class DefaultTerminalDetector : ITerminalDetector
    {
        public bool IsAvailable(string appName)
        {
            return Directory.Exists($"/Applications/{appName}.app");
        }
    }
}
