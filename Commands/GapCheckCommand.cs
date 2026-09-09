namespace DynaDocs.Commands;

using System.ComponentModel;
using System.Diagnostics;
using System.CommandLine;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class GapCheckCommand
{
    public static Command Create()
    {
        var command = new Command("gap-check", "Run the configured coverage gap check");
        command.SetAction(_ => ExecuteAsync([], Environment.CurrentDirectory, CancellationToken.None));
        return command;
    }

    internal static async Task<int> ExecuteAsync(
        IReadOnlyList<string> callerArguments,
        string startPath,
        CancellationToken cancellationToken)
    {
        string? absoluteConfigPath = null;
        try
        {
            var configService = new ConfigService();
            var configPath = configService.FindConfigFile(startPath);
            if (configPath == null)
                return Error($"dydo.json testing.runner is required; no dydo.json was found from {Path.GetFullPath(startPath)}.");

            absoluteConfigPath = Path.GetFullPath(configPath);
            var config = configService.LoadConfigStrict(startPath)!;
            var runner = config.Testing?.Runner;
            if (runner == null)
                return Error($"dydo.json testing.runner is required in {absoluteConfigPath}.");
            var configDirectory = Path.GetDirectoryName(absoluteConfigPath)!;
            var executable = IsExplicitPath(runner[0])
                ? Path.GetFullPath(runner[0], configDirectory)
                : runner[0];
            var runnerPath = IsExplicitPath(runner[0])
                ? executable
                : Path.GetFullPath(runner[0], configDirectory);
            var start = new ProcessStartInfo(executable)
            {
                WorkingDirectory = configDirectory,
                UseShellExecute = false
            };
            foreach (var argument in runner.Skip(1))
                start.ArgumentList.Add(argument);
            foreach (var argument in callerArguments)
                start.ArgumentList.Add(argument);

            Process? process;
            try
            {
                process = Process.Start(start);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                return Error($"dydo.json testing.runner could not start {runnerPath} from {absoluteConfigPath}: {ex.Message}");
            }

            if (process == null)
                return Error($"dydo.json testing.runner could not start {runnerPath} from {absoluteConfigPath}.");

            using (process)
            {
                try
                {
                    await process.WaitForExitAsync(cancellationToken);
                    return process.ExitCode;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException) when (process.HasExited)
                    {
                    }

                    await process.WaitForExitAsync(CancellationToken.None);
                    return Error($"dydo.json testing.runner was cancelled after starting {runnerPath} from {absoluteConfigPath}.");
                }
            }
        }
        catch (InvalidDataException ex)
        {
            return Error($"dydo.json testing.runner is invalid in {absoluteConfigPath ?? "the selected configuration"}: {ex.Message}");
        }
        catch (IOException ex)
        {
            return Error($"dydo.json testing.runner could not read {absoluteConfigPath ?? "the selected configuration"}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Error($"dydo.json testing.runner could not read {absoluteConfigPath ?? "the selected configuration"}: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return Error($"dydo.json testing.runner could not resolve the start or runner path: {ex.Message}");
        }
    }

    private static bool IsExplicitPath(string executable) =>
        Path.IsPathRooted(executable)
        || executable.StartsWith(".", StringComparison.Ordinal)
        || executable.Contains(Path.DirectorySeparatorChar)
        || executable.Contains(Path.AltDirectorySeparatorChar);

    private static int Error(string message)
    {
        Console.Error.WriteLine($"gap-check: {message}");
        return ExitCodes.ToolError;
    }
}
