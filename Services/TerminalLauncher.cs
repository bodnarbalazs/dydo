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

public class TerminalLauncher
{
    private readonly IProcessStarter _processStarter;

    public record TerminalConfig(string FileName, Func<string, string?, string> GetArguments);

    /// <summary>
    /// Create a TerminalLauncher with optional process starter for testing.
    /// </summary>
    public TerminalLauncher(IProcessStarter? processStarter = null)
    {
        _processStarter = processStarter ?? new DefaultProcessStarter();
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
        new("gnome-terminal", (agentName, wd) => $"-- bash -c \"{CdPrefix(wd)}claude '{agentName} --inbox'; exec bash\""),
        new("konsole", (agentName, wd) => $"-e bash -c \"{CdPrefix(wd)}claude '{agentName} --inbox'; exec bash\""),
        new("xfce4-terminal", (agentName, wd) => $"-e \"bash -c \\\"{CdPrefix(wd)}claude '{agentName} --inbox'; exec bash\\\"\""),

        // Popular third-party terminals
        new("alacritty", (agentName, wd) => $"-e bash -c \"{CdPrefix(wd)}claude '{agentName} --inbox'; exec bash\""),
        new("kitty", (agentName, wd) => $"bash -c \"{CdPrefix(wd)}claude '{agentName} --inbox'; exec bash\""),
        new("wezterm", (agentName, wd) => $"start -- bash -c \"{CdPrefix(wd)}claude '{agentName} --inbox'; exec bash\""),
        new("tilix", (agentName, wd) => $"-e \"bash -c \\\"{CdPrefix(wd)}claude '{agentName} --inbox'; exec bash\\\"\""),

        // Wayland-native
        new("foot", (agentName, wd) => $"bash -c \"{CdPrefix(wd)}claude '{agentName} --inbox'; exec bash\""),

        // Fallback (usually available)
        new("xterm", (agentName, wd) => $"-e bash -c \"{CdPrefix(wd)}claude '{agentName} --inbox'; exec bash\""),
    ];

    public static readonly TerminalConfig[] MacTerminals =
    [
        // Terminal.app is always present on macOS
        new("osascript", (agentName, wd) =>
            $"-e 'tell app \"Terminal\" to do script \"{CdPrefix(wd)}claude \\\"{agentName} --inbox\\\"\"'"),
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

    public static string GetWindowsArguments(string agentName)
    {
        var prompt = GetClaudePrompt(agentName);
        // -NoExit keeps PowerShell open after the command completes
        // Single quotes in PowerShell create a literal string, ensuring the entire
        // prompt (including --inbox) is passed as one argument to claude.
        // Double-quote escaping ("") breaks when passing through wt → powershell layers.
        var escapedPrompt = prompt.Replace("'", "''");
        return $"-NoExit -Command \"claude '{escapedPrompt}'\"";
    }

    public static string GetLinuxArguments(string terminalName, string agentName, string? workingDirectory = null)
    {
        var config = LinuxTerminals.FirstOrDefault(t => t.FileName == terminalName);
        return config?.GetArguments(agentName, workingDirectory) ?? throw new ArgumentException($"Unknown terminal: {terminalName}");
    }

    public static string GetMacArguments(string agentName, string? workingDirectory = null)
    {
        return MacTerminals[0].GetArguments(agentName, workingDirectory);
    }

    /// <summary>
    /// Static convenience method for backward compatibility.
    /// </summary>
    public static void LaunchNewTerminal(string agentName, string? workingDirectory = null)
    {
        new TerminalLauncher().Launch(agentName, workingDirectory);
    }

    /// <summary>
    /// Launch a new terminal for the specified agent.
    /// </summary>
    public void Launch(string agentName, string? workingDirectory = null)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                LaunchWindows(agentName, workingDirectory);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (!TryLaunchTerminals(MacTerminals, agentName, workingDirectory))
                {
                    throw new InvalidOperationException("No terminal found");
                }
            }
            else
            {
                if (!TryLaunchTerminals(LinuxTerminals, agentName, workingDirectory))
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
    public void LaunchWindows(string agentName, string? workingDirectory = null)
    {
        // Try Windows Terminal first (modern)
        try
        {
            var wtDirArg = workingDirectory != null
                ? $"--startingDirectory \"{workingDirectory}\" "
                : "";
            var psi = new ProcessStartInfo
            {
                FileName = "wt",
                Arguments = $"new-tab {wtDirArg}powershell {GetWindowsArguments(agentName)}",
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

        // Fall back to PowerShell
        var fallbackPsi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = GetWindowsArguments(agentName),
            UseShellExecute = true
        };
        if (workingDirectory != null)
            fallbackPsi.WorkingDirectory = workingDirectory;
        _processStarter.Start(fallbackPsi);
    }

    /// <summary>
    /// Try to launch one of the configured terminals.
    /// </summary>
    public bool TryLaunchTerminals(TerminalConfig[] terminals, string agentName, string? workingDirectory = null)
    {
        foreach (var terminal in terminals)
        {
            try
            {
                _processStarter.Start(new ProcessStartInfo
                {
                    FileName = terminal.FileName,
                    Arguments = terminal.GetArguments(agentName, workingDirectory),
                    UseShellExecute = true
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
}
