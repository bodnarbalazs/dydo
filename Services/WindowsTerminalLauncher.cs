namespace DynaDocs.Services;

using System.Diagnostics;
using DynaDocs.Commands;

public static class WindowsTerminalLauncher
{
    // Disable focus event reporting that Claude Code leaves enabled on exit
    private const string TerminalReset = "; [Console]::Write([char]27 + '[?1004l')";

    // #0197 (F13): re-source the user's PowerShell profiles *inside* -Command, after the
    // DYDO_AGENT pin. Paired with -NoProfile this closes the pre-profile inheritance window:
    // nothing runs before the pin, yet profile customisations still load. Mirrors PowerShell's
    // native four-file load order; each load is try/guarded so a throwing profile cannot
    // abort the dispatch.
    private const string ProfileReSource =
        "foreach($__dydoP in $PROFILE.AllUsersAllHosts,$PROFILE.AllUsersCurrentHost," +
        "$PROFILE.CurrentUserAllHosts,$PROFILE.CurrentUserCurrentHost){" +
        "if($__dydoP -and (Test-Path -LiteralPath $__dydoP)){" +
        "try{. $__dydoP}catch{Write-Warning ('dydo: profile load failed: ' + $_)}}}; ";

    public static string GetArguments(string agentName, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null, string? workingDirectory = null, string host = "claude")
    {
        var prompt = TerminalLauncher.GetClaudePrompt(agentName);
        var escapedPrompt = prompt.Replace("'", "''");
        var executable = TerminalLauncher.GetLaunchExecutable(host);
        // Codex launch posture (issue 0253) sits between the executable and the prompt; empty for claude.
        var codexPosture = TerminalLauncher.CodexLaunchPosture(host);
        var launchInvocation = TerminalLauncher.PowerShellExecutableInvocation(executable, $"{codexPosture}'{escapedPrompt}'");
        var postClaudeCheck = autoClose
            ? $"; if ((dydo agent status {agentName} 2>&1) -match 'free') {{ exit 0 }}"
            : "";
        // Always -NoExit. PowerShell's -NoExit only suppresses the implicit end-of-`-Command`
        // exit; explicit `exit 0` in postClaudeCheck still terminates on the free path.
        // On any other exit (claude crash, /exit, watchdog kill, context limit), the script
        // body completes without `exit 0` and -NoExit keeps the terminal open with the claude
        // output visible — diagnostic mirror of the Linux `exec bash` fallback. (issue #0124)
        // -NoProfile: PowerShell would otherwise run profile scripts BEFORE -Command, letting
        // them observe a stale inherited DYDO_AGENT (#0197/F13). With -NoProfile nothing runs
        // pre-Command; agentEnv pins DYDO_AGENT first, then re-sources the profiles itself.
        var noExitFlag = "-NoProfile -NoExit ";

        var agentEnv = $"$env:DYDO_AGENT='{agentName.Replace("'", "''")}'; " + ProfileReSource;
        var windowEnv = windowName != null
            ? $"$env:DYDO_WINDOW='{windowName.Replace("'", "''")}'; "
            : "";

        // Worktree is already created by DispatchService before terminal launch.
        // Terminal script only cd's into it, creates junctions, and sets up cleanup.
        if (worktreeId != null)
        {
            if (mainProjectRoot != null)
            {
                var escapedRoot = mainProjectRoot.Replace("'", "''");
                var wtDir = $"'{escapedRoot}/dydo/_system/.local/worktrees/{worktreeId}'";
                return $"{noExitFlag}-Command \"{agentEnv}{windowEnv}" +
                       $"Set-Location {wtDir}; " +
                       WorktreeCommand.GeneratePsJunctionScript(escapedRoot, isVariable: false) +
                       $"try {{ dydo worktree init-settings --main-root '{escapedRoot}' }} catch {{ Write-Warning ('init-settings failed: ' + $_) }}; " +
                       $"Start-Sleep -Seconds 1; " +
                       $"try {{ Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; {launchInvocation}{TerminalReset}{postClaudeCheck} }} " +
                       $"finally {{ Set-Location '{escapedRoot}'; dydo worktree cleanup {worktreeId} --agent {agentName} }}\"";
            }

            var wtDirRel = $"dydo/_system/.local/worktrees/{worktreeId}";
            return $"{noExitFlag}-Command \"{agentEnv}{windowEnv}$_wt_root = Get-Location; " +
                   $"Set-Location {wtDirRel}; " +
                   WorktreeCommand.GeneratePsJunctionScript("$_wt_root.Path", isVariable: true) +
                   $"try {{ dydo worktree init-settings --main-root $_wt_root.Path }} catch {{ Write-Warning ('init-settings failed: ' + $_) }}; " +
                   $"Start-Sleep -Seconds 1; " +
                   $"try {{ Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; {launchInvocation}{TerminalReset}{postClaudeCheck} }} " +
                   $"finally {{ Set-Location $_wt_root; dydo worktree cleanup {worktreeId} --agent {agentName} }}\"";
        }

        // Inherited worktree: no creation, but cd to worktree, init-settings, sleep, cleanup on exit
        if (worktreeId == null && cleanupWorktreeId != null && mainProjectRoot != null)
        {
            var escapedRoot = mainProjectRoot.Replace("'", "''");
            var setLocation = workingDirectory != null
                ? $"Set-Location '{workingDirectory.Replace("'", "''")}'; "
                : "";
            return $"{noExitFlag}-Command \"{agentEnv}{windowEnv}" +
                   $"{setLocation}" +
                   $"try {{ dydo worktree init-settings --main-root '{escapedRoot}' }} catch {{ Write-Warning ('init-settings failed: ' + $_) }}; " +
                   $"Start-Sleep -Seconds 1; " +
                   $"try {{ Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; {launchInvocation}{TerminalReset}{postClaudeCheck} }} " +
                   $"finally {{ Set-Location '{escapedRoot}'; dydo worktree cleanup {cleanupWorktreeId} --agent {agentName} }}\"";
        }

        return $"{noExitFlag}-Command \"{agentEnv}{windowEnv}Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; {launchInvocation}{TerminalReset}{postClaudeCheck}\"";
    }

