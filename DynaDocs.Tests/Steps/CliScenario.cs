namespace DynaDocs.Tests.Steps;

using System.Diagnostics;
using System.Collections;
using DynaDocs.Commands;
using Reqnroll;

[Binding]
public class CliScenario
{
    private bool _cleanupConfirmed = true;
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), $"dydo-acceptance-{Guid.NewGuid():N}");
    public CliResult Result { get; private set; } = new(-1, "", "");

    public CliScenario() => Directory.CreateDirectory(DirectoryPath);

    [AfterScenario]
    public void Cleanup()
    {
        Assert.True(_cleanupConfirmed, $"Child termination is unconfirmed; retained scratch directory: {DirectoryPath}");
        Directory.Delete(DirectoryPath, recursive: true);
    }

    public async Task RunAsync(params string[] arguments)
    {
        var runtime = Path.Combine(AppContext.BaseDirectory, "DynaDocs.Tests");
        string[] invocation = ["exec", "--runtimeconfig", runtime + ".runtimeconfig.json",
            "--depsfile", runtime + ".deps.json", typeof(CheckCommand).Assembly.Location, .. arguments];
        var environment = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value);
        Result = await RunProcessAsync(CreateStartInfo(DirectoryPath, invocation, environment),
            TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(5));
    }

    internal async Task<CliResult> RunProcessAsync(ProcessStartInfo start, TimeSpan timeout, TimeSpan cleanupTimeout)
    {
        Assert.True(_cleanupConfirmed, $"Previous child termination is unconfirmed; retained scratch directory: {DirectoryPath}");
        using var process = Process.Start(start)!;
        _cleanupConfirmed = false;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        var completion = Task.WhenAll(stdout, stderr, process.WaitForExitAsync());
        try
        {
            await completion.WaitAsync(timeout);
            _cleanupConfirmed = true;
        }
        catch (TimeoutException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                await completion.WaitAsync(cleanupTimeout);
                _cleanupConfirmed = true;
            }
            catch (Exception error)
            {
                throw new InvalidOperationException(
                    $"Could not confirm child termination and output completion; retained scratch directory: {DirectoryPath}", error);
            }
            throw new TimeoutException($"{start.FileName} {string.Join(' ', start.ArgumentList)} timed out.\n{await stdout}\n{await stderr}");
        }
        return new CliResult(process.ExitCode, await stdout, await stderr);
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
