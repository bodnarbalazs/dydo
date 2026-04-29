namespace DynaDocs.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;

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

    // Closes #0128: "claudia.exe", "claude-dev.exe" — anything that merely contains
    // "claude" — must NOT be picked as the watchdog anchor.
    internal static bool MatchesProcessName(string? actualName, string needle) =>
        actualName != null &&
        Path.GetFileNameWithoutExtension(actualName)
            .Equals(needle, StringComparison.OrdinalIgnoreCase);

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
