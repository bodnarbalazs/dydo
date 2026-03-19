namespace DynaDocs.Services;

using System.Diagnostics;

public static class MacTerminalLauncher
{
    // Disable focus event reporting that Claude Code leaves enabled on exit
    // Double-escaped: \\\" → \" in output → " after AppleScript; \\\\ → \\ → \ after AppleScript
    private const string TerminalReset = "; printf \\\"\\\\e[?1004l\\\"";

    public static string GetArguments(string agentName, string? workingDirectory = null,
        bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
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
            wtCleanup = $"; cd '{mainProjectRoot}' && {TerminalLauncher.WorktreeCleanupScript(cleanupWorktreeId, agentName)}";
        }

        var postClaude = wtCleanup + (autoClose ? $"; {BashPostClaudeCheck(agentName)}" : "");

        return $"-e 'tell app \"Terminal\" to do script \"{cdPrefix}{agentExport}{windowExport}{wtSetup}unset CLAUDECODE; claude \\\"{agentName} --inbox\\\"{TerminalReset}{postClaude}\"'";
    }

    public static void Launch(IProcessStarter processStarter, ITerminalDetector terminalDetector,
        string agentName, string? workingDirectory = null, bool useTab = false,
        bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
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
            wtCleanup = $"; cd '{mainProjectRoot}' && {TerminalLauncher.WorktreeCleanupScript(cleanupWorktreeId, agentName)}";
        }

        var shellCommand = $"{cdPrefix}{agentExport}{windowExport}{wtSetup}unset CLAUDECODE; claude \\\"{agentName} --inbox\\\"{TerminalReset}";
        var postCheck = wtCleanup + (autoClose ? $"; {BashPostClaudeCheck(agentName)}" : "");

        string script;
        if (useTab && terminalDetector.IsAvailable("iTerm"))
        {
            script = GetITermTabScript(shellCommand, postCheck);
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

        processStarter.Start(psi);
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

    private static string BashPostClaudeCheck(string agentName) =>
        $"if dydo agent status {agentName} 2>/dev/null | grep -q 'free'; then exit 0; fi; exec bash";
}
