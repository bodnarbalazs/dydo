namespace DynaDocs.Services;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>The shell that runs a command line. Unknown reads it both ways, failing closed.</summary>
internal enum ShellDialect
{
    Unknown,
    Posix,
    PowerShell,
}

/// <summary>
/// The dydo invocations a shell command line runs, each as the words after the dydo executable.
/// The line is read as its shell reads it: bash (backslash escapes, backtick substitution) or
/// PowerShell (backtick escapes, literal backslashes). When the shell is unknown, and for cmd, whose
/// quoting neither reading matches, it is read both ways and every invocation either finds counts. Only a word at command position is a program: past environment
/// assignments, shell keywords and transparent wrappers (env, sudo, nohup, xargs, ...). Words inside
/// quotes, here-strings and heredocs are arguments, never commands, except where a shell runs them:
/// <c>$(...)</c> and backtick substitutions, and the command string of cmd /c, pwsh -Command,
/// bash -c, eval or Invoke-Expression, which is read again with that shell's rules. Start-Process runs its FilePath with
/// its ArgumentList. <see cref="Scan.Uncertain"/> is set when a reading cannot finish (an unbalanced
/// quote, nesting past the limit) or a shell reads commands from a pipe.
/// </summary>
internal sealed partial class DydoInvocationScanner
{
    internal sealed record Scan(IReadOnlyList<IReadOnlyList<string>> Invocations, bool Uncertain);

    private const int MaxDepth = 8;

    private static readonly HashSet<string> Keywords =
        new(StringComparer.Ordinal) { "!", "if", "then", "else", "elif", "do", "while", "until", "call", "." };

    // Wrappers that run their remaining words as a command, with the options that take a value.
    private static readonly Dictionary<string, HashSet<string>> Wrappers = new(StringComparer.Ordinal)
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

    private static readonly HashSet<string> PosixShells = new(StringComparer.Ordinal) { "bash", "sh", "zsh", "dash", "ksh" };

    private static readonly string[] PwshValueOptions =
        ["-executionpolicy", "-ep", "-ex", "-windowstyle", "-workingdirectory", "-wd", "-configurationname",
         "-version", "-inputformat", "-if", "-outputformat", "-of", "-settingsfile", "-psconsolefile", "-custompipename"];

    private static readonly string[] StartProcessValueOptions =
        ["-workingdirectory", "-wd", "-redirectstandardinput", "-rsi", "-redirectstandardoutput", "-rso",
         "-redirectstandarderror", "-rse", "-credential", "-windowstyle", "-verb", "-environment"];

    private readonly List<IReadOnlyList<string>> _invocations = [];
    private bool _uncertain;

    public static Scan Find(string command, ShellDialect dialect = ShellDialect.Unknown)
    {
        var scanner = new DydoInvocationScanner();
        scanner.ScanText(command, 0, dialect);
        return new Scan(scanner._invocations, scanner._uncertain);
    }

    private static bool[] Readings(ShellDialect dialect) => dialect switch
    {
        ShellDialect.Posix => [true],
        ShellDialect.PowerShell => [false],
        _ => [true, false],
    };

    private void ScanText(string text, int depth, ShellDialect dialect)
    {
        if (depth > MaxDepth)
        {
            _uncertain = true;
            return;
        }

        foreach (var posix in Readings(dialect))
        {
            var reading = ShellReader.Read(text, posix);
            _uncertain |= reading.Failed;
            foreach (var substitution in reading.Substitutions)
                ScanText(substitution, depth + 1, ShellDialect.Posix);
            foreach (var command in reading.Commands)
                Analyse(command.Words, command.Stdin, depth);
        }
    }

    private void Analyse(IReadOnlyList<string> words, IReadOnlyList<string> stdin, int depth)
    {
        var at = SkipPrefixes(words);
        if (at >= words.Count)
            return;

        var program = ProgramName(words[at]);
        var rest = words.Skip(at + 1).ToList();
        if (program == "dydo")
            _invocations.Add(rest);
        else if (program == "dotnet")
            AddDotnet(rest);
        else if (program == "cmd")
            RunCommandString(CmdString(rest), stdin, depth, ShellDialect.Unknown);
        else if (program is "pwsh" or "powershell")
            RunPwsh(rest, stdin, depth);
        else if (PosixShells.Contains(program))
            RunCommandString(PosixShellString(rest), stdin, depth, ShellDialect.Posix);
        else if (program == "eval")
            RunCommandString(EvalString(rest), stdin, depth, ShellDialect.Posix);
        else if (program is "iex" or "invoke-expression")
            RunCommandString(EvalString(rest), stdin, depth, ShellDialect.PowerShell);
        else if (program is "start-process" or "saps" or "start")
            AnalyseStartProcess(rest, program == "start", depth);
    }

