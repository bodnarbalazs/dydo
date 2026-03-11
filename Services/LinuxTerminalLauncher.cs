namespace DynaDocs.Services;

using System.Diagnostics;

public static class LinuxTerminalLauncher
{
    private static string BashPostClaudeCheck(string agentName) =>
        $"if dydo agent status {agentName} 2>/dev/null | grep -q 'free'; then exit 0; fi; exec bash";

    public static string GetArguments(string terminalName, string agentName, string? workingDirectory = null,
        bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null)
    {
        var config = TerminalLauncher.LinuxTerminals.FirstOrDefault(t => t.FileName == terminalName);
        if (config == null) throw new ArgumentException($"Unknown terminal: {terminalName}");

        var args = (useTab && config.GetTabArguments != null)
            ? config.GetTabArguments(agentName, workingDirectory)
            : config.GetArguments(agentName, workingDirectory);

        if (windowName != null)
            args = args.Replace("unset CLAUDECODE", $"export DYDO_WINDOW='{windowName}'; unset CLAUDECODE");

        if (worktreeId != null)
        {
            args = args.Replace("unset CLAUDECODE", TerminalLauncher.WorktreeSetupScript(worktreeId) + "unset CLAUDECODE");
            args = args.Replace("exec bash", TerminalLauncher.WorktreeCleanupScript(worktreeId) + "; exec bash");
        }

        if (autoClose)
            args = args.Replace("exec bash", BashPostClaudeCheck(agentName));

        return args;
    }

    public static bool TryLaunch(IProcessStarter processStarter, TerminalLauncher.TerminalConfig[] terminals,
        string agentName, string? workingDirectory = null, bool useTab = false,
        bool autoClose = false, string? worktreeId = null, string? windowName = null)
    {
        foreach (var terminal in terminals)
        {
            try
            {
                var arguments = useTab && terminal.GetTabArguments != null
                    ? terminal.GetTabArguments(agentName, workingDirectory)
                    : terminal.GetArguments(agentName, workingDirectory);

                if (windowName != null)
                    arguments = arguments.Replace("unset CLAUDECODE", $"export DYDO_WINDOW='{windowName}'; unset CLAUDECODE");

                if (worktreeId != null)
                {
                    arguments = arguments.Replace("unset CLAUDECODE", TerminalLauncher.WorktreeSetupScript(worktreeId) + "unset CLAUDECODE");
                    arguments = arguments.Replace("exec bash", TerminalLauncher.WorktreeCleanupScript(worktreeId) + "; exec bash");
                }

                if (autoClose)
                    arguments = arguments.Replace("exec bash", BashPostClaudeCheck(agentName));

                processStarter.Start(new ProcessStartInfo
                {
                    FileName = terminal.FileName,
                    Arguments = arguments,
                    UseShellExecute = false
                });
                return true;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch
            {
                // Terminal not found — try next one
            }
        }
        return false;
    }
}
