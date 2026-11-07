namespace DynaDocs.Tests.Statechart;

using System.ComponentModel;
using System.Diagnostics;
using DynaDocs.Services.Statechart;

public class StatechartPosixTests
{
    private const string LinuxOnly = "POSIX ownership runs on Linux.";

    [Fact]
    public void PosixOwnershipDoesNotInvokeWindowsFallbacks()
    {
        if (OperatingSystem.IsWindows())
            Assert.Throws<PlatformNotSupportedException>(() =>
                PosixStatechartProcess.Start("missing", [], []));
        else
            Assert.Throws<System.ComponentModel.Win32Exception>(() =>
                PosixStatechartProcess.Start("/statechart-missing-native-probe", [], []));
    }

    // A failed exec keeps this process, so the caller receives the exec error instead of a replacement program.
    [SkippableFact]
    public void AFailedHandoffReturnsTheExecErrorAndKeepsTheProcess()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        var error = PosixStatechartProcess.Handoff("/statechart-missing-native-handoff", ["argument"], ["NAME=value"]);
        Assert.Equal("Statechart exec handoff failed.", error.Message);
    }

    [SkippableFact]
    public void GroupChecksRefuseAMissingGroupBeforeAnyNativeCall()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        Assert.Throws<Win32Exception>(() => PosixStatechartProcess.JoinGroup(0));
        Assert.Throws<IOException>(() => PosixStatechartProcess.RequireGroup(Environment.ProcessId, 0));
        Assert.Throws<IOException>(() => PosixStatechartProcess.RequireGroup(Environment.ProcessId, int.MaxValue));
    }

    // A task the kernel releases while ps reads it has no group yet: procps lists its pgid -1 unsigned, as 4294967295.
    [Fact]
    public void AGroupInspectionReadsATaskBeingReleasedAsNoMember()
    {
        Assert.False(PosixStatechartProcess.ListsLiveMember("   4321 Z\n4294967295 X+\n4294967295 Z+\n", 4321));
        Assert.True(PosixStatechartProcess.ListsLiveMember("4294967295 X+\n   4321 S\n", 4321));
        Assert.Throws<InvalidDataException>(() => PosixStatechartProcess.ListsLiveMember("   4321\n", 4321));
    }

    // A child that never became its own group leader cannot stand for an owned group, and a reaped one is gone for good.
    [SkippableFact]
    public async Task AnUnconfirmedChildIsNeitherAGroupNorReusableAfterItsReap()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        var child = PosixStatechartProcess.Start("/bin/sleep", ["60"], []);
        Assert.Throws<InvalidDataException>(() => child.ConfirmGroup(child.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => child.WaitForEmptyAsync(TimeSpan.FromSeconds(5)));
        child.Stop();
        Assert.Equal(9, child.Reap() & 0x7f);
        Assert.Throws<InvalidOperationException>(child.Stop);
        Assert.Throws<InvalidOperationException>(() => child.Reap());
    }

    // Exec inherits only descriptors without close-on-exec; dydo opens its own with it set.
    [SkippableFact]
    public async Task InheritLetsOnlyTheNamedDescriptorReachAnExecutedChild()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        Assert.Throws<Win32Exception>(() => PosixStatechartDescriptors.Inherit(-1));
        using var scope = new StatechartTestDirectory();
        using var held = new FileStream(Path.Combine(scope.Path, "held"), FileMode.CreateNew);
        var fd = held.SafeFileHandle.DangerousGetHandle().ToInt32();
        Assert.Equal(1, await ChildSees(fd));
        Assert.Equal(fd, PosixStatechartDescriptors.Inherit(held.SafeFileHandle.DangerousGetHandle()));
        Assert.Equal(0, await ChildSees(fd));
    }

    private static async Task<int> ChildSees(int fd)
    {
        using var child = Process.Start(new ProcessStartInfo("/bin/sh", ["-c", $"test -e /proc/self/fd/{fd}"]))!;
        await child.WaitForExitAsync();
        return child.ExitCode;
    }
}
