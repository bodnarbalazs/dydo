namespace DynaDocs.Tests.Commands;

using System.Diagnostics;
using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Services;

[Collection("Integration")]
public class GapCheckCommandTests : IAsyncLifetime, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dydo-gap-check-" + Guid.NewGuid().ToString("N"));
    private readonly string _probeRoot;
    private readonly string _originalDirectory = Environment.CurrentDirectory;
    private string ProbeAssembly => Path.Combine(_probeRoot, "bin", "Debug", "net10.0", "Probe.dll");

    public GapCheckCommandTests()
    {
        Directory.CreateDirectory(_root);
        _probeRoot = Path.Combine(_root, "probe");
        Directory.CreateDirectory(_probeRoot);
    }

    public async Task InitializeAsync()
    {
        File.WriteAllText(Path.Combine(_probeRoot, "Probe.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(_probeRoot, "Program.cs"), ProbeProgram);

        var build = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _probeRoot,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        build.ArgumentList.Add("build");
        build.ArgumentList.Add("--nologo");
        build.ArgumentList.Add("Probe.csproj");
        using var process = Process.Start(build)!;
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDirectory;
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Launcher_ForwardsFixedAndCallerArgumentsWithConfigRootAndInheritedStreams()
    {
        var project = Project("forwarding project");
        WriteConfig(project, Runner("inspect", "", "fixed space", "fixed\"quote", "固定λ"));
        var caller = new[] { "", "caller space", "caller\"quote", "雪/é", "--unknown", "one", "--unknown", "two", "--", "--after" };

        var result = await RunDydo(project, [.. caller.Prepend("gap-check")]);

        Assert.Equal(23, result.ExitCode);
        using var output = JsonDocument.Parse(result.Stdout);
        Assert.Equal(new[] { "inspect", "", "fixed space", "fixed\"quote", "固定λ" }.Concat(caller).ToArray(),
            output.RootElement.GetProperty("arguments").EnumerateArray().Select(element => element.GetString()).ToArray());
        Assert.Equal(project, output.RootElement.GetProperty("currentDirectory").GetString());
        Assert.Equal("probe stderr", result.Stderr);
    }

    [Fact]
    public async Task LauncherInheritsSentinelStandardInputIntoTheRealRunner()
    {
        var project = Project("stdin project");
        WriteConfig(project, Runner("inspect-stdin"));
        var sentinel = "stdin-sentinel\0固定λ\r\n"u8.ToArray();

        var result = await RunDydo(project, ["gap-check"], sentinel);

        Assert.Equal(23, result.ExitCode);
        using var output = JsonDocument.Parse(result.Stdout);
        Assert.Equal(sentinel, Convert.FromBase64String(output.RootElement.GetProperty("stdin").GetString()!));
        Assert.Equal("probe stderr", result.Stderr);
    }

    [Fact]
    public async Task LauncherResolvesExplicitRelativeRunnerFromConfigRootBeforePathLookup()
    {
        var project = Project("relative runner project");
        var nested = Path.Combine(project, "nested", "start");
        Directory.CreateDirectory(nested);
        var runnerDirectory = Path.Combine(project, "relative runner");
        Directory.CreateDirectory(runnerDirectory);
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(ProbeAssembly)!))
            File.Copy(file, Path.Combine(runnerDirectory, Path.GetFileName(file)), overwrite: true);
        var collisionDirectory = Path.Combine(project, "path collision");
        Directory.CreateDirectory(collisionDirectory);
        File.WriteAllText(Path.Combine(collisionDirectory, "Probe.exe"), "not an executable");
        WriteConfig(project, [".\\relative runner\\Probe.exe", "inspect"]);

        var result = await RunDydo(nested, ["gap-check"], additionalPath: collisionDirectory);

        Assert.Equal(23, result.ExitCode);
        using var output = JsonDocument.Parse(result.Stdout);
        Assert.Equal(project, output.RootElement.GetProperty("currentDirectory").GetString());
    }

    [Fact]
    public async Task ProgramDispatchesGapCheckHelpAndVersionToTheRunnerWhileRootHelpAndVersionRemainAvailable()
    {
        var project = Project("help project");
        WriteConfig(project, Runner("inspect"));

        var gapHelp = await RunDydo(project, ["gap-check", "--help"]);
        var gapVersion = await RunDydo(project, ["gap-check", "--version"]);
        var rootHelp = await RunDydo(project, ["--help"]);
        var rootVersion = await RunDydo(project, ["--version"]);

        Assert.Equal(23, gapHelp.ExitCode);
        Assert.Equal(23, gapVersion.ExitCode);
        Assert.Contains("--help", gapHelp.Stdout);
        Assert.Contains("--version", gapVersion.Stdout);
        Assert.Equal(0, rootHelp.ExitCode);
        Assert.Contains("gap-check", rootHelp.Stdout);
        Assert.Equal(0, rootVersion.ExitCode);
        Assert.NotEmpty(rootVersion.Stdout);
    }

    [Fact]
    public async Task LauncherUsesTheNearestStrictConfiguration()
    {
        var outer = Project("outer");
        var nested = Path.Combine(outer, "nested", "start");
        Directory.CreateDirectory(nested);
        WriteConfig(outer, Runner("inspect", "outer"));
        WriteConfig(Path.Combine(outer, "nested"), Runner("inspect", "nested"));

        var result = await RunDydo(nested, ["gap-check"]);

        Assert.Equal(23, result.ExitCode);
        using var output = JsonDocument.Parse(result.Stdout);
        Assert.Equal("nested", output.RootElement.GetProperty("arguments")[1].GetString());
        Assert.Equal(Path.Combine(outer, "nested"), output.RootElement.GetProperty("currentDirectory").GetString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"testing\":[]}")]
    [InlineData("{\"testing\":{\"runner\":[]}}")]
    [InlineData("{\"testing\":{\"runner\":[\" \"]}}")]
    [InlineData("{\"testing\":{\"runner\":[1]}}")]
    public async Task LauncherConfigurationErrorsNameTestingRunner(string config)
    {
        var project = Project("invalid config");
        File.WriteAllText(Path.Combine(project, ConfigService.ConfigFileName), config);

        var (exitCode, _, stderr) = ConsoleCapture.All(() =>
            GapCheckCommand.ExecuteAsync([], project, CancellationToken.None).GetAwaiter().GetResult());

        Assert.Equal(2, exitCode);
        Assert.Contains("dydo.json testing.runner", stderr);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task LauncherMapsAnInvalidStartPathToTestingRunnerGuidance()
    {
        var (exitCode, _, stderr) = ConsoleCapture.All(() =>
            GapCheckCommand.ExecuteAsync([], "\0", CancellationToken.None).GetAwaiter().GetResult());

        Assert.Equal(2, exitCode);
        Assert.Contains("dydo.json testing.runner", stderr);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task LauncherErrorsWhenConfigurationOrRunnerIsUnavailable()
    {
        var missing = Project("no config");
        var noConfig = ConsoleCapture.All(() =>
            GapCheckCommand.ExecuteAsync([], missing, CancellationToken.None).GetAwaiter().GetResult());

        var missingRunner = Project("missing runner");
        WriteConfig(missingRunner, [Path.Combine(missingRunner, "not-there.exe")]);
        var unavailable = ConsoleCapture.All(() =>
            GapCheckCommand.ExecuteAsync([], missingRunner, CancellationToken.None).GetAwaiter().GetResult());

        var nonExecutable = Path.Combine(missingRunner, "not-an-executable.txt");
        File.WriteAllText(nonExecutable, "not executable");
        WriteConfig(missingRunner, [nonExecutable]);
        var notExecutable = ConsoleCapture.All(() =>
            GapCheckCommand.ExecuteAsync([], missingRunner, CancellationToken.None).GetAwaiter().GetResult());

        foreach (var result in new[] { noConfig, unavailable, notExecutable })
        {
            Assert.Equal(2, result.exitCode);
            Assert.Contains("dydo.json testing.runner", result.stderr);
        }
        Assert.Contains(Path.GetFullPath(missingRunner), unavailable.stderr);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task LauncherCancelsTheRunnerProcessTreeAndWaitsForItsDirectChild()
    {
        var project = Project("cancellation project");
        WriteConfig(project, Runner("tree", project));
        using var cancellation = new CancellationTokenSource();
        var launch = GapCheckCommand.ExecuteAsync([], project, cancellation.Token);
        var parentFile = Path.Combine(project, "parent.pid");
        var descendantFile = Path.Combine(project, "descendant.pid");
        await WaitForFile(parentFile);
        await WaitForFile(descendantFile);

        cancellation.Cancel();
        var exitCode = await launch.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, exitCode);
        await WaitForPidAbsent(int.Parse(File.ReadAllText(parentFile)));
        await WaitForPidAbsent(int.Parse(File.ReadAllText(descendantFile)));
    }

    private string Project(string name)
    {
        var directory = Path.Combine(_root, name);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private string[] Runner(params string[] fixedArguments) => ["dotnet", ProbeAssembly, .. fixedArguments];

    private static void WriteConfig(string project, string[] runner)
    {
        var config = JsonSerializer.Serialize(new { testing = new { runner } });
        File.WriteAllText(Path.Combine(project, ConfigService.ConfigFileName), config);
    }

    private static async Task WaitForFile(string path)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!File.Exists(path))
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Timed out waiting for {path}.");
            await Task.Delay(20);
        }
    }

    private static async Task WaitForPidAbsent(int pid)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (true)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                    return;
            }
            catch (ArgumentException)
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Process {pid} is still running.");
            await Task.Delay(20);
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunDydo(
        string directory,
        string[] arguments,
        byte[]? standardInput = null,
        string? additionalPath = null)
    {
        var runtime = Path.Combine(AppContext.BaseDirectory, "DynaDocs.Tests");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput != null
        };
        if (additionalPath != null)
            start.Environment["PATH"] = additionalPath + Path.PathSeparator + start.Environment["PATH"];
        foreach (var argument in new[]
                 {
                     "exec", "--runtimeconfig", runtime + ".runtimeconfig.json", "--depsfile", runtime + ".deps.json",
                     typeof(CheckCommand).Assembly.Location
                 }.Concat(arguments))
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (standardInput != null)
        {
            await process.StandardInput.BaseStream.WriteAsync(standardInput);
            process.StandardInput.Close();
        }
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        return (process.ExitCode, await stdout, await stderr);
    }

    private const string ProbeProgram = """
        using System;
        using System.Diagnostics;
        using System.IO;
        using System.Reflection;
        using System.Text.Json;
        using System.Threading;

        if (args[0] == "inspect" || args[0] == "inspect-stdin")
        {
            using var stdin = new MemoryStream();
            if (args[0] == "inspect-stdin")
                Console.OpenStandardInput().CopyTo(stdin);
            Console.Write(JsonSerializer.Serialize(new { arguments = args, currentDirectory = Environment.CurrentDirectory, stdin = Convert.ToBase64String(stdin.ToArray()) }));
            Console.Error.Write("probe stderr");
            return 23;
        }

        var folder = args[1];
        if (args[0] == "hold")
        {
            File.WriteAllText(Path.Combine(folder, "descendant.pid"), Environment.ProcessId.ToString());
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }

        File.WriteAllText(Path.Combine(folder, "parent.pid"), Environment.ProcessId.ToString());
        var child = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        child.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        child.ArgumentList.Add("hold");
        child.ArgumentList.Add(folder);
        using var process = Process.Start(child)!;
        Thread.Sleep(Timeout.Infinite);
        return 0;
        """;
}
