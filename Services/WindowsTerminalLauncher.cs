namespace DynaDocs.Services;

using System.Diagnostics;
using DynaDocs.Commands;

public static class WindowsTerminalLauncher
{
    // Disable focus event reporting that Claude Code leaves enabled on exit
    private const string TerminalReset = "; [Console]::Write([char]27 + '[?1004l')";

    public static string GetArguments(string agentName, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null, string? workingDirectory = null)
    {
        var prompt = TerminalLauncher.GetClaudePrompt(agentName);
        var escapedPrompt = prompt.Replace("'", "''");
        var postClaudeCheck = autoClose
            ? $"; if ((dydo agent status {agentName} 2>&1) -match 'free') {{ exit 0 }}"
            : "";
        // Always -NoExit. PowerShell's -NoExit only suppresses the implicit end-of-`-Command`
        // exit; explicit `exit 0` in postClaudeCheck still terminates on the free path.
        // On any other exit (claude crash, /exit, watchdog kill, context limit), the script
        // body completes without `exit 0` and -NoExit keeps the terminal open with the claude
        // output visible — diagnostic mirror of the Linux `exec bash` fallback. (issue #0124)
        var noExitFlag = "-NoExit ";

        var agentEnv = $"$env:DYDO_AGENT='{agentName.Replace("'", "''")}'; ";
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
                       $"try {{ Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; claude '{escapedPrompt}'{TerminalReset}{postClaudeCheck} }} " +
                       $"finally {{ Set-Location '{escapedRoot}'; dydo worktree cleanup {worktreeId} --agent {agentName} }}\"";
            }

            var wtDirRel = $"dydo/_system/.local/worktrees/{worktreeId}";
            return $"{noExitFlag}-Command \"{agentEnv}{windowEnv}$_wt_root = Get-Location; " +
                   $"Set-Location {wtDirRel}; " +
                   WorktreeCommand.GeneratePsJunctionScript("$_wt_root.Path", isVariable: true) +
                   $"try {{ dydo worktree init-settings --main-root $_wt_root.Path }} catch {{ Write-Warning ('init-settings failed: ' + $_) }}; " +
                   $"Start-Sleep -Seconds 1; " +
                   $"try {{ Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; claude '{escapedPrompt}'{TerminalReset}{postClaudeCheck} }} " +
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
                   $"try {{ Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; claude '{escapedPrompt}'{TerminalReset}{postClaudeCheck} }} " +
                   $"finally {{ Set-Location '{escapedRoot}'; dydo worktree cleanup {cleanupWorktreeId} --agent {agentName} }}\"";
        }

        return $"{noExitFlag}-Command \"{agentEnv}{windowEnv}Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; claude '{escapedPrompt}'{TerminalReset}{postClaudeCheck}\"";
    }

    public static int Launch(IProcessStarter processStarter, string agentName, string? workingDirectory = null,
        bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
    {
        var shell = ProcessUtils.ResolvePowerShell();

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
                Arguments = $"{wtAction} {wtDirArg}{shell} {GetArguments(agentName, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, workingDirectory).Replace(";", "\\;")}",
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
            Arguments = GetArguments(agentName, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, workingDirectory),
            UseShellExecute = true
        };
        if (workingDirectory != null)
            fallbackPsi.WorkingDirectory = workingDirectory;
        return processStarter.Start(fallbackPsi);
    }
}
