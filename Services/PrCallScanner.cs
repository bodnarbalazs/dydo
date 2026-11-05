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

    // An option's value and the index of the word it was read from (Words.Count when it is missing).
    private readonly record struct Option(string Name, string Value, int Word);

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

    /// <summary>
    /// The body file of the <c>gh pr create</c> call launched at <paramref name="launch"/> in
    /// <paramref name="command"/> (DYD-639): its last <c>--body-file</c> or <c>-F</c>, as gh takes it,
    /// or null when the guard may not read it. The guard emulates no shell; it reads an allowlist.
    /// Every reading of the whole line must finish, find the call at <paramref name="launch"/> at the
    /// line's top level, and name the same operand written plainly (<see cref="PlainOperand"/>).
    /// Nothing else in the line may change that file before gh opens it, so the guard reads it as gh
    /// will (<see cref="LeavesTheFileAlone"/>). Anything else reads as null, so a check that needs the
    /// body keeps its warning.
    /// </summary>
    public static string? BodyFileAt(string command, int launch, ShellDialect dialect = ShellDialect.Unknown)
    {
        var files = Readings(dialect, command)
            .Select(posix => BodyFileIn(ShellReader.Read(command, posix), launch, posix))
            .Distinct()
            .ToList();
        return files is [{ } file] ? file : null;
    }

    // On Windows a path rooted at no drive opens on the shell's current drive, so it is never read there.
    private static string? BodyFileIn(ShellReader.Reading reading, int launch, bool posix)
    {
        var commands = reading.Commands.ToList();
        var call = commands.FindIndex(c => c.Source == reading.Source && c.WordRanges is [var first, ..] && first.Start.Value == launch);
        if (reading.Failed || call < 0 || !LeavesTheFileAlone(reading, commands, call, posix)
            || PlainOperand(commands[call], posix) is not { } file)
            return null;
        return Path.IsPathFullyQualified(file) || !Path.IsPathRooted(file) ? file : null;
    }

    /// <summary>
    /// Whether nothing else in the line may change the body file before gh opens it (DYD-639 ruling,
    /// point 6). Up to the end of the call's pipeline (<see cref="Pipeline"/>), every command, nested
    /// ones included, takes no file redirection or heredoc (<see cref="Quiet"/>); the call's later
    /// members are filters, and every other command only reads (<see cref="ReadsOnly"/>). The rest of
    /// the line starts with <c>;</c>, <c>&amp;&amp;</c>, <c>||</c> or a line break and holds no lone
    /// <c>&amp;</c>, which would run the call in the background beside what follows. No word up to
    /// the pipeline's end may expand (<see cref="ExpandsNothing"/>), and the line holds no backtick
    /// substitution, whose commands the reader returns unread.
    /// </summary>
    private static bool LeavesTheFileAlone(ShellReader.Reading reading, List<ShellReader.Command> commands, int call, bool posix)
    {
        var source = reading.Source;
        if (reading.Substitutions.Count > 0 || Pipeline(source, commands[call]) is not var (last, end))
            return false;

        var rest = source.Text[end..];
        return EndRegex().IsMatch(rest) && !LoneAmpersandRegex().IsMatch(rest) && ExpandsNothing(source, end, posix)
            && commands.Take(commands.IndexOf(last) + 1).Select((command, i) => MayRun(command, i, call, source)).All(ok => ok);
    }

    /// <summary>
    /// The last member of the pipeline holding <paramref name="call"/>, one of the line's top-level
    /// commands, and where its text ends; null when anything but blanks and <c>; &amp; |</c> joins the
    /// commands before the call, when a pipe joins the call to the one before it, or when a command
    /// up to the one after the pipeline has no word.
    /// </summary>
    private static (ShellReader.Command Last, int End)? Pipeline(ShellReader.Scope source, ShellReader.Command call)
    {
        var at = source.Commands.IndexOf(call);
        var (last, end) = (call, 0);
        for (var i = 0; i < source.Commands.Count; i++)
        {
            var command = source.Commands[i];
            if (command.WordRanges is not [var first, ..])
                return null;
            var gap = source.Text[end..first.Start.Value];
            var joined = (i - at) switch
            {
                < 0 => JoinRegex().IsMatch(gap),
                0 => CallJoinRegex().IsMatch(gap),
                _ => PipeRegex().IsMatch(gap),
            };
            if (!joined)
                return i > at ? (last, end) : null;
            (last, end) = (command, command.RedirectTargets.Select(r => r.Range.End.Value).Append(command.WordRanges[^1].End.Value).Max());
        }
        return (last, end);
    }

    // A filter after the call in its pipeline, the call itself, and a read-only command anywhere else up to there.
    private static bool MayRun(ShellReader.Command command, int index, int call, ShellReader.Scope source) =>
        Quiet(command) && (index > call && command.Source == source ? Filters(command.Words) : index == call || ReadsOnly(command.Words));

    // No heredoc and no redirection to a file: only an input, or an fd duplicated or closed.
    private static bool Quiet(ShellReader.Command command) =>
        command.Redirections.Count == command.RedirectTargets.Count
        && command.RedirectTargets.All(r => ReadOnlyRedirectionRegex().IsMatch(command.Source.Text[r.Range]));

    private static bool Filters(List<string> words) => words is ["cat" or "head" or "tail" or "grep", ..];

    // Commands that write no file, by bare name: echo and true; printf and test with no -v, which
    // evaluates an array subscript and so runs a substitution in it; dydo pr-ready; and git status,
    // diff, log, show and rev-parse with no global option before them and no --output, which writes one.
    private static bool ReadsOnly(List<string> words) => words switch
    {
        ["echo" or "true", ..] => true,
        ["printf" or "test", .. var args] => !args.Any(a => a.StartsWith("-v", StringComparison.Ordinal)),
        ["dydo", "pr-ready", ..] => true,
        ["git", "status" or "diff" or "log" or "show" or "rev-parse", .. var args] => !args.Any(a => a.StartsWith("--output", StringComparison.Ordinal)),
        _ => false,
    };

    /// <summary>
    /// Whether no word before <paramref name="end"/>, the end of the call's pipeline, may expand
    /// (DYD-639 ruling, points 5 and 6): into more arguments, which could hand gh a later body file,
    /// or into commands the reader does not read. It reads the raw text from the line's start, with
    /// single-quoted text and each substitution masked, split at double quotes. bash expands an
    /// unquoted <c>$</c> (<c>${...}</c> and <c>$'...'</c> among them), backtick, glob, brace or tilde,
    /// and <c>"$@"</c>, an array's <c>[@]</c> or a <c>${...}</c> in double quotes, so double-quoted
    /// text may hold only a plain <c>$name</c> or a substitution, whose commands are read. PowerShell
    /// passes a variable, a splat, a group or a comma list as many arguments, runs a variable's
    /// methods, and passes <c>--%</c> the rest unread, so no <c>$</c> or <c>@</c> may stand outside
    /// single quotes. A backslash or backtick leaves the quoting unknown, so it counts too.
    /// </summary>
    private static bool ExpandsNothing(ShellReader.Scope source, int end, bool posix)
    {
        var raw = source.Text.ToCharArray(0, end);
        foreach (var (range, mark) in source.SingleQuotes.Select(q => (q, '_')).Concat(source.Executions.Select(h => (h.Token, '$'))))
        {
            for (var at = range.Start.Value; at < Math.Min(range.End.Value, end); at++)
                raw[at] = at == range.Start.Value ? mark : '_';
        }

        var pieces = new string(raw).Split((posix ? "\"" : "\"“”„").ToCharArray());
        var (unquoted, quoted) = posix
            ? (PosixUnquotedExpansionRegex(), PosixQuotedExpansionRegex())
            : (PowerShellUnquotedExpansionRegex(), PowerShellQuotedExpansionRegex());
        return pieces.Length % 2 == 1 && !pieces.Where((piece, i) => (i % 2 == 0 ? unquoted : quoted).IsMatch(piece)).Any();
    }

    private static string? PlainOperand(ShellReader.Command call, bool posix)
    {
        var words = call.Words;
        if (CommandPosition.ProgramName(words[0]) != "gh")
            return null;

        var rest = words.Skip(1).ToList();
        var sub = GhSubcommand(rest, "pr", "api");
        if (sub < 0 || !rest[sub].Equals("pr", StringComparison.OrdinalIgnoreCase))
            return null;

        var parsed = Parse(rest[(sub + 1)..], PrOptions);
        if (parsed.Positionals.FirstOrDefault() is not { } verb || !verb.Equals("create", StringComparison.OrdinalIgnoreCase) || parsed.Help)
            return null;

        var last = parsed.Options.FindLastIndex(o => o.Name == "body-file");
        if (last < 0)
            return null;
        var file = parsed.Options[last];
        var at = sub + 2 + file.Word;
        return at < words.Count && IsWrittenPlainly(call, at, file.Value, posix) ? file.Value : null;
    }

    /// <summary>
    /// Whether the word at <paramref name="at"/>, which ends in the operand <paramref name="value"/>,
    /// is written as its reading decodes it: the flag (<c>--body-file=</c>, or short flags with no
    /// <c>=</c>, since gh drops one <c>=</c> after a short flag and the reader every one), then the
    /// operand bare or wholly in one pair of quotes, of the reading's literal characters (<see cref="PosixLiteralRegex"/>,
    /// <see cref="PowerShellLiteralRegex"/>). The reader splits a word at a brace, a parenthesis, a
    /// carriage return or any Unicode space, where bash does not, so the word must also stand between
    /// a blank and a blank, a separator or the line's end.
    /// </summary>
    private static bool IsWrittenPlainly(ShellReader.Command call, int at, string value, bool posix)
    {
        var flag = call.Words[at][..^value.Length];
        var written = call.RawWord(at);
        var operand = written.StartsWith(flag, StringComparison.Ordinal) ? written[flag.Length..] : null;
        var (text, range) = (call.Source.Text, call.WordRanges[at]);
        return (flag == "--body-file=" || !flag.Contains('='))
            && (operand == value || operand == $"'{value}'" || operand == $"\"{value}\"")
            && (posix ? PosixLiteralRegex() : PowerShellLiteralRegex()).IsMatch(value)
            && text[range.Start.Value - 1] is ' ' or '\t'
            && (range.End.Value == text.Length || text[range.End.Value] is ' ' or '\t' or '\n' or ';' or '&' or '|');
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

    private static List<PrCall> Calls(ShellReader.Reading reading, string? inheritedRepo, int depth)
    {
        List<PrCall> calls =
        [
            .. reading.Commands.Select(command => Match(command.Words, inheritedRepo, depth)).OfType<PrCall>(),
            .. reading.Substitutions.SelectMany(text => Scan(text, ShellDialect.Posix, inheritedRepo, depth + 1)),
        ];
        // A failed child may hide later calls, and a partial call may have lost its target flags.
        // Refuse either unread; an unfinished literal alone still introduces no PR call. A hidden
        // call needs gh, or pwsh or powershell to run it encoded, so a line whose raw text holds
        // none of those words hides none (DYD-515).
        return reading.Failed && (calls.Count > 0 || (reading.Source.Children.Count > 0 && HiderWordRegex().IsMatch(reading.Source.Text)))
            ? [new PrCall(null, Wrapper: "unreadable shell syntax")]
            : calls;
    }

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

    private static PrCall? GhCall(List<string> rest, string? envRepo)
    {
        var sub = GhSubcommand(rest, "pr", "api");
        if (sub < 0)
            return null;

        var args = rest[(sub + 1)..];
        if (!rest[sub].Equals("pr", StringComparison.OrdinalIgnoreCase))
            return ApiCall(args, envRepo);
        return PrVerbCall(args, Values(Parse(rest[..sub], RepoOptions).Options, "repo").LastOrDefault() ?? envRepo);
    }

    // The index of gh's first subcommand among names, or -1: gh's own flags may precede it, each
    // with at most one value word.
    private static int GhSubcommand(List<string> rest, params string[] names)
    {
        var sub = rest.FindIndex(w => names.Any(name => w.Equals(name, StringComparison.OrdinalIgnoreCase)));
        return sub < 0 || Enumerable.Range(0, sub).Any(i => !IsFlag(rest[i]) && (i == 0 || !IsFlag(rest[i - 1]))) ? -1 : sub;
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
    private static (List<Option> Options, List<string> Positionals, bool Help) Parse(List<string> words, Options options)
    {
        List<Option> values = [];
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
    private static void ReadLongOption(List<string> words, ref int i, Options options, List<Option> values)
    {
        var word = words[i];
        var eq = word.IndexOf('=');
        var name = eq > 0 ? word[2..eq] : word[2..];
        if (!options.Long.Contains(name))
            return;
        var value = eq > 0 ? word[(eq + 1)..] : Next(words, ref i);
        values.Add(new Option(name, value, i));
    }

    // Short flags combine (-dH b); the first one taking a value takes the rest of the word or the next.
    private static void ReadShortFlags(List<string> words, ref int i, Options options, List<Option> values)
    {
        var word = words[i];
        for (var j = 1; j < word.Length; j++)
        {
            if (!options.Short.TryGetValue(word[j], out var name))
                continue;
            var value = j + 1 < word.Length ? word[(j + 1)..].TrimStart('=') : Next(words, ref i);
            values.Add(new Option(name, value, i));
            return;
        }
    }

    private static string Next(List<string> words, ref int i) => ++i < words.Count ? words[i] : "";

    private static IEnumerable<string> Values(List<Option> options, string name) =>
        options.Where(o => o.Name == name).Select(o => o.Value);

    // gh, or pwsh or powershell (the only programs that run -EncodedCommand), as a word anywhere,
    // under any path, case or .exe; over-matching only keeps a refusal.
    [GeneratedRegex(@"\b(?:gh|pwsh|powershell)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HiderWordRegex();

    [GeneratedRegex(@"\{(owner|repo|branch)\}")]
    private static partial Regex GhPlaceholderRegex();

    // A plainly written body file's characters (DYD-639 ruling): letters, digits, . _ - + / and,
    // quoted, space. PowerShell also takes \ and a leading drive; in bash a backslash escapes.
    [GeneratedRegex(@"^[A-Za-z0-9._+/ -]+\z")]
    private static partial Regex PosixLiteralRegex();

    [GeneratedRegex(@"^(?:[A-Za-z]:[\\/])?[A-Za-z0-9._+/\\ -]+\z")]
    private static partial Regex PowerShellLiteralRegex();

    // What may join the line's commands before the call, before the call itself (no lone pipe), and
    // the call's pipeline members; and how the rest of the line after that pipeline may start.
    [GeneratedRegex(@"^[ \t\n;&|]*\z")]
    private static partial Regex JoinRegex();

    [GeneratedRegex(@"^(?:[ \t\n;&]|\|\|)*\z")]
    private static partial Regex CallJoinRegex();

    [GeneratedRegex(@"^[ \t]*\|[ \t\n]*\z")]
    private static partial Regex PipeRegex();

    [GeneratedRegex(@"^[ \t]*(?:[;\n]|&&|\|\||\z)")]
    private static partial Regex EndRegex();

    // A & that backgrounds a list: neither && nor part of a redirection (2>&1, <&0, &>).
    [GeneratedRegex(@"(?<![&<>])&(?![&>])")]
    private static partial Regex LoneAmpersandRegex();

    // A redirection that writes no file: an input (2<f, not <> or <<), or an fd duplicated or closed.
    [GeneratedRegex(@"^\d*(?:<(?![<>&])|[<>]&[ \t]*(?:\d+|-)\z)")]
    private static partial Regex ReadOnlyRedirectionRegex();

    // What may add arguments, outside and inside double quotes (DYD-639 ruling, point 5).
    [GeneratedRegex(@"[$`*?\[{}~()\\]")]
    private static partial Regex PosixUnquotedExpansionRegex();

    [GeneratedRegex(@"[`\\]|\$(?![A-Za-z0-9_])")]
    private static partial Regex PosixQuotedExpansionRegex();

    [GeneratedRegex(@"[$@`(){},%]")]
    private static partial Regex PowerShellUnquotedExpansionRegex();

    [GeneratedRegex(@"[$@`]")]
    private static partial Regex PowerShellQuotedExpansionRegex();

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
