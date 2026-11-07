namespace DynaDocs.Services;

/// <summary>
/// The branch changes a line makes before its pushes (DYD-887, DR 061 decision 7). A line's own
/// <c>git checkout</c>, <c>git switch</c>, <c>git branch -m</c> or <c>gh pr checkout</c> moves HEAD
/// before its later commands run, so a push to the current branch or its upstream carries every change
/// the line runs before it, in order: after any separator, in a pipe or in a wrapper's command string.
/// A change only <c>&amp;&amp;</c> separates from the push is <see cref="BranchChange.Chained"/>. The
/// guard keeps the changes in the push's own worktree and resolves each named branch there. The order
/// bash runs substitutions in is not read: a line that changes branch and pushes, where a substitution
/// (backtick or <c>$( ... )</c>) holds a change or a push, makes every push <see cref="MergeRoute.Unreadable"/>.
/// </summary>
internal static partial class PrCallScanner
{
    // A route or a branch change, in the order the line runs them. Run is the && run it is in: each
    // command of a run runs only after the one before it succeeds. A step of no run has none.
    // Substituted: a substitution holds it.
    private readonly record struct Step(MergeRoute? Route = null, BranchChange? Change = null, object? Run = null, bool Substituted = false);

    // A branch change as its words say it. Sure: when it succeeds, HEAD is on Branch.
    private readonly record struct Switch(string Branch, bool Creates = false, bool Sure = true);

    // checkout's and switch's words, read with git's options (git-checkout(1), git-switch(1)).
    private sealed class CheckoutWords
    {
        public string? Created { get; set; }
        public List<string> Operands { get; } = [];
        public bool Paths { get; set; }
        public bool Unreadable { get; set; }
    }

    // checkout's -b, -B and --orphan and switch's -c, -C, --create, --force-create and --orphan name the branch they create.
    private static readonly HashSet<string> CreateOptions =
        new(StringComparer.Ordinal) { "-b", "-B", "-c", "-C", "--create", "--force-create", "--orphan" };

    // The other options of checkout or switch that take a value, which may be the next word.
    private static readonly HashSet<string> CheckoutValueOptions = new(StringComparer.Ordinal) { "--conflict", "--pathspec-from-file" };

    // The flags of checkout or switch that take no value; each long one also has a --no- form.
    private static readonly HashSet<string> CheckoutFlags = new(StringComparer.Ordinal)
    {
        "-q", "--quiet", "--progress", "-f", "--force", "--discard-changes", "--ours", "--theirs", "-2", "-3", "--no-track",
        "--guess", "-l", "--ignore-skip-worktree-bits", "-m", "--merge", "-p", "--patch", "--ignore-other-worktrees",
        "--overwrite-ignore", "--recurse-submodules", "--overlay", "--pathspec-file-nul",
    };

    private static readonly char[] UnreadableChars = ['$', '`', '(', ')', '*', '?', '['];

    private static readonly char[] SeparatorChars = [';', '|', '(', ')', '{', '}', '#', '`', '\n', '\r'];

    // Each route once; a push to the current branch or its upstream carries the branch changes before
    // it, and every push is unreadable when the line changes branch and pushes with a substitution
    // holding either (DYD-887).
    private static List<MergeRoute> Followed(List<Step> steps)
    {
        var unreadable = steps.Any(IsChange) && steps.Any(IsPush) && steps.Any(step => step.Substituted && (IsChange(step) || IsPush(step)));
        List<Step> changes = [];
        List<MergeRoute> routes = [];
        foreach (var step in steps)
        {
            if (IsChange(step))
                changes.Add(step);
            else
                routes.Add(unreadable && IsPush(step) ? step.Route! with { Unreadable = true } : Carrying(step, changes));
        }
        return [.. routes.Distinct()];
    }

    private static bool IsChange(Step step) => step.Change != null;

    private static bool IsPush(Step step) => step.Route?.Kind == MergeRouteKind.Push;

    // A route, a push to the current branch or its upstream carrying the changes before it.
    private static MergeRoute Carrying(Step step, List<Step> changes) =>
        changes.Count > 0 && step.Route!.Destination is MergeRoute.CurrentBranch or MergeRoute.Upstream
            ? step.Route! with { Changes = new([.. changes.Select(change => change.Change! with { Chained = SameRun(change, step) })]) }
            : step.Route!;

    private static bool SameRun(Step change, Step push) => change.Run != null && ReferenceEquals(change.Run, push.Run);

    // A reading's && runs, per scope: a command continues the run of the command before it in its
    // scope when nothing but && (and that command's redirections) separates them. && and || bind
    // alike, left to right, so in a || b && c the c runs when a succeeds and b never ran: a command
    // after || (or after a separator the text does not show) gets a run of its own for its own steps,
    // while the commands after it continue the run.
    private sealed class Runs
    {
        private readonly Dictionary<ShellReader.Scope, (ShellReader.Command Last, object Run)> _last = new();

        public (object Run, object? Own) Of(ShellReader.Command command)
        {
            var prior = _last.TryGetValue(command.Source, out var last);
            var gap = prior ? Gap(last.Last, command) : null;
            var run = gap != null && gap.EndsWith("&&", StringComparison.Ordinal) && IsRedirections(gap[..^2]) ? last.Run : new object();
            _last[command.Source] = (command, run);
            var orElse = prior && (gap == null || gap.EndsWith("||", StringComparison.Ordinal));
            return (run, orElse ? new object() : null);
        }
    }

