namespace DynaDocs.Tests.Statechart;

using System.ComponentModel;
using DynaDocs.Services.Statechart;
using Microsoft.Win32.SafeHandles;

// Each failed native call reports the errno it failed with. Coverage instrumentation runs its own P/Invokes
// between statements, so these hold under the coverage gate only when each call reads errno in its own statement.
public class StatechartPosixErrnoTests
{
    private const string LinuxOnly = "POSIX ownership runs on Linux.";
    private const int NoEntry = 2; // ENOENT
    private const int BadDescriptor = 9; // EBADF
    private const int Exists = 17; // EEXIST
    private const int NotDirectory = 20; // ENOTDIR

    [SkippableFact]
    public void CreatingAnExistingDirectoryReportsExists()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var scope = new StatechartTestDirectory();
        AssertErrno(Exists, () => PosixStatechartFiles.CreateExclusive(scope.Path));
    }

    [SkippableFact]
    public void OpeningAMissingOrPlainPathReportsWhyItIsNoDirectory()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var scope = new StatechartTestDirectory();
        var file = Path.Combine(scope.Path, "plain");
        File.WriteAllText(file, "");
        AssertErrno(NoEntry, () => PosixStatechartFiles.Open(Path.Combine(scope.Path, "missing")));
        AssertErrno(NotDirectory, () => PosixStatechartFiles.Open(file));
    }

    [SkippableFact]
    public void IdentifyingAClosedDescriptorReportsBadDescriptor()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var closed = new SafeFileHandle(-1, ownsHandle: false);
        AssertErrno(BadDescriptor, () => PosixStatechartFiles.Identity(closed));
    }

    [SkippableFact]
    public void InheritingAClosedDescriptorReportsBadDescriptor()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        AssertErrno(BadDescriptor, () => PosixStatechartDescriptors.Inherit(-1));
    }

    [SkippableFact]
    public void AFailedHandoffReportsTheExecErrno()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        var error = PosixStatechartProcess.Handoff("/statechart-missing-native-handoff", [], []);
        Assert.Equal(NoEntry, error.NativeErrorCode);
        Assert.Equal("Statechart exec handoff failed.", error.Message);
    }

    // mkdir fails for a reason other than EEXIST, so the vetting statx fails too.
    [SkippableFact]
    public void ASocketDirectoryUnderAMissingParentIsRefusedWithTheRemedyAndItsNoEntry()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var scope = new StatechartTestDirectory();
        var directory = Path.Combine(scope.Path, "missing", "socket");
        var error = Assert.Throws<IOException>(() => PosixStatechartSocket.Private(directory, 0));
        Assert.Equal($"Statechart socket directory {directory} must be a directory owned by uid 0 with mode 0700 " +
            "or stricter, and not a link. Remove it, or set TMPDIR to a shorter path.", error.Message);
        AssertErrno(NoEntry, error.InnerException);
    }

    private static void AssertErrno(int errno, Action call) => AssertErrno(errno, Assert.Throws<Win32Exception>(call));

    private static void AssertErrno(int errno, Exception? error)
    {
        var native = Assert.IsType<Win32Exception>(error);
        Assert.Equal(errno, native.NativeErrorCode);
        Assert.Equal(new Win32Exception(errno).Message, native.Message);
    }
}
