namespace DynaDocs.Services;

using System.Text.RegularExpressions;

/// <summary>
/// The PR-opening gh calls a command line makes (DYD-391): <c>gh pr create</c>, <c>gh pr new</c>,
/// <c>gh pr ready</c>, a <c>gh api</c> POST to <c>repos/OWNER/REPO/pulls</c>, and <c>gh api graphql</c>
/// naming createPullRequest or markPullRequestReadyForReview; a PR verb given --help or -h only prints
/// gh's help, so it is no call. The line is read as its shell reads
/// it: bash, PowerShell, or both ways when the shell is unknown or a bash line uses PowerShell syntax
/// (a backtick line continuation, the &amp; call operator, <c>$env:</c>). gh counts at command position
/// (<see cref="CommandPosition"/>), with any path, case or .exe. Text a command only reads (quotes,
/// heredoc and here-string bodies, comments) is no call. A shell, eval or env -S given a command string is a
/// wrapper: when that string, read with the same rules, makes a PR call, or the shell is given an
/// encoded PowerShell command, the wrapper is the call, never decoded. A command substitution's
/// command runs, so <c>$(...)</c> and a bash backtick substitution are read like a direct line. It
/// stops accidents, not a deliberate bypass: aliases, functions and other programs are not followed.
/// </summary>
internal static partial class PrCallScanner
{
    private const int MaxDepth = 8;

    private static readonly HashSet<string> PrVerbs = new(StringComparer.OrdinalIgnoreCase) { "create", "new", "ready" };

    private static readonly Options RepoOptions = new(new() { ['R'] = "repo" }, ["repo"]);

    private static readonly Options PrOptions = new(
        new()
        {
            ['t'] = "title", ['b'] = "body", ['B'] = "base", ['F'] = "body-file", ['l'] = "label", ['a'] = "assignee",
            ['r'] = "reviewer", ['m'] = "milestone", ['p'] = "project", ['T'] = "template", ['H'] = "head", ['R'] = "repo",
        },
        ["title", "body", "base", "body-file", "label", "assignee", "reviewer", "milestone", "project", "template",
         "head", "repo", "recover"]);

    private static readonly Options ApiOptions = new(
        new() { ['X'] = "method", ['H'] = "header", ['f'] = "field", ['F'] = "field", ['q'] = "jq", ['t'] = "template", ['p'] = "preview" },
        ["method", "header", "raw-field", "field", "hostname", "input", "jq", "template", "cache", "preview"]);

    private sealed record Options(Dictionary<char, string> Short, HashSet<string> Long);

    /// <summary>
    /// The PR calls <paramref name="command"/> makes when <paramref name="dialect"/> runs it.
    /// <paramref name="inheritedRepo"/> is the GH_REPO the command inherits, if any.
    /// </summary>
    public static List<PrCall> Find(string command, ShellDialect dialect = ShellDialect.Unknown, string? inheritedRepo = null) =>
        Scan(command, dialect, NonEmpty(inheritedRepo), 0);

    /// <summary>
    /// The PR calls an argv array runs. Codex's shell envelope (<see cref="CommandPosition.Envelope"/>)
    /// runs its script as a direct command line. Any other array runs its first word: gh directly, a
    /// shell or eval as a wrapper, and any other program given a word that reads as a PR call is a
    /// wrapper too (admiral ruling 2026-10-27).
    /// </summary>
    public static List<PrCall> FindInArgv(IReadOnlyList<string> argv, string? inheritedRepo = null)
    {
        if (argv.Count == 0)
            return [];
        if (CommandPosition.Envelope(argv) is { } envelope)
            return Find(envelope.Text, envelope.Dialect, inheritedRepo);
        if (Match(argv, NonEmpty(inheritedRepo), 0) is { } call)
            return [call];

        var program = CommandPosition.ProgramName(argv[0]);
        return program != "gh" && argv.Skip(1).Any(word => Scan(word, ShellDialect.Unknown, null, 1).Count > 0)
            ? [new PrCall(null, Wrapper: program)]
            : [];
    }