    private static int SkipPrefixes(IReadOnlyList<string> words)
    {
        var at = 0;
        while (at < words.Count)
        {
            var name = ProgramName(words[at]);
            if (AssignmentRegex().IsMatch(words[at]) || Keywords.Contains(name))
                at++;
            else if (Wrappers.TryGetValue(name, out var valueOptions))
                at = SkipWrapperOptions(words, at + 1, valueOptions, name == "timeout");
            else
                break;
        }

        return at;
    }

    private static int SkipWrapperOptions(IReadOnlyList<string> words, int at, HashSet<string> valueOptions, bool takesDuration)
    {
        while (at < words.Count && words[at].Length > 1 && words[at][0] == '-')
        {
            if (words[at] == "--")
                return at + 1 + (takesDuration ? 1 : 0);
            at += valueOptions.Contains(words[at]) ? 2 : 1;
        }

        return at + (takesDuration ? 1 : 0);
    }

    /// <summary>The word's file name, lower case, without a .exe, .cmd, .bat, .com or .ps1 suffix.</summary>
    private static string ProgramName(string word)
    {
        var name = word[(word.LastIndexOfAny(['/', '\\']) + 1)..].ToLowerInvariant();
        return ExecutableSuffixRegex().Replace(name, "");
    }

    private void AddDotnet(List<string> rest)
    {
        if (rest.Count > 0 && rest[0] == "run")
        {
            var separator = rest.IndexOf("--");
            if (separator >= 0 && RunsDydo(rest.Take(separator).ToList()))
                _invocations.Add(rest.Skip(separator + 1).ToList());
            return;
        }

        var at = rest.Count > 0 && rest[0] == "exec" ? 1 : 0;
        while (at < rest.Count && rest[at].StartsWith('-'))
            at += 2;
        if (at < rest.Count && ProgramName(rest[at]) == "dydo.dll")
            _invocations.Add(rest.Skip(at + 1).ToList());
    }

    // dotnet run targets dydo when --project or -p names dydo or DynaDocs, as a .csproj or a
    // directory. With no project it runs the current directory's, which may be dydo: counted, fail-closed.
    private static bool RunsDydo(List<string> options)
    {
        string? project = null;
        for (var at = 0; at < options.Count; at++)
        {
            var match = ProjectOptionRegex().Match(options[at]);
            if (!match.Success)
                continue;
            project = match.Groups[1].Success ? match.Groups[1].Value : options.ElementAtOrDefault(++at) ?? "";
        }

        if (project == null)
            return true;
        var path = project.TrimEnd('/', '\\');
        var name = path[(path.LastIndexOfAny(['/', '\\']) + 1)..];
        if (name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            name = name[..^".csproj".Length];
        return name.Equals("dydo", StringComparison.OrdinalIgnoreCase) || name.Equals("DynaDocs", StringComparison.OrdinalIgnoreCase);
    }

    // A shell given no command string reads its commands from stdin: a here-string or heredoc is
    // read again, and a pipe cannot be seen.
    private void RunCommandString(string? commandString, IReadOnlyList<string> stdin, int depth, ShellDialect dialect)
    {
        if (commandString != null)
        {
            ScanText(commandString, depth + 1, dialect);
            return;
        }

        if (stdin.Count == 0)
            _uncertain = true;
        foreach (var text in stdin)
            ScanText(text, depth + 1, dialect);
    }

    private static string? CmdString(List<string> rest)
    {
        var at = rest.FindIndex(word => word.Length >= 2 && word[0] == '/' && char.ToLowerInvariant(word[1]) is 'c' or 'k');
        return at < 0 ? null : string.Join(' ', rest.Skip(at + 1).Prepend(rest[at][2..]).Where(word => word.Length > 0));
    }

    private static string? PosixShellString(List<string> rest)
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
        if (commandMode)
            return at < rest.Count ? rest[at] : "";
        return at < rest.Count ? "" : null;
    }

