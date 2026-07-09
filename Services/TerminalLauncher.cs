namespace DynaDocs.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;
using DynaDocs.Commands;
using DynaDocs.Models;

public class TerminalLauncher
{
    private readonly IProcessStarter _processStarter;
    private readonly ITerminalDetector _terminalDetector;

    public record TerminalConfig(
        string FileName,
        Func<string, string?, string> GetArguments,
        Func<string, string?, string>? GetTabArguments = null);

    public TerminalLauncher(IProcessStarter? processStarter = null, ITerminalDetector? terminalDetector = null)
    {
        _processStarter = processStarter ?? new DefaultProcessStarter();
        _terminalDetector = terminalDetector ?? new DefaultTerminalDetector();
    }

    public static string GetClaudePrompt(string agentName)
    {
        return $"{agentName} --inbox";
    }

    internal static string NormalizeLaunchHost(string? host) =>
        AgentSession.NormalizeHost(host) == "codex" ? "codex" : "claude";

    /// <summary>
    /// When set, <see cref="GetLaunchExecutable"/> uses this instead of searching the
    /// dispatcher's PATH. Test hook for deterministic resolution.
    /// </summary>
    internal static Func<string, string>? ExecutableResolverOverride { get; set; }

    /// <summary>
    /// Resolves the launch host (claude/codex) to the absolute path of its executable on the
    /// <em>dispatcher's</em> PATH. dydo inherits the user's interactive PATH, but the terminal it
    /// spawns does not — so a bare 'codex' that resolves here fails with 'not recognized' in the
    /// child (#227). Falls back to the bare name when the executable is not found, which keeps a
    /// globally-installed host (e.g. claude) working.
    /// </summary>
    internal static string GetLaunchExecutable(string? host) =>
        (ExecutableResolverOverride ?? ResolveOnPath)(NormalizeLaunchHost(host));

    private static string ResolveOnPath(string command)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
            return command;

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        string[] extensions = isWindows
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [];

        var rejectedWindowsAppsCodex = false;
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            // Only probe the extensionless name on Unix. On Windows CreateProcess/where.exe match
            // PATHEXT extensions exclusively — an extensionless file (e.g. npm's `claude`/`codex` sh
            // shim shipped alongside `.cmd`/`.ps1`) is not launchable and PowerShell's call operator
            // silently no-ops on it, so returning it breaks every launch/resume.
            if (!isWindows)
            {
                var directCandidate = Path.Combine(dir, command);
                if (File.Exists(directCandidate))
                    return directCandidate;
            }

            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(dir, command + ext);
                if (!File.Exists(candidate))
                    continue;
                if (IsRejectedWindowsCodexAlias(command, candidate))
                {
                    rejectedWindowsAppsCodex = true;
                    continue;
                }