    private static List<PrCall> Scan(string text, ShellDialect dialect, string? inheritedRepo, int depth)
    {
        // gh's {owner}, {repo} and {branch} placeholders, spelt as its :owner form, so braces split no command.
        var line = GhPlaceholderRegex().Replace(text, ":$1");
        var readings = Readings(dialect, line)
            .Select(posix => Calls(ShellReader.Read(line, posix), inheritedRepo, depth))
            .ToList();

        // A reading missing a head another reading names lost that call's target, so its headless calls are not added.
        var named = readings.SelectMany(calls => calls).Where(call => call.Branch != null).ToList();
        return [.. readings
            .SelectMany(calls => named.All(calls.Contains) ? calls : calls.Where(call => call.Branch != null || call.Wrapper != null))
            .Distinct()];
    }

    // A reading's calls: its commands', then those its bash backtick substitutions run, read as bash.
    private static List<PrCall> Calls(ShellReader.Reading reading, string? inheritedRepo, int depth) =>
    [
        .. reading.Commands.Select(command => Match(command.Words, inheritedRepo, depth)).OfType<PrCall>(),
        .. reading.Substitutions.SelectMany(text => Scan(text, ShellDialect.Posix, inheritedRepo, depth + 1)),
    ];

    private static bool[] Readings(ShellDialect dialect, string line) => dialect switch
    {
        ShellDialect.PowerShell => [false],
        ShellDialect.Posix when !PowerShellSyntaxRegex().IsMatch(line) => [true],
        _ => [true, false],
    };

    private static PrCall? Match(IReadOnlyList<string> words, string? inheritedRepo, int depth)
    {
        List<(string Name, string Value)> assignments = [];
        var at = CommandPosition.Skip(words, assignments);
        if (at >= words.Count)
            return null;

        var program = CommandPosition.ProgramName(words[at]);
        var rest = words.Skip(at + 1).ToList();
        if (program == "gh")
            return GhCall(rest, GhRepo(assignments, inheritedRepo));

        return CommandPosition.ScriptOf(program, rest) is { } script && Wraps(script, depth)
            ? new PrCall(null, Wrapper: program)
            : null;
    }

    // GH_REPO after the assignments and env changes, left to right: set, emptied, unset, or cleared by env -i.
    private static string? GhRepo(List<(string Name, string Value)> changes, string? inheritedRepo) =>
        changes.Aggregate(inheritedRepo, (repo, change) => change.Name switch
        {
            "GH_REPO" => NonEmpty(change.Value),
            CommandPosition.AllVariables => null,
            _ => repo,
        });

    // A command string wraps a PR call when it is encoded or makes one; nesting past the limit fails closed.
    private static bool Wraps(ShellScript script, int depth) =>
        script.Encoded || depth >= MaxDepth || Scan(script.Text, script.Dialect, null, depth + 1).Count > 0;

    // gh's own flags may precede its subcommand, each with at most one value word.
    private static PrCall? GhCall(List<string> rest, string? envRepo)
    {
        var sub = rest.FindIndex(w => w.Equals("pr", StringComparison.OrdinalIgnoreCase) || w.Equals("api", StringComparison.OrdinalIgnoreCase));
        if (sub < 0 || Enumerable.Range(0, sub).Any(i => !IsFlag(rest[i]) && (i == 0 || !IsFlag(rest[i - 1]))))
            return null;

        var args = rest[(sub + 1)..];
        if (!rest[sub].Equals("pr", StringComparison.OrdinalIgnoreCase))
            return ApiCall(args, envRepo);
        return PrVerbCall(args, Values(Parse(rest[..sub], RepoOptions).Options, "repo").LastOrDefault() ?? envRepo);
    }

