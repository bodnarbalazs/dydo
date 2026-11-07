namespace DynaDocs.Services.Statechart;

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static class PosixStatechartFiles
{
    public static void RequireLinux()
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Statechart directory identity currently requires Linux statx; macOS public lifetime proof remains unavailable.");
    }

    public static void CreateExclusive(string path)
    {
        RequireLinux();
        PosixStatechartNative.Check(Mkdir(path, 0x1c0), Marshal.GetLastPInvokeError()); // 0700
    }

    public static SafeFileHandle Open(string path)
    {
        RequireLinux();
        // O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC
        var (fd, error) = (OpenDirectory(path, 0x10000 | 0x20000 | 0x80000), Marshal.GetLastPInvokeError());
        if (fd < 0) throw new Win32Exception(error);
        return new SafeFileHandle(fd, ownsHandle: true);
    }

    public static string Identity(SafeFileHandle handle)
    {
        RequireLinux();
        var info = Marshal.AllocHGlobal(256);
        try
        {
            // Linux statx is a fixed kernel ABI on both shipped Linux architectures.
            PosixStatechartNative.Check(Statx(handle.DangerousGetHandle().ToInt32(), "", 0x1000, 0x101, info),
                Marshal.GetLastPInvokeError());
            if ((Marshal.ReadInt32(info) & 0x101) != 0x101 || (Marshal.ReadInt16(info, 28) & 0xf000) != 0x4000)
                throw new IOException("Statechart scratch has no verified directory identity.");
            return $"{Marshal.ReadInt32(info, 136):x8}:{Marshal.ReadInt32(info, 140):x8}:{Marshal.ReadInt64(info, 32):x16}";
        }
        finally { Marshal.FreeHGlobal(info); }
    }

    public static bool Matches(string path, SafeFileHandle handle)
    {
        try
        {
            using var current = Open(path);
            return Identity(current) == Identity(handle);
        }
        catch (Exception error) when (error is IOException or Win32Exception) { return false; }
    }

    public static bool DeleteRegistered(string path, SafeFileHandle handle)
    {
        if (!Matches(path, handle)) return false;
        // Traverse through the held directory, never through a replaced pathname or child link.
        var held = "/proc/self/fd/" + handle.DangerousGetHandle().ToInt32();
        foreach (var entry in Directory.EnumerateFileSystemEntries(held))
        {
            if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0) Directory.Delete(entry, recursive: true);
            else File.Delete(entry);
        }
        if (!Matches(path, handle)) return false;
        Directory.Delete(path, recursive: false);
        return true;
    }

    [DllImport("libc", EntryPoint = "mkdir", SetLastError = true)]
    private static extern int Mkdir([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int OpenDirectory([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int Statx(int fd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags, uint mask, nint information);
}
