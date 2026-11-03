namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;

// The dydo build beside the tests; STATECHART_TEST_BINARY selects another build, such as a NativeAOT publish.
internal static class StatechartTestBinary
{
    public static ProcessStartInfo Start()
    {
        var name = OperatingSystem.IsWindows() ? "dydo.exe" : "dydo";
        var binary = Environment.GetEnvironmentVariable("STATECHART_TEST_BINARY") is { Length: > 0 } selected
            ? selected
            : Path.Combine(AppContext.BaseDirectory, name);
        Assert.True(File.Exists(binary), "Missing dydo test binary: " + binary);
        return new ProcessStartInfo(binary);
    }
}