                return candidate;
            }
        }

        if (rejectedWindowsAppsCodex)
            throw new InvalidOperationException(
                "Codex resolves only to the packaged WindowsApps alias, which cannot be launched from dydo terminals. " +
                "Install a launchable Codex CLI on PATH or launch the agent manually.");

        return command;
    }

    private static bool IsRejectedWindowsCodexAlias(string command, string candidate)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;
        if (!string.Equals(command, "codex", StringComparison.OrdinalIgnoreCase))
            return false;

        // Reject any codex under a WindowsApps directory — covers both the Program Files
        // packaged layout (…\WindowsApps\OpenAI.Codex_*\…\codex.exe) and the MSIX App Execution
        // Alias on the default user PATH (%LOCALAPPDATA%\Microsoft\WindowsApps\codex.exe); neither
        // is launchable from a dydo-spawned terminal.
        var parts = candidate.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = 0; i + 1 < parts.Length; i++)
        {
            if (string.Equals(parts[i], "WindowsApps", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string GetBareLaunchExecutable(string? host) => NormalizeLaunchHost(host);

    /// <summary>
    /// Test seam pinning the resolved codex posture, bypassing <c>dydo.json</c> discovery so
    /// launch-argument assertions stay hermetic. Null in production — the posture is loaded
    /// from config.
    /// </summary>
    internal static CodexDispatchConfig? CodexConfigOverride { get; set; }

    private static CodexDispatchConfig ResolveCodexConfig()
    {
        var codex = CodexConfigOverride
            ?? new ConfigService().LoadConfig()?.Dispatch.Codex
            ?? new CodexDispatchConfig();

        var errors = codex.Validate();
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Invalid dispatch.codex configuration: " + string.Join("; ", errors));

        return codex;
    }

    /// <summary>
    /// The configured codex launch posture (issue 0253) — <c>--sandbox &lt;mode&gt;
    /// --ask-for-approval &lt;policy&gt;</c> with a trailing space, empty for claude. Placed after
    /// the executable and before the prompt/resume subcommand: both are global codex options
    /// (developers.openai.com/codex/cli/reference, verified 2026-07-09). The dangerous-bypass flag
    /// is never a config value and never appears here.
    /// </summary>
    internal static string CodexLaunchPosture(string? host)
    {
        if (NormalizeLaunchHost(host) != "codex")
            return "";

        var codex = ResolveCodexConfig();
        return $"--sandbox {codex.Sandbox} --ask-for-approval {codex.ApprovalPolicy} ";
    }

    private static string GetBareLaunchCommand(string agentName, string? host)
    {
        var prompt = GetClaudePrompt(agentName);
        return $"{GetBareLaunchExecutable(host)} {CodexLaunchPosture(host)}\"{prompt}\"";
    }

    /// <summary>
    /// The continuation prompt used when the watchdog auto-resumes a crashed
    /// host session. Identity is restored by the resumed conversation.
    /// Verbatim from Decision 022.
    /// </summary>
    public const string ResumeContinuationPrompt =
        "Your terminal tab crashed and you have been auto-resumed. " +
        "Your dydo identity, role, and task are unchanged. " +
        "Re-orient briefly from your most recent context and continue from where you left off.";

    /// <summary>
    /// The host-appropriate resume argument that precedes the session id. The Codex CLI has no
    /// root-level <c>--resume</c> flag; resuming is the subcommand form <c>codex resume &lt;id&gt;
    /// [prompt]</c> (developers.openai.com/codex/cli/reference). Emitting <c>--resume</c> for codex
    /// fails with an unexpected-argument error and burns the watchdog's resume budget (#0231).
    /// Claude uses the <c>--resume &lt;id&gt;</c> flag.
    /// </summary>
    internal static string ResumeArgumentToken(string? host) =>
        NormalizeLaunchHost(host) == "codex" ? "resume" : "--resume";

    private static string GetBareResumeCommand(string sessionId, string? host) =>
        $"{GetBareLaunchExecutable(host)} {CodexLaunchPosture(host)}{ResumeArgumentToken(host)} \"{sessionId}\" \"{ResumeContinuationPrompt}\"";

    public static string GetClaudeResumeCommand(string sessionId) =>
        GetBareResumeCommand(sessionId, "claude");

    internal static string ShellExecutableToken(string executable)
    {
        return Path.IsPathRooted(executable)
            ? $"'{BashSingleQuoteEscape(executable)}'"
            : executable;
    }

    internal static string PowerShellExecutableInvocation(string executable, string arguments)
    {
        return Path.IsPathRooted(executable)
            ? $"& '{executable.Replace("'", "''")}' {arguments}"
            : $"{executable} {arguments}";
    }

    public static string GetCodexCommand(string agentName)
    {
        var prompt = GetClaudePrompt(agentName);
        return $"codex {CodexLaunchPosture("codex")}\"{prompt}\"";
    }

    public static string GetClaudeCommand(string agentName)
    {
        var prompt = GetClaudePrompt(agentName);
        return $"claude \"{prompt}\"";
    }

    public static string GenerateWorktreeId(string taskName, string? parentWorktreeId = null)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(taskName, @"^[a-zA-Z0-9_.\-]+$"))
            throw new ArgumentException($"Task name contains unsafe characters (allowed: a-zA-Z0-9_.-): {taskName}", nameof(taskName));
        if (taskName is "." or "..")
            throw new ArgumentException($"Task name cannot be '{taskName}' (path traversal): {taskName}", nameof(taskName));
        if (taskName.Contains(".+."))
            throw new ArgumentException($"Task name cannot contain '.+.' sequence (reserved for branch encoding): {taskName}", nameof(taskName));
        return parentWorktreeId != null ? $"{parentWorktreeId}/{taskName}" : taskName;
    }

    /// <summary>
    /// Validates a worktree ID received from external input (e.g. CLI arguments).
    /// Rejects path traversal components and unsafe characters.
    /// </summary>
    public static void ValidateWorktreeId(string worktreeId)
    {
        if (string.IsNullOrEmpty(worktreeId))
            throw new ArgumentException("Worktree ID cannot be empty.", nameof(worktreeId));

        // Reject backslashes — worktree IDs use forward slash for hierarchy
        if (worktreeId.Contains('\\'))
            throw new ArgumentException($"Worktree ID contains backslash (use '/' for hierarchy): {worktreeId}", nameof(worktreeId));

        foreach (var component in worktreeId.Split('/'))
        {
            if (component is "" or "." or "..")
                throw new ArgumentException($"Worktree ID contains path traversal component: {worktreeId}", nameof(worktreeId));
            if (!System.Text.RegularExpressions.Regex.IsMatch(component, @"^[a-zA-Z0-9_.\-]+$"))
                throw new ArgumentException($"Worktree ID contains unsafe characters (allowed: a-zA-Z0-9_.-/): {worktreeId}", nameof(worktreeId));
        }
    }

    public static string WorktreeIdToBranchSuffix(string worktreeId) =>
        worktreeId.Replace("/", ".+.");

    public static string BranchSuffixToWorktreeId(string suffix) =>
        suffix.Replace(".+.", "/");

    public static readonly TerminalConfig[] LinuxTerminals =
    [
        new("gnome-terminal",
            (agentName, wd) => $"-- bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\"",
            (agentName, wd) => $"--tab -- bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\""),
        new("konsole",
            (agentName, wd) => $"-e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\"",
            (agentName, wd) => $"--new-tab -e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\""),
        new("xfce4-terminal",
            (agentName, wd) => $"-e \"bash -c \\\"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\\\"\"",
            (agentName, wd) => $"--tab -e \"bash -c \\\"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\\\"\""),
        new("alacritty", (agentName, wd) => $"-e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\""),
        new("kitty", (agentName, wd) => $"bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\""),
        new("wezterm", (agentName, wd) => $"start -- bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\""),
        new("tilix", (agentName, wd) => $"-e \"bash -c \\\"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\\\"\""),
        new("foot", (agentName, wd) => $"bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\""),
        new("xterm", (agentName, wd) => $"-e bash -c \"{CdPrefix(wd)}unset CLAUDECODE; claude '{agentName} --inbox'; printf '\\e[?1004l'; exec bash\""),
    ];

    // Escape a value for safe interpolation inside a bash single-quoted string.
    // Closes the quoted segment (`'`), inserts a literal apostrophe (`\'`), and reopens (`'`).
    internal static string BashSingleQuoteEscape(string value) => value.Replace("'", "'\\''");

    internal static string WorktreeSetupScript(string worktreeId, string? mainProjectRoot = null)
    {
        // Worktree is assumed to already exist before terminal launch.
        // This script only cd's into it and sets up symlinks.
        if (mainProjectRoot != null)
        {
            var escapedRoot = BashSingleQuoteEscape(mainProjectRoot);
            return $"cd '{escapedRoot}/dydo/_system/.local/worktrees/{worktreeId}' && " +
                   WorktreeCommand.GenerateBashJunctionScript(escapedRoot, isVariable: false) +
                   $"(dydo worktree init-settings --main-root '{escapedRoot}' || echo 'WARNING: init-settings failed' >&2) && sleep 1 && ";
        }

        return $"_wt_root=\"$(pwd)\" && " +
               $"cd dydo/_system/.local/worktrees/{worktreeId} && " +
               WorktreeCommand.GenerateBashJunctionScript("$_wt_root", isVariable: true) +
               $"(dydo worktree init-settings --main-root \"$_wt_root\" || echo 'WARNING: init-settings failed' >&2) && sleep 1 && ";
    }

    internal static string WorktreeInitSettingsScript(string? mainProjectRoot)
    {
        if (mainProjectRoot == null) return "";
        var escapedRoot = BashSingleQuoteEscape(mainProjectRoot);
        return $"(dydo worktree init-settings --main-root '{escapedRoot}' || echo 'WARNING: init-settings failed' >&2) && sleep 1 && ";
    }

    internal static string WorktreeInheritedSetupScript(string? mainProjectRoot, string? workingDirectory)
    {
        if (mainProjectRoot == null) return "";
        var escapedRoot = BashSingleQuoteEscape(mainProjectRoot);
        var cdPrefix = workingDirectory != null
            ? $"cd '{BashSingleQuoteEscape(workingDirectory)}' && "
            : "";
        return $"{cdPrefix}(dydo worktree init-settings --main-root '{escapedRoot}' || echo 'WARNING: init-settings failed' >&2) && sleep 1 && ";
    }

    internal static string WorktreeCleanupScript(string worktreeId, string agentName) =>
        $"dydo worktree cleanup {worktreeId} --agent {agentName}";

    internal static string BashPostClaudeCheck(string agentName) =>
        $"if dydo agent status {agentName} 2>/dev/null | grep -q 'free'; then exit 0; fi; exec bash";

    internal static string CdPrefix(string? workingDirectory)
    {
        if (workingDirectory == null)
            return "";

        if (workingDirectory.Contains('\''))
            throw new ArgumentException(
                $"Project path contains a single quote and cannot be safely used in a shell cd command: {workingDirectory}");

        return $"cd '{workingDirectory}' && ";
    }

    public static string GetWindowsArguments(string agentName, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null, string? workingDirectory = null, string host = "claude")
        => WindowsTerminalLauncher.GetArguments(agentName, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, workingDirectory, host);

    public static string GetLinuxArguments(string terminalName, string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null, string host = "claude")
        => LinuxTerminalLauncher.GetArguments(terminalName, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, host);

    public static string GetMacArguments(string agentName, string? workingDirectory = null, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null, string host = "claude")
        => MacTerminalLauncher.GetArguments(agentName, workingDirectory, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, host);

    public static string GetWindowsResumeArguments(string agentName, string sessionId, string? workingDirectory = null, string? worktreeId = null, string? mainProjectRoot = null, string host = "claude")
        => WindowsTerminalLauncher.GetResumeArguments(agentName, sessionId, workingDirectory, worktreeId, mainProjectRoot, host);

    public static string GetLinuxResumeArguments(string terminalName, string agentName, string sessionId, string? workingDirectory = null, string? worktreeId = null, string? mainProjectRoot = null, string host = "claude")
        => LinuxTerminalLauncher.GetResumeArguments(terminalName, agentName, sessionId, workingDirectory, worktreeId, mainProjectRoot, host);

    public static string GetMacResumeArguments(string agentName, string sessionId, string? workingDirectory = null, string? worktreeId = null, string? mainProjectRoot = null, string host = "claude")
        => MacTerminalLauncher.GetResumeArguments(agentName, sessionId, workingDirectory, worktreeId, mainProjectRoot, host);

    public static string GetITermTabScript(string shellCommand, string postCheck, string? windowId = null)
        => MacTerminalLauncher.GetITermTabScript(shellCommand, postCheck, windowId);

    public static string GetITermWindowScript(string shellCommand, string postCheck)
        => MacTerminalLauncher.GetITermWindowScript(shellCommand, postCheck);

    public static IProcessStarter? ProcessStarterOverride { get; set; }

    public static int LaunchNewTerminal(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null, string host = "claude")
    {
        return new TerminalLauncher(ProcessStarterOverride).Launch(agentName, workingDirectory, useTab, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, host);
    }

    public int Launch(string agentName, string? workingDirectory = null, bool useTab = false, bool autoClose = false, string? worktreeId = null, string? windowName = null, string? cleanupWorktreeId = null, string? mainProjectRoot = null, string host = "claude")
    {
        var launchHost = NormalizeLaunchHost(host);
        try
        {
            // A dispatch can outlive its working directory (e.g. a merger finalized
            // and tore down the worktree before launch). Bail before Process.Start so
            // the target doesn't crash on ERROR_DIRECTORY (0x8007010b).
            if (workingDirectory != null && !Directory.Exists(workingDirectory))
                throw new DirectoryNotFoundException(
                    $"Working directory no longer exists: {workingDirectory}");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return WindowsTerminalLauncher.Launch(_processStarter, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, launchHost);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return MacTerminalLauncher.Launch(_processStarter, _terminalDetector, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, launchHost);

            var pid = LinuxTerminalLauncher.TryLaunch(_processStarter, LinuxTerminals, agentName, workingDirectory, useTab, autoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot, launchHost);
            if (pid == 0)
                throw new InvalidOperationException("No terminal found");
            return pid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARN: Could not launch terminal: {ex.Message}");
            Console.WriteLine($"Please manually open a new terminal and run:");
            Console.WriteLine($"  {GetBareLaunchCommand(agentName, launchHost)}");
            return 0;
        }
    }

    public static int LaunchResumeTerminal(string agentName, string sessionId, string? workingDirectory = null,
        string? windowName = null, bool useTab = false, string? worktreeId = null, string? mainProjectRoot = null,
        string host = "claude")
    {
        return new TerminalLauncher(ProcessStarterOverride)
            .LaunchResume(agentName, sessionId, workingDirectory, windowName, useTab, worktreeId, mainProjectRoot, host);
    }

    public int LaunchResume(string agentName, string sessionId, string? workingDirectory = null,
        string? windowName = null, bool useTab = false, string? worktreeId = null, string? mainProjectRoot = null,
        string host = "claude")
    {
        var launchHost = NormalizeLaunchHost(host);
        try
        {
            if (workingDirectory != null && !Directory.Exists(workingDirectory))
                throw new DirectoryNotFoundException(
                    $"Working directory no longer exists: {workingDirectory}");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return WindowsTerminalLauncher.LaunchResume(_processStarter, agentName, sessionId, workingDirectory, windowName, useTab, worktreeId, mainProjectRoot, launchHost);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return MacTerminalLauncher.LaunchResume(_processStarter, _terminalDetector, agentName, sessionId, workingDirectory, windowName, useTab, worktreeId, mainProjectRoot, launchHost);

            var pid = LinuxTerminalLauncher.TryLaunchResume(_processStarter, LinuxTerminals, agentName, sessionId, workingDirectory, worktreeId, mainProjectRoot, launchHost);
            if (pid == 0)
                throw new InvalidOperationException("No terminal found");
            return pid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARN: Could not launch resume terminal: {ex.Message}");
            Console.WriteLine($"Please manually open a new terminal and run:");
            Console.WriteLine($"  {GetBareResumeCommand(sessionId, launchHost)}");
            return 0;
        }
    }

    private class DefaultProcessStarter : IProcessStarter
    {
        public int Start(ProcessStartInfo psi)
        {
            var process = Process.Start(psi);
            if (process == null) return 0;
            try { return process.Id; }
            catch { return 0; }
        }
    }

    private class DefaultTerminalDetector : ITerminalDetector
    {
        public bool IsAvailable(string appName)
        {
            return Directory.Exists($"/Applications/{appName}.app");
        }

        public string? GetRunningTerminal()
        {
            return Environment.GetEnvironmentVariable("TERM_PROGRAM") switch
            {
                "iTerm.app" => "iTerm",
                "Apple_Terminal" => "Terminal",
                _ => null
            };
        }
    }
}
