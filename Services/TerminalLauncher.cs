namespace DynaDocs.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Interface for starting processes. Enables testing without actually launching terminals.
/// </summary>
public interface IProcessStarter
{
    void Start(ProcessStartInfo psi);
}

/// <summary>
/// Interface for detecting installed terminal applications. Enables testing without filesystem checks.
/// </summary>
public interface ITerminalDetector
{
    bool IsAvailable(string appName);
}

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

    public static string GetWindowsArguments(string agentName, bool autoClose = false)
    {
        var prompt = GetClaudePrompt(agentName);
        // -NoExit keeps PowerShell open after the command completes
        // Single quotes in PowerShell create a literal string, ensuring the entire
        // prompt (including --inbox) is passed as one argument to claude.
        // Double-quote escaping ("") breaks when passing through wt → powershell layers.
        var escapedPrompt = prompt.Replace("'", "''");
        var postClaudeCheck = autoClose
            ? $"; if ((dydo agent status {agentName} 2>&1) -match 'free') {{ exit 0 }}"
            : "";
        return $"-NoExit -Command \"Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; claude '{escapedPrompt}'{postClaudeCheck}\"";
    }

    public static string GetLinuxArguments(string terminalName, string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false)
    {
        var config = LinuxTerminals.FirstOrDefault(t => t.FileName == terminalName);
        if (config == null) throw new ArgumentException($"Unknown terminal: {terminalName}");

        var args = (useTab && config.GetTabArguments != null)
            ? config.GetTabArguments(agentName, workingDirectory)
            : config.GetArguments(agentName, workingDirectory);

        if (autoClose)
            args = args.Replace("exec bash", BashPostClaudeCheck(agentName));

        return args;
    }

    public static string GetMacArguments(string agentName, string? workingDirectory = null, bool autoClose = false)
    {
        if (autoClose)
        {
            var cdPrefix = CdPrefix(workingDirectory);
            return $"-e 'tell app \"Terminal\" to do script \"{cdPrefix}unset CLAUDECODE; claude \\\"{agentName} --inbox\\\"; {BashPostClaudeCheck(agentName)}\"'";
        }
        return MacTerminals[0].GetArguments(agentName, workingDirectory);
    }

    /// <summary>
    /// Static convenience method for backward compatibility.
    /// </summary>
    public static void LaunchNewTerminal(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false)
    {
        new TerminalLauncher().Launch(agentName, workingDirectory, useTab, autoClose);
    }

    /// <summary>
    /// Launch a new terminal for the specified agent.
    /// </summary>
    public void Launch(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                LaunchWindows(agentName, workingDirectory, useTab, autoClose);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                LaunchMac(agentName, workingDirectory, useTab, autoClose);
            }
            else
            {
                if (!TryLaunchTerminals(LinuxTerminals, agentName, workingDirectory, useTab, autoClose))
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
    public void LaunchWindows(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false)
    {
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
                Arguments = $"{wtAction} {wtDirArg}powershell {GetWindowsArguments(agentName, autoClose).Replace(";", "\\;")}",
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
            FileName = "powershell",
            Arguments = GetWindowsArguments(agentName, autoClose),
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
    public void LaunchMac(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false)
    {
        var cdPrefix = CdPrefix(workingDirectory);
        var shellCommand = $"{cdPrefix}unset CLAUDECODE; claude \\\"{agentName} --inbox\\\"";
        var postCheck = autoClose ? $"; {BashPostClaudeCheck(agentName)}" : "";

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
    public bool TryLaunchTerminals(TerminalConfig[] terminals, string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false)
    {
        foreach (var terminal in terminals)
        {
            try
            {
                var arguments = useTab && terminal.GetTabArguments != null
                    ? terminal.GetTabArguments(agentName, workingDirectory)
                    : terminal.GetArguments(agentName, workingDirectory);

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