    private static string? EvalString(List<string> rest)
    {
        if (rest.Count > 0 && IsPrefixOption(rest[0].ToLowerInvariant(), "-command"))
            rest = rest.Skip(1).ToList();
        return rest.Count == 0 ? null : string.Join(' ', rest);
    }

    private void RunPwsh(List<string> rest, IReadOnlyList<string> stdin, int depth)
    {
        var at = 0;
        while (at < rest.Count && rest[at].Length >= 2 && rest[at][0] is '-' or '/')
        {
            var option = "-" + rest[at][1..].ToLowerInvariant();
            var tail = rest.Skip(at + 1).ToList();
            if (IsPrefixOption(option, "-command"))
            {
                RunCommandString(tail is [] or ["-"] ? null : string.Join(' ', tail), stdin, depth, ShellDialect.PowerShell);
                return;
            }

            if (option == "-ec" || IsPrefixOption(option, "-encodedcommand"))
            {
                RunEncoded(tail.FirstOrDefault() ?? "", depth);
                return;
            }

            if (IsPrefixOption(option, "-file"))
                return;
            at += PwshValueOptions.Any(value => value.StartsWith(option, StringComparison.Ordinal)) ? 2 : 1;
        }

        // Windows PowerShell runs its first positional words as a command.
        RunCommandString(at < rest.Count ? string.Join(' ', rest.Skip(at)) : null, stdin, depth, ShellDialect.PowerShell);
    }

    private void RunEncoded(string encoded, int depth)
    {
        try
        {
            ScanText(Encoding.Unicode.GetString(Convert.FromBase64String(encoded)), depth + 1, ShellDialect.PowerShell);
        }
        catch (FormatException)
        {
            _uncertain = true;
        }
    }

    // PowerShell takes any unambiguous prefix of a parameter name; -c and -co read as -Command.
    private static bool IsPrefixOption(string option, string name) =>
        option.Length >= 2 && name.StartsWith(option, StringComparison.Ordinal);

    /// <summary>
    /// Start-Process runs its FilePath (the first positional word) with its ArgumentList (the second,
    /// a comma-separated array or one command-line string). cmd's start runs its first word that is
    /// neither a /switch nor the window title.
    /// </summary>
    private void AnalyseStartProcess(List<string> rest, bool cmdStart, int depth)
    {
        var (file, arguments) = ParseStartProcess(rest);
        if (file != null)
            Analyse([file, .. arguments], [], depth + 1);
        if (!cmdStart)
            return;

        var words = rest.Where(word => !word.StartsWith('/')).ToList();
        Analyse(words, [], depth + 1);
        Analyse(words.Skip(1).ToList(), [], depth + 1);
    }

    private static (string? File, List<string> Arguments) ParseStartProcess(List<string> rest)
    {
        string? file = null;
        var arguments = new List<string>();
        var at = 0;
        while (at < rest.Count)
        {
            var option = rest[at].Length > 1 && rest[at][0] == '-' ? rest[at].ToLowerInvariant() : null;
            if (option == null && file == null)
                file = rest[at++];
            else if (option == null)
                at = ReadArray(rest, at, arguments);
            else if (option == "-args" || IsPrefixOption(option, "-argumentlist"))
                at = ReadArray(rest, at + 1, arguments);
            else if (option is "-path" or "-pspath" || IsPrefixOption(option, "-filepath"))
            {
                file = rest.ElementAtOrDefault(at + 1);
                at += 2;
            }
            else
                at += StartProcessValueOptions.Any(value => value.StartsWith(option, StringComparison.Ordinal)) ? 2 : 1;
        }

        return (file, arguments);
    }

    // One PowerShell array argument: words joined by commas, each split again into command-line words.
    private static int ReadArray(List<string> rest, int at, List<string> arguments)
    {
        while (at < rest.Count)
        {
            var word = rest[at++];
            arguments.AddRange(word.Split([',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
            var continues = word.EndsWith(',') || (at < rest.Count && rest[at].StartsWith(','));
            if (!continues)
                break;
        }

        return at;
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*=")]
    private static partial Regex AssignmentRegex();

    [GeneratedRegex(@"\.(?:exe|cmd|bat|com|ps1)$")]
    private static partial Regex ExecutableSuffixRegex();

    [GeneratedRegex(@"^(?:--project|-p)(?:[=:](.*))?$")]
    private static partial Regex ProjectOptionRegex();
}
