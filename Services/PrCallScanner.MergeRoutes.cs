namespace DynaDocs.Services;

using System.Text.RegularExpressions;

/// <summary>
/// The routes around <c>dydo merge</c> a command line takes (DYD-425, DR 061 decision 7), read with
/// the PR calls' rules: the same shell readings, command position, substitutions and data a command
/// only reads; a bash push's git -C tree is also taken as written, so a Windows path names its tree
/// (DYD-739). A route is <c>gh pr merge</c>; <c>gh api</c> on <c>pulls/N/merge</c>,
/// <c>pulls/N/merge-async</c> or <c>merges</c>, as path or URL, whatever the method; <c>gh api
/// graphql</c> with a field naming mergePullRequest, enablePullRequestAutoMerge or mergeBranch;
/// <c>gh repo edit --default-branch</c>, or a <c>gh api</c> PATCH of <c>repos/OWNER/REPO</c> with a
/// default_branch field; and every <c>git push</c>, one route per branch it updates, a push of the
/// current branch carrying the branch changes before it (see PrCallScanner.BranchChanges). A wrapper's
/// command string is read the same way and its routes are the line's; another program's arguments,
/// in a shell line or a bare argv array alike, are data. An encoded command or nesting past the limit is left to the PR check, which
/// refuses it unread in a git tree. A GraphQL query or API body read from a file or stdin is not read.
/// </summary>
internal static partial class PrCallScanner
{
    private static readonly Options RepoEditOptions = new(new(), ["default-branch"]);

    // git's own options that take the next word as their value.
    private static readonly HashSet<string> GitValueOptions =
        new(StringComparer.Ordinal) { "-C", "-c", "--git-dir", "--work-tree", "--namespace", "--config-env" };

    // git push's options that take the next word as their value.
    private static readonly HashSet<string> PushValueOptions =
        new(StringComparer.Ordinal) { "-o", "--push-option", "--repo", "--receive-pack", "--exec" };

    private static readonly HashSet<string> PushEveryBranch = new(StringComparer.Ordinal) { "--all", "--branches", "--mirror" };

    /// <summary>The merge routes <paramref name="command"/> takes when <paramref name="dialect"/> runs it.</summary>
    public static List<MergeRoute> FindMergeRoutes(string command, ShellDialect dialect = ShellDialect.Unknown) =>
        [.. Readings(command, dialect, 0).SelectMany(Followed).Distinct()];

    /// <summary>
    /// The merge routes an argv array takes: Codex's shell envelope runs its script as a direct line,
    /// and any other array runs as one command, its program's arguments data unless it is a wrapper,
    /// as a shell would read the same words. (The PR check's bare-argv rule is stricter; see FindInArgv.)
    /// </summary>
    public static List<MergeRoute> FindMergeRoutesInArgv(IReadOnlyList<string> argv) =>
        CommandPosition.Envelope(argv) is { } envelope ? FindMergeRoutes(envelope.Text, envelope.Dialect) : Followed(StepsOf(argv, 0));

    // Each reading's routes and branch changes in the order it runs them.
    private static IEnumerable<List<Step>> Readings(string text, ShellDialect dialect, int depth)
    {
        var line = GhPlaceholderRegex().Replace(text, ":$1");
        return Readings(dialect, line).Select(posix => StepsOf(ShellReader.Read(line, posix), depth));
    }

    // A reading's commands in order, each with the && run it is in; a backtick substitution runs just
    // before the command that holds it (one whose command the reading lost, first), in no run.
    private static List<Step> StepsOf(ShellReader.Reading reading, int depth)
    {
        var hosted = reading.Captures.ToLookup(capture => capture.Host.Command);
        List<Step> steps = [.. reading.Captures.Where(capture => !reading.Commands.Contains(capture.Host.Command))
            .SelectMany(capture => ScanSteps(capture.Text, ShellDialect.Posix, depth + 1))];
        var runs = new Runs();
        foreach (var command in reading.Commands)
        {
            var (run, own) = runs.Of(command);
            steps.AddRange(hosted[command].SelectMany(capture => ScanSteps(capture.Text, ShellDialect.Posix, depth + 1)));
            steps.AddRange(StepsOf(command.Words, depth, command).Select(step => step.Run == null ? step with { Run = own ?? run } : step));
        }
        return steps;
    }

