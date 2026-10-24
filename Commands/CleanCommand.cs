namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;

/// <summary>
/// <c>dydo clean</c>: remove this repository's linked worktrees without losing work. The rules live in
/// <see cref="WorktreeCleaner"/>; the command only parses.
/// </summary>
public static class CleanCommand
{
    public static Command Create() => Create(WorktreeCleaner.DefaultT3WorktreeRoot);

    internal static Command Create(string t3WorktreeRoot)
    {
        var worktreesArgument = new Argument<string[]>("worktree")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Description = "Linked worktree to remove, by path or branch name"
        };
        var allOption = new Option<bool>("--all") { Description = "Every linked worktree of this repository" };
        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Print what would happen, including what would be archived; change nothing"
        };
        var archiveOption = new Option<bool>("--archive") { Description = "List the clean archive's entries" };

        var command = new Command("clean",
            "Remove linked worktrees safely: archive uncommitted work, keep unmerged branches. "
            + "Busy-worktree detection: Windows and Linux only.");
        command.Arguments.Add(worktreesArgument);
        command.Options.Add(allOption);
        command.Options.Add(dryRunOption);
        command.Options.Add(archiveOption);
        command.SetAction(parseResult =>
        {
            var cleaner = new WorktreeCleaner(Environment.CurrentDirectory, t3WorktreeRoot);
            if (parseResult.GetValue(archiveOption))
                return cleaner.ListArchive();
            return cleaner.Clean(parseResult.GetValue(worktreesArgument) ?? [],
                parseResult.GetValue(allOption), parseResult.GetValue(dryRunOption));
        });
        return command;
    }
}
