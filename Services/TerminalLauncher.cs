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

    public record TerminalConfig(string FileName, Func<string, string> GetArguments);

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
        new("gnome-terminal", agentName => $"-- bash -c \"claude '{agentName} --inbox'; exec bash\""),
        new("konsole", agentName => $"-e bash -c \"claude '{agentName} --inbox'; exec bash\""),
        new("xfce4-terminal", agentName => $"-e \"bash -c \\\"claude '{agentName} --inbox'; exec bash\\\"\""),

        // Popular third-party terminals
        new("alacritty", agentName => $"-e bash -c \"claude '{agentName} --inbox'; exec bash\""),
        new("kitty", agentName => $"bash -c \"claude '{agentName} --inbox'; exec bash\""),
        new("wezterm", agentName => $"start -- bash -c \"claude '{agentName} --inbox'; exec bash\""),
        new("tilix", agentName => $"-e \"bash -c \\\"claude '{agentName} --inbox'; exec bash\\\"\""),

        // Wayland-native
        new("foot", agentName => $"bash -c \"claude '{agentName} --inbox'; exec bash\""),

        // Fallback (usually available)
        new("xterm", agentName => $"-e bash -c \"claude '{agentName} --inbox'; exec bash\""),
    ];

    public static readonly TerminalConfig[] MacTerminals =
    [
        // Terminal.app is always present on macOS
        new("osascript", agentName => $"-e 'tell app \"Terminal\" to do script \"claude \\\"{agentName} --inbox\\\"\"'"),
    ];

    public static string GetWindowsArguments(string agentName)
    {
        var prompt = GetClaudePrompt(agentName);
        // -NoExit keeps PowerShell open after the command completes
        // The prompt is simple (AgentName --inbox) so minimal escaping needed
        return $"-NoExit -Command \"claude \"\"{prompt}\"\"\"";
    }

    public static string GetLinuxArguments(string terminalName, string agentName)
    {
        var config = LinuxTerminals.FirstOrDefault(t => t.FileName == terminalName);
        return config?.GetArguments(agentName) ?? throw new ArgumentException($"Unknown terminal: {terminalName}");
    }

    public static string GetMacArguments(string agentName)
    {
        return MacTerminals[0].GetArguments(agentName);
    }

    /// <summary>
    /// Static convenience method for backward compatibility.
    /// </summary>
    public static void LaunchNewTerminal(string agentName)
    {
        new TerminalLauncher().Launch(agentName);
    }

    /// <summary>
    /// Launch a new terminal for the specified agent.
    /// </summary>
    public void Launch(string agentName)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                LaunchWindows(agentName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (!TryLaunchTerminals(MacTerminals, agentName))
                {
                    throw new InvalidOperationException("No terminal found");
                }
            }
            else
            {
                if (!TryLaunchTerminals(LinuxTerminals, agentName))
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
    public void LaunchWindows(string agentName)
    {
        // Try Windows Terminal first (modern)
        try
        {
            _processStarter.Start(new ProcessStartInfo
            {
                FileName = "wt",
                Arguments = $"new-tab powershell {GetWindowsArguments(agentName)}",
                UseShellExecute = true
            });
            return;
        }
        catch
        {
            // Windows Terminal not available, fall back to PowerShell
        }

        // Fall back to PowerShell
        _processStarter.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = GetWindowsArguments(agentName),
            UseShellExecute = true
        });
    }

    /// <summary>
    /// Try to launch one of the configured terminals.
    /// </summary>
    public bool TryLaunchTerminals(TerminalConfig[] terminals, string agentName)
    {
        foreach (var terminal in terminals)
        {
            try
            {
                _processStarter.Start(new ProcessStartInfo
                {
                    FileName = terminal.FileName,
                    Arguments = terminal.GetArguments(agentName),
                    UseShellExecute = true
                });
                return true;
            }
            catch
            {
                // Try next terminal
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
