namespace DynaDocs.Services;

/// <summary>
/// The branch changes a line makes before its pushes (DYD-887, DR 061 decision 7). A line's own
/// <c>git checkout</c> or <c>git switch</c> moves HEAD before its later commands run, so a push to the
/// current branch or its upstream that follows one is also judged on the branch that change leaves
/// HEAD on. A change counts wherever it runs: after any separator, in a pipe, a wrapper's command
/// string or a substitution, and in any git -C tree, since HEAD is the repository's state, not the
/// shell's. Each change before a push counts, as a failed one leaves HEAD where it was; a backtick
/// substitution, whose place is not kept, counts before every push. The branch is
/// <see cref="MergeRoute.UnreadableBranch"/> when the line does not say it for certain.
/// </summary>
internal static partial class PrCallScanner
{
    // A route, or the branch a branch change leaves HEAD on, in the order the line runs them.
    private readonly record struct Step(MergeRoute? Route = null, string? Branch = null);

    // checkout's -b, -B and --orphan and switch's -c, -C, --create, --force-create and --orphan name the branch they create.
    private static readonly HashSet<string> CreateOptions =
        new(StringComparer.Ordinal) { "-b", "-B", "-c", "-C", "--create", "--force-create", "--orphan" };

    private static readonly char[] UnreadableChars = ['$', '`', '(', ')', '*', '?', '['];

    // Each route once, with a push to the current branch or its upstream again for every branch change before it.
    private static List<MergeRoute> Followed(IEnumerable<Step> steps)
    {
        List<string> switches = [];
        List<MergeRoute> routes = [];
        foreach (var step in steps)
        {
            if (step.Route is not { } route)
            {
                switches.Add(step.Branch!);
                continue;
            }
            routes.Add(route);
            if (route.Destination is MergeRoute.CurrentBranch or MergeRoute.Upstream)
                routes.AddRange(switches.Select(branch => route with { SwitchedTo = branch }));
        }
        return [.. routes.Distinct()];
    }

    // The branch a git subcommand leaves HEAD on, or null when it changes no branch.
    private static string? SwitchedBranch(string subcommand, List<string> args) => subcommand switch
    {
        "checkout" => CheckoutBranch(args, isSwitch: false),
        "switch" => CheckoutBranch(args, isSwitch: true),
        "branch" => RenamedBranch(args),
        _ => null,
    };

    // gh pr checkout (or gh's own co alias) checks out a PR's head branch, which only GitHub knows.
    private static bool ChecksOutAPr(List<string> rest)
    {
        if (GhSubcommand(rest, "co") >= 0)
            return true;
        var sub = GhSubcommand(rest, "pr");
        if (sub < 0)
            return false;
        var parsed = Parse(rest[(sub + 1)..], PrOptions);
        return !parsed.Help && "checkout".Equals(parsed.Positionals.FirstOrDefault(), StringComparison.OrdinalIgnoreCase);
    }

    // checkout or switch: the branch it creates, else its one operand. A detach, --track (which names
    // the branch from a remote ref), a start point, a short-option cluster or a second switch operand is
    // unreadable; checkout of paths (after --, with -p or a pathspec file, or a tree-ish and paths) changes no branch.
    private static string? CheckoutBranch(List<string> args, bool isSwitch)
    {
        var dashes = args.IndexOf("--");
        var options = dashes < 0 ? args : args[..dashes];
        List<string> operands = dashes < 0 ? [] : args[(dashes + 1)..];
        if (!isSwitch && (operands.Count > 0 || options.Any(w => OptionName(w) is "-p" or "--patch" or "--pathspec-from-file")))
            return null;
        if (options.Any(w => HidesTheBranch(w, isSwitch)))
            return MergeRoute.UnreadableBranch;

        string? created = null;
        for (var i = 0; i < options.Count; i++)
        {
            var name = OptionName(options[i]);
            if (CreateOptions.Contains(name))
                created = name != options[i] ? options[i][(name.Length + 1)..] : options.ElementAtOrDefault(++i) ?? "";
            else if (!IsFlag(options[i]))
                operands.Add(options[i]);
        }
        if (created != null)
            return operands.Count == 0 ? Named(created) ?? MergeRoute.UnreadableBranch : MergeRoute.UnreadableBranch;
        return operands switch
        {
            [] => null,
            [var only] => Named(only),
            _ when isSwitch => MergeRoute.UnreadableBranch,
            _ => null,
        };
    }

    // A long option's name without its =value.
    private static string OptionName(string word) =>
        word.StartsWith("--", StringComparison.Ordinal) && word.IndexOf('=') is > 0 and var eq ? word[..eq] : word;

    // A detach, --track (whose branch a remote ref names) or a short-option cluster, which may hold -b.
    private static bool HidesTheBranch(string word, bool isSwitch) =>
        OptionName(word) is "--detach" or "-t" or "--track"
        || (isSwitch && word == "-d")
        || (word.Length > 2 && word[0] == '-' && word[1] != '-');

    // git branch -m or -M renames the current branch to its last operand when it names one branch, so
    // that name is judged; with two the current branch may be the one renamed, so it is judged too.
    private static string? RenamedBranch(List<string> args)
    {
        if (!args.Any(w => w is "-m" or "-M" or "--move"))
            return null;
        return args.LastOrDefault(w => !IsFlag(w)) is { } name ? Named(name) : null;
    }

    // A branch word: HEAD or @ stays where it is; -, @{-N}, a variable, a substitution or a glob
    // cannot be read offline.
    private static string? Named(string word)
    {
        if (word is "HEAD" or "@")
            return null;
        if (word.Length == 0 || word == "-" || word.Contains("@{", StringComparison.Ordinal) || word.IndexOfAny(UnreadableChars) >= 0)
            return MergeRoute.UnreadableBranch;
        return word.StartsWith("refs/heads/", StringComparison.Ordinal) ? word["refs/heads/".Length..] : word;
    }
}
