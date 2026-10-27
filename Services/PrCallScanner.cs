namespace DynaDocs.Services;

using System.Text.RegularExpressions;

/// <summary>
/// The PR-opening gh calls a command line makes, read as bash reads it (DYD-391): <c>gh pr create</c>,
/// <c>gh pr new</c>, <c>gh pr ready</c>, a <c>gh api</c> POST to <c>repos/OWNER/REPO/pulls</c>, and
/// <c>gh api graphql</c> naming createPullRequest or markPullRequestReadyForReview. gh counts at
/// command position past VAR=value words, shell keywords and env, time, nice and timeout, with any
/// path, case or .exe. Text a command only reads (quotes, heredoc and here-string bodies, comments)
/// is no call. A shell or eval given a command string is not read: gh text shaped like such a call
/// in its words, or an encoded PowerShell command, makes it a wrapped call. It stops accidents, not
/// a deliberate bypass: substitutions, aliases, functions and other wrappers are not followed.
/// </summary>
internal static partial class PrCallScanner
{
    private static readonly HashSet<string> Keywords =
        new(StringComparer.Ordinal) { "!", "if", "elif", "then", "else", "do", "while", "until" };

    // Programs that run their remaining words, with the options that take a value.
    private static readonly Dictionary<string, string[]> Prefixes = new(StringComparer.Ordinal)
    {
        ["env"] = ["-u", "--unset", "-C", "--chdir"],
        ["time"] = ["-f", "-o", "--format", "--output"],
        ["nice"] = ["-n", "--adjustment"],
        ["timeout"] = ["-s", "-k", "--signal", "--kill-after"],
    };

    private static readonly HashSet<string> Wrappers =
        new(StringComparer.Ordinal) { "pwsh", "powershell", "bash", "sh", "zsh", "dash", "ksh", "cmd", "eval" };

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

    public static List<PrCall> Find(string command)
    {
        // gh's {owner}, {repo} and {branch} placeholders, spelt as its :owner form, so braces split no command.
        var reading = ShellReader.Read(GhPlaceholderRegex().Replace(command, ":$1"), posix: true);
        return [.. reading.Commands.Select(c => Match(c.Words)).OfType<PrCall>()];
    }

    private static PrCall? Match(IReadOnlyList<string> words)
    {
        var (at, envRepos) = SkipPrefixes(words);
        if (at >= words.Count)
            return null;

        var program = ProgramName(words[at]);
        var rest = words.Skip(at + 1).ToList();
        if (program == "gh")
            return GhCall(rest, envRepos);
        if (Wrappers.Contains(program) && (IsEncodedPowerShell(program, rest) || WrappedPrTextRegex().IsMatch(string.Join(' ', rest))))
            return new PrCall([], Wrapper: program);
        return null;
    }

    private static (int At, List<string> EnvRepos) SkipPrefixes(IReadOnlyList<string> words)
    {
        var at = 0;
        List<string> envRepos = [];
        while (at < words.Count)
        {
            var assignment = AssignmentRegex().Match(words[at]);
            if (assignment.Success)
            {
                if (assignment.Groups["name"].Value == "GH_REPO")
                    envRepos.Add(assignment.Groups["value"].Value);
                at++;
            }
            else if (Keywords.Contains(words[at]))
                at++;
            else if (Prefixes.TryGetValue(ProgramName(words[at]), out var valueOptions))
                at = SkipOptions(words, at + 1, valueOptions, takesOperand: ProgramName(words[at]) == "timeout");
            else
                break;
        }
        return (at, envRepos);
    }

    private static int SkipOptions(IReadOnlyList<string> words, int at, string[] valueOptions, bool takesOperand)
    {
        while (at < words.Count && IsFlag(words[at]))
            at += valueOptions.Contains(words[at]) ? 2 : 1;
        return takesOperand ? at + 1 : at;
    }

    // gh's own flags may precede its subcommand, each with at most one value word.
    private static PrCall? GhCall(List<string> rest, List<string> envRepos)
    {
        var sub = rest.FindIndex(w => w.Equals("pr", StringComparison.OrdinalIgnoreCase) || w.Equals("api", StringComparison.OrdinalIgnoreCase));
        if (sub < 0 || Enumerable.Range(0, sub).Any(i => !IsFlag(rest[i]) && (i == 0 || !IsFlag(rest[i - 1]))))
            return null;

        List<string> repos = [.. envRepos, .. Values(Parse(rest[..sub], RepoOptions).Options, "repo")];
        var args = rest[(sub + 1)..];
        return rest[sub].Equals("pr", StringComparison.OrdinalIgnoreCase) ? PrVerbCall(args, repos) : ApiCall(args, repos);
    }

