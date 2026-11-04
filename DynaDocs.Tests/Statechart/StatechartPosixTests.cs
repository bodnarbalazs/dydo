namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;

public class StatechartPosixTests
{
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
}