    // The text between two commands of one scope, trimmed: their separator and the first one's redirections.
    private static string? Gap(ShellReader.Command a, ShellReader.Command b)
    {
        if (a.WordRanges.Count == 0 || b.WordRanges.Count == 0)
            return null;
        var text = a.Source.Text;
        var from = a.WordRanges[^1].End.GetOffset(text.Length);
        var to = b.WordRanges[0].Start.GetOffset(text.Length);
        return from <= to ? text[from..to].Trim() : null;
    }

    // Redirections hold no separator, and an & only in >&, <& or &>.
    private static bool IsRedirections(string text) =>
        text.IndexOfAny(SeparatorChars) < 0
        && Enumerable.Range(0, text.Length).All(i => text[i] != '&'
            || (i > 0 && text[i - 1] is '>' or '<') || (i + 1 < text.Length && text[i + 1] == '>'));

    // The branch change a git subcommand makes, or null when it changes no branch.
    private static Switch? SwitchedBranch(string subcommand, List<string> args) => subcommand switch
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

    // checkout or switch: the branch it creates, else its one operand, a name git resolves. A detach,
    // --track (which names the branch from a remote ref), a start point, a short-option cluster, an
    // unknown option before a word or a second switch operand is unreadable; checkout of paths (after
    // --, with -p or a pathspec file, or a tree-ish and paths) changes no branch.
    private static Switch? CheckoutBranch(List<string> args, bool isSwitch)
    {
        var dashes = args.IndexOf("--");
        var words = ReadCheckoutWords(dashes < 0 ? args : args[..dashes]);
        List<string> operands = [.. words.Operands, .. dashes < 0 ? [] : args[(dashes + 1)..]];
        if (!isSwitch && (operands.Count > words.Operands.Count || words.Paths))
            return null;
        if (words.Unreadable)
            return new(MergeRoute.UnreadableBranch);
        if (words.Created != null)
            return operands.Count == 0 ? Created(words.Created) : new(MergeRoute.UnreadableBranch);
        return operands switch
        {
            [] => null,
            [var only] => Named(only),
            _ when isSwitch => new(MergeRoute.UnreadableBranch),
            _ => null,
        };
    }

    // An option's value is taken with it, so a value is never an operand; an unknown option before
    // a word may take that word, so the line does not say its operand for certain.
    private static CheckoutWords ReadCheckoutWords(List<string> options)
    {
        var words = new CheckoutWords();
        for (var i = 0; i < options.Count; i++)
        {
            var word = options[i];
            var name = OptionName(word);
            var wordFollows = name == word && i + 1 < options.Count && !IsFlag(options[i + 1]);
            if (!IsFlag(word))
                words.Operands.Add(word);
            else if (CreateOptions.Contains(name))
                words.Created = name != word ? word[(name.Length + 1)..] : options.ElementAtOrDefault(++i) ?? "";
            else if (CheckoutValueOptions.Contains(name))
            {
                words.Paths |= name == "--pathspec-from-file";
                i += name == word ? 1 : 0;
            }
            else if (HidesTheBranch(word) || (!IsCheckoutFlag(name) && wordFollows))
                words.Unreadable = true;
            else
                words.Paths |= name is "-p" or "--patch";
        }
        return words;
    }

    // A long option's name without its =value.
    private static string OptionName(string word) =>
        word.StartsWith("--", StringComparison.Ordinal) && word.IndexOf('=') is > 0 and var eq ? word[..eq] : word;

    private static bool IsCheckoutFlag(string name) =>
        CheckoutFlags.Contains(name) || (name.StartsWith("--no-", StringComparison.Ordinal) && CheckoutFlags.Contains("--" + name[5..]));

    // A detach (--detach, -d), --track (whose branch a remote ref names) or a short-option cluster, which may hold -b.
    private static bool HidesTheBranch(string word) =>
        OptionName(word) is "--detach" or "-d" or "-t" or "--track"
        || (word.Length > 2 && word[0] == '-' && word[1] != '-');

    // git branch -m or -M renames the current branch to its last operand; with two operands the
    // current branch may not be the one renamed, so HEAD is not sure to move.
    private static Switch? RenamedBranch(List<string> args)
    {
        if (!args.Any(w => w is "-m" or "-M" or "--move"))
            return null;
        List<string> names = [.. args.Where(w => !IsFlag(w))];
        return names.Count == 0 ? null : Created(names[^1]) with { Sure = names.Count == 1 };
    }

    // A branch the line creates: a name, never HEAD, -, @{-N}, a variable, a substitution or a glob.
    private static Switch Created(string word) =>
        Named(word) is { Branch: var branch } && branch != MergeRoute.UnreadableBranch
            ? new(branch, Creates: true)
            : new(MergeRoute.UnreadableBranch);

    // A word git resolves to a branch, or to a commit it detaches onto; HEAD or @ stays where it is;
    // -, @{-N}, a variable, a substitution or a glob cannot be read offline.
    private static Switch? Named(string word)
    {
        if (word is "HEAD" or "@")
            return null;
        if (word.Length == 0 || word == "-" || word.Contains("@{", StringComparison.Ordinal) || word.IndexOfAny(UnreadableChars) >= 0)
            return new(MergeRoute.UnreadableBranch);
        return new(word);
    }
}
