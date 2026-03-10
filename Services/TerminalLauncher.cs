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

    /// <summary>
    /// Create a TerminalLauncher with optional process starter and terminal detector for testing.
    /// </summary>
    public TerminalLauncher(IProcessStarter? processStarter = null, ITerminalDetector? terminalDetector = null)
    {
        _processStarter = processStarter ?? new DefaultProcessStarter();
        _terminalDetector = terminalDetector ?? new DefaultTerminalDetector();
    }

    /// <summary>
    /// Get the prompt to pass to Claude for inbox processing.
    /// The format is "AgentName --inbox" which tells Claude (via CLAUDE.md docs)
    /// to claim that agent identity and check the inbox for dispatched work.
    /// </summary>
    public static string GetClaudePrompt(string agentName)
    {
        return $"{agentName} --inbox";
    }

    /// <summary>
    /// Get the full Claude command with proper escaping for the shell.
    /// </summary>
    public static string GetClaudeCommand(string agentName)
    {
        var prompt = GetClaudePrompt(agentName);
        return $"claude \"{prompt}\"";
    }

    /// <summary>
    /// Generate a unique worktree identifier for an agent.
    /// Used as both the directory name and branch suffix.
    /// </summary>
    public static string GenerateWorktreeId(string agentName) =>
        $"{agentName}-{DateTime.UtcNow:yyyyMMddHHmmss}";

    public static readonly TerminalConfig[] LinuxTerminals =
    [
        // Modern terminals (most common on current distros)
        // Prompt format: claude "AgentName --inbox"
        // When workingDirectory is provided, prefix with cd to ensure shell starts in the right place
        new("gnome-terminal",
            (agentName, wd) => $"-- bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\"",
            (agentName, wd) => $"--tab -- bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\""),
        new("konsole",
            (agentName, wd) => $"-e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\"",
            (agentName, wd) => $"--new-tab -e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\""),
        new("xfce4-terminal",
            (agentName, wd) => $"-e \"bash -c \\\"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\\\"\"",
            (agentName, wd) => $"--tab -e \"bash -c \\\"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\\\"\""),

        // Popular third-party terminals (no tab support via CLI)
        new("alacritty", (agentName, wd) => $"-e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\""),
        new("kitty", (agentName, wd) => $"bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\""),
        new("wezterm", (agentName, wd) => $"start -- bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\""),
        new("tilix", (agentName, wd) => $"-e \"bash -c \\\"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\\\"\""),

        // Wayland-native
        new("foot", (agentName, wd) => $"bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\""),

        // Fallback (usually available)
        new("xterm", (agentName, wd) => $"-e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; exec bash\""),
    ];

    private static string BashPostClaudeCheck(string agentName)
    {
        return $"if dydo agent status {agentName} 2>/dev/null | grep -q 'free'; then exit 0; fi; exec bash";
    }

    public static readonly TerminalConfig[] MacTerminals =
    [
        // Terminal.app is always present on macOS
        new("osascript", (agentName, wd) =>
            $"-e 'tell app \"Terminal\" to do script \"{CdPrefix(wd)}unset CLAUDECODE; claude \\\"{agentName} --inbox\\\"\"'"),
    ];

    /// <summary>
    /// Shell commands to create a git worktree and cd into it.
    /// Trailing " && " lets the caller chain the next command.
    /// </summary>
    internal static string WorktreeSetupScript(string worktreeId) =>
        $"mkdir -p .dydo/worktrees && git worktree prune && git worktree add .dydo/worktrees/{worktreeId} -b worktree/{worktreeId} && cd .dydo/worktrees/{worktreeId} && ";

    /// <summary>
    /// Shell commands to navigate back to the repo root and remove the worktree.
    /// Callers add their own separator: Linux suffixes "; ", macOS prefixes "; ".
    /// </summary>
    internal static string WorktreeCleanupScript(string worktreeId) =>
        $"cd ../../.. && git worktree remove .dydo/worktrees/{worktreeId} --force";

    /// <summary>
    /// Returns "cd '/path' && " when a working directory is provided, empty string otherwise.
    /// Used to prefix shell commands in Linux terminals.
    /// </summary>
    internal static string CdPrefix(string? workingDirectory)
    {
        if (workingDirectory == null)
            return "";

        if (workingDirectory.Contains('\''))
            throw new ArgumentException(
                $"Project path contains a single quote and cannot be safely used in a shell cd command: {workingDirectory}");

        return $"cd '{workingDirectory}' && ";
    }

    public static string GetWindowsArguments(string agentName, bool autoClose = false, string? worktreeId = null)
    {
        var prompt = GetClaudePrompt(agentName);
        // Single quotes in PowerShell create a literal string, ensuring the entire
        // prompt (including --inbox) is passed as one argument to claude.
        // Double-quote escaping ("") breaks when passing through wt → powershell layers.
        var escapedPrompt = prompt.Replace("'", "''");
        var postClaudeCheck = autoClose
            ? $"; if ((dydo agent status {agentName} 2>&1) -match 'free') {{ exit 0 }}"
            : "";
        // -NoExit keeps PowerShell open after the command completes.
        // Omit it when auto-close is active so the shell exits naturally.
        var noExitFlag = autoClose ? "" : "-NoExit ";

        if (worktreeId != null)
        {
            var wtDir = $".dydo/worktrees/{worktreeId}";
            var branch = $"worktree/{worktreeId}";
            // try/finally ensures worktree cleanup even if claude crashes
            return $"{noExitFlag}-Command \"$_wt_root = Get-Location; " +
                   $"New-Item -ItemType Directory -Force -Path .dydo/worktrees | Out-Null; " +
                   $"git worktree prune; " +
                   $"git worktree add {wtDir} -b {branch}; " +
                   $"Set-Location {wtDir}; " +
                   $"try {{ Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; claude '{escapedPrompt}'{postClaudeCheck} }} " +
                   $"finally {{ Set-Location $_wt_root; git worktree remove {wtDir} --force }}\"";
        }

        return $"{noExitFlag}-Command \"Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; claude '{escapedPrompt}'{postClaudeCheck}\"";
    }

    public static string GetLinuxArguments(string terminalName, string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null)
    {
        var config = LinuxTerminals.FirstOrDefault(t => t.FileName == terminalName);
        if (config == null) throw new ArgumentException($"Unknown terminal: {terminalName}");

        var args = (useTab && config.GetTabArguments != null)
            ? config.GetTabArguments(agentName, workingDirectory)
            : config.GetArguments(agentName, workingDirectory);

        // Worktree wrapping must be applied before autoClose so cleanup runs before the exit check.
        // cd ../../.. navigates back from .dydo/worktrees/{id} (always 3 levels deep).
        if (worktreeId != null)
        {
            args = args.Replace("unset CLAUDECODE", WorktreeSetupScript(worktreeId) + "unset CLAUDECODE");
            args = args.Replace("exec bash", WorktreeCleanupScript(worktreeId) + "; exec bash");
        }

        if (autoClose)
            args = args.Replace("exec bash", BashPostClaudeCheck(agentName));

        return args;
    }

    public static string GetMacArguments(string agentName, string? workingDirectory = null, bool autoClose = false, string? worktreeId = null)
    {
        var cdPrefix = CdPrefix(workingDirectory);

        string wtSetup = "", wtCleanup = "";
        if (worktreeId != null)
        {
            wtSetup = WorktreeSetupScript(worktreeId);
            wtCleanup = "; " + WorktreeCleanupScript(worktreeId);
        }

        var postClaude = wtCleanup + (autoClose ? $"; {BashPostClaudeCheck(agentName)}" : "");

        return $"-e 'tell app \"Terminal\" to do script \"{cdPrefix}{wtSetup}unset CLAUDECODE; claude \\\"{agentName} --inbox\\\"{postClaude}\"'";
    }

    /// <summary>
    /// Override the process starter used by LaunchNewTerminal for testing.
    /// Set to a NoOpProcessStarter to suppress real terminal launches in tests.
    /// </summary>
    public static IProcessStarter? ProcessStarterOverride { get; set; }

    /// <summary>
    /// Static convenience method for backward compatibility.
    /// </summary>
    public static void LaunchNewTerminal(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null)
    {
        new TerminalLauncher(ProcessStarterOverride).Launch(agentName, workingDirectory, useTab, autoClose, worktreeId);
    }

    /// <summary>
    /// Launch a new terminal for the specified agent.
    /// </summary>
    public void Launch(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                LaunchWindows(agentName, workingDirectory, useTab, autoClose, worktreeId);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                LaunchMac(agentName, workingDirectory, useTab, autoClose, worktreeId);
            }
            else
            {
                if (!TryLaunchTerminals(LinuxTerminals, agentName, workingDirectory, useTab, autoClose, worktreeId))
                {
                    throw new InvalidOperationException("No terminal found");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARN: Could not launch terminal: {ex.Message}");
            Console.WriteLine($"Please manually open a new terminal and run:");
            Console.WriteLine($"  {GetClaudeCommand(agentName)}");
        }
    }

    /// <summary>
    /// Launch terminal on Windows. Tries Windows Terminal first, falls back to PowerShell.
    /// </summary>
    public void LaunchWindows(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null)
    {
        var shell = ProcessUtils.ResolvePowerShell();

        // Try Windows Terminal first (modern)
        try
        {
            var wtAction = useTab ? "-w 0 new-tab" : "new-window";
            var wtDirArg = workingDirectory != null
                ? $"--startingDirectory \"{workingDirectory}\" "
                : "";
            var psi = new ProcessStartInfo
            {
                FileName = "wt",
                // wt uses ';' as its own subcommand separator, so escape it with '\;'
                Arguments = $"{wtAction} {wtDirArg}{shell} {GetWindowsArguments(agentName, autoClose, worktreeId).Replace(";", "\\;")}",
                UseShellExecute = true
            };
            if (workingDirectory != null)
                psi.WorkingDirectory = workingDirectory;
            _processStarter.Start(psi);
            return;
        }
        catch
        {
            // Windows Terminal not available, fall back to PowerShell
        }

        // Fall back to PowerShell (no tab support)
        var fallbackPsi = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = GetWindowsArguments(agentName, autoClose, worktreeId),
            UseShellExecute = true
        };
        if (workingDirectory != null)
            fallbackPsi.WorkingDirectory = workingDirectory;
        _processStarter.Start(fallbackPsi);
    }

    /// <summary>
    /// Launch terminal on macOS. Detects iTerm2 for native tab support.
    /// Terminal.app does not support tab creation via AppleScript, so tab mode
    /// falls back to a new window with an informational message.
    /// </summary>
    public void LaunchMac(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null)
    {
        var cdPrefix = CdPrefix(workingDirectory);

        string wtSetup = "", wtCleanup = "";
        if (worktreeId != null)
        {
            wtSetup = WorktreeSetupScript(worktreeId);
            wtCleanup = "; " + WorktreeCleanupScript(worktreeId);
        }

        var shellCommand = $"{cdPrefix}{wtSetup}unset CLAUDECODE; claude \\\"{agentName} --inbox\\\"";
        var postCheck = wtCleanup + (autoClose ? $"; {BashPostClaudeCheck(agentName)}" : "");

        string script;
        if (useTab && _terminalDetector.IsAvailable("iTerm"))
        {
            // iTerm2 has native AppleScript tab support
            script = GetITermTabScript(shellCommand, postCheck);
        }
        else
        {
            if (useTab)
            {
                Console.WriteLine("INFO: Terminal.app does not support tab creation via scripting. Launching new window instead. Install iTerm2 for tab support.");
            }
            // Window mode (or Terminal.app tab fallback): always open a new window
            script = $"tell app \"Terminal\" to do script \"{shellCommand}{postCheck}\"";
        }

        var psi = new ProcessStartInfo
        {
            FileName = "osascript",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-e");
        psi.ArgumentList.Add(script);

        _processStarter.Start(psi);
    }

    /// <summary>
    /// Generate AppleScript for creating a new tab in iTerm2 and running a command.
    /// Creates a tab in the current window if one exists, otherwise creates a new window.
    /// </summary>
    public static string GetITermTabScript(string shellCommand, string postCheck)
    {
        var cmd = $"{shellCommand}{postCheck}";
        return "tell application \"iTerm\"\n" +
               "  if (count of windows) > 0 then\n" +
               "    tell current window\n" +
               "      create tab with default profile\n" +
               "      tell current session\n" +
               $"        write text \"{cmd}\"\n" +
               "      end tell\n" +
               "    end tell\n" +
               "  else\n" +
               "    create window with default profile\n" +
               "    tell current session of current window\n" +
               $"      write text \"{cmd}\"\n" +
               "    end tell\n" +
               "  end if\n" +
               "  activate\n" +
               "end tell";
    }

    /// <summary>
    /// Try to launch one of the configured terminals.
    /// </summary>
    public bool TryLaunchTerminals(TerminalConfig[] terminals, string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null)
    {
        foreach (var terminal in terminals)
        {
            try
            {
                var arguments = useTab && terminal.GetTabArguments != null
                    ? terminal.GetTabArguments(agentName, workingDirectory)
                    : terminal.GetArguments(agentName, workingDirectory);

                if (worktreeId != null)
                {
                    arguments = arguments.Replace("unset CLAUDECODE", WorktreeSetupScript(worktreeId) + "unset CLAUDECODE");
                    arguments = arguments.Replace("exec bash", WorktreeCleanupScript(worktreeId) + "; exec bash");
                }

                if (autoClose)
                    arguments = arguments.Replace("exec bash", BashPostClaudeCheck(agentName));

                _processStarter.Start(new ProcessStartInfo
                {
                    FileName = terminal.FileName,
                    Arguments = arguments,
                    UseShellExecute = false
                });
                return true;
            }
            catch (ArgumentException)
            {
                // Path validation error (e.g. single quote in path) — re-throw immediately
                throw;
            }
            catch
            {
                // Terminal not found — try next one
            }
        }
        return false;
    }

    /// <summary>
    /// Default implementation that actually starts processes.
    /// </summary>
    private class DefaultProcessStarter : IProcessStarter
    {
        public void Start(ProcessStartInfo psi) => Process.Start(psi);
    }

    /// <summary>
    /// Default implementation that checks for macOS .app bundles in /Applications.
    /// </summary>
    private class DefaultTerminalDetector : ITerminalDetector
    {
        public bool IsAvailable(string appName)
        {
            return Directory.Exists($"/Applications/{appName}.app");
        }
    }
}
