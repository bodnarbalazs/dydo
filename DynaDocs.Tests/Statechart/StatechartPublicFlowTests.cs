namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;
using System.Text.Json;

public class StatechartPublicFlowTests
{
    [Fact]
    public async Task MissingPublicInvocationSavesOriginalInputsAndReturnsCompleteLockedPlanWithoutInstalling()
    {
        using var scope = new StatechartTestDirectory();
        Model(scope.Path);
        var result = await Invoke(scope.Path, ["statechart", "spaced model.tla", "--out", "chart output"]);
        Assert.Equal(3, result.Exit);
        Assert.Equal("", result.Error);
        using var plan = JsonDocument.Parse(result.Output);
        var root = plan.RootElement;
        Assert.Equal("approval-required", root.GetProperty("state").GetString());
        Assert.Equal(scope.Path, root.GetProperty("caller").GetString());
        Assert.Equal(Path.Combine(scope.Path, "spaced model.tla"), root.GetProperty("spec").GetString());
        Assert.Equal(Path.Combine(scope.Path, "chart output"), root.GetProperty("output").GetString());
        Assert.InRange(root.GetProperty("missing").GetArrayLength(), 3, 4);
        var missing = root.GetProperty("missing").EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToArray();
        Assert.All(new[] { "node", "tlc", "pele" }, name => Assert.Contains(name, missing));
        Assert.All(root.GetProperty("missing").EnumerateArray(), item =>
        {
            Assert.StartsWith("https://", item.GetProperty("url").GetString());
            Assert.True(item.GetProperty("compressedByteLimit").GetInt64() > 0);
        });
        var path = root.GetProperty("request").GetString()!;
        Assert.True(File.Exists(path));
        using var request = JsonDocument.Parse(File.ReadAllBytes(path));
        Assert.Equal(["spaced model.tla", "--out", "chart output"],
            request.RootElement.GetProperty("Arguments").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(path, root.GetProperty("installAndContinue").EnumerateArray().Last().GetString());
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "dependencies")));
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "chart output")));
    }

    [Fact]
    public async Task ChangedSavedInputsRejectResumeBeforeProvisioningAndPreserveRequest()
    {
        using var scope = new StatechartTestDirectory();
        Model(scope.Path);
        var initial = await Invoke(scope.Path, ["statechart", "spaced model.tla"]);
        Assert.Equal(3, initial.Exit);
        using var plan = JsonDocument.Parse(initial.Output);
        var path = plan.RootElement.GetProperty("request").GetString()!;
        File.AppendAllText(Path.Combine(scope.Path, "spaced model.tla"), "\nchanged during approval");
        var resumed = await Invoke(scope.Path, ["statechart", "resume", "--request", path]);
        Assert.Equal(2, resumed.Exit);
        Assert.Contains("changed", resumed.Error);
        Assert.True(File.Exists(path));
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "dependencies")));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, "failure-*.json"));
    }

    [Fact]
    public async Task InvalidExplicitOverrideIsAnErrorBeforeAnyDownloadPlan()
    {
        using var scope = new StatechartTestDirectory();
        Model(scope.Path);
        var result = await Invoke(scope.Path, ["statechart", "spaced model.tla"], java: "missing java.exe");
        Assert.Equal(2, result.Exit);
        Assert.Contains("Invalid explicit JAVA", result.Error);
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "dependencies")));
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "state", "requests")));
    }

    [Fact]
    public void PublicationRetainsAConcurrentForeignArrival()
    {
        using var scope = new StatechartTestDirectory();
        var done = Path.Combine(scope.Path, "done");
        var output = Path.Combine(scope.Path, "output");
        Directory.CreateDirectory(done);
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(done, "report.md"), "complete");
        File.WriteAllText(Path.Combine(output, "foreign"), "keep");
        Assert.Throws<IOException>(() => DynaDocs.Services.Statechart.StatechartRunner.Publish(done, output));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(output, "foreign")));
        Assert.Equal("complete", File.ReadAllText(Path.Combine(done, "report.md")));
    }

    private static void Model(string root)
    {
        File.WriteAllText(Path.Combine(root, "spaced model.tla"), "---- MODULE Model ----\nVARIABLE x\nInit == x = 0\nNext == x' = x\n====\n");
        File.WriteAllText(Path.Combine(root, "spaced model.cfg"), "INIT Init\nNEXT Next\nCHECK_DEADLOCK FALSE\n");
    }

    private static async Task<(int Exit, string Output, string Error)> Invoke(string root, string[] arguments, string? java = null)
    {
        var start = StatechartTestBinary.Start();
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
        start.Environment["PATH"] = "";
        start.Environment.Remove("TLA2TOOLS");
        if (java is null) start.Environment.Remove("JAVA");
        else start.Environment["JAVA"] = java;
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync();
        var errors = child.StandardError.ReadToEndAsync();
        await child.WaitForExitAsync();
        return (child.ExitCode, await output, await errors);
    }
}