    public static string GetResumeArguments(string agentName, string sessionId, string? workingDirectory = null,
        string? worktreeId = null, string? mainProjectRoot = null, string host = "claude")
    {
        var escapedSession = sessionId.Replace("'", "''");
        var escapedPrompt = TerminalLauncher.ResumeContinuationPrompt.Replace("'", "''");
        var executable = TerminalLauncher.GetLaunchExecutable(host);
        // Codex launch posture (issue 0253) precedes the resume subcommand; empty for claude.
        var codexPosture = TerminalLauncher.CodexLaunchPosture(host);
        var resumeInvocation = TerminalLauncher.PowerShellExecutableInvocation(
            executable, $"{codexPosture}{TerminalLauncher.ResumeArgumentToken(host)} '{escapedSession}' '{escapedPrompt}'");
        // #0197 (F13): pin DYDO_AGENT first, then re-source profiles — same as GetArguments.
        var agentEnv = $"$env:DYDO_AGENT='{agentName.Replace("'", "''")}'; " + ProfileReSource;
        // #0207: no shell-spawned `dydo wait` re-arm here. Such a wait is a sibling of
        // `claude`, never a descendant, so it can never pass the F11 ownership gate —
        // it failed silently on every resume. How a resumed agent arms its own general
        // wait is handled separately (#0207 part 2).
        var resumeBody = $"Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; " +
                         $"{resumeInvocation}{TerminalReset}";

        // Symmetry with GetArguments worktree path (#0175): wrap the resume body in
        // Set-Location → junctions → init-settings → try/finally cleanup so the
        // resumed claude lands in the worktree with the same environment the
        // dispatcher tab had — and the worktree is cleaned up on its release.
        if (worktreeId != null && mainProjectRoot != null)
        {
            var escapedRoot = mainProjectRoot.Replace("'", "''");
            var wtDir = $"'{escapedRoot}/dydo/_system/.local/worktrees/{worktreeId}'";
            return $"-NoProfile -NoExit -Command \"{agentEnv}" +
                   $"Set-Location {wtDir}; " +
                   WorktreeCommand.GeneratePsJunctionScript(escapedRoot, isVariable: false) +
                   $"try {{ dydo worktree init-settings --main-root '{escapedRoot}' }} catch {{ Write-Warning ('init-settings failed: ' + $_) }}; " +
                   $"Start-Sleep -Seconds 1; " +
                   $"try {{ {resumeBody} }} " +
                   $"finally {{ Set-Location '{escapedRoot}'; dydo worktree cleanup {worktreeId} --agent {agentName} }}\"";
        }

        return $"-NoProfile -NoExit -Command \"{agentEnv}{resumeBody}\"";
    }

