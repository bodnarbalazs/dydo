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
        var configService = new ConfigService();
        var configPath = configService.FindConfigFile(startPath);
        if (configPath == null)
            return Error($"dydo.json testing.runner is required; no dydo.json was found from {Path.GetFullPath(startPath)}.");

        var absoluteConfigPath = Path.GetFullPath(configPath);
        try
        {
            var config = configService.LoadConfigStrict(startPath)!;
            var runner = config.Testing?.Runner;
            if (runner == null)
                return Error($"dydo.json testing.runner is required in {absoluteConfigPath}.");
            var configDirectory = Path.GetDirectoryName(absoluteConfigPath)!;
            var runnerPath = Path.GetFullPath(runner[0], configDirectory);
            var start = new ProcessStartInfo(runner[0])
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
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
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
            return Error($"dydo.json testing.runner is invalid in {absoluteConfigPath}: {ex.Message}");
        }
        catch (IOException ex)
        {
            return Error($"dydo.json testing.runner could not read {absoluteConfigPath}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Error($"dydo.json testing.runner could not read {absoluteConfigPath}: {ex.Message}");
        }
    }

    private static int Error(string message)
    {
        Console.Error.WriteLine($"gap-check: {message}");
        return ExitCodes.ToolError;
    }
}
