namespace DynaDocs.Services.Statechart;

using System.ComponentModel;
using System.Runtime.InteropServices;

internal static class PosixStatechartDescriptors
{
    public static int Inherit(nint handle)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var fd = handle.ToInt32();
        var flags = Fcntl(fd, 1, 0); // F_GETFD
        if (flags < 0 || Fcntl(fd, 2, flags & ~1) != 0) throw new Win32Exception(); // F_SETFD, ~FD_CLOEXEC
        return fd;
    }

    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int Fcntl(int fd, int command, int value);
}
