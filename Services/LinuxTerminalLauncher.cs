namespace DynaDocs.Services;

using System.Diagnostics;

public static class LinuxTerminalLauncher
{
    private static string ApplyOverrides(string baseArgs, string agentName,
        bool autoClose, string? worktreeId, string? windowName, string? cleanupWorktreeId, string? mainProjectRoot, string? workingDirectory)
    {
        var args = baseArgs;
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
            var cleanup = $"cd '{TerminalLauncher.BashSingleQuoteEscape(mainProjectRoot)}' && {TerminalLauncher.WorktreeCleanupScript(cleanupWorktreeId, agentName)}";
            args = args.Replace("exec bash", cleanup + "; exec bash");
        }

        if (autoClose)
            args = args.Replace("exec bash", TerminalLauncher.BashPostClaudeCheck(agentName));

        return args;
    }

    public static string GetArguments(string terminalName, string agentName, string? workingDirectory = null,
        bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
    {
        var config = TerminalLauncher.LinuxTerminals.FirstOrDefault(t => t.FileName == terminalName);
        if (config == null) throw new ArgumentException($"Unknown terminal: {terminalName}");

        var baseArgs = (useTab && config.GetTabArguments != null)
            ? config.GetTabArguments(agentName, workingDirectory)
            : config.GetArguments(agentName, workingDirectory);

        return ApplyOverrides(baseArgs, agentName, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, workingDirectory);
    }

    /// <summary>
    /// Body that replaces the fresh-launch <c>unset CLAUDECODE; … ; exec bash</c>
    /// segment in any LinuxTerminals entry. The cd-prefix and DYDO_* exports come
    /// from the surrounding base args (preserved); we only swap in the resume
    /// claude invocation plus a backgrounded <c>dydo wait</c>.
    /// </summary>
    internal static string BuildResumeBashCommand(string agentName, string sessionId)
    {
        var escapedSession = sessionId.Replace("'", "'\\''");
        var escapedPrompt = TerminalLauncher.BashSingleQuoteEscape(TerminalLauncher.ResumeContinuationPrompt);
        return $"export DYDO_AGENT='{agentName}'; unset CLAUDECODE; " +
               $"(dydo wait >/dev/null 2>&1 &) ; " +
               $"claude --resume '{escapedSession}' '{escapedPrompt}'; " +
               $"printf '\\e[?1004l'; exec bash";
    }

    /// <summary>
    /// Reuses each terminal's existing window-mode arg builder by substituting the
    /// fresh-launch bash body with our resume bash body. Sentinel = the `claude '…
    /// --inbox'` token the existing LinuxTerminals entries always emit. Avoids a
    /// parallel 9-arm switch (CRAP) that mirrors the existing config table.
    /// </summary>
    public static string GetResumeArguments(string terminalName, string agentName, string sessionId, string? workingDirectory = null)
    {
        var config = TerminalLauncher.LinuxTerminals.FirstOrDefault(t => t.FileName == terminalName);
        if (config == null) throw new ArgumentException($"Unknown terminal: {terminalName}");
        return SwapInResumeBody(config.GetArguments(agentName, workingDirectory),
            BuildResumeBashCommand(agentName, sessionId));
    }

    private static string SwapInResumeBody(string baseArgs, string resumeBashBody)
    {
        // The fresh-launch bash body always ends with `exec bash` and starts with the
        // CLAUDECODE unset; the inner content is everything between those anchors.
        // Replace from `unset CLAUDECODE` to `exec bash` (inclusive) with the resume body.
        const string startAnchor = "unset CLAUDECODE";
        const string endAnchor = "exec bash";
        var startIdx = baseArgs.IndexOf(startAnchor, StringComparison.Ordinal);
        var endIdx = baseArgs.IndexOf(endAnchor, StringComparison.Ordinal);
        if (startIdx < 0 || endIdx < 0) return baseArgs; // shape changed; degrade gracefully
        return baseArgs[..startIdx] + resumeBashBody + baseArgs[(endIdx + endAnchor.Length)..];
    }

    public static int TryLaunchResume(IProcessStarter processStarter, TerminalLauncher.TerminalConfig[] terminals,
        string agentName, string sessionId, string? workingDirectory = null)
    {
        var resumeBody = BuildResumeBashCommand(agentName, sessionId);
        foreach (var terminal in terminals)
        {
            try
            {
                var arguments = SwapInResumeBody(terminal.GetArguments(agentName, workingDirectory), resumeBody);
                return processStarter.Start(new ProcessStartInfo
                {
                    FileName = terminal.FileName,
                    Arguments = arguments,
                    UseShellExecute = false
                });
            }
            catch (ArgumentException) { throw; }
            catch { /* terminal not found — try next */ }
        }
        return 0;
    }

    public static int TryLaunch(IProcessStarter processStarter, TerminalLauncher.TerminalConfig[] terminals,
        string agentName, string? workingDirectory = null, bool useTab = false,
        bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
    {
        foreach (var terminal in terminals)
        {
            try
            {
                var baseArgs = useTab && terminal.GetTabArguments != null
                    ? terminal.GetTabArguments(agentName, workingDirectory)
                    : terminal.GetArguments(agentName, workingDirectory);

                var arguments = ApplyOverrides(baseArgs, agentName, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, workingDirectory);

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