    // A nested script's steps, its readings in turn.
    private static List<Step> ScanSteps(string text, ShellDialect dialect, int depth) =>
        [.. Readings(text, dialect, depth).SelectMany(steps => steps)];

    // Bash drops an unquoted backslash, so its word for git -C C:\Users\wt is C:Userswt, a tree that
    // does not exist; the word as written names the tree the agent meant (DYD-739). Only a -C word of
    // a push bash runs is taken as written, so an escaped quote or a continued line stays bash's.
    private static string? WrittenWord(ShellReader.Command? command, int index)
    {
        var raw = command is { Source.Posix: true } ? command.RawWord(index) : "";
        return raw.Contains('\\') && ShellReader.Read(raw, posix: false).Commands is [{ Words: [var word] }] ? word : null;
    }

    private static List<Step> StepsOf(IReadOnlyList<string> words, int depth, ShellReader.Command? command = null)
    {
        var at = CommandPosition.Skip(words);
        if (at >= words.Count)
            return [];

        var program = CommandPosition.ProgramName(words[at]);
        var rest = words.Skip(at + 1).ToList();
        if (program == "gh" && GhRoute(rest) is { } route)
            return [new(route)];
        if (program == "gh")
            return ChecksOutAPr(rest) ? [new(Change: new BranchChange(MergeRoute.UnreadableBranch))] : [];
        // A negated command (!) succeeds when git fails, so it starts no run.
        if (program == "git")
            return [.. GitSteps(rest, index => WrittenWord(command, at + 1 + index))
                .Select(step => words.Take(at).Contains("!") ? step with { Run = new object() } : step)];
        return CommandPosition.ScriptOf(program, rest) is { Encoded: false } script && depth < MaxDepth
            ? ScanSteps(script.Text, script.Dialect, depth + 1)
            : [];
    }

    private static MergeRoute? GhRoute(List<string> rest)
    {
        var sub = GhSubcommand(rest, "pr", "api", "repo");
        if (sub < 0)
            return null;

        var args = rest[(sub + 1)..];
        var parsed = Parse(args, rest[sub].ToLowerInvariant() switch { "pr" => PrOptions, "repo" => RepoEditOptions, _ => ApiOptions });
        var first = parsed.Positionals.FirstOrDefault();
        if (first == null || parsed.Help)
            return null;
        return rest[sub].ToLowerInvariant() switch
        {
            "pr" => first.Equals("merge", StringComparison.OrdinalIgnoreCase) ? new MergeRoute(MergeRouteKind.Merge, "gh pr merge") : null,
            "repo" => first.Equals("edit", StringComparison.OrdinalIgnoreCase) && Values(parsed.Options, "default-branch").Any()
                ? new MergeRoute(MergeRouteKind.DefaultBranchChange, "gh repo edit --default-branch")
                : null,
            _ => ApiRoute(first, parsed.Options),
        };
    }

    private static MergeRoute? ApiRoute(string endpoint, List<Option> options)
    {
        var fields = options.Where(o => o.Name is "field" or "raw-field").Select(o => o.Value).ToList();
        if (GraphQlEndpointRegex().IsMatch(endpoint))
            return fields.Select(f => GraphQlMergeMutationRegex().Match(f)).FirstOrDefault(m => m.Success) is { } mutation
                ? new MergeRoute(MergeRouteKind.Merge, $"gh api graphql {mutation.Value}")
                : null;
        if (RestMergeRegex().IsMatch(endpoint))
            return new MergeRoute(MergeRouteKind.Merge, $"gh api {endpoint}");

        var patch = "PATCH".Equals(Values(options, "method").LastOrDefault(), StringComparison.OrdinalIgnoreCase);
        return patch && RestRepoRegex().IsMatch(endpoint) && fields.Any(f => f.StartsWith("default_branch=", StringComparison.Ordinal))
            ? new MergeRoute(MergeRouteKind.DefaultBranchChange, $"gh api -X PATCH {endpoint} default_branch")
            : null;
    }

