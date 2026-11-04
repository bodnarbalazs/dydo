namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using DynaDocs.Services.Statechart;
using System.Runtime.Versioning;

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

    // The IDE runtime lives under Program Files, which Linux lacks, so a JetBrains folder in the caller's directory is no Java.
    [SkippableFact]
    [UnsupportedOSPlatform("windows")]
    public async Task AnIdeJavaIsNeverLookedForOutsideWindows()
    {
        Skip.If(OperatingSystem.IsWindows(), "Windows looks for the IDE runtime under Program Files.");
        using var scope = new StatechartTestDirectory();
        Model(scope.Path);
        var java = Path.Combine(scope.Path, "JetBrains", "ide", "jbr", "bin", "java.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(java)!);
        File.WriteAllText(java, "#!/bin/sh\necho 'openjdk version \"21.0.1\"' >&2\n");
        File.SetUnixFileMode(java, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var result = await Invoke(scope.Path, ["statechart", "spaced model.tla"]);
        Assert.Equal(3, result.Exit);
        using var plan = JsonDocument.Parse(result.Output);
        Assert.Contains("java", plan.RootElement.GetProperty("missing").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
    }

    // Only managed content is repaired; a JAVA the caller named that cannot start stays the caller's error.
    [SkippableFact]
    [UnsupportedOSPlatform("windows")]
    public async Task AnExplicitJavaThatCannotExecuteIsAnErrorWithoutAPlan()
    {
        Skip.If(OperatingSystem.IsWindows(), "POSIX modes decide whether a runtime can start.");
        using var scope = new StatechartTestDirectory();
        Model(scope.Path);
        var java = Path.Combine(scope.Path, "java");
        File.WriteAllText(java, "#!/bin/sh\necho 'openjdk version \"21.0.1\"' >&2\n");
        File.SetUnixFileMode(java, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var result = await Invoke(scope.Path, ["statechart", "spaced model.tla"], java: java);
        Assert.Equal(2, result.Exit);
        Assert.Equal("", result.Output);
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "state", "requests")));
    }

    [Fact]
    public async Task DamagedManagedComponentYieldsARepairPlanAndIsLeftInPlace()
    {
        using var scope = new StatechartTestDirectory();
        Model(scope.Path);
        var assets = new StatechartAssets();
        var root = Path.Combine(scope.Path, "dependencies", assets.LockDigest, RuntimeInformation.RuntimeIdentifier);
        Directory.CreateDirectory(Path.Combine(root, "tlc"));
        File.WriteAllText(Path.Combine(root, ".statechart-store"), assets.LockDigest);
        var damaged = Path.Combine(root, "tlc", "tla2tools.jar");
        File.WriteAllText(damaged, "damaged managed bytes");
        var result = await Invoke(scope.Path, ["statechart", "spaced model.tla"]);
        Assert.Equal(3, result.Exit);
        using var plan = JsonDocument.Parse(result.Output);
        Assert.Equal("approval-required", plan.RootElement.GetProperty("state").GetString());
        Assert.Equal(["tlc"], plan.RootElement.GetProperty("repair").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("tlc", plan.RootElement.GetProperty("missing").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
        Assert.Equal("damaged managed bytes", File.ReadAllText(damaged));
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

    [Theory]
    [InlineData(1, 1, false, 1)]
    [InlineData(0, 0, false, 0)]
    [InlineData(1, 0, true, 0)]
    [InlineData(1, 0, false, 2)]
    [InlineData(7, 7, false, 2)]
    public async Task OnlyACompleteVerdictTheCheckerExitAgreesWithIsPublished(int verdict, int status, bool renderError, int expected)
    {
        using var scope = new StatechartTestDirectory();
        var done = Directory.CreateDirectory(Path.Combine(scope.Path, "stage", "done")).FullName;
        File.WriteAllText(Path.Combine(scope.Path, "stage", "verdict.json"), "{\"verdict\":" + verdict + "}");
        File.WriteAllText(Path.Combine(done, "report.md"), "Bound breaks");
        if (renderError) File.WriteAllText(Path.Combine(done, "render-error.json"), "{}");
        var destination = Path.Combine(scope.Path, "caller", "chart");
        using var output = new StringWriter();
        Assert.Equal(expected, await StatechartRunner.PublishAsync(Path.Combine(scope.Path, "stage"), status, destination, output,
            CancellationToken.None));
        Assert.Equal(expected != 2, File.Exists(Path.Combine(destination, "report.md")));
        Assert.Equal(expected == 2 ? "" : $"Bound breaks{Environment.NewLine}Wrote {destination}{Environment.NewLine}", output.ToString());
    }

    [Fact]
    public void PublicationReplacesOnlyAnEmptyPlainDestination()
    {
        using var scope = new StatechartTestDirectory();
        var done = Directory.CreateDirectory(Path.Combine(scope.Path, "done")).FullName;
        File.WriteAllText(Path.Combine(done, "report.md"), "complete");
        var destination = Directory.CreateDirectory(Path.Combine(scope.Path, "output")).FullName;
        StatechartRunner.Publish(done, destination);
        Assert.Equal("complete", File.ReadAllText(Path.Combine(destination, "report.md")));
        Assert.False(Directory.Exists(done));
    }

    [Theory]
    [InlineData("verdict.json")]
    [InlineData("done/report.md")]
    public async Task AnIncompleteStageIsNeverPublished(string missing)
    {
        using var scope = new StatechartTestDirectory();
        Directory.CreateDirectory(Path.Combine(scope.Path, "done"));
        File.WriteAllText(Path.Combine(scope.Path, "verdict.json"), "{\"verdict\":1}");
        File.WriteAllText(Path.Combine(scope.Path, "done", "report.md"), "Bound breaks");
        File.Delete(Path.Combine(scope.Path, missing));
        var destination = Path.Combine(scope.Path, "chart");
        Assert.Equal(2, await StatechartRunner.PublishAsync(scope.Path, 1, destination, TextWriter.Null, CancellationToken.None));
        Assert.False(Directory.Exists(destination));
    }

    [Fact]
    public async Task ACancelledRunPublishesNothing()
    {
        using var scope = new StatechartTestDirectory();
        Directory.CreateDirectory(Path.Combine(scope.Path, "done"));
        File.WriteAllText(Path.Combine(scope.Path, "verdict.json"), "{\"verdict\":0}");
        File.WriteAllText(Path.Combine(scope.Path, "done", "report.md"), "No error");
        var destination = Path.Combine(scope.Path, "chart");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            StatechartRunner.PublishAsync(scope.Path, 0, destination, TextWriter.Null, new CancellationToken(canceled: true)));
        Assert.False(Directory.Exists(destination));
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublicationRetainsAForeignFileOrDanglingLinkAtTheDestination(bool link)
    {
        Skip.If(link && OperatingSystem.IsWindows(), "Creating symbolic links needs Windows developer mode.");
        using var scope = new StatechartTestDirectory();
        var done = Directory.CreateDirectory(Path.Combine(scope.Path, "done")).FullName;
        File.WriteAllText(Path.Combine(done, "report.md"), "complete");
        var destination = Path.Combine(scope.Path, "output");
        if (link) File.CreateSymbolicLink(destination, Path.Combine(scope.Path, "missing"));
        else File.WriteAllText(destination, "foreign");
        Assert.Throws<IOException>(() => StatechartRunner.Publish(done, destination));
        Assert.Equal("complete", File.ReadAllText(Path.Combine(done, "report.md")));
        Assert.Equal(link, new FileInfo(destination).LinkTarget is not null);
        if (!link) Assert.Equal("foreign", File.ReadAllText(destination));
    }

    // Fake runtimes answer the version probes, so discovery runs its real owned probes without Node or Java.
    [SkippableTheory]
    [UnsupportedOSPlatform("windows")]
    [InlineData("v26.13.0", "21.0.1", "tlc,pele")]
    [InlineData("v22.17.0", "1.8.0_402", "node,java,tlc,pele")]
    public async Task DiscoveredRuntimesLeaveThePlanOnlyWhenTheirProbedVersionIsSupported(string node, string java, string missing)
    {
        Skip.If(OperatingSystem.IsWindows(), "POSIX modes decide whether a runtime can start.");
        using var scope = new StatechartTestDirectory();
        Model(scope.Path);
        File.WriteAllText(Path.Combine(scope.Path, "view.mjs"), "export default {};\n");
        var result = await Invoke(scope.Path, ["statechart", "spaced model.tla", "--view", "view.mjs"], path: Runtimes(scope.Path, node, java));
        Assert.Equal(3, result.Exit);
        Assert.Equal("", result.Error);
        using var plan = JsonDocument.Parse(result.Output);
        Assert.Equal(missing, string.Join(",", plan.RootElement.GetProperty("missing").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())));
        Assert.Equal(Path.Combine(scope.Path, "view.mjs"), plan.RootElement.GetProperty("view").GetString());
        Assert.All(Directory.GetFiles(Path.Combine(scope.Path, "state"), "scratch-*.journal"), journal =>
            Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(journal)));
    }

    [SkippableFact]
    [UnsupportedOSPlatform("windows")]
    public async Task AnExplicitJavaOlderThanElevenIsAnErrorWithoutAPlan()
    {
        Skip.If(OperatingSystem.IsWindows(), "POSIX modes decide whether a runtime can start.");
        using var scope = new StatechartTestDirectory();
        Model(scope.Path);
        var bin = Runtimes(scope.Path, "v26.13.0", "1.8.0_402");
        var result = await Invoke(scope.Path, ["statechart", "spaced model.tla"], java: Path.Combine(bin, "java"), path: bin);
        Assert.Equal(2, result.Exit);
        Assert.Equal("Statechart: Explicit JAVA must execute Java11 or later.", result.Error.Trim());
        Assert.Equal("", result.Output);
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "state", "requests")));
    }

    [Fact]
    public async Task ResumingACompletedRequestReturnsItsRecordedVerdictWithoutPreparingAgain()
    {
        using var scope = new StatechartTestDirectory();
        Model(scope.Path);
        var initial = await Invoke(scope.Path, ["statechart", "spaced model.tla", "--out", "chart"]);
        Assert.Equal(3, initial.Exit);
        using var plan = JsonDocument.Parse(initial.Output);
        var path = plan.RootElement.GetProperty("request").GetString()!;
        var store = new StatechartRequests(new StatechartHome(Path.Combine(scope.Path, "assets"), Path.Combine(scope.Path, "dependencies"),
            Path.Combine(scope.Path, "state")), new StatechartAssets(), RuntimeInformation.RuntimeIdentifier);
        var request = store.Load(path, validateInputs: false);
        Directory.CreateDirectory(request.Output);
        File.WriteAllText(Path.Combine(request.Output, "report.md"), "Bound breaks");
        store.Complete(path, request, 1);
        var resumed = await Invoke(scope.Path, ["statechart", "resume", "--request", path]);
        Assert.Equal(1, resumed.Exit);
        Assert.Equal(("", ""), (resumed.Output, resumed.Error));
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "dependencies")));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "failure-*.json"));
    }

    // The approved continuation installs only through the network; offline, it fails without placing any component.
    [Fact]
    public async Task AnApprovedResumeWithoutNetworkKeepsTheRequestAndInstallsNothing()
    {
        using var scope = new StatechartTestDirectory();
        Model(scope.Path);
        var initial = await Invoke(scope.Path, ["statechart", "spaced model.tla"]);
        Assert.Equal(3, initial.Exit);
        using var plan = JsonDocument.Parse(initial.Output);
        var path = plan.RootElement.GetProperty("request").GetString()!;
        var resumed = await Invoke(scope.Path, ["statechart", "resume", "--request", path]);
        Assert.Equal(2, resumed.Exit);
        Assert.StartsWith("Statechart: ", resumed.Error);
        Assert.True(File.Exists(path));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, "failure-*.json"));
        var store = Path.Combine(scope.Path, "dependencies", new StatechartAssets().LockDigest);
        Assert.Empty(Directory.GetDirectories(store, ".install-*"));
        Assert.Empty(Directory.GetDirectories(Path.Combine(store, RuntimeInformation.RuntimeIdentifier)));
    }

    // A framework-dependent dydo runs under the dotnet host; its relaunches name that host and the same assembly.
    [SkippableFact]
    public async Task AFrameworkDependentLaunchContinuesThroughTheSameHostAndAssembly()
    {
        var assembly = Path.ChangeExtension(StatechartTestBinary.Start().FileName, ".dll");
        Skip.IfNot(File.Exists(assembly), "The selected dydo build has no framework-dependent assembly.");
        var host = Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet"
            ? Environment.ProcessPath : Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        Skip.If(host is null, "No dotnet host is known to this test process.");
        using var scope = new StatechartTestDirectory();
        Model(scope.Path);
        var launcher = new ProcessStartInfo(host!);
        launcher.ArgumentList.Add(assembly);
        var result = await Invoke(scope.Path, ["statechart", "spaced model.tla"], launcher: launcher);
        Assert.True(result.Exit == 3, result.Error);
        using var plan = JsonDocument.Parse(result.Output);
        var continuation = plan.RootElement.GetProperty("installAndContinue").EnumerateArray().Select(item => item.GetString()!).ToArray();
        Assert.Equal("dotnet", Path.GetFileNameWithoutExtension(continuation[0]));
        Assert.Equal([assembly, "statechart", "resume", "--request", plan.RootElement.GetProperty("request").GetString()!], continuation[1..]);
    }

    [UnsupportedOSPlatform("windows")]
    private static string Runtimes(string root, string node, string java)
    {
        var bin = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
        foreach (var (name, script) in new[] { ("node", $"echo {node}"), ("java", $"echo 'openjdk version \"{java}\"' >&2") })
        {
            File.WriteAllText(Path.Combine(bin, name), "#!/bin/sh\n" + script + "\n");
            File.SetUnixFileMode(Path.Combine(bin, name), UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
        return bin;
    }

    private static void Model(string root)
    {
        File.WriteAllText(Path.Combine(root, "spaced model.tla"), "---- MODULE Model ----\nVARIABLE x\nInit == x = 0\nNext == x' = x\n====\n");
        File.WriteAllText(Path.Combine(root, "spaced model.cfg"), "INIT Init\nNEXT Next\nCHECK_DEADLOCK FALSE\n");
    }

    // Runs dydo offline: proxies refuse every download, and PATH holds only the runtimes a test names.
    private static async Task<(int Exit, string Output, string Error)> Invoke(string root, string[] arguments, string? java = null,
        string path = "", ProcessStartInfo? launcher = null)
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
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync();
        var errors = child.StandardError.ReadToEndAsync();
        await child.WaitForExitAsync();
        return (child.ExitCode, await output, await errors);
    }
}
