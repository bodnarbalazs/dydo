namespace DynaDocs.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

public static partial class ProcessUtils
{
    /// <summary>
    /// When set, FindAncestorProcess uses this instead of walking the real process tree.
    /// </summary>
    public static Func<string, int, int?>? FindAncestorProcessOverride { get; set; }

    /// <summary>
    /// Gets the parent process ID for a given PID.
    /// Uses .NET's Process class on Windows; parses /proc on Linux; ps on macOS.
    /// </summary>
    public static int? GetParentPid(int pid)
    {
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

            var name = GetProcessName(parentPid.Value);
            if (MatchesProcessName(name, "claude")) return parentPid.Value;
            if (isWindows && MatchesProcessName(name, "node")) return parentPid.Value;

            pid = parentPid.Value;
        }
        return null;
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
