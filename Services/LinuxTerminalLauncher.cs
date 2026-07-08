namespace DynaDocs.Services;

using System.Diagnostics;

public static class LinuxTerminalLauncher
{
    private static string ApplyOverrides(string baseArgs, string agentName,
        bool autoClose, string? worktreeId, string? windowName, string? cleanupWorktreeId, string? mainProjectRoot, string? workingDirectory,
        string host = "claude")
    {
        var args = baseArgs;
        var executable = TerminalLauncher.GetLaunchExecutable(host);
        if (executable != "claude")
        {
            var invocation = TerminalLauncher.ShellExecutableToken(executable) + " ";
            args = args.Replace("claude ", invocation);
        }

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
        bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null,
        string host = "claude")
    {
        var config = TerminalLauncher.LinuxTerminals.FirstOrDefault(t => t.FileName == terminalName);
        if (config == null) throw new ArgumentException($"Unknown terminal: {terminalName}");

        var baseArgs = (useTab && config.GetTabArguments != null)
            ? config.GetTabArguments(agentName, workingDirectory)
            : config.GetArguments(agentName, workingDirectory);

        return ApplyOverrides(baseArgs, agentName, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, workingDirectory, host);
    }

    /// <summary>
    /// Body that replaces the fresh-launch <c>unset CLAUDECODE; … ; exec bash</c>
    /// segment in any LinuxTerminals entry. The cd-prefix and DYDO_* exports come
    /// from the surrounding base args (preserved); we only swap in the resume
    /// claude invocation. When worktreeId and mainProjectRoot are non-null the body
    /// is wrapped in the same setup/cleanup envelope as the original dispatch
    /// (Finding #4; closes #0175).
    /// </summary>
    internal static string BuildResumeBashCommand(string agentName, string sessionId,
        string? worktreeId = null, string? mainProjectRoot = null, string host = "claude")
    {
        var escapedSession = sessionId.Replace("'", "'\\''");
        var escapedPrompt = TerminalLauncher.BashSingleQuoteEscape(TerminalLauncher.ResumeContinuationPrompt);
        var executable = TerminalLauncher.GetLaunchExecutable(host);
        var executableToken = TerminalLauncher.ShellExecutableToken(executable);
        // #0207: no shell-spawned `dydo wait` re-arm — it is a sibling of `claude`, never
        // a descendant, so it cannot pass the F11 ownership gate and failed silently on
        // every resume. How a resumed agent arms its own wait is handled separately
        // (#0207 part 2).
        var resumeBody = $"export DYDO_AGENT='{agentName}'; unset CLAUDECODE; " +
                         $"{executableToken} {TerminalLauncher.ResumeArgumentToken(host)} '{escapedSession}' '{escapedPrompt}'; " +
                         $"printf '\\e[?1004l'";

        if (worktreeId != null && mainProjectRoot != null)
        {
            var escapedRoot = TerminalLauncher.BashSingleQuoteEscape(mainProjectRoot);
            return TerminalLauncher.WorktreeSetupScript(worktreeId, mainProjectRoot) +
                   resumeBody +
                   $"; cd '{escapedRoot}' && {TerminalLauncher.WorktreeCleanupScript(worktreeId, agentName)}" +
                   "; exec bash";
        }

        return resumeBody + "; exec bash";
    }

    /// <summary>
    /// Reuses each terminal's existing window-mode arg builder by substituting the
    /// fresh-launch bash body with our resume bash body. Sentinel = the `claude '…
    /// --inbox'` token the existing LinuxTerminals entries always emit. Avoids a
    /// parallel 9-arm switch (CRAP) that mirrors the existing config table.
    /// </summary>
    public static string GetResumeArguments(string terminalName, string agentName, string sessionId,
        string? workingDirectory = null, string? worktreeId = null, string? mainProjectRoot = null,
        string host = "claude")
    {
        var config = TerminalLauncher.LinuxTerminals.FirstOrDefault(t => t.FileName == terminalName);
        if (config == null) throw new ArgumentException($"Unknown terminal: {terminalName}");
        return SwapInResumeBody(config.GetArguments(agentName, workingDirectory),
            BuildResumeBashCommand(agentName, sessionId, worktreeId, mainProjectRoot, host));
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
        string agentName, string sessionId, string? workingDirectory = null,
        string? worktreeId = null, string? mainProjectRoot = null, string host = "claude")
    {
        var launchHost = TerminalLauncher.NormalizeLaunchHost(host);
        var resumeBody = BuildResumeBashCommand(agentName, sessionId, worktreeId, mainProjectRoot, launchHost);
        foreach (var terminal in terminals)
        {
            try
            {
                var arguments = SwapInResumeBody(terminal.GetArguments(agentName, workingDirectory), resumeBody);
                var psi = new ProcessStartInfo
                {
                    FileName = terminal.FileName,
                    Arguments = arguments,
                    UseShellExecute = false
                };
                psi.Environment["DYDO_AGENT"] = agentName;
                return processStarter.Start(psi);
            }
            catch (ArgumentException) { throw; }
            catch { /* terminal not found — try next */ }
        }
        return 0;
    }

    public static int TryLaunch(IProcessStarter processStarter, TerminalLauncher.TerminalConfig[] terminals,
        string agentName, string? workingDirectory = null, bool useTab = false,
        bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null,
        string host = "claude")
    {
        foreach (var terminal in terminals)
        {
            try
            {
                var baseArgs = useTab && terminal.GetTabArguments != null
                    ? terminal.GetTabArguments(agentName, workingDirectory)
                    : terminal.GetArguments(agentName, workingDirectory);

                var arguments = ApplyOverrides(baseArgs, agentName, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, workingDirectory, host);

                var psi = new ProcessStartInfo
                {
                    FileName = terminal.FileName,
                    Arguments = arguments,
                    UseShellExecute = false
                };
                // #0197 (F13): pin DYDO_AGENT on the child process so the OS-level inheritance
                // is fixed before the bash command's own `export DYDO_AGENT` runs. The in-shell
                // export remains as belt-and-suspenders — both set the same value.
                psi.Environment["DYDO_AGENT"] = agentName;
                return processStarter.Start(psi);
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
