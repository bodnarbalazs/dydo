namespace DynaDocs.Services;

using System.Diagnostics;

public static class WindowsTerminalLauncher
{
    public static string GetArguments(string agentName, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null)
    {
        var prompt = TerminalLauncher.GetClaudePrompt(agentName);
        var escapedPrompt = prompt.Replace("'", "''");
        var postClaudeCheck = autoClose
            ? $"; if ((dydo agent status {agentName} 2>&1) -match 'free') {{ exit 0 }}"
            : "";
        var noExitFlag = autoClose ? "" : "-NoExit ";

        var agentEnv = $"$env:DYDO_AGENT='{agentName}'; ";
        var windowEnv = windowName != null
            ? $"$env:DYDO_WINDOW='{windowName}'; "
            : "";

        if (worktreeId != null)
        {
            var wtDir = $"dydo/_system/.local/worktrees/{worktreeId}";
            var branch = $"worktree/{worktreeId}";
            return $"{noExitFlag}-Command \"{agentEnv}{windowEnv}$_wt_root = Get-Location; " +
                   $"New-Item -ItemType Directory -Force -Path dydo/_system/.local/worktrees | Out-Null; " +
                   $"git worktree prune; " +
                   $"git worktree add {wtDir} -b {branch}; " +
                   $"Set-Location {wtDir}; " +
                   $"if (Test-Path dydo/agents) {{ cmd /c rmdir dydo/agents; }} " +
                   $"New-Item -ItemType Junction -Path dydo/agents -Target (Join-Path $_wt_root.Path 'dydo/agents') | Out-Null; " +
                   $"try {{ Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; claude '{escapedPrompt}'{postClaudeCheck} }} " +
                   $"finally {{ Set-Location $_wt_root; dydo worktree cleanup {worktreeId} --agent {agentName} }}\"";
        }

        // Inherited worktree: no setup, but cleanup on exit
        if (worktreeId == null && cleanupWorktreeId != null && mainProjectRoot != null)
        {
            var escapedRoot = mainProjectRoot.Replace("'", "''");
            return $"{noExitFlag}-Command \"{agentEnv}{windowEnv}try {{ Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; claude '{escapedPrompt}'{postClaudeCheck} }} " +
                   $"finally {{ Set-Location '{escapedRoot}'; dydo worktree cleanup {cleanupWorktreeId} --agent {agentName} }}\"";
        }

        return $"{noExitFlag}-Command \"{agentEnv}{windowEnv}Remove-Item Env:CLAUDECODE -ErrorAction SilentlyContinue; claude '{escapedPrompt}'{postClaudeCheck}\"";
    }

    public static void Launch(IProcessStarter processStarter, string agentName, string? workingDirectory = null,
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
                Arguments = $"{wtAction} {wtDirArg}{shell} {GetArguments(agentName, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot).Replace(";", "\\;")}",
                UseShellExecute = true
            };
            if (workingDirectory != null)
                psi.WorkingDirectory = workingDirectory;
            processStarter.Start(psi);
            return;
        }
        catch
        {
            // Windows Terminal not available, fall back to PowerShell
        }

        // Fall back to PowerShell (no tab support)
        var fallbackPsi = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = GetArguments(agentName, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot),
            UseShellExecute = true
        };
        if (workingDirectory != null)
            fallbackPsi.WorkingDirectory = workingDirectory;
        processStarter.Start(fallbackPsi);
    }
}