    private static PrCall? PrVerbCall(List<string> args, List<string> repos)
    {
        var parsed = Parse(args, PrOptions);
        var verb = parsed.Positionals.FirstOrDefault();
        if (verb == null || !PrVerbs.Contains(verb))
            return null;

        repos.AddRange(Values(parsed.Options, "repo"));
        verb = verb.ToLowerInvariant();
        if (verb == "ready")
        {
            var target = parsed.Positionals.ElementAtOrDefault(1);
            return target == null || target.All(char.IsAsciiDigit) || target.Contains("://")
                ? new PrCall(repos)
                : new PrCall(repos, BranchOf(target), $"gh pr ready {target}");
        }

        var head = Values(parsed.Options, "head").LastOrDefault();
        return head == null ? new PrCall(repos) : new PrCall(repos, BranchOf(head), $"gh pr {verb} --head {head}");
    }

    private static PrCall? ApiCall(List<string> args, List<string> repos)
    {
        var parsed = Parse(args, ApiOptions);
        var endpoint = parsed.Positionals.FirstOrDefault();
        if (endpoint == null)
            return null;

        var fields = parsed.Options.Where(o => o.Name is "field" or "raw-field").Select(o => o.Value).ToList();
        if (GraphQlEndpointRegex().IsMatch(endpoint))
            return fields.Any(f => GraphQlPrMutationRegex().IsMatch(f)) ? new PrCall(repos) : null;

        var pulls = RestPullsRegex().Match(endpoint);
        var sendsBody = fields.Count > 0 || Values(parsed.Options, "input").Any();
        var method = Values(parsed.Options, "method").LastOrDefault() ?? (sendsBody ? "POST" : "GET");
        if (!pulls.Success || !method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            return null;

        if (pulls.Groups["owner"].Value != ":owner")
            repos.Add($"{pulls.Groups["owner"].Value}/{pulls.Groups["repo"].Value}");
        var head = fields.LastOrDefault(f => f.StartsWith("head=", StringComparison.Ordinal))?["head=".Length..];
        return head is null or ":branch" ? new PrCall(repos) : new PrCall(repos, BranchOf(head), $"gh api {endpoint} head={head}");
    }

    // A head may name its fork's owner: OWNER:BRANCH.
    private static string BranchOf(string head) => head[(head.IndexOf(':') + 1)..];

    private static bool IsEncodedPowerShell(string program, List<string> rest) =>
        program is "pwsh" or "powershell" && rest.Any(w => w.Length > 1 && w[0] is '-' or '/' && IsEncodedCommandFlag(w[1..].ToLowerInvariant()));

    private static bool IsEncodedCommandFlag(string flag) =>
        flag is "e" or "ec" || (flag.Length >= 2 && "encodedcommand".StartsWith(flag, StringComparison.Ordinal));

    private static bool IsFlag(string word) => word.Length > 1 && word[0] == '-';

    private static string ProgramName(string word)
    {
        var name = word.Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..].ToLowerInvariant();
        return name.EndsWith(".exe", StringComparison.Ordinal) ? name[..^4] : name;
    }

    private static (List<(string Name, string Value)> Options, List<string> Positionals) Parse(List<string> words, Options options)
    {
        List<(string Name, string Value)> values = [];
        List<string> positionals = [];
        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];
            if (word.StartsWith("--", StringComparison.Ordinal) && word.Length > 2)
            {
                var eq = word.IndexOf('=');
                var name = eq > 0 ? word[2..eq] : word[2..];
                if (options.Long.Contains(name))
                    values.Add((name, eq > 0 ? word[(eq + 1)..] : Next(words, ref i)));
            }
            else if (IsFlag(word))
                ReadShortFlags(words, ref i, options, values);
            else
                positionals.Add(word);
        }
        return (values, positionals);
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

    [GeneratedRegex(@"^(?<name>[A-Za-z_][A-Za-z0-9_]*)=(?<value>.*)$", RegexOptions.Singleline)]
    private static partial Regex AssignmentRegex();

    [GeneratedRegex(@"^(?:https?://[^/]+(?:/api/v3)?)?/?repos/(?<owner>[^/]+)/(?<repo>[^/?]+)/pulls/?(?:\?.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex RestPullsRegex();

    [GeneratedRegex(@"(?:^|/)graphql$", RegexOptions.IgnoreCase)]
    private static partial Regex GraphQlEndpointRegex();

    [GeneratedRegex(@"createPullRequest|markPullRequestReadyForReview", RegexOptions.IgnoreCase)]
    private static partial Regex GraphQlPrMutationRegex();

    // The textual backstop for wrapped calls: gh, a few flag or value words, then a PR verb or a PR API.
    [GeneratedRegex(@"\bgh(?:\.exe)?[""']?\s+(?:[^\s;&|]+\s+)*?(?:pr\s+(?:[^\s;&|]+\s+)*?(?:create|new|ready)\b|api\b[^;&|]*?(?:/pulls\b|createPullRequest|markPullRequestReadyForReview))",
        RegexOptions.IgnoreCase)]
    private static partial Regex WrappedPrTextRegex();
}
