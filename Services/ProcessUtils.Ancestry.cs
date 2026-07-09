namespace DynaDocs.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using DynaDocs.Models;

public static partial class ProcessUtils
{
    /// <summary>
    /// When set, FindAncestorProcess uses this instead of walking the real process tree.
    /// </summary>
    public static Func<string, int, int?>? FindAncestorProcessOverride { get; set; }

    /// <summary>
    /// When set, GetParentPid uses this instead of probing the system.
    /// </summary>
    public static Func<int, int?>? GetParentPidOverride { get; set; }

    /// <summary>
    /// Gets the parent process ID for a given PID.
    /// Uses .NET's Process class on Windows; parses /proc on Linux; ps on macOS.
    /// </summary>
    public static int? GetParentPid(int pid)
    {
        if (GetParentPidOverride != null) return GetParentPidOverride(pid);

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetParentPidWindows(pid);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return GetParentPidLinux(pid);
            return GetParentPidMac(pid);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns true when the given PID is in the current process's parent chain.
    /// This backs the non-host fallback claim path where dydo records the parent shell.
    /// </summary>
    public static bool IsCurrentProcessDescendantOf(int ancestorPid, int maxDepth = 10)
    {
        if (ancestorPid <= 1) return false;

        var pid = Environment.ProcessId;
        for (var i = 0; i < maxDepth; i++)
        {
            var parentPid = GetParentPid(pid);
            if (parentPid == null || parentPid <= 1) return false;
            if (parentPid.Value == ancestorPid) return true;
            pid = parentPid.Value;
        }

        return false;
    }

    /// <summary>
    /// Walks up the process tree from the current process, returning the first ancestor
    /// whose process name matches the given basename exactly (extension stripped,
    /// case-insensitive).
    /// </summary>
    public static int? FindAncestorProcess(string processName, int maxDepth = 10)
    {
        if (FindAncestorProcessOverride != null) return FindAncestorProcessOverride(processName, maxDepth);

        var pid = Environment.ProcessId;

        for (var i = 0; i < maxDepth; i++)
        {
            var parentPid = GetParentPid(pid);
            if (parentPid == null || parentPid <= 1) return null;

            if (MatchesProcessName(GetProcessName(parentPid.Value), processName))
                return parentPid.Value;

            pid = parentPid.Value;
        }

        return null;
    }

    /// <summary>
    /// Walks up the process tree returning the first ancestor that looks like the
    /// running claude binary. Linux/Mac: basename "claude". Windows: basename "claude"
    /// OR "node" (the official npm distribution is a Node script). MatchesProcessName
    /// also covers the post-update "claude.exe.old.&lt;unix-ms&gt;" rename so anchoring,
    /// ClaimedPid capture, and the watchdog's kill-target whitelist share one source
    /// of truth. Closes #0151.
    /// </summary>
    public static int? FindClaudeAncestor(int maxDepth = 10)
    {
        if (FindAncestorProcessOverride != null)
        {
            var injected = FindAncestorProcessOverride("claude", maxDepth);
            if (injected.HasValue) return injected;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return FindAncestorProcessOverride("node", maxDepth);
            return null;
        }

        var pid = Environment.ProcessId;
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        for (var i = 0; i < maxDepth; i++)
        {
            var parentPid = GetParentPid(pid);
            if (parentPid == null || parentPid <= 1) return null;

            var kind = ClassifyAncestorHost(parentPid.Value, isWindows, isLauncherPosition: i == 0);
            if (kind is AncestorHostKind.Claude or AncestorHostKind.AmbiguousNode) return parentPid.Value;

            pid = parentPid.Value;
        }
        return null;
    }

    // #0250/F1: classifies a walk ancestor as an agent host. claude/codex match by process name;
    // a Windows `node` ancestor is classified by command line (ClassifyNodeAncestor) — the vendor
    // CLIs are hosts, the npm launcher shim and unrelated node scripts are not. An unreadable node
    // command line keeps the old name-based treatment (AmbiguousNode), EXCEPT at the launcher
    // position (direct parent of the initial dydo process), which is the npm shim, not a host.
    // AmbiguousNode resolves to the vendor the caller is looking for (claude for FindClaudeAncestor,
    // codex for FindCodexAncestor) and counts as foreign in the nearest-host guard.
    private enum AncestorHostKind { None, Claude, Codex, AmbiguousNode }

    private static AncestorHostKind ClassifyAncestorHost(int pid, bool isWindows, bool isLauncherPosition)
    {
        var name = GetProcessName(pid);
        if (MatchesProcessName(name, "claude")) return AncestorHostKind.Claude;
        if (MatchesProcessName(name, "codex")) return AncestorHostKind.Codex;
        if (isWindows && MatchesProcessName(name, "node"))
            return ClassifyNodeAncestor(pid) switch
            {
                NodeAncestorKind.ClaudeHost => AncestorHostKind.Claude,
                NodeAncestorKind.CodexHost => AncestorHostKind.Codex,
                NodeAncestorKind.Unreadable when !isLauncherPosition => AncestorHostKind.AmbiguousNode,
                _ => AncestorHostKind.None
            };
        return AncestorHostKind.None;
    }

    /// <summary>
    /// Walks up the process tree returning the first ancestor that looks like the
    /// host runtime that claimed a dydo identity. Unknown hosts preserve the
    /// historical Claude lookup so old sessions keep working.
    /// </summary>
    public static int? FindAgentHostAncestor(string? host, int maxDepth = 10) =>
        AgentSession.NormalizeHost(host) switch
        {
            "codex" => FindCodexAncestor(maxDepth),
            _ => FindClaudeAncestor(maxDepth)
        };

    /// <summary>
    /// Walks up the process tree returning the first ancestor that looks like the
    /// running Codex CLI. On Windows, npm-installed CLIs may run under node.exe, so
    /// the Windows-only node fallback mirrors Claude's host detection.
    /// </summary>
    public static int? FindCodexAncestor(int maxDepth = 10)
    {
        if (FindAncestorProcessOverride != null)
        {
            var injected = FindAncestorProcessOverride("codex", maxDepth);
            if (injected.HasValue) return injected;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return FindAncestorProcessOverride("node", maxDepth);
            return null;
        }

        var pid = Environment.ProcessId;
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        for (var i = 0; i < maxDepth; i++)
        {
            var parentPid = GetParentPid(pid);
            if (parentPid == null || parentPid <= 1) return null;

            var kind = ClassifyAncestorHost(parentPid.Value, isWindows, isLauncherPosition: i == 0);
            if (kind is AncestorHostKind.Codex or AncestorHostKind.AmbiguousNode) return parentPid.Value;

            pid = parentPid.Value;
        }
        return null;
    }

    /// <summary>
    /// Nearest-host-wins ownership guard (#0250). Walks up from the current process toward the
    /// claimed host PID and returns false if a *different* agent-host process (claude/codex, or
    /// node on Windows where both ship as Node scripts) sits nearer than the claimed host —
    /// that inner host is a foreign-vendor worker spawned under an outer session, not the agent.
    /// Returns true once the claimed host is reached, or if the walk ends without passing a
    /// foreign host. Stops at the claimed PID, so it never wanders up to an unrelated host that
    /// merely happens to sit above the whole tree.
    /// </summary>
    public static bool NoForeignHostNearerThanClaimedHost(int claimedPid, int maxDepth = 10)
    {
        // Caller is the claimed host itself — nothing can sit between it and itself.
        if (Environment.ProcessId == claimedPid) return true;

        // Tests inject a single stand-in host PID for the whole ancestry via
        // FindAncestorProcessOverride. Treat that as the nearest host: nothing foreign is
        // nearer unless the injected host is a different PID than the claimed host.
        if (FindAncestorProcessOverride != null)
        {
            var injected = FindAncestorProcessOverride("claude", maxDepth) ?? FindAncestorProcessOverride("codex", maxDepth);
            return !injected.HasValue || injected.Value == claimedPid;
        }

        var pid = Environment.ProcessId;
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        for (var i = 0; i < maxDepth; i++)
        {
            var parentPid = GetParentPid(pid);
            if (parentPid == null || parentPid <= 1) return true;
            if (parentPid.Value == claimedPid) return true;

            // Any agent host nearer than the claimed host means the caller is an inner worker.
            // The npm launcher shim and unrelated node scripts classify as None and keep walking.
            if (ClassifyAncestorHost(parentPid.Value, isWindows, isLauncherPosition: i == 0) != AncestorHostKind.None)
                return false;

            pid = parentPid.Value;
        }
        return true;
    }

    // Anchored regex per known needle. Closes #0128 ("claudia.exe", "claude-dev.exe"
    // must NOT match) and #0151's post-update rename (after a Claude Code self-update
    // on Windows the running image name becomes "claude.exe.old.<unix-ms>"; the OS
    // retains that name for the running process's lifetime). The "node" entry covers
    // Windows where claude ships as a Node script. Other needles fall through to the
    // literal-stem path so unrelated callers (FindAncestorProcess("dotnet"), etc.)
    // keep their existing semantics.
    private static readonly Dictionary<string, Regex> ProcessNameRegexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude"] = new Regex(@"^claude(\.exe(\.old\.\d+)?)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled),
        ["codex"] = new Regex(@"^codex(\.exe)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled),
        ["node"] = new Regex(@"^node(\.exe)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled),
    };

    internal static bool MatchesProcessName(string? actualName, string needle)
    {
        if (actualName == null) return false;
        if (ProcessNameRegexes.TryGetValue(needle, out var rx))
            return rx.IsMatch(actualName);
        return Path.GetFileNameWithoutExtension(actualName)
            .Equals(needle, StringComparison.OrdinalIgnoreCase);
    }

    // #0250/F1: a Windows `node` ancestor is classified by its command line, not its name.
    // The npm-installed dydo runs dydo.exe as a child of a `node` launcher shim (npm/bin/dydo),
    // so name-based matching flagged every legitimate npm CLI call as sitting under a foreign
    // host. The command line is the truth: the launcher shim and unrelated node scripts are
    // transparent (keep walking); only a command line that names the vendor CLI is a host.
    internal enum NodeAncestorKind { Transparent, ClaudeHost, CodexHost, Unreadable }

    // The launcher script is npm/bin/dydo, installed as .../node_modules/dydo/bin/dydo — a
    // bin/dydo path segment identifies it regardless of dydo's own arguments (which may
    // themselves contain "claude"/"codex"), so this check must precede the vendor matches.
    private static readonly Regex DydoLauncherCmdlineRegex = new(
        @"[\\/]bin[\\/]dydo(?![A-Za-z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ClaudeCmdlineRegex = new(
        @"(?<![A-Za-z])claude(?![A-Za-z])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CodexCmdlineRegex = new(
        @"(?<![A-Za-z])codex(?![A-Za-z])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static NodeAncestorKind ClassifyNodeCommandLine(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return NodeAncestorKind.Unreadable;
        if (DydoLauncherCmdlineRegex.IsMatch(commandLine)) return NodeAncestorKind.Transparent;
        if (CodexCmdlineRegex.IsMatch(commandLine)) return NodeAncestorKind.CodexHost;
        if (ClaudeCmdlineRegex.IsMatch(commandLine)) return NodeAncestorKind.ClaudeHost;
        return NodeAncestorKind.Transparent;
    }

    private static NodeAncestorKind ClassifyNodeAncestor(int pid) =>
        ClassifyNodeCommandLine(GetProcessCommandLine(pid));

    private static int? GetParentPidWindows(int pid)
    {
        using var process = Process.GetProcessById(pid);
        var pbi = new PROCESS_BASIC_INFORMATION();
        int status = NtQueryInformationProcess(
            process.Handle, 0, ref pbi,
            Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out _);
        return status == 0 ? (int)pbi.InheritedFromUniqueProcessId : null;
    }

    internal static int? GetParentPidLinux(int pid)
    {
        var statusPath = $"/proc/{pid}/status";
        if (!File.Exists(statusPath)) return null;
        return ParseProcStatusForPpid(File.ReadLines(statusPath));
    }

    /// <summary>
    /// Parses /proc/PID/status lines to extract the parent PID.
    /// </summary>
    internal static int? ParseProcStatusForPpid(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            if (line.StartsWith("PPid:") && int.TryParse(line[5..].Trim(), out var ppid))
                return ppid;
        }
        return null;
    }

    internal static int? GetParentPidMac(int pid)
    {
        var output = RunProcess("ps", $"-o ppid= -p {pid}");
        return output != null ? ParsePsPpidOutput(output) : null;
    }

    /// <summary>
    /// Parses `ps -o ppid=` output to extract a parent PID.
    /// </summary>
    internal static int? ParsePsPpidOutput(string output)
    {
        return int.TryParse(output.Trim(), out var result) ? result : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public nint ExitStatus;
        public nint PebBaseAddress;
        public nint AffinityMask;
        public nint BasePriority;
        public nint UniqueProcessId;
        public nint InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        nint processHandle, int processInformationClass,
        ref PROCESS_BASIC_INFORMATION processInformation,
        int processInformationLength, out int returnLength);
}
