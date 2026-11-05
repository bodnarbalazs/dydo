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

    [SkippableFact]
    public void ASocketDirectoryUnderAMissingParentReportsNoEntry()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var scope = new StatechartTestDirectory();
        var directory = Path.Combine(scope.Path, "missing", "socket");
        AssertErrno(NoEntry, () => PosixStatechartSocket.Private(directory, 0));
    }

    private static void AssertErrno(int errno, Action call)
    {
        var error = Assert.Throws<Win32Exception>(call);
        Assert.Equal(errno, error.NativeErrorCode);
        Assert.Equal(new Win32Exception(errno).Message, error.Message);
    }
}
