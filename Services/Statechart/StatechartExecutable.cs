namespace DynaDocs.Services.Statechart;

using System.Diagnostics;

internal static class StatechartExecutable
{
    private static readonly AsyncLocal<string?> Relaunch = new();

    public static ProcessStartInfo Current()
    {
        var executable = Relaunch.Value ?? Environment.ProcessPath ?? throw new IOException("Cannot locate installed dydo executable.");
        var start = new ProcessStartInfo(executable);
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var assembly = Environment.GetCommandLineArgs()[0];
            if (!Path.IsPathFullyQualified(assembly) || !File.Exists(assembly))
                throw new IOException("Cannot locate the installed dydo assembly for private relaunch.");
            start.ArgumentList.Add(assembly);
        }
        return start;
    }

    // An in-process test runs inside a host that is not dydo; for its own async flow, private relaunches use this dydo build.
    internal static void RelaunchThrough(string? executable) => Relaunch.Value = executable;
}
