namespace DynaDocs.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;

public class TerminalLauncher
{
    public record TerminalConfig(string FileName, Func<string, string> GetArguments);

    internal static readonly TerminalConfig[] LinuxTerminals =
    [
        // Modern terminals (most common on current distros)
        new("gnome-terminal", agentName => $"-- bash -c \"claude '--inbox {agentName}'; exec bash\""),
        new("konsole", agentName => $"-e bash -c \"claude '--inbox {agentName}'; exec bash\""),
        new("xfce4-terminal", agentName => $"-e \"bash -c \\\"claude '--inbox {agentName}'; exec bash\\\"\""),

        // Popular third-party terminals
        new("alacritty", agentName => $"-e bash -c \"claude '--inbox {agentName}'; exec bash\""),
        new("kitty", agentName => $"bash -c \"claude '--inbox {agentName}'; exec bash\""),
        new("wezterm", agentName => $"start -- bash -c \"claude '--inbox {agentName}'; exec bash\""),
        new("tilix", agentName => $"-e \"bash -c \\\"claude '--inbox {agentName}'; exec bash\\\"\""),

        // Wayland-native
        new("foot", agentName => $"bash -c \"claude '--inbox {agentName}'; exec bash\""),

        // Fallback (usually available)
        new("xterm", agentName => $"-e bash -c \"claude '--inbox {agentName}'; exec bash\""),
    ];

    internal static readonly TerminalConfig[] MacTerminals =
    [
        // Terminal.app is always present on macOS
        new("osascript", agentName => $"-e 'tell app \"Terminal\" to do script \"claude \\\"--inbox {agentName}\\\"\"'"),
    ];

    public static string GetWindowsArguments(string agentName)
    {
        // -NoExit keeps PowerShell open after the command completes
        return $"-NoExit -Command \"claude \"\"--inbox {agentName}\"\"\"";
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

    public static void LaunchNewTerminal(string agentName)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Try Windows Terminal first (modern), fall back to PowerShell
                if (!TryLaunchWindowsTerminal(agentName))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = GetWindowsArguments(agentName),
                        UseShellExecute = true
                    });
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (!TryLaunchTerminal(MacTerminals, agentName))
                {
                    throw new InvalidOperationException("No terminal found");
                }
            }
            else
            {
                if (!TryLaunchTerminal(LinuxTerminals, agentName))
                {
                    throw new InvalidOperationException("No terminal found");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARN: Could not launch terminal: {ex.Message}");
            Console.WriteLine($"Please manually run: claude \"--inbox {agentName}\"");
        }
    }

    private static bool TryLaunchTerminal(TerminalConfig[] terminals, string agentName)
    {
        foreach (var terminal in terminals)
        {
            try
            {
                Process.Start(new ProcessStartInfo
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

    private static bool TryLaunchWindowsTerminal(string agentName)
    {
        try
        {
            // Windows Terminal (wt) with new tab running PowerShell
            Process.Start(new ProcessStartInfo
            {
                FileName = "wt",
                Arguments = $"new-tab powershell {GetWindowsArguments(agentName)}",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            // Windows Terminal not available
            return false;
        }
    }
}
