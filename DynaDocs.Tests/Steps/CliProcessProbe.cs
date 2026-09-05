namespace DynaDocs.Tests.Steps;

using System.Collections;
using System.Diagnostics;

public class CliProcessProbe : IAsyncLifetime
{
    private readonly CliScenario _build = new();
    private string AssemblyPath => Path.Combine(_build.DirectoryPath, "bin", "Debug", "net10.0", "Probe.dll");

    public async Task InitializeAsync()
    {
        File.WriteAllText(Path.Combine(_build.DirectoryPath, "Probe.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>
            """);
        File.WriteAllText(Path.Combine(_build.DirectoryPath, "Program.cs"), Program);
        var build = Start(_build.DirectoryPath, ["build", "Probe.csproj", "--nologo"]);
        var result = await _build.RunProcessAsync(build, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(5));
        result.AssertSuccess();
    }

    public ProcessStartInfo Command(string directory, params string[] arguments) =>
        Start(directory, [AssemblyPath, .. arguments]);

    private static ProcessStartInfo Start(string directory, string[] arguments)
    {
        var environment = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value);
        return CliScenario.CreateStartInfo(directory, arguments, environment);
    }

    public Task DisposeAsync()
    {
        _build.Cleanup();
        return Task.CompletedTask;
    }

    private const string Program = """
        using System;
        using System.Diagnostics;
        using System.IO;
        using System.Reflection;
        using System.Text.Json;
        using System.Threading;

        if (args[0] == "flood")
        {
            Console.Out.Write(new string('O', 1_300_000));
            Console.Error.Write(new string('E', 1_300_000));
            return 0;
        }
        if (args[0] == "arguments")
        {
            Console.Write(JsonSerializer.Serialize(args[1..]));
            return 0;
        }
        if (args[0] == "failure")
        {
            Console.Write("retained stdout");
            Console.Error.Write("retained stderr");
            return 23;
        }
        var folder = args[1];
        File.WriteAllText(Path.Combine(folder, args[0] + ".pid"), Environment.ProcessId.ToString());
        if (args[0] == "hold")
        {
            Console.WriteLine("descendant ready");
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }
        var child = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        child.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        child.ArgumentList.Add("hold");
        child.ArgumentList.Add(folder);
        using var process = Process.Start(child)!;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!File.Exists(Path.Combine(folder, "hold.pid")))
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("descendant did not start");
            Thread.Sleep(10);
        }
        Console.WriteLine("parent ready");
        Console.Error.WriteLine("parent stderr");
        if (args[0] == "orphan") return 0;
        Thread.Sleep(Timeout.Infinite);
        return 0;
        """;
}
