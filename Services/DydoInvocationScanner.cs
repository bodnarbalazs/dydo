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
/// assignments, shell keywords and transparent wrappers (env, sudo, nohup, xargs, ...; see <see cref="CommandPosition"/>). Words inside
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
        var at = CommandPosition.Skip(words);
        if (at >= words.Count)
            return;

        var program = CommandPosition.ProgramName(words[at]);
        var rest = words.Skip(at + 1).ToList();
        if (program == "dydo")
            _invocations.Add(rest);
        else if (program == "dotnet")
            AddDotnet(rest);
        else if (program == "cmd")
            RunCommandString(CommandPosition.CmdString(rest), stdin, depth, ShellDialect.Unknown);
        else if (program is "pwsh" or "powershell")
            RunPwsh(rest, stdin, depth);
        else if (CommandPosition.PosixShells.Contains(program))
            RunCommandString(CommandPosition.PosixShellString(rest), stdin, depth, ShellDialect.Posix);
        else if (program == "eval")
            RunCommandString(CommandPosition.EvalString(rest), stdin, depth, ShellDialect.Posix);
        else if (program is "iex" or "invoke-expression")
            RunCommandString(CommandPosition.EvalString(rest), stdin, depth, ShellDialect.PowerShell);
        else if (program is "start-process" or "saps" or "start")
            AnalyseStartProcess(rest, program == "start", depth);
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
        if (at < rest.Count && CommandPosition.ProgramName(rest[at]) == "dydo.dll")
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

    private void RunPwsh(List<string> rest, IReadOnlyList<string> stdin, int depth)
    {
        var (mode, tail) = CommandPosition.ReadPwsh(rest);
        if (mode == CommandPosition.PwshMode.Encoded)
            RunEncoded(tail.FirstOrDefault() ?? "", depth);
        else if (mode == CommandPosition.PwshMode.Command)
            RunCommandString(tail is [] or ["-"] ? null : string.Join(' ', tail), stdin, depth, ShellDialect.PowerShell);
        else if (mode == CommandPosition.PwshMode.Positional)
            RunCommandString(tail.Count > 0 ? string.Join(' ', tail) : null, stdin, depth, ShellDialect.PowerShell);
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
            else if (option == "-args" || CommandPosition.IsPrefixOption(option, "-argumentlist"))
                at = ReadArray(rest, at + 1, arguments);
            else if (option is "-path" or "-pspath" || CommandPosition.IsPrefixOption(option, "-filepath"))
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

    [GeneratedRegex(@"^(?:--project|-p)(?:[=:](.*))?$")]
    private static partial Regex ProjectOptionRegex();
}
