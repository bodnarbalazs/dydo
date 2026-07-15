namespace DynaDocs.Commands;

using System.CommandLine;

public static class WatchdogCommand
{
    // The watchdog's agent-lifecycle machinery was stripped in the 2.1.0 campaign (DR-041);
    // the process is awaiting its repurpose as the Notion-sync daemon (DR-041 resolved-3).
    // start/stop/run stay registered but are inert stubs so nothing that shells out to
    // `dydo watchdog …` breaks.
    private const string AwaitingMessage =
        "Watchdog is awaiting its Notion-sync repurpose (DR-041) — nothing to do.";

    public static Command Create()
    {
        var command = new Command("watchdog", "Manage the watchdog process (awaiting Notion-sync repurpose)");

        var startCommand = new Command("start", "Start the watchdog (stub — awaiting Notion-sync repurpose)");
        startCommand.SetAction(_ => { Console.WriteLine(AwaitingMessage); return 0; });

        var stopCommand = new Command("stop", "Stop the watchdog (stub — awaiting Notion-sync repurpose)");
        stopCommand.SetAction(_ => { Console.WriteLine(AwaitingMessage); return 0; });

        var runCommand = new Command("run", "Run the watchdog loop (stub — awaiting Notion-sync repurpose)");
        runCommand.Hidden = true;
        runCommand.SetAction(_ => { Console.WriteLine(AwaitingMessage); return 0; });

        command.Subcommands.Add(startCommand);
        command.Subcommands.Add(stopCommand);
        command.Subcommands.Add(runCommand);

        return command;
    }
}
