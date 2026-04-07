namespace DynaDocs.Services;

using System.Diagnostics;

public static class MacTerminalLauncher
{
    // Disable focus event reporting that Claude Code leaves enabled on exit
    // Double-escaped: \\\" → \" in output → " after AppleScript; \\\\ → \\ → \ after AppleScript
    private const string TerminalReset = "; printf \\\"\\\\e[?1004l\\\"";

    private static (string shellCommand, string postCheck) BuildShellComponents(string agentName, string? workingDirectory,
        bool autoClose, string? worktreeId, string? windowName, string? cleanupWorktreeId, string? mainProjectRoot)
    {
        var cdPrefix = TerminalLauncher.CdPrefix(workingDirectory);
        var agentExport = $"export DYDO_AGENT={agentName}; ";
        var windowExport = windowName != null ? $"export DYDO_WINDOW={windowName}; " : "";

        string wtSetup = "", wtCleanup = "";
        if (worktreeId != null)
        {
            wtSetup = TerminalLauncher.WorktreeSetupScript(worktreeId, mainProjectRoot);
            wtCleanup = "; " + TerminalLauncher.WorktreeCleanupScript(worktreeId, agentName);
        }
        else if (cleanupWorktreeId != null && mainProjectRoot != null)
        {
            wtSetup = TerminalLauncher.WorktreeInheritedSetupScript(mainProjectRoot, workingDirectory);
            wtCleanup = $"; cd '{mainProjectRoot}' && {TerminalLauncher.WorktreeCleanupScript(cleanupWorktreeId, agentName)}";
        }

        var shellCommand = $"{cdPrefix}{agentExport}{windowExport}{wtSetup}unset CLAUDECODE; claude \\\"{agentName} --inbox\\\"{TerminalReset}";
        var postCheck = wtCleanup + (autoClose ? $"; {TerminalLauncher.BashPostClaudeCheck(agentName)}" : "");

        return (shellCommand, postCheck);
    }

    public static string GetArguments(string agentName, string? workingDirectory = null,
        bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
    {
        var (shellCommand, postCheck) = BuildShellComponents(agentName, workingDirectory, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot);
        return $"-e 'tell app \"Terminal\" to do script \"{shellCommand}{postCheck}\"'";
    }

    public static int Launch(IProcessStarter processStarter, ITerminalDetector terminalDetector,
        string agentName, string? workingDirectory = null, bool useTab = false,
        bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
    {
        var (shellCommand, postCheck) = BuildShellComponents(agentName, workingDirectory, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot);

        var runningTerminal = terminalDetector.GetRunningTerminal();
        var useITerm = runningTerminal == "iTerm"
            || (runningTerminal == null && useTab && terminalDetector.IsAvailable("iTerm"));

        string script;
        if (useITerm)
        {
            script = useTab
                ? GetITermTabScript(shellCommand, postCheck)
                : GetITermWindowScript(shellCommand, postCheck);
        }
        else
        {
            if (useTab)
                Console.WriteLine("INFO: Terminal.app does not support tab creation via scripting. Launching new window instead. Install iTerm2 for tab support.");
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

        return processStarter.Start(psi);
    }

    public static string GetITermWindowScript(string shellCommand, string postCheck)
    {
        var cmd = $"{shellCommand}{postCheck}";
        return "tell application \"iTerm\"\n" +
               "  create window with default profile\n" +
               "  tell current session of current window\n" +
               $"    write text \"{cmd}\"\n" +
               "  end tell\n" +
               "  activate\n" +
               "end tell";
    }

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
}
