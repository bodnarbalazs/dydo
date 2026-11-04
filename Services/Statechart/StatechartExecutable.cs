namespace DynaDocs.Services.Statechart;

using System.Diagnostics;

internal static class StatechartExecutable
{
    public static ProcessStartInfo Current()
    {
        var executable = Environment.ProcessPath ?? throw new IOException("Cannot locate installed dydo executable.");
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
}
