namespace DynaDocs.Services.Statechart;

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static class WindowsStatechartFiles
{
    public static void CreateExclusive(string path)
    {
        if (!CreateDirectory(path, 0)) throw new Win32Exception();
    }
    public static SafeFileHandle Open(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var handle = CreateFile(path, 0x10080, 3, 0, 3, 0x02200000, 0);
        if (!handle.IsInvalid) return handle;
        handle.Dispose();
        throw new Win32Exception();
    }

    public static string Identity(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var info)) throw new Win32Exception();
        if ((info.Attributes & 0x410) != 0x10)
            throw new IOException("Statechart scratch is not a plain directory.");
        return $"{info.Volume:x8}:{info.IndexHigh:x8}{info.IndexLow:x8}";
    }

    public static void DeleteOnClose(SafeFileHandle handle)
    {
        byte delete = 1;
        if (!SetFileInformationByHandle(handle, 4, ref delete, 1)) throw new Win32Exception();
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, nint security,
        uint disposition, uint flags, nint template);

    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectory(string path, nint security);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle,
        out WindowsStatechartFileInfo information);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int kind,
        ref byte information, uint length);
}
