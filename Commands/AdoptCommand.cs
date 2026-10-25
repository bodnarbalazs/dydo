namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text.RegularExpressions;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// <c>dydo adopt &lt;version&gt;</c>: record the dydo release whose skills and documents the project
/// has adopted. dydo.json is off limits to guarded agents, so the update-dydo skill needs a dydo
/// command to write it.
/// </summary>
public static partial class AdoptCommand
{
    public static Command Create()
    {
        var versionArgument = new Argument<string>("version")
        {
            Description = "The adopted dydo release, e.g. 3.5.0 or v3.5.0"
        };

        var command = new Command("adopt", "Record the dydo release this project has adopted (dydoVersion)");
        command.Arguments.Add(versionArgument);
        command.SetAction(parseResult => Execute(parseResult.GetValue(versionArgument)!));
        return command;
    }

    private static int Execute(string value)
    {
        // Release tags carry a leading v; dydo.json records the bare version `dydo version` prints.
        var version = value.StartsWith('v') ? value[1..] : value;
        if (!ReleaseRegex().IsMatch(version))
        {
            ConsoleOutput.WriteError($"'{value}' is not a dydo release. Expected a version like 3.5.0, v3.5.0 or 3.5.0-beta.3.");
            return ExitCodes.ToolError;
        }

        var configService = new ConfigService();
        var configPath = configService.FindConfigFile();
        if (configPath == null)
        {
            ConsoleOutput.WriteError("No DynaDocs project found. Run 'dydo init <integration>' first to create one.");
            return ExitCodes.ToolError;
        }

        try
        {
            // Strict, so a dydo.json that fails to parse is reported rather than overwritten.
            var config = configService.LoadConfigStrict(Path.GetDirectoryName(configPath))!;
            config.DydoVersion = version;
            configService.SaveConfig(config, configPath);
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }

        Console.WriteLine($"Recorded dydoVersion {version} in {ConfigService.ConfigFileName}");
        return ExitCodes.Success;
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$")]
    private static partial Regex ReleaseRegex();
}