    private static PrCall? PrVerbCall(List<string> args, string? repo)
    {
        var parsed = Parse(args, PrOptions);
        var verb = parsed.Positionals.FirstOrDefault();
        if (verb == null || !PrVerbs.Contains(verb) || parsed.Help)
            return null;

        repo = Values(parsed.Options, "repo").LastOrDefault() ?? repo;
        verb = verb.ToLowerInvariant();
        if (verb == "ready")
            return ReadyCall(parsed.Positionals.ElementAtOrDefault(1), repo);

        var head = Values(parsed.Options, "head").LastOrDefault();
        return head == null ? new PrCall(repo) : new PrCall(repo, BranchOf(head), $"gh pr {verb} --head {head}");
    }

    // gh pr ready takes a number, a PR URL (whose repository it readies) or a branch.
    private static PrCall ReadyCall(string? target, string? repo)
    {
        if (target != null && PrUrlRegex().Match(target) is { Success: true } url)
            return new PrCall($"{url.Groups["host"].Value}/{url.Groups["owner"].Value}/{url.Groups["repo"].Value}");
        return target == null || target.All(char.IsAsciiDigit) || target.Contains("://")
            ? new PrCall(repo)
            : new PrCall(repo, BranchOf(target), $"gh pr ready {target}");
    }

    private static PrCall? ApiCall(List<string> args, string? envRepo)
    {
        var parsed = Parse(args, ApiOptions);
        var endpoint = parsed.Positionals.FirstOrDefault();
        if (endpoint == null)
            return null;

        var fields = parsed.Options.Where(o => o.Name is "field" or "raw-field").Select(o => o.Value).ToList();
        if (GraphQlEndpointRegex().IsMatch(endpoint))
            return fields.Any(f => GraphQlPrMutationRegex().IsMatch(f)) ? new PrCall(null) : null;

        var pulls = RestPullsRegex().Match(endpoint);
        var sendsBody = fields.Count > 0 || Values(parsed.Options, "input").Any();
        var method = Values(parsed.Options, "method").LastOrDefault() ?? (sendsBody ? "POST" : "GET");
        if (!pulls.Success || !method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            return null;

        // {owner}/{repo} is the current repository, which GH_REPO names when set. A lone placeholder takes
        // its own part of it, from GH_REPO here or from the checkout's origin later (InRepository).
        var repo = pulls.Groups["owner"].Value == ":owner" && pulls.Groups["repo"].Value == ":repo"
            ? envRepo
            : EndpointRepo(pulls, Values(parsed.Options, "hostname").LastOrDefault());
        if (repo != null && envRepo != null)
            repo = InRepository(repo, envRepo);
        var head = fields.LastOrDefault(f => f.StartsWith("head=", StringComparison.Ordinal))?["head=".Length..];
        return head is null or ":branch" ? new PrCall(repo) : new PrCall(repo, BranchOf(head), $"gh api {endpoint} head={head}");
    }

    // An endpoint's repository with its host: the URL's (api.github.com is github.com's API), else --hostname's.
    private static string EndpointRepo(Match pulls, string? hostname)
    {
        var host = pulls.Groups["host"].Success ? pulls.Groups["host"].Value : hostname;
        if (string.Equals(host, "api.github.com", StringComparison.OrdinalIgnoreCase))
            host = "github.com";
        var repo = $"{pulls.Groups["owner"].Value}/{pulls.Groups["repo"].Value}";
        return host == null ? repo : $"{host}/{repo}";
    }

    /// <summary>
    /// <paramref name="repo"/> with gh's :owner and :repo placeholder segments filled from
    /// <paramref name="current"/>, the current repository in any form gh or git spells it, and its
    /// host when <paramref name="repo"/> names none; unchanged when it has none or either is unreadable.
    /// </summary>
    public static string InRepository(string repo, string current)
    {
        if (RepoParts(repo) is not { } call || (call.Owner != ":owner" && call.Name != ":repo") || RepoParts(current) is not { } here)
            return repo;
        var prefix = call.Prefix.Length > 0 ? call.Prefix : here.Prefix;
        return $"{prefix}{(call.Owner == ":owner" ? here.Owner : call.Owner)}/{(call.Name == ":repo" ? here.Name : call.Name)}";
    }

    // A repository name's last two segments, owner and name, and the text before them (a host or URL prefix).
    private static (string Prefix, string Owner, string Name)? RepoParts(string repo)
    {
        var text = repo.Trim().TrimEnd('/');
        if (text.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            text = text[..^4];
        var slash = text.LastIndexOf('/');
        if (slash <= 0)
            return null;
        // scp's host:owner splits at a colon inside the segment; :owner, a placeholder, starts with one.
        var start = text.LastIndexOf('/', slash - 1) + 1;
        var colon = text.LastIndexOf(':', slash - 1, slash - start);
        if (colon > start)
            start = colon + 1;
        return (text[..start], text[start..slash], text[(slash + 1)..]);
    }

    // A head may name its fork's owner: OWNER:BRANCH.
    private static string BranchOf(string head) => head[(head.IndexOf(':') + 1)..];

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool IsFlag(string word) => word.Length > 1 && word[0] == '-';

    // Help is --help or -h as a flag of its own (gh 2.88.1 prints help for either and acts on
    // nothing), not a flag's value; after --, every word is an operand.
    private static (List<(string Name, string Value)> Options, List<string> Positionals, bool Help) Parse(List<string> words, Options options)
    {
        List<(string Name, string Value)> values = [];
        List<string> positionals = [];
        var help = false;
        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];
            if (word == "--")
            {
                positionals.AddRange(words.Skip(i + 1));
                break;
            }

            help |= word is "--help" or "-h";
            if (word.StartsWith("--", StringComparison.Ordinal) && word.Length > 2)
                ReadLongOption(words, ref i, options, values);
            else if (IsFlag(word))
                ReadShortFlags(words, ref i, options, values);
            else
                positionals.Add(word);
        }
        return (values, positionals, help);
    }

