namespace DynaDocs.Services;

using System.Text.RegularExpressions;

/// <summary>A command string a shell or eval is given, and the dialect that shell reads it in.</summary>
internal sealed record ShellScript(string Text, ShellDialect Dialect, bool Encoded = false);

/// <summary>
/// Where the program sits in one command's words, shared by the scanners that read shell command
/// lines (<see cref="DydoInvocationScanner"/>, <see cref="PrCallScanner"/>): past VAR=value words,
/// shell keywords and the programs that run their remaining words (env, sudo, nohup, time,
/// timeout, ...) with their options, and with any path, case or executable suffix. It also finds
/// the command string a shell is given (bash -c, cmd /c, eval, env -S, pwsh -Command or -EncodedCommand)
/// with the dialect that string is read in, and Codex's shell envelope: an argv array that is a
/// shell, its options, its script flag and one script.
/// </summary>
internal static partial class CommandPosition
{
    private static readonly HashSet<string> Keywords =
        new(StringComparer.Ordinal) { "!", "if", "then", "else", "elif", "do", "while", "until", "call", "." };

    // Programs that run their remaining words as a command, with the options that take a value.
    private static readonly Dictionary<string, HashSet<string>> Runners = new(StringComparer.Ordinal)
    {
        ["env"] = ["-u", "--unset", "-C", "--chdir"],
        ["command"] = [],
        ["builtin"] = [],
        ["exec"] = ["-a"],
        ["nohup"] = [],
        ["time"] = ["-f", "-o", "--format", "--output"],
        ["nice"] = ["-n", "--adjustment"],
        ["timeout"] = ["-s", "-k", "--signal", "--kill-after"],
        ["sudo"] = ["-u", "-g", "-C", "-h", "-p", "-r", "-t", "-U", "-D", "-R", "-T", "--user", "--group"],
        ["doas"] = ["-u", "-C"],
        ["xargs"] = ["-I", "-L", "-n", "-P", "-s", "-d", "-E", "-a", "--max-args", "--max-procs",
            "--delimiter", "--arg-file", "--max-lines", "--max-chars"],
    };

    /// <summary>The name <see cref="Skip"/> reports for env -i, which clears every variable.</summary>
    public const string AllVariables = "*";

    internal static readonly HashSet<string> PosixShells = new(StringComparer.Ordinal) { "bash", "sh", "zsh", "dash", "ksh" };

    // The shells whose argv envelope Codex sends (admiral ruling 2026-10-27).
    private static readonly HashSet<string> EnvelopeShells =
        new(StringComparer.Ordinal) { "bash", "sh", "zsh", "pwsh", "powershell" };

    private static readonly string[] PwshValueOptions =
        ["-executionpolicy", "-ep", "-ex", "-windowstyle", "-workingdirectory", "-wd", "-configurationname",
         "-version", "-inputformat", "-if", "-outputformat", "-of", "-settingsfile", "-psconsolefile", "-custompipename"];

    /// <summary>How pwsh or powershell is told what to run: the words after that choice are its tail.</summary>
    internal enum PwshMode
    {
        /// <summary>-Command: the tail is the command string.</summary>
        Command,

        /// <summary>-EncodedCommand: the tail's first word is the encoded command.</summary>
        Encoded,

        /// <summary>-File: a script file, no command string.</summary>
        File,

        /// <summary>No mode flag: Windows PowerShell runs its positional words (the tail) as a command.</summary>
        Positional,
    }

    /// <summary>
    /// The index of the program word in <paramref name="words"/> (the count when there is none). Each
    /// VAR=value word skipped is added to <paramref name="assignments"/> when it is given, in order with
    /// env's own changes: -u/--unset NAME as (NAME, ""), -i as (<see cref="AllVariables"/>, ""). env
    /// -S/--split-string runs a string, so env is then the program (<see cref="ScriptOf"/>).
    /// </summary>
    public static int Skip(IReadOnlyList<string> words, List<(string Name, string Value)>? assignments = null)
    {
        var at = 0;
        while (at < words.Count)
        {
            var name = ProgramName(words[at]);
            var assignment = AssignmentRegex().Match(words[at]);
            if (assignment.Success)
            {
                assignments?.Add((assignment.Groups["name"].Value, assignment.Groups["value"].Value));
                at++;
            }
            else if (Keywords.Contains(name))
                at++;
            else if (name == "env" && EnvSplitString(words.Skip(at + 1).ToList()) != null)
                break;
            else if (Runners.TryGetValue(name, out var valueOptions))
                at = SkipRunnerOptions(words, at + 1, valueOptions, name == "timeout", name == "env" ? assignments : null);
            else
                break;
        }

        return at;
    }

