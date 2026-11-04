namespace DynaDocs.Services;

using System.Text.RegularExpressions;

/// <summary>
/// The routes around <c>dydo merge</c> a command line takes (DYD-425, DR 061 decision 7), read with
/// the PR calls' rules: the same shell readings, command position, substitutions and data a command
/// only reads. A route is <c>gh pr merge</c>; <c>gh api</c> on <c>pulls/N/merge</c>,
/// <c>pulls/N/merge-async</c> or <c>merges</c>, as path or URL, whatever the method; <c>gh api
/// graphql</c> with a field naming mergePullRequest, enablePullRequestAutoMerge or mergeBranch;
/// <c>gh repo edit --default-branch</c>, or a <c>gh api</c> PATCH of <c>repos/OWNER/REPO</c> with a
/// default_branch field; and every <c>git push</c>, one route per branch it updates. A wrapper's
/// command string, and any word of a non-gh, non-git argv array, is read the same way and its routes
/// are the line's. An encoded command or nesting past the limit is left to the PR check, which
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
        ScanRoutes(command, dialect, 0);

    /// <summary>
    /// The merge routes an argv array takes: Codex's shell envelope runs its script as a direct line,
    /// any other array runs its first word, and any other program's words are read as lines.
    /// </summary>
    public static List<MergeRoute> FindMergeRoutesInArgv(IReadOnlyList<string> argv)
    {
        if (argv.Count == 0)
            return [];
        if (CommandPosition.Envelope(argv) is { } envelope)
            return FindMergeRoutes(envelope.Text, envelope.Dialect);
        var routes = RoutesOf(argv, 0);
        return routes.Count > 0 || CommandPosition.ProgramName(argv[0]) is "gh" or "git"
            ? routes
            : [.. argv.Skip(1).SelectMany(word => ScanRoutes(word, ShellDialect.Unknown, 1)).Distinct()];
    }

    private static List<MergeRoute> ScanRoutes(string text, ShellDialect dialect, int depth)
    {
        var line = GhPlaceholderRegex().Replace(text, ":$1");
        return [.. Readings(dialect, line)
            .Select(posix => ShellReader.Read(line, posix))
            .SelectMany(reading => reading.Commands.SelectMany(command => RoutesOf(command.Words, depth))
                .Concat(reading.Substitutions.SelectMany(substitution => ScanRoutes(substitution, ShellDialect.Posix, depth + 1))))
            .Distinct()];
    }

    private static List<MergeRoute> RoutesOf(IReadOnlyList<string> words, int depth)
    {
        var at = CommandPosition.Skip(words);
        if (at >= words.Count)
            return [];

        var program = CommandPosition.ProgramName(words[at]);
        var rest = words.Skip(at + 1).ToList();
        if (program == "gh")
            return GhRoute(rest) is { } route ? [route] : [];
        if (program == "git")
            return PushRoutes(rest);
        return CommandPosition.ScriptOf(program, rest) is { Encoded: false } script && depth < MaxDepth
            ? ScanRoutes(script.Text, script.Dialect, depth + 1)
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

    private static MergeRoute? ApiRoute(string endpoint, List<(string Name, string Value)> options)
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

    // git's own options, then push: its first operand is the remote and the rest are refspecs. With
    // none, a push updates the upstream; --all, --branches and --mirror update every branch; --tags alone none.
    private static List<MergeRoute> PushRoutes(List<string> rest)
    {
        string? directory = null;
        var at = 0;
        for (; at < rest.Count && IsFlag(rest[at]); at += GitValueOptions.Contains(rest[at]) ? 2 : 1)
        {
            if (rest[at] == "-C" && at + 1 < rest.Count)
                directory = directory == null ? rest[at + 1] : Path.Combine(directory, rest[at + 1]);
        }
        if (at >= rest.Count || rest[at] != "push")
            return [];

        var (operands, flags) = PushWords(rest[(at + 1)..]);
        var form = string.Join(' ', rest[at..].Prepend("git"));
        List<string> destinations = operands.Count > 1 ? [.. operands.Skip(1).Select(Destination)] : [MergeRoute.Upstream];
        if (flags.Overlaps(PushEveryBranch))
            destinations = ["*"];
        else if (flags.Contains("--tags") && operands.Count < 2)
            destinations = [];
        return [.. destinations.Select(destination => new MergeRoute(MergeRouteKind.Push, form, destination, directory))];
    }

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
