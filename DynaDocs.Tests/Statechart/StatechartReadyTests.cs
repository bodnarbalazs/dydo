namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynaDocs.Services.Statechart;
using System.Runtime.Versioning;

public class StatechartReadyTests
{
    private static readonly string Relative = Path.Combine(new StatechartAssets().LockDigest, RuntimeInformation.RuntimeIdentifier);

    // Runs on the verified fixture at its stable runtime path; only the other cases need a private copy. A fixture
    // an earlier build verified is re-stamped on this run: its receipts change, its content never does.
    [SkippableFact]
    public async Task InstalledReadyInvocationRunsOfflineWithContentUnchangedAndReceiptsCurrent()
    {
        using var scope = new StatechartTestDirectory();
        var fixture = Fixture();
        var before = Content(fixture);
        var result = await RunReadyModel(scope.Path, fixture);
        Assert.Equal(1, result.Exit);
        Assert.Equal("", result.Error);
        Assert.Contains("Bound breaks", result.Output);
        Assert.Contains("shortest path has 2 steps", result.Output);
        Assert.Contains("bugPath", File.ReadAllText(Path.Combine(scope.Path, "chart", "chart.svg")));
        Assert.All(Directory.GetFiles(Path.Combine(scope.Path, "state"), "scratch-*.journal"), journal =>
            Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(journal)));
        Assert.Equal(before, Content(fixture));
        AssertCurrentReceipts(fixture);
    }

    [SkippableFact]
    public async Task ChangedManagedBytesYieldARepairPlanAndStayInPlace()
    {
        using var scope = new StatechartTestDirectory();
        var dependencies = PrivateCopy(Fixture(), scope.Path);
        var renderer = Path.Combine(dependencies, Relative, "pele", "content", "package", "dist", "pele.js");
        File.AppendAllText(renderer, "\nchanged managed content");
        var before = Files(dependencies);
        var result = await RunReadyModel(scope.Path, dependencies);
        Assert.Equal(3, result.Exit);
        using var plan = JsonDocument.Parse(result.Output);
        Assert.Equal("approval-required", plan.RootElement.GetProperty("state").GetString());
        Assert.Equal(["pele"], plan.RootElement.GetProperty("repair").EnumerateArray().Select(item => item.GetString()));
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "chart")));
        Assert.EndsWith("changed managed content", File.ReadAllText(renderer));
        Assert.Equal(before, Files(dependencies));
    }

    [SkippableFact]
    [UnsupportedOSPlatform("windows")]
    public async Task AManagedRuntimeThatCannotExecuteYieldsARepairPlanAndStaysInPlace()
    {
        Skip.If(OperatingSystem.IsWindows(), "POSIX modes decide whether a runtime can start.");
        using var scope = new StatechartTestDirectory();
        var dependencies = PrivateCopy(Fixture(), scope.Path);
        var java = Directory.GetFiles(Path.Combine(dependencies, Relative, "java", "content"), "java", SearchOption.AllDirectories)
            .Single(path => Path.GetFileName(Path.GetDirectoryName(path)) == "bin");
        File.SetUnixFileMode(java, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var before = Files(dependencies);
        var result = await RunReadyModel(scope.Path, dependencies);
        Assert.True(result.Exit == 3, result.Error);
        using var plan = JsonDocument.Parse(result.Output);
        Assert.Equal(["java"], plan.RootElement.GetProperty("repair").EnumerateArray().Select(item => item.GetString()));
        Assert.True(File.Exists(plan.RootElement.GetProperty("request").GetString()));
        Assert.Equal(before, Files(dependencies));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(java));
    }

    // The run is offline, so the restored assets can only come from the payload embedded in dydo.
    [SkippableFact]
    public async Task DamagedAssetsAreExtractedAgainOfflineAndTheRunReachesItsVerdict()
    {
        using var scope = new StatechartTestDirectory();
        var fixture = Fixture();
        var first = await RunReadyModel(scope.Path, fixture);
        Assert.True(first.Exit == 1, first.Error);
        var assets = new StatechartAssets();
        var payload = Path.Combine(scope.Path, "assets", assets.Release, assets.Digest);
        File.AppendAllText(Path.Combine(payload, "skills", "engineering", "statechart", "scripts", "worker.ts"), "\n// tampered");
        File.Delete(Path.Combine(payload, "skills", "engineering", "statechart", "scripts", "java", "statechart-tlc-bootstrap.jar"));
        Directory.Delete(Path.Combine(scope.Path, "chart"), recursive: true);
        var second = await RunReadyModel(scope.Path, fixture);
        Assert.True(second.Exit == 1, second.Error);
        Assert.Contains("Bound breaks", second.Output);
        assets.Verify(payload);
    }

    [SkippableFact]
    public async Task AnUpdateThatKeepsTheLockReverifiesAndRestampsWithoutInstalling()
    {
        using var scope = new StatechartTestDirectory();
        var dependencies = PrivateCopy(Fixture(), scope.Path);
        var receipts = Directory.GetFiles(Path.Combine(dependencies, Relative), ".ready.json", SearchOption.AllDirectories);
        Assert.Equal(4, receipts.Length);
        foreach (var receipt in receipts)
        {
            var identity = JsonNode.Parse(File.ReadAllText(receipt))!.AsObject();
            identity["release"] = "0.0.1";
            identity["payload"] = "earlier-payload";
            File.WriteAllText(receipt, identity.ToJsonString());
        }
        var before = Content(dependencies);
        var result = await RunReadyModel(scope.Path, dependencies);
        Assert.True(result.Exit == 1, result.Error);
        Assert.DoesNotContain("approval-required", result.Output);
        Assert.Contains("Bound breaks", result.Output);
        AssertCurrentReceipts(dependencies);
        Assert.Equal(before, Content(dependencies));
    }

    // An explicit JAVA proves readiness with another Java, so the managed JRE's receipt keeps its earlier stamp.
    [SkippableFact]
    public async Task ARestampWritesOnlyTheComponentsTheVerifiedSelectionRan()
    {
        using var scope = new StatechartTestDirectory();
        var fixture = Fixture();
        var dependencies = PrivateCopy(fixture, scope.Path);
        foreach (var receipt in Directory.GetFiles(Path.Combine(dependencies, Relative), ".ready.json", SearchOption.AllDirectories))
        {
            var identity = JsonNode.Parse(File.ReadAllText(receipt))!.AsObject();
            identity["release"] = "0.0.1";
            File.WriteAllText(receipt, identity.ToJsonString());
        }
        var java = Directory.GetFiles(Path.Combine(fixture, Relative, "java", "content"), "java", SearchOption.AllDirectories)
            .Single(path => Path.GetFileName(Path.GetDirectoryName(path)) == "bin");
        WriteReadyModel(scope.Path);
        var result = await Run(scope.Path, dependencies, ["statechart", "ReadyModel.tla", "--out", "chart"], java);
        Assert.True(result.Exit == 1, result.Error);
        var releases = new[] { "java", "node", "pele", "tlc" }.Select(component => JsonNode.Parse(File.ReadAllText(
            Path.Combine(dependencies, Relative, component, ".ready.json")))!["release"]!.GetValue<string>());
        var current = new StatechartAssets().Release;
        Assert.Equal(["0.0.1", current, current, current], releases);
    }

    [SkippableFact]
    public async Task AnUnchangedSkillFromAnEarlierReleaseUpdatesThroughInstallSkillAndCharts()
    {
        using var scope = new StatechartTestDirectory();
        var fixture = Fixture();
        var project = Path.Combine(scope.Path, "project");
        Directory.CreateDirectory(project);
        var installed = await Run(scope.Path, fixture, ["statechart", "install-skill", "--root", project]);
        Assert.True(installed.Exit == 0, installed.Error);
        var skill = Path.Combine(project, "skills", "engineering", "statechart");
        File.AppendAllText(Path.Combine(skill, "SKILL.md"), "\nEarlier release wording.");
        var receipt = new JsonObject { ["schema"] = 1, ["protocol"] = 1, ["release"] = "0.0.1", ["payload"] = "earlier-payload",
            ["lock"] = "earlier-lock", ["files"] = StatechartInventory.Capture(skill, ".statechart-skill.json") };
        File.WriteAllText(Path.Combine(skill, ".statechart-skill.json"), receipt.ToJsonString());
        var stranded = await RunReadyModel(scope.Path, fixture, "--skill", skill);
        Assert.Equal(2, stranded.Exit);
        Assert.Contains("dydo statechart install-skill --root " + project, stranded.Error);
        var updated = await Run(scope.Path, fixture, ["statechart", "install-skill", "--root", project]);
        Assert.True(updated.Exit == 0, updated.Error);
        var result = await RunReadyModel(scope.Path, fixture, "--skill", skill);
        Assert.True(result.Exit == 1, result.Error);
        Assert.Contains("Bound breaks", result.Output);
        Assert.DoesNotContain("Earlier release wording", File.ReadAllText(Path.Combine(skill, "SKILL.md")));
    }

    // A fake runtime speaks the native bootstrap's startup gate, so the owned launch runs without Node, Java or TLC.
    [SkippableFact]
    [UnsupportedOSPlatform("windows")]
    public async Task TheNativeBootstrapStartsOnlyAfterGoInsideTheOwnedGroupAndReportsItsExit()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), "POSIX ownership runs on Linux.");
        using var scope = new StatechartTestDirectory();
        var node = FakeRuntime(scope.Path, "echo \"ready:1:$3\"\nread -r go\n[ \"$go\" = \"go:1:$3\" ] || exit 9\n" +
            "cat \"$4\"\necho\necho \"JAVA=$JAVA TLA2TOOLS=$TLA2TOOLS TMPDIR=$TMPDIR args=$1,$2\" >&2\nexit 1\n");
        var dependencies = new StatechartDependencies(node, "/managed/java", "/managed/tla2tools.jar", "/managed/pele.js");
        await using var lifetime = await PosixStatechartLifetime.StartAsync(StatechartTestBinary.Start(), scope.Path, scope.Path, scope.Path);
        await lifetime.RegisterAsync();
        var (exit, forwarded) = await Forwarded(() => StatechartNode.RunAsync(dependencies, "/payload",
            new JsonObject { ["mode"] = "render-ready" }, lifetime, scope.Path, CancellationToken.None));
        Assert.Equal(1, exit);
        var lines = forwarded.Split('\n');
        var configuration = JsonNode.Parse(lines[0])!;
        Assert.Equal("render-ready", configuration["mode"]!.GetValue<string>());
        Assert.Equal(lifetime.Stage, configuration["stage"]!.GetValue<string>());
        Assert.Equal("/managed/pele.js", configuration["renderer"]!.GetValue<string>());
        Assert.Equal("/payload/skills/engineering/statechart/references/render.md", configuration["renderGuide"]!.GetValue<string>());
        Assert.Equal($"JAVA=/managed/java TLA2TOOLS=/managed/tla2tools.jar TMPDIR={lifetime.Temp} " +
            "args=--disable-warning=ExperimentalWarning,/payload/skills/engineering/statechart/scripts/native-entry.ts", lines[1]);
        Assert.True(await lifetime.CompleteAsync());
    }

    [SkippableFact]
    [UnsupportedOSPlatform("windows")]
    public async Task ASilentBootstrapForwardsNothingAndReportsItsExit()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), "POSIX ownership runs on Linux.");
        using var scope = new StatechartTestDirectory();
        var node = FakeRuntime(scope.Path, "echo \"ready:1:$3\"\nread -r go\n[ \"$go\" = \"go:1:$3\" ] || exit 9\n");
        await using var lifetime = await PosixStatechartLifetime.StartAsync(StatechartTestBinary.Start(), scope.Path, scope.Path, scope.Path);
        await lifetime.RegisterAsync();
        var (exit, forwarded) = await Forwarded(() => StatechartNode.RunAsync(new StatechartDependencies(node, null, null, null),
            "/payload", new JsonObject { ["mode"] = "render-ready" }, lifetime, scope.Path, CancellationToken.None));
        Assert.Equal((0, ""), (exit, forwarded));
        Assert.True(await lifetime.CompleteAsync());
    }

    [SkippableFact]
    [UnsupportedOSPlatform("windows")]
    public async Task ABootstrapWithoutTheExactReadyFrameNeverReceivesGo()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), "POSIX ownership runs on Linux.");
        using var scope = new StatechartTestDirectory();
        var released = Path.Combine(scope.Path, "released");
        var node = FakeRuntime(scope.Path, $"echo ready:1\nread -r go\necho \"$go\" > {released}\n");
        await using var lifetime = await PosixStatechartLifetime.StartAsync(StatechartTestBinary.Start(), scope.Path, scope.Path, scope.Path);
        await lifetime.RegisterAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => StatechartNode.RunAsync(new StatechartDependencies(node, null, null, null),
            "/payload", new JsonObject { ["mode"] = "check" }, lifetime, scope.Path, CancellationToken.None));
        Assert.True(await lifetime.CompleteAsync());
        Assert.False(File.Exists(released) && File.ReadAllText(released).StartsWith("go:", StringComparison.Ordinal));
    }

    [SkippableFact]
    public async Task TheCheckingRuntimeIsConfiguredInsideTheOwnedStageAndStartsFromTheCaller()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), "POSIX ownership runs on Linux.");
        using var scope = new StatechartTestDirectory();
        var request = CapturedRequest(scope.Path);
        var dependencies = new StatechartDependencies("/managed/node", "/managed/java", "/managed/tla2tools.jar", "/managed/pele.js");
        var executable = StatechartTestBinary.Start();
        await using var owner = await PosixStatechartLifetime.StartAsync(executable, scope.Path, scope.Path, scope.Path);
        await owner.RegisterAsync();
        var start = await PosixStatechartRun.CheckingAsync(request, dependencies, "/payload", owner, executable,
            "/requests/continued/request.json", CancellationToken.None);
        var file = Path.Combine(owner.Stage, "posix-cli.json");
        Assert.Equal("/managed/node", start.FileName);
        Assert.Equal(request.Caller, start.WorkingDirectory);
        Assert.Equal(["--disable-warning=ExperimentalWarning", "/payload/skills/engineering/statechart/scripts/native-entry.ts",
            "posix-cli", file], start.ArgumentList);
        Assert.Equal(["/managed/java", "/managed/tla2tools.jar", owner.Temp, owner.Temp, owner.Temp],
            new[] { "JAVA", "TLA2TOOLS", "TEMP", "TMP", "TMPDIR" }.Select(name => start.Environment[name]));
        var configuration = JsonNode.Parse(File.ReadAllText(file))!;
        Assert.Equal("check", configuration["mode"]!.GetValue<string>());
        Assert.Equal(request.Id, configuration["request"]!["Id"]!.GetValue<string>());
        Assert.Equal([owner.Stage, owner.Temp, "/managed/pele.js", "/payload/skills/engineering/statechart/references/render.md",
                executable.FileName, "/requests/continued/request.json"],
            new[] { "stage", "temp", "renderer", "renderGuide", "nativeExecutable", "completedRequest" }
                .Select(name => configuration[name]!.GetValue<string>()));
        Assert.Empty(configuration["nativeArguments"]!.AsArray());
        Assert.True(await owner.CompleteAsync());
    }

    // The exec fails here, so the handoff error reaches this process and the independent owner still drains and cleans up.
    [SkippableFact]
    public async Task AFailedRuntimeHandoffSurfacesAndTheOwnerStillCleansUp()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), "POSIX ownership runs on Linux.");
        using var scope = new StatechartTestDirectory();
        var request = CapturedRequest(scope.Path);
        var home = new StatechartHome(Path.Combine(scope.Path, "assets"), Path.Combine(scope.Path, "dependencies"),
            Directory.CreateDirectory(Path.Combine(scope.Path, "state")).FullName);
        var dependencies = new StatechartDependencies(Path.Combine(scope.Path, "missing-node"), null, null, null);
        var directory = Environment.CurrentDirectory;
        try
        {
            var error = await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() => PosixStatechartRun.RunAsync(
                request, dependencies, "/payload", home, null, StatechartTestBinary.Start(), CancellationToken.None));
            Assert.Equal("Statechart exec handoff failed.", error.Message);
            Assert.Equal(request.Caller, Environment.CurrentDirectory);
        }
        finally { Environment.CurrentDirectory = directory; }
        var journals = Directory.GetFiles(home.State, "scratch-*.journal");
        Assert.Equal(2, journals.Length);
        Assert.All(journals, journal => Assert.EndsWith("deleted-after-authoritative-drain\n", File.ReadAllText(journal)));
        Assert.False(Directory.Exists(request.Output));
    }

    // Cancellation before the handoff stops the run before any runtime starts; the owner still drains and cleans up.
    [SkippableFact]
    public async Task ACancelledRunNeverHandsOffAndTheOwnerStillCleansUp()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), "POSIX ownership runs on Linux.");
        using var scope = new StatechartTestDirectory();
        var request = CapturedRequest(scope.Path);
        var home = new StatechartHome(Path.Combine(scope.Path, "assets"), Path.Combine(scope.Path, "dependencies"),
            Directory.CreateDirectory(Path.Combine(scope.Path, "state")).FullName);
        var directory = Environment.CurrentDirectory;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PosixStatechartRun.RunAsync(request,
            new StatechartDependencies("/managed/node", null, null, null), "/payload", home, null, StatechartTestBinary.Start(),
            new CancellationToken(canceled: true)));
        Assert.Equal(directory, Environment.CurrentDirectory);
        var journals = Directory.GetFiles(home.State, "scratch-*.journal");
        Assert.Equal(2, journals.Length);
        Assert.All(journals, journal => Assert.EndsWith("deleted-after-authoritative-drain\n", File.ReadAllText(journal)));
    }

    private static StatechartRequest CapturedRequest(string root)
    {
        var caller = Directory.CreateDirectory(Path.Combine(root, "caller")).FullName;
        WriteReadyModel(caller);
        return StatechartRequest.Capture(["ReadyModel.tla", "--out", "chart"], caller, new StatechartAssets(), RuntimeInformation.RuntimeIdentifier);
    }

    [UnsupportedOSPlatform("windows")]
    private static string FakeRuntime(string root, string script)
    {
        var path = Path.Combine(root, "fake-node");
        File.WriteAllText(path, "#!/bin/sh\n" + script);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        return path;
    }

    // The native bootstrap forwards the child's output to this process's standard error.
    private static async Task<(int Exit, string Forwarded)> Forwarded(Func<Task<int>> run)
    {
        var original = Console.Error;
        using var captured = new StringWriter();
        Console.SetError(captured);
        try { return (await run(), captured.ToString()); }
        finally { Console.SetError(original); }
    }

    private static string[] Content(string dependencies) => Files(Path.Combine(dependencies, Relative))
        .Where(file => !file.Contains(".ready.json", StringComparison.Ordinal)).ToArray();

    private static void AssertCurrentReceipts(string dependencies)
    {
        var assets = new StatechartAssets();
        var receipts = Directory.GetFiles(Path.Combine(dependencies, Relative), ".ready.json", SearchOption.AllDirectories);
        Assert.Equal(4, receipts.Length);
        Assert.All(receipts, receipt =>
        {
            var identity = JsonNode.Parse(File.ReadAllText(receipt))!.AsObject();
            Assert.Equal(assets.Release, identity["release"]!.GetValue<string>());
            Assert.Equal(assets.Digest, identity["payload"]!.GetValue<string>());
        });
    }

    private static string Fixture()
    {
        var fixture = Environment.GetEnvironmentVariable("STATECHART_TEST_DEPENDENCY_ROOT");
        Skip.If(fixture is null, "STATECHART_TEST_DEPENDENCY_ROOT must name a real verified managed installation (Node, Java, TLC, Pele).");
        Assert.True(Path.IsPathFullyQualified(fixture) && Directory.Exists(fixture),
            "STATECHART_TEST_DEPENDENCY_ROOT must name the real task-local verified installation; no fake ready fixture.");
        return fixture;
    }

    private static string PrivateCopy(string fixture, string scope)
    {
        var dependencies = Path.Combine(scope, "dependencies");
        Copy(Path.Combine(fixture, Relative), Path.Combine(dependencies, Relative));
        return dependencies;
    }

    // Runs the built dydo offline on a model whose Bound breaks after two steps.
    private static Task<(int Exit, string Output, string Error)> RunReadyModel(string scope, string dependencies, params string[] options)
    {
        WriteReadyModel(scope);
        return Run(scope, dependencies, ["statechart", "ReadyModel.tla", "--out", "chart", .. options]);
    }

    private static void WriteReadyModel(string scope)
    {
        File.WriteAllText(Path.Combine(scope, "ReadyModel.tla"),
            "---- MODULE ReadyModel ----\nEXTENDS Naturals\nVARIABLE x\nInit == x = 0\nNext == (x < 2 /\\ x' = x + 1) \\/ (x = 2 /\\ x' = x)\nBound == x < 2\n====\n");
        File.WriteAllText(Path.Combine(scope, "ReadyModel.cfg"), "INIT Init\nNEXT Next\nINVARIANT Bound\nCHECK_DEADLOCK FALSE\n");
    }

    // Runs the built dydo offline with the given managed dependencies and private asset and state roots.
    private static async Task<(int Exit, string Output, string Error)> Run(string scope, string dependencies, string[] arguments,
        string? java = null)
    {
        var start = StatechartTestBinary.Start();
        start.WorkingDirectory = scope;
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("DYDO_", StringComparison.Ordinal)).ToArray())
            start.Environment.Remove(name);
        start.Environment["DYDO_STATECHART_DEPENDENCY_ROOT"] = dependencies;
        start.Environment["DYDO_STATECHART_ASSET_ROOT"] = Path.Combine(scope, "assets");
        start.Environment["DYDO_STATECHART_STATE_ROOT"] = Path.Combine(scope, "state");
        foreach (var name in new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY" }) start.Environment[name] = "http://127.0.0.1:9";
        start.Environment["NO_PROXY"] = "";
        start.Environment["PATH"] = "";
        if (java is null) start.Environment.Remove("JAVA");
        else start.Environment["JAVA"] = java;
        start.Environment.Remove("TLA2TOOLS");
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync();
        var errors = child.StandardError.ReadToEndAsync();
        await child.WaitForExitAsync();
        return (child.ExitCode, await output, await errors);
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
            var directory = (attributes & FileAttributes.Directory) != 0;
            var destination = Path.Combine(target, Path.GetFileName(entry));
            // POSIX runtime archives carry relative links; the managed inventory records them as links.
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                var link = directory ? new DirectoryInfo(entry).LinkTarget! : new FileInfo(entry).LinkTarget!;
                if (directory) Directory.CreateSymbolicLink(destination, link);
                else File.CreateSymbolicLink(destination, link);
            }
            else if (directory) Copy(entry, destination);
            else File.Copy(entry, destination, overwrite: false);
        }
    }
}
