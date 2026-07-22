namespace DynaDocs.Commands;

using System.CommandLine;
using System.Net.Http;
using DynaDocs.Services;
using DynaDocs.Sync.Notion;
using DynaDocs.Utils;

/// <summary>
/// The <c>dydo watchdog</c> command group — the Notion-sync daemon (ns-13, the DR-041 repurpose). <c>start</c>
/// spawns a detached background loop, <c>stop</c> ends it, and the hidden <c>run</c> is the loop itself. All the
/// process/loop mechanics live in <see cref="WatchdogService"/>; this file only parses options and resolves the
/// token/config the daemon needs.
/// </summary>
public static class WatchdogCommand
{
    public static Command Create()
    {
        var command = new Command("watchdog", "Run a background daemon that keeps the Notion board in sync");
        command.Subcommands.Add(CreateStartCommand());
        command.Subcommands.Add(CreateStopCommand());
        command.Subcommands.Add(CreateRunCommand());
        return command;
    }

    private static Option<int> IntervalOption() => new("--interval")
    {
        Description = $"Seconds between sync ticks (default {WatchdogService.DefaultIntervalSeconds}, floor {WatchdogService.MinIntervalSeconds}).",
        DefaultValueFactory = _ => WatchdogService.DefaultIntervalSeconds,
    };

    private static Option<int> CensusOption() => new("--census-interval")
    {
        Description = $"Ticks between full remote-archive censuses (default {WatchdogService.DefaultCensusInterval} ≈ hourly at the default interval).",
        DefaultValueFactory = _ => WatchdogService.DefaultCensusInterval,
    };

    private static Command CreateStartCommand()
    {
        var command = new Command("start", "Start the Notion-sync daemon in the background");
        var interval = IntervalOption();
        var census = CensusOption();
        command.Options.Add(interval);
        command.Options.Add(census);
        command.SetAction(parse =>
        {
            var config = new ConfigService();
            var token = ResolveToken(config);
            return WatchdogService.Start(
                config.GetDydoRoot(), parse.GetValue(interval), parse.GetValue(census),
                NotionSyncService.DaemonConfigError(token, config), Console.Out);
        });
        return command;
    }

    private static Command CreateStopCommand()
    {
        var command = new Command("stop", "Stop the running Notion-sync daemon");
        command.SetAction(_ => WatchdogService.Stop(new ConfigService().GetDydoRoot(), Console.Out));
        return command;
    }

    private static Command CreateRunCommand()
    {
        var command = new Command("run", "Run the Notion-sync loop in the foreground (used by `start`)") { Hidden = true };
        var interval = IntervalOption();
        var census = CensusOption();
        command.Options.Add(interval);
        command.Options.Add(census);
        command.SetAction(parse =>
        {
            var config = new ConfigService();
            var token = ResolveToken(config);
            var configError = NotionSyncService.DaemonConfigError(token, config);
            INotionClient? client = configError == null ? new NotionClient(new HttpClient(), token!) : null;
            return new WatchdogService().Run(
                config.GetDydoRoot(), parse.GetValue(interval), parse.GetValue(census),
                WatchdogService.ProvisionProbeInterval, configError,
                tick: (censusTick, validate) => NotionSyncService.DeltaTick(token!, config, _ => client!, censusTick, validate),
                keepRunning: _ => true,
                wait: Thread.Sleep,
                output: Console.Out);
        });
        return command;
    }

    /// <summary>Resolve the Notion token the same three-source way the manual sync does (local store → namespaced env
    /// → generic env). A daemon has no interactive TTY, so a passphrase-locked vault is not unlocked here — such a
    /// project runs the daemon with the passphrase in the environment, or uses the local store.</summary>
    private static string? ResolveToken(ConfigService config) =>
        NotionTokenResolver.Resolve(config.LoadConfig(), config.GetProjectRoot(), config.GetDydoRoot(), () => null);
}
