namespace DynaDocs.Services.Statechart;

using System.Runtime.InteropServices;

internal static class PosixStatechartNative
{
    [DllImport("libc", EntryPoint = "posix_spawn")]
    internal static extern int Spawn(out int pid, [MarshalAs(UnmanagedType.LPUTF8Str)] string file,
        nint actions, nint attributes, nint arguments, nint environment);

    [DllImport("libc", EntryPoint = "execve", SetLastError = true)]
    internal static extern int Exec([MarshalAs(UnmanagedType.LPUTF8Str)] string file,
        nint arguments, nint environment);

    [DllImport("libc", EntryPoint = "setpgid", SetLastError = true)]
    internal static extern int SetGroup(int pid, int group);

    [DllImport("libc", EntryPoint = "getpgid", SetLastError = true)]
    internal static extern int GetGroup(int pid);

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    internal static extern int Kill(int pid, int signal);

    [DllImport("libc", EntryPoint = "waitid", SetLastError = true)]
    internal static extern int WaitId(int kind, uint pid, nint information, int options);

    [DllImport("libc", EntryPoint = "waitpid", SetLastError = true)]
    internal static extern int WaitPid(int pid, out int status, int options);
}