    // envChanges, given for env, receives its -u/--unset and -i changes.
    private static int SkipRunnerOptions(IReadOnlyList<string> words, int at, HashSet<string> valueOptions, bool takesDuration,
        List<(string Name, string Value)>? envChanges)
    {
        while (at < words.Count && words[at].Length > 1 && words[at][0] == '-')
        {
            if (words[at] == "--")
                return at + 1 + (takesDuration ? 1 : 0);
            if (envChanges != null && EnvChange(words, at) is { } change)
                envChanges.Add((change, ""));
            at += valueOptions.Contains(words[at]) ? 2 : 1;
        }

        return at + (takesDuration ? 1 : 0);
    }

    // The variable an env option at words[at] removes: -u NAME, --unset NAME, --unset=NAME, or every one for -i.
    private static string? EnvChange(IReadOnlyList<string> words, int at) => words[at] switch
    {
        "-i" or "--ignore-environment" => AllVariables,
        "-u" or "--unset" => at + 1 < words.Count ? words[at + 1] : null,
        var word when word.StartsWith("--unset=", StringComparison.Ordinal) => word["--unset=".Length..],
        _ => null,
    };

    /// <summary>
    /// The string env -S (or --split-string) splits into the command it runs, with env's remaining
    /// words after it; null when env is given no -S.
    /// </summary>
    public static string? EnvSplitString(IReadOnlyList<string> rest)
    {
        var valueOptions = Runners["env"];
        for (var at = 0; at < rest.Count && rest[at].Length > 1 && rest[at][0] == '-' && rest[at] != "--";
             at += valueOptions.Contains(rest[at]) ? 2 : 1)
        {
            var (split, next) = rest[at] switch
            {
                "-S" or "--split-string" => (at + 1 < rest.Count ? rest[at + 1] : "", at + 2),
                var word when word.StartsWith("--split-string=", StringComparison.Ordinal) => (word["--split-string=".Length..], at + 1),
                var word when word.StartsWith("-S", StringComparison.Ordinal) => (word[2..], at + 1),
                _ => ((string?)null, 0),
            };
            if (split != null)
                return string.Join(' ', rest.Skip(next).Prepend(split));
        }

        return null;
    }

    /// <summary>A program word's name: no directory, lower case, no executable suffix.</summary>
    public static string ProgramName(string word)
    {
        var name = word[(word.LastIndexOfAny(['/', '\\']) + 1)..].ToLowerInvariant();
        return ExecutableSuffixRegex().Replace(name, "");
    }

    /// <summary>
    /// The command string <paramref name="program"/> runs given <paramref name="rest"/>, its words,
    /// or null when it is no shell or eval or names no command string (a script file, stdin).
    /// </summary>
    public static ShellScript? ScriptOf(string program, IReadOnlyList<string> rest)
    {
        if (program is "pwsh" or "powershell")
            return PwshScript(rest);
        var (text, dialect) = program switch
        {
            "cmd" => (CmdString(rest), ShellDialect.Unknown),
            "eval" => (EvalString(rest), ShellDialect.Posix),
            "env" => (EnvSplitString(rest), ShellDialect.Posix),
            "iex" or "invoke-expression" => (EvalString(rest), ShellDialect.PowerShell),
            _ when PosixShells.Contains(program) => (PosixShellString(rest), ShellDialect.Posix),
            _ => (null, ShellDialect.Unknown),
        };
        return string.IsNullOrEmpty(text) ? null : new ShellScript(text, dialect);
    }

    private static ShellScript? PwshScript(IReadOnlyList<string> rest)
    {
        var (mode, tail) = ReadPwsh(rest);
        return mode switch
        {
            PwshMode.Encoded => new ShellScript("", ShellDialect.PowerShell, Encoded: true),
            PwshMode.File => null,
            _ when tail is [] or ["-"] => null,
            _ => new ShellScript(string.Join(' ', tail), ShellDialect.PowerShell),
        };
    }

