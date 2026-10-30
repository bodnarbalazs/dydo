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
        Console.WriteLine("DynaDocs (dydo) - Durable project knowledge and native skills for AI coding assistants.");
        Console.WriteLine();
        Console.WriteLine("Setup Commands:");
        Console.WriteLine("  init <integration>     Initialize DynaDocs (claude, codex, all, none)");
        Console.WriteLine("  init <int> --join      Join existing project as new team member");
        Console.WriteLine("  adopt <version>        Record the dydo release the project adopted (dydoVersion)");
        Console.WriteLine();
        Console.WriteLine("Documentation Commands:");
        Console.WriteLine("  check [path]           Validate docs, report violations");
        Console.WriteLine("  check --root <dir>     Check another project (graph, validate also take --root)");
        Console.WriteLine("  fix [path]             Auto-fix issues where possible");
        Console.WriteLine("  index [path]           Regenerate index.md from structure");
        Console.WriteLine("  graph <file>           Show graph connections for a file");
        Console.WriteLine("  graph stats [--top N]  Show top docs by incoming links");
        Console.WriteLine();
        Console.WriteLine("Linear Commands:");
        Console.WriteLine("  map [--no-browser]     Serve a read-only map of a Linear Project on localhost");
        Console.WriteLine();
        Console.WriteLine("Workspace Commands:");
        Console.WriteLine("  guard                  Check if action is allowed (for hooks)");
        Console.WriteLine("  mode show              List this machine's mode flags (overnight)");
        Console.WriteLine("  mode set <flag> <val>  Set a mode flag, e.g. mode set overnight on");
        Console.WriteLine("  clean [<worktree>...]  Remove linked worktrees; archives uncommitted work, keeps unmerged branches");
        Console.WriteLine("  clean --all            Every linked worktree (--dry-run first; --archive lists the archive)");
        Console.WriteLine("  pr-ready [path]        Check a PR can be opened: clean tree, HEAD pushed (--body checks the body)");
        Console.WriteLine("  merge <pr> --match-head-commit <sha>");
        Console.WriteLine("                         Merge a reviewed PR into its feature/* branch (--dry-run checks only)");
        Console.WriteLine();
        Console.WriteLine("Validation Commands:");
        Console.WriteLine("  validate               Validate local configuration and nudges");
        Console.WriteLine("  gap-check [args]       Run the configured coverage gap check");
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