    // git's own options, then its subcommand: a push's routes, or a branch change.
    private static List<Step> GitSteps(List<string> rest, Func<int, string?> written)
    {
        string? directory = null, asWritten = null;
        var at = 0;
        for (; at < rest.Count && IsFlag(rest[at]); at += GitValueOptions.Contains(rest[at]) ? 2 : 1)
        {
            if (rest[at] == "-C" && at + 1 < rest.Count)
            {
                directory = Under(directory, rest[at + 1]);
                asWritten = Under(asWritten, written(at + 1) ?? rest[at + 1]);
            }
        }
        if (at >= rest.Count)
            return [];
        if (rest[at] == "push")
            return [.. PushRoutes(rest[at..], directory, asWritten).Select(route => new Step(route))];
        if (SwitchedBranch(rest[at], rest[(at + 1)..]) is not { } change)
            return [];
        // A change HEAD is not sure to follow starts no run, so no push takes it as the last.
        var branchChange = new BranchChange(change.Branch, change.Creates, directory, asWritten == directory ? null : asWritten);
        return [new(Change: branchChange, Run: change.Sure ? null : new object())];
    }

    // push's first operand is the remote and the rest are refspecs. With none, a push updates the
    // upstream; --all, --branches and --mirror update every branch; --tags alone none. Each -C tree
    // is taken as bash reads it and as written, one route per distinct tree.
    private static List<MergeRoute> PushRoutes(List<string> push, string? directory, string? asWritten)
    {
        var (operands, flags) = PushWords(push[1..]);
        var form = string.Join(' ', push.Prepend("git"));
        List<string> destinations = operands.Count > 1 ? [.. operands.Skip(1).Select(Destination)] : [MergeRoute.Upstream];
        if (flags.Overlaps(PushEveryBranch))
            destinations = ["*"];
        else if (flags.Contains("--tags") && operands.Count < 2)
            destinations = [];
        return [.. new[] { directory, asWritten }.Distinct()
            .SelectMany(tree => destinations.Select(destination => new MergeRoute(MergeRouteKind.Push, form, destination, tree)))];
    }

    private static string Under(string? directory, string tree) => directory == null ? tree : Path.Combine(directory, tree);

    private static (List<string> Operands, HashSet<string> Flags) PushWords(List<string> words)
    {
        List<string> operands = [];
        HashSet<string> flags = new(StringComparer.Ordinal);
        for (var i = 0; i < words.Count; i++)
        {
            if (words[i] == "--")
            {
                operands.AddRange(words.Skip(i + 1));
                break;
            }
            if (!IsFlag(words[i]))
                operands.Add(words[i]);
            else if (flags.Add(words[i]) && PushValueOptions.Contains(words[i]))
                i++;
        }
        return (operands, flags);
    }

    // The branch a refspec updates: dst of [+]src:dst, else src itself; @ is HEAD, and refs/heads/ is dropped.
    private static string Destination(string refspec)
    {
        var spec = refspec.TrimStart('+');
        var name = spec[(spec.IndexOf(':') + 1)..];
        if (name == "@")
            return MergeRoute.CurrentBranch;
        return name.StartsWith("refs/heads/", StringComparison.Ordinal) ? name["refs/heads/".Length..] : name;
    }

    [GeneratedRegex(@"mergePullRequest|enablePullRequestAutoMerge|mergeBranch", RegexOptions.IgnoreCase)]
    private static partial Regex GraphQlMergeMutationRegex();

    [GeneratedRegex(@"^(?:https?://[^/]+(?:/api/v3)?)?/?repos/[^/]+/[^/]+/(?:pulls/[^/]+/merge(?:-async)?|merges)/?(?:\?.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex RestMergeRegex();

    [GeneratedRegex(@"^(?:https?://[^/]+(?:/api/v3)?)?/?repos/[^/]+/[^/?]+/?(?:\?.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex RestRepoRegex();
}
