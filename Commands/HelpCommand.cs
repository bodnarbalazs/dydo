namespace DynaDocs.Commands;

using System.CommandLine;

public static class HelpCommand
{
    public static Command Create()
    {
        var command = new Command("help", "Display help information");
        command.SetAction(_ =>
        {
            PrintHelp();
            return 0;
        });
        return command;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("DynaDocs (dydo) - Documentation-driven context and agent orchestration for AI coding assistants.");
        Console.WriteLine();
        Console.WriteLine("Setup Commands:");
        Console.WriteLine("  init <integration>     Initialize DynaDocs (claude, codex, none)");
        Console.WriteLine("  init <int> --join      Join existing project as new team member");
        Console.WriteLine();
        Console.WriteLine("Documentation Commands:");
        Console.WriteLine("  check [path]           Validate docs, report violations");
        Console.WriteLine("  fix [path]             Auto-fix issues where possible");
        Console.WriteLine("  index [path]           Regenerate index.md from structure");
        Console.WriteLine("  graph <file>           Show graph connections for a file");
        Console.WriteLine("  graph stats [--top N]  Show top docs by incoming links");
        Console.WriteLine();
        Console.WriteLine("Workspace Commands:");
        Console.WriteLine("  guard                  Check if action is allowed (for hooks)");
        Console.WriteLine();
        Console.WriteLine("Role Commands:");
        Console.WriteLine("  sync                   Compile mode templates into native Claude + Codex agents/skills");
        Console.WriteLine();
        Console.WriteLine("Validation Commands:");
        Console.WriteLine("  validate               Validate config and templates");
        Console.WriteLine();
        Console.WriteLine("Template Commands:");
        Console.WriteLine("  template update        Update framework templates and docs");
        Console.WriteLine();
        Console.WriteLine("Task Commands:");
        Console.WriteLine("  task create <name>     Create a new task");
        Console.WriteLine("  task ready-for-review  Mark task ready for review");
        Console.WriteLine("  task done <name>       Mark task done after verification");
        Console.WriteLine("  task list              List tasks");
        Console.WriteLine();
        Console.WriteLine("  review complete <task> Complete a code review");
        Console.WriteLine();
        Console.WriteLine("Issue Commands:");
        Console.WriteLine("  issue create             Create a new issue");
        Console.WriteLine("  issue list               List issues");
        Console.WriteLine("  issue resolve <id>       Resolve an issue");
        Console.WriteLine();
        Console.WriteLine("Notion Commands:");
        Console.WriteLine("  notion connect         Store a Notion integration token locally");
        Console.WriteLine("  notion reveal-token    Print the stored Notion token (guarded)");
        Console.WriteLine("  notion sync            Reconcile dydo docs with a Notion workspace");
        Console.WriteLine("  notion reset           Wipe the tracked databases and recreate them from the model");
        Console.WriteLine();
        Console.WriteLine("Model Commands:");
        Console.WriteLine("  model cap <model>      Rebind an unavailable model's tiers to a fallback");
        Console.WriteLine("  model uncap <model>    Restore a capped model's tier bindings");
        Console.WriteLine("  model status           Show active model caps (target, fallback, reset time)");
        Console.WriteLine();
        Console.WriteLine("Utility:");
        Console.WriteLine("  completions <shell>    Generate shell completions (bash, zsh, powershell)");
        Console.WriteLine("  version                Display version information");
        Console.WriteLine("  help                   Display this help");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0 - Success / Action allowed");
        Console.WriteLine("  1 - Validation errors found");
        Console.WriteLine("  2 - Tool error / Action blocked");
        Console.WriteLine();
        Console.WriteLine("For detailed command reference, see: ./dydo/reference/dydo-commands.md");
    }
}