    /// <summary>
    /// Codex's shell envelope: <c>[shell, options..., script flag, script]</c> with the shell bash, sh,
    /// zsh, pwsh or powershell. Its script runs as that shell's direct command line; null for any
    /// other array.
    /// </summary>
    public static ShellScript? Envelope(IReadOnlyList<string> argv)
    {
        if (argv.Count < 3 || !EnvelopeShells.Contains(ProgramName(argv[0])))
            return null;

        var rest = argv.Skip(1).ToList();
        if (ProgramName(argv[0]) is "pwsh" or "powershell")
        {
            var (mode, tail) = ReadPwsh(rest);
            return mode == PwshMode.Command && tail.Count == 1 ? new ShellScript(tail[0], ShellDialect.PowerShell) : null;
        }

        var (commandMode, at) = PosixShellOperands(rest);
        return commandMode && at == rest.Count - 1 ? new ShellScript(rest[at], ShellDialect.Posix) : null;
    }

    /// <summary>cmd's command string: the words after /c or /k (//c from Git Bash, which would rewrite /c as a path).</summary>
    public static string? CmdString(IReadOnlyList<string> rest)
    {
        var at = rest.ToList().FindIndex(word => CmdSwitchLength(word) > 0);
        return at < 0 ? null : string.Join(' ', rest.Skip(at + 1).Prepend(rest[at][CmdSwitchLength(rest[at])..]).Where(word => word.Length > 0));
    }

    private static int CmdSwitchLength(string word)
    {
        var slashes = word.StartsWith("//", StringComparison.Ordinal) ? 2 : 1;
        return word.Length > slashes && word[0] == '/' && char.ToLowerInvariant(word[slashes]) is 'c' or 'k' ? slashes + 1 : 0;
    }

    /// <summary>
    /// A POSIX shell's command string with -c; "" when it runs a script file or -c has no operand;
    /// null when it reads its commands from stdin.
    /// </summary>
    public static string? PosixShellString(IReadOnlyList<string> rest)
    {
        var (commandMode, at) = PosixShellOperands(rest);
        if (commandMode)
            return at < rest.Count ? rest[at] : "";
        return at < rest.Count ? "" : null;
    }

    // The shell's options, and whether one of them (-c, -lc, ...) makes its first operand the command string.
    private static (bool CommandMode, int At) PosixShellOperands(IReadOnlyList<string> rest)
    {
        var commandMode = false;
        var at = 0;
        while (at < rest.Count && rest[at].Length > 1 && rest[at][0] is '-' or '+' && rest[at] != "--")
        {
            var option = rest[at];
            commandMode |= option[0] == '-' && !option.StartsWith("--", StringComparison.Ordinal) && option.Contains('c');
            at += option is "-o" or "+o" or "-O" or "+O" or "--rcfile" or "--init-file" ? 2 : 1;
        }

        if (at < rest.Count && rest[at] == "--")
            at++;
        return (commandMode, at);
    }

    /// <summary>eval's (or Invoke-Expression's) command string: its words joined.</summary>
    public static string? EvalString(IReadOnlyList<string> rest)
    {
        var words = rest.Count > 0 && IsPrefixOption(rest[0].ToLowerInvariant(), "-command") ? rest.Skip(1).ToList() : [.. rest];
        return words.Count == 0 ? null : string.Join(' ', words);
    }

    /// <summary>What pwsh or powershell runs given <paramref name="rest"/>, its words past the program.</summary>
    public static (PwshMode Mode, List<string> Tail) ReadPwsh(IReadOnlyList<string> rest)
    {
        var at = 0;
        while (at < rest.Count && rest[at].Length >= 2 && rest[at][0] is '-' or '/')
        {
            var option = "-" + rest[at][1..].ToLowerInvariant();
            var tail = rest.Skip(at + 1).ToList();
            if (IsPrefixOption(option, "-command"))
                return (PwshMode.Command, tail);
            if (option == "-ec" || IsPrefixOption(option, "-encodedcommand"))
                return (PwshMode.Encoded, tail);
            if (IsPrefixOption(option, "-file"))
                return (PwshMode.File, tail);
            at += PwshValueOptions.Any(value => value.StartsWith(option, StringComparison.Ordinal)) ? 2 : 1;
        }

        return (PwshMode.Positional, rest.Skip(at).ToList());
    }

    /// <summary>PowerShell takes any unambiguous prefix of a parameter name; -c and -co read as -Command.</summary>
    public static bool IsPrefixOption(string option, string name) =>
        option.Length >= 2 && name.StartsWith(option, StringComparison.Ordinal);

    [GeneratedRegex(@"^(?<name>[A-Za-z_][A-Za-z0-9_]*)=(?<value>.*)$", RegexOptions.Singleline)]
    private static partial Regex AssignmentRegex();

    [GeneratedRegex(@"\.(?:exe|cmd|bat|com|ps1)$")]
    private static partial Regex ExecutableSuffixRegex();
}
