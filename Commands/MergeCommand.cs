namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services.Merge;

/// <summary>
/// <c>dydo merge</c>: merge a reviewed PR into its <c>feature/*</c> base with a merge commit. The rules
/// live in <see cref="PullRequestMerge"/>; the command only parses. There is no repository,
/// base-selection or override option: the repository is origin's, the target is the PR's base, and
/// every refusal holds; <c>--match-base-commit</c> only adds one.
/// </summary>
public static class MergeCommand
{
    public static Command Create() => Create(host => new GhApi(host));

    internal static Command Create(Func<string, IGitHubApi> connect)
    {
        var prArgument = new Argument<int>("pr-number") { Description = "The pull request to merge" };
        // Both pins are validated by the merge itself, so a missing value is the same refusal as a short one.
        var headOption = new Option<string?>("--match-head-commit")
        {
            Description = "Required: the full 40-character SHA the reviewer passed; the PR head must equal it, and it is what merges"
        };
        var baseOption = new Option<string?>("--match-base-commit")
        {
            Description = "Required: the full 40-character SHA of the target branch tip you pinned; the PR's base branch must be "
                + "at it, or nothing merges. It only refuses; it never selects the target"
        };
        var dryRunOption = new Option<bool>("--dry-run") { Description = "Run every read and check; merge nothing" };

        var command = new Command("merge",
            "Merge a reviewed PR into its feature/* branch with a merge commit; master, main and the default branch are refused");
        command.Arguments.Add(prArgument);
        command.Options.Add(headOption);
        command.Options.Add(baseOption);
        command.Options.Add(dryRunOption);
        command.SetAction(parseResult => PullRequestMerge.Run(Environment.CurrentDirectory,
            parseResult.GetValue(prArgument), parseResult.GetValue(headOption), parseResult.GetValue(baseOption), parseResult.GetValue(dryRunOption), connect));
        return command;
    }
}
