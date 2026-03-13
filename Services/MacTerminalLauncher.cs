namespace DynaDocs.Services;

using System.Diagnostics;

public static class MacTerminalLauncher
{
    public static string GetArguments(string agentName, string? workingDirectory = null,
        bool autoClose = false, string? worktreeId = null, string? windowName = null)
    {
        var cdPrefix = TerminalLauncher.CdPrefix(workingDirectory);
        var windowExport = windowName != null ? $"export DYDO_WINDOW={windowName}; " : "";

        string wtSetup = "", wtCleanup = "";
        if (worktreeId != null)
        {
            wtSetup = TerminalLauncher.WorktreeSetupScript(worktreeId);
            wtCleanup = "; " + TerminalLauncher.WorktreeCleanupScript(worktreeId, agentName);
        }

        var postClaude = wtCleanup + (autoClose ? $"; {BashPostClaudeCheck(agentName)}" : "");

        return $"-e 'tell app \"Terminal\" to do script \"{cdPrefix}{windowExport}{wtSetup}unset CLAUDECODE; claude \\\"{agentName} --inbox\\\"{postClaude}\"'";
    }

    public static void Launch(IProcessStarter processStarter, ITerminalDetector terminalDetector,
        string agentName, string? workingDirectory = null, bool useTab = false,
        bool autoClose = false, string? worktreeId = null, string? windowName = null)
    {
        var cdPrefix = TerminalLauncher.CdPrefix(workingDirectory);
        var windowExport = windowName != null ? $"export DYDO_WINDOW={windowName}; " : "";

        string wtSetup = "", wtCleanup = "";
        if (worktreeId != null)
        {
            wtSetup = TerminalLauncher.WorktreeSetupScript(worktreeId);
            wtCleanup = "; " + TerminalLauncher.WorktreeCleanupScript(worktreeId, agentName);
        }

        var shellCommand = $"{cdPrefix}{windowExport}{wtSetup}unset CLAUDECODE; claude \\\"{agentName} --inbox\\\"";
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