    public static int LaunchResume(IProcessStarter processStarter, string agentName, string sessionId,
        string? workingDirectory = null, string? windowName = null, bool useTab = false,
        string? worktreeId = null, string? mainProjectRoot = null, string host = "claude")
    {
        var shell = ProcessUtils.ResolvePowerShell();
        var launchHost = TerminalLauncher.NormalizeLaunchHost(host);

        try
        {
            // #0144: prefer the persisted dispatcher window-name so the resumed claude
            // lands as a new tab in the original wt window. Falls back to the prior
            // fresh-GUID-window behaviour for older state.md and fresh-window dispatches.
            string wtAction;
            if (useTab && windowName != null)
                wtAction = $"-w {windowName} new-tab";
            else
                wtAction = $"--window {Guid.NewGuid().ToString("N")[..8]} new-tab";
            var wtDirArg = workingDirectory != null
                ? $"--startingDirectory \"{workingDirectory}\" "
                : "";
            var psi = new ProcessStartInfo
            {
                FileName = "wt",
                Arguments = $"{wtAction} {wtDirArg}{shell} {GetResumeArguments(agentName, sessionId, workingDirectory, worktreeId, mainProjectRoot, launchHost).Replace(";", "\\;")}",
                UseShellExecute = true
            };
            if (workingDirectory != null)
                psi.WorkingDirectory = workingDirectory;
            return processStarter.Start(psi);
        }
        catch
        {
        }

        var fallbackPsi = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = GetResumeArguments(agentName, sessionId, workingDirectory, worktreeId, mainProjectRoot, launchHost),
            UseShellExecute = true
        };
        if (workingDirectory != null)
            fallbackPsi.WorkingDirectory = workingDirectory;
        return processStarter.Start(fallbackPsi);
    }

    public static int Launch(IProcessStarter processStarter, string agentName, string? workingDirectory = null,
        bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null, string host = "claude")
    {
        var shell = ProcessUtils.ResolvePowerShell();
        var launchHost = TerminalLauncher.NormalizeLaunchHost(host);

        // Try Windows Terminal first (modern)
        try
        {
            string wtAction;
            if (useTab)
            {
                wtAction = windowName != null ? $"-w {windowName} new-tab" : "-w 0 new-tab";
            }
            else
            {
                windowName ??= Guid.NewGuid().ToString("N")[..8];
                wtAction = $"--window {windowName} new-tab";
            }

            var wtDirArg = workingDirectory != null
                ? $"--startingDirectory \"{workingDirectory}\" "
                : "";
            var psi = new ProcessStartInfo
            {
                FileName = "wt",
                Arguments = $"{wtAction} {wtDirArg}{shell} {GetArguments(agentName, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, workingDirectory, launchHost).Replace(";", "\\;")}",
                UseShellExecute = true
            };
            if (workingDirectory != null)
                psi.WorkingDirectory = workingDirectory;
            return processStarter.Start(psi);
        }
        catch
        {
            // Windows Terminal not available, fall back to PowerShell
        }

        // Fall back to PowerShell (no tab support)
        var fallbackPsi = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = GetArguments(agentName, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, workingDirectory, launchHost),
            UseShellExecute = true
        };
        if (workingDirectory != null)
            fallbackPsi.WorkingDirectory = workingDirectory;
        return processStarter.Start(fallbackPsi);
    }

    // #0197 (F13): DYDO_AGENT is pinned as the first -Command statement under -NoProfile;
    // profiles are then re-sourced (ProfileReSource) so they observe the correct value.
    // ProcessStartInfo is deliberately left UseShellExecute=true with no psi.Environment —
    // that combination throws in Process.Start.
}
