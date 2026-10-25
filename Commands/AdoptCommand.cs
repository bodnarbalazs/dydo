namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text.RegularExpressions;
using DynaDocs.Models;
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

        List<NudgeConfig> added;
        try
        {
            // Strict, so a dydo.json that fails to parse is reported rather than overwritten.
            var config = configService.LoadConfigStrict(Path.GetDirectoryName(configPath))!;
            added = ApplyMigrations(config, version);
            config.DydoVersion = version;
            configService.SaveConfig(config, configPath);
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }

        foreach (var nudge in added)
            Console.WriteLine($"Added {nudge.Match ?? "command"} nudge: {nudge.Message}");
        Console.WriteLine($"Recorded dydoVersion {version} in {ConfigService.ConfigFileName}");
        return ExitCodes.Success;
    }

    /// <summary>
    /// Nudges a release adds to existing projects, once: when the recorded dydoVersion is below
    /// the release (or absent) and the adopted version is at or above it, and the config holds no
    /// nudge with the same match and pattern. A later edit or removal stands.
    /// </summary>
    private static readonly (string Release, NudgeConfig Nudge)[] Migrations =
    [
        ("3.5.0", ConfigFactory.DocsOutsideDydoNudge),
    ];

    internal static List<NudgeConfig> ApplyMigrations(DydoConfig config, string adopted)
    {
        var added = new List<NudgeConfig>();
        foreach (var (release, nudge) in Migrations)
        {
            var crossed = CompareCore(adopted, release) >= 0
                && (config.DydoVersion == null || CompareCore(config.DydoVersion, release) < 0);
            if (!crossed || config.Nudges.Any(n => ConfigFactory.SameNudge(n, nudge)))
                continue;

            var copy = ConfigFactory.Copy(nudge);
            config.Nudges.Add(copy);
            added.Add(copy);
        }

        return added;
    }

    /// <summary>
    /// Orders versions by major.minor.patch only: a prerelease counts as its release, so
    /// 3.5.0-beta.3 crosses 3.5.0 and adopting 3.5.0 afterwards adds nothing again. A recorded
    /// value that is not a version sorts below every release.
    /// </summary>
    internal static int CompareCore(string a, string b) => ParseCore(a).CompareTo(ParseCore(b));

    private static Version ParseCore(string value)
    {
        var core = (value.StartsWith('v') ? value[1..] : value).Split('-', 2)[0];
        return Version.TryParse(core, out var parsed) ? parsed : new Version(0, 0);
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$")]
    private static partial Regex ReleaseRegex();
}
