namespace DynaDocs.Services.Statechart;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

internal static class PosixStatechartSocket
{
    private const int SunPath = 108; // Linux sockaddr_un.sun_path, NUL included.

    // An absolute pipe name is the socket's own path; this one is where .NET puts a bare name.
    // Both ends derive it from the TMPDIR they share, so a long one sends both to the same short directory.
    public static string For(string name, string temp)
    {
        var socket = Path.Combine(temp, "CoreFxPipe_" + name);
        if (Encoding.UTF8.GetByteCount(socket) < SunPath) return socket;
        PosixStatechartFiles.RequireLinux();
        var owner = EffectiveUser();
        return Path.Combine(Private("/tmp/dydo-statechart-" + owner, owner), name);
    }

    // In a shared /tmp, a squatted, linked or open directory would hand the socket to another user.
    public static string Private(string directory, uint owner)
    {
        try { PosixStatechartFiles.CreateExclusive(directory); }
        catch (Win32Exception) { } // Whatever stands there now, or nothing, is vetted below; errno is not trusted here.
        var info = Marshal.AllocHGlobal(256);
        try
        {
            // AT_FDCWD, AT_SYMLINK_NOFOLLOW, STATX_TYPE | STATX_MODE | STATX_UID; fields as in PosixStatechartFiles.
            PosixStatechartNative.Check(Statx(-100, directory, 0x100, 0xb, info), Marshal.GetLastPInvokeError());
            var mode = Marshal.ReadInt16(info, 28) & 0xffff;
            if ((mode & 0xf000) == 0x4000 && (mode & 0xe3f) == 0 && (uint)Marshal.ReadInt32(info, 20) == owner)
                return directory;
        }
        catch (Win32Exception error) { throw Refused(directory, owner, error); } // Nothing to vet: mkdir failed, not with EEXIST.
        finally { Marshal.FreeHGlobal(info); }
        throw Refused(directory, owner, null);
    }

    private static IOException Refused(string directory, uint owner, Win32Exception? cause) =>
        new($"Statechart socket directory {directory} must be a directory owned by uid {owner} " +
            "with mode 0700 or stricter, and not a link. Remove it, or set TMPDIR to a shorter path.", cause);

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint EffectiveUser();

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int Statx(int directory, [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags, uint mask, nint information);
}
