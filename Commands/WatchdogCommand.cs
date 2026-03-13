namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;

public static class WatchdogCommand
{
    public static Command Create()
    {
        var command = new Command("watchdog", "Manage the auto-close watchdog process");

        var startCommand = new Command("start", "Start the watchdog (if not already running)");
        startCommand.SetAction(_ =>
        {
            if (WatchdogService.EnsureRunning())
                Console.WriteLine("Watchdog started.");
            else
                Console.WriteLine("Watchdog is already running.");
            return 0;
        });

        var stopCommand = new Command("stop", "Stop the watchdog");
        stopCommand.SetAction(_ =>
        {
            if (WatchdogService.Stop())
                Console.WriteLine("Watchdog stopped.");
            else
                Console.WriteLine("Watchdog is already stopped.");
            return 0;
        });

        var runCommand = new Command("run", "Run the watchdog polling loop (internal)");
        runCommand.Hidden = true;
        runCommand.SetAction(_ =>
        {
            WatchdogService.Run();
            return 0;
        });

        command.Subcommands.Add(startCommand);
        command.Subcommands.Add(stopCommand);
        command.Subcommands.Add(runCommand);

        return command;
    }
}
