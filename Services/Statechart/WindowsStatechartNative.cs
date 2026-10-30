namespace DynaDocs.Services.Statechart;

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static class WindowsStatechartNative
{
    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true)]
    internal static extern SafeFileHandle CreateJobObject(nint attributes, nint name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetInformationJobObject(SafeFileHandle job, int kind,
        ref WindowsJobLimits limits, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetInformationJobObject(SafeFileHandle job, int kind,
        ref WindowsJobPort port, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryInformationJobObject(SafeFileHandle job, int kind,
        out WindowsJobAccounting accounting, uint size, nint returnedLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern SafeFileHandle OpenProcess(uint access,
        [MarshalAs(UnmanagedType.Bool)] bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AssignProcessToJobObject(SafeFileHandle job, SafeFileHandle process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsProcessInJob(SafeFileHandle process, SafeFileHandle job,
        [MarshalAs(UnmanagedType.Bool)] out bool member);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TerminateJobObject(SafeFileHandle job, uint code);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern SafeFileHandle CreateIoCompletionPort(nint file, nint existingPort,
        nuint key, uint threads);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DuplicateHandle(nint sourceProcess, SafeFileHandle source,
        nint targetProcess, out nint target, uint access,
        [MarshalAs(UnmanagedType.Bool)] bool inherit, uint options);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetQueuedCompletionStatus(SafeFileHandle port, out uint message,
        out nuint key, out nint overlapped, uint milliseconds);
}
