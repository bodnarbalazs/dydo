namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;
using System.Security.Cryptography;
using DynaDocs.Services.Statechart;

public class StatechartReadyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InstalledReadyInvocationRunsOfflineAndRejectsChangedManagedBytes(bool changed)
    {
        using var scope = new StatechartTestDirectory();
        var binary = Environment.GetEnvironmentVariable("STATECHART_TEST_BINARY");
        var fixture = Environment.GetEnvironmentVariable("STATECHART_TEST_DEPENDENCY_ROOT");
        Assert.True(binary is not null && File.Exists(binary), "STATECHART_TEST_BINARY is required; no source fallback.");
        Assert.True(fixture is not null && Path.IsPathFullyQualified(fixture) && Directory.Exists(fixture),
            "STATECHART_TEST_DEPENDENCY_ROOT must name the real task-local verified installation; no fake ready fixture.");
        var assets = new StatechartAssets();
        var relative = Path.Combine(assets.LockDigest, "win-x64");
        // The successful ready case reads the verified fixture at its stable runtime path.
        // Only the corruption case needs a private copy, and it rejects before Java launch.
        var dependencies = changed ? Path.Combine(scope.Path, "dependencies") : fixture!;
        if (changed) Copy(Path.Combine(fixture!, relative), Path.Combine(dependencies, relative));
        var renderer = Path.Combine(dependencies, relative, "pele", "content", "package", "dist", "pele.js");
        if (changed) File.AppendAllText(renderer, "\nchanged managed content");
        var before = Files(dependencies);
        File.WriteAllText(Path.Combine(scope.Path, "ReadyModel.tla"),
            "---- MODULE ReadyModel ----\nEXTENDS Naturals\nVARIABLE x\nInit == x = 0\nNext == (x < 2 /\\ x' = x + 1) \\/ (x = 2 /\\ x' = x)\nBound == x < 2\n====\n");
        File.WriteAllText(Path.Combine(scope.Path, "ReadyModel.cfg"), "INIT Init\nNEXT Next\nINVARIANT Bound\nCHECK_DEADLOCK FALSE\n");
        var start = new ProcessStartInfo(binary!) { WorkingDirectory = scope.Path, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "statechart", "ReadyModel.tla", "--out", "chart" }) start.ArgumentList.Add(argument);
        foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("DYDO_", StringComparison.Ordinal)).ToArray())
            start.Environment.Remove(name);
        start.Environment["DYDO_STATECHART_DEPENDENCY_ROOT"] = dependencies;
        start.Environment["DYDO_STATECHART_ASSET_ROOT"] = Path.Combine(scope.Path, "assets");
        start.Environment["DYDO_STATECHART_STATE_ROOT"] = Path.Combine(scope.Path, "state");
        foreach (var name in new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY" }) start.Environment[name] = "http://127.0.0.1:9";
        start.Environment["NO_PROXY"] = "";
        start.Environment["PATH"] = "";
        start.Environment.Remove("JAVA");
        start.Environment.Remove("TLA2TOOLS");
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync();
        var errors = child.StandardError.ReadToEndAsync();
        await child.WaitForExitAsync();
        var text = await output;
        var error = await errors;
        Assert.Equal(changed ? 2 : 1, child.ExitCode);
        if (changed)
        {
            Assert.Contains("Managed statechart content changed", error);
            Assert.False(Directory.Exists(Path.Combine(scope.Path, "chart")));
            Assert.EndsWith("changed managed content", File.ReadAllText(renderer));
        }
        else
        {
            Assert.Equal("", error);
            Assert.Contains("Bound breaks", text);
            Assert.Contains("shortest path has 2 steps", text);
            Assert.Contains("bugPath", File.ReadAllText(Path.Combine(scope.Path, "chart", "chart.svg")));
            Assert.All(Directory.GetFiles(Path.Combine(scope.Path, "state"), "scratch-*.journal"), journal =>
                Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(journal)));
        }
        Assert.Equal(before, Files(dependencies));
    }

    private static string[] Files(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal).Select(file => Path.GetRelativePath(root, file) + ":" +
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)))).ToArray();

    private static void Copy(string source, string target)
    {
        Assert.True(Directory.Exists(source), "Missing verified managed fixture component root.");
        Directory.CreateDirectory(target);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            var attributes = File.GetAttributes(entry);
            Assert.False((attributes & FileAttributes.ReparsePoint) != 0, "Windows proof fixture must contain plain archive files.");
            var destination = Path.Combine(target, Path.GetFileName(entry));
            if ((attributes & FileAttributes.Directory) != 0) Copy(entry, destination);
            else File.Copy(entry, destination, overwrite: false);
        }
    }
}