    // --name=value or --name value, kept when name is one of options' long options.
    private static void ReadLongOption(List<string> words, ref int i, Options options, List<(string Name, string Value)> values)
    {
        var word = words[i];
        var eq = word.IndexOf('=');
        var name = eq > 0 ? word[2..eq] : word[2..];
        if (options.Long.Contains(name))
            values.Add((name, eq > 0 ? word[(eq + 1)..] : Next(words, ref i)));
    }

    // Short flags combine (-dH b); the first one taking a value takes the rest of the word or the next.
    private static void ReadShortFlags(List<string> words, ref int i, Options options, List<(string Name, string Value)> values)
    {
        var word = words[i];
        for (var j = 1; j < word.Length; j++)
        {
            if (!options.Short.TryGetValue(word[j], out var name))
                continue;
            values.Add((name, j + 1 < word.Length ? word[(j + 1)..].TrimStart('=') : Next(words, ref i)));
            return;
        }
    }

    private static string Next(List<string> words, ref int i) => ++i < words.Count ? words[i] : "";

    private static IEnumerable<string> Values(List<(string Name, string Value)> options, string name) =>
        options.Where(o => o.Name == name).Select(o => o.Value);

    [GeneratedRegex(@"\{(owner|repo|branch)\}")]
    private static partial Regex GhPlaceholderRegex();

    [GeneratedRegex(@"^(?:https?://(?<host>[^/]+)(?:/api/v3)?)?/?repos/(?<owner>[^/]+)/(?<repo>[^/?]+)/pulls/?(?:\?.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex RestPullsRegex();

    [GeneratedRegex(@"^https?://(?<host>[^/]+)/(?<owner>[^/]+)/(?<repo>[^/]+)/pull/\d+(?:[/?#].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex PrUrlRegex();

    [GeneratedRegex(@"(?:^|/)graphql$", RegexOptions.IgnoreCase)]
    private static partial Regex GraphQlEndpointRegex();

    [GeneratedRegex(@"createPullRequest|markPullRequestReadyForReview", RegexOptions.IgnoreCase)]
    private static partial Regex GraphQlPrMutationRegex();

    // PowerShell syntax a bash line would not use: a backtick ending a line, & starting one, $env:.
    [GeneratedRegex(@"`\r?$|^\s*&\s|\$env:", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex PowerShellSyntaxRegex();
}
