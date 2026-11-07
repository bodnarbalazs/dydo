namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;

// Runs dydo offline: proxies refuse every download, and PATH holds only the runtimes a test names.
internal static class StatechartOfflineProcess
{
    public static ProcessStartInfo Start(string root, string path, IEnumerable<string> arguments, string? java = null,
        ProcessStartInfo? launcher = null)
    {
        var start = launcher ?? StatechartTestBinary.Start();
        start.WorkingDirectory = root;
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("DYDO_", StringComparison.Ordinal)).ToArray())
            start.Environment.Remove(name);
        start.Environment["DYDO_STATECHART_ASSET_ROOT"] = Path.Combine(root, "assets");
        start.Environment["DYDO_STATECHART_DEPENDENCY_ROOT"] = Path.Combine(root, "dependencies");
        start.Environment["DYDO_STATECHART_STATE_ROOT"] = Path.Combine(root, "state");
        foreach (var name in new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY" }) start.Environment[name] = "http://127.0.0.1:9";
        start.Environment["NO_PROXY"] = "";
        start.Environment["PATH"] = path;
        start.Environment.Remove("TLA2TOOLS");
        if (java is null) start.Environment.Remove("JAVA");
        else start.Environment["JAVA"] = java;
        return start;
    }

    public static async Task<(int Exit, string Output, string Error)> Run(ProcessStartInfo start)
    {
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync();
        var errors = child.StandardError.ReadToEndAsync();
        await child.WaitForExitAsync();
        return (child.ExitCode, await output, await errors);
    }
}
