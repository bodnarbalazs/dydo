namespace DynaDocs.Tests.Steps;

using System.Diagnostics;
using System.Collections;
using DynaDocs.Commands;
using Reqnroll;

[Binding]
public class CliScenario
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), $"dydo-acceptance-{Guid.NewGuid():N}");
    public CliResult Result { get; private set; } = new(-1, "", "");

    public CliScenario() => Directory.CreateDirectory(DirectoryPath);

    [AfterScenario]
    public void Cleanup() => Directory.Delete(DirectoryPath, recursive: true);

    public async Task RunAsync(params string[] arguments)
    {
        var runtime = Path.Combine(AppContext.BaseDirectory, "DynaDocs.Tests");
        string[] invocation = ["exec", "--runtimeconfig", runtime + ".runtimeconfig.json",
            "--depsfile", runtime + ".deps.json", typeof(CheckCommand).Assembly.Location, .. arguments];
        var environment = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value);
        using var process = Process.Start(CreateStartInfo(DirectoryPath, invocation, environment))!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync()).WaitAsync(TimeSpan.FromSeconds(60));
        }
        catch (TimeoutException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var capturedOutput = stdout.IsCompletedSuccessfully ? stdout.Result : "stdout capture incomplete";
            var capturedError = stderr.IsCompletedSuccessfully ? stderr.Result : "stderr capture incomplete";
            throw new TimeoutException($"dydo {string.Join(' ', arguments)} timed out.\n{capturedOutput}\n{capturedError}");
        }
        Result = new CliResult(process.ExitCode, await stdout, await stderr);
    }

    internal static ProcessStartInfo CreateStartInfo(
        string directory, IEnumerable<string> arguments, IDictionary<string, string?> environment)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        start.Environment.Clear();
        foreach (var entry in environment.Where(entry => !entry.Key.StartsWith("DYDO_", StringComparison.OrdinalIgnoreCase)))
            start.Environment.Add(entry.Key, entry.Value);
        // An unrecognized, nonempty shell disables completion installation on every OS.
        start.Environment["SHELL"] = "dydo-acceptance-no-shell";
        return start;
    }
}
