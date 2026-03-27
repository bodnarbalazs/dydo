namespace DynaDocs.Services;

using System.Diagnostics;

public static class LinuxTerminalLauncher
{
    private static string BashPostClaudeCheck(string agentName) =>
        $"if dydo agent status {agentName} 2>/dev/null | grep -q 'free'; then exit 0; fi; exec bash";

    public static string GetArguments(string terminalName, string agentName, string? workingDirectory = null,
        bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
    {
        var config = TerminalLauncher.LinuxTerminals.FirstOrDefault(t => t.FileName == terminalName);
        if (config == null) throw new ArgumentException($"Unknown terminal: {terminalName}");

        var args = (useTab && config.GetTabArguments != null)
            ? config.GetTabArguments(agentName, workingDirectory)
            : config.GetArguments(agentName, workingDirectory);

        args = args.Replace("unset CLAUDECODE", $"export DYDO_AGENT='{agentName}'; unset CLAUDECODE");

        if (windowName != null)
            args = args.Replace("unset CLAUDECODE", $"export DYDO_WINDOW='{windowName}'; unset CLAUDECODE");

        if (worktreeId != null)
        {
            args = args.Replace("unset CLAUDECODE", TerminalLauncher.WorktreeSetupScript(worktreeId, mainProjectRoot) + "unset CLAUDECODE");
            args = args.Replace("exec bash", TerminalLauncher.WorktreeCleanupScript(worktreeId, agentName) + "; exec bash");
        }
        else if (cleanupWorktreeId != null && mainProjectRoot != null)
        {
            args = args.Replace("unset CLAUDECODE", TerminalLauncher.WorktreeInheritedSetupScript(mainProjectRoot, workingDirectory) + "unset CLAUDECODE");
            var cleanup = $"cd '{mainProjectRoot}' && {TerminalLauncher.WorktreeCleanupScript(cleanupWorktreeId, agentName)}";
            args = args.Replace("exec bash", cleanup + "; exec bash");
        }

        if (autoClose)
            args = args.Replace("exec bash", BashPostClaudeCheck(agentName));

        return args;
    }

    public static int TryLaunch(IProcessStarter processStarter, TerminalLauncher.TerminalConfig[] terminals,
        string agentName, string? workingDirectory = null, bool useTab = false,
        bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
    {
        foreach (var terminal in terminals)
        {
            try
            {
                var arguments = useTab && terminal.GetTabArguments != null
                    ? terminal.GetTabArguments(agentName, workingDirectory)
                    : terminal.GetArguments(agentName, workingDirectory);

                arguments = arguments.Replace("unset CLAUDECODE", $"export DYDO_AGENT='{agentName}'; unset CLAUDECODE");

                if (windowName != null)
                    arguments = arguments.Replace("unset CLAUDECODE", $"export DYDO_WINDOW='{windowName}'; unset CLAUDECODE");

                if (worktreeId != null)
                {
                    arguments = arguments.Replace("unset CLAUDECODE", TerminalLauncher.WorktreeSetupScript(worktreeId, mainProjectRoot) + "unset CLAUDECODE");
                    arguments = arguments.Replace("exec bash", TerminalLauncher.WorktreeCleanupScript(worktreeId, agentName) + "; exec bash");
                }
                else if (cleanupWorktreeId != null && mainProjectRoot != null)
                {
                    arguments = arguments.Replace("unset CLAUDECODE", TerminalLauncher.WorktreeInheritedSetupScript(mainProjectRoot, workingDirectory) + "unset CLAUDECODE");
                    var cleanup = $"cd '{mainProjectRoot}' && {TerminalLauncher.WorktreeCleanupScript(cleanupWorktreeId, agentName)}";
                    arguments = arguments.Replace("exec bash", cleanup + "; exec bash");
                }

                if (autoClose)
                    arguments = arguments.Replace("exec bash", BashPostClaudeCheck(agentName));

                return processStarter.Start(new ProcessStartInfo
                {
                    FileName = terminal.FileName,
                    Arguments = arguments,
                    UseShellExecute = false
                });
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
        return 0;
    }
}
