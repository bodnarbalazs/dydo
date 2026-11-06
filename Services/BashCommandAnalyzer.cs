namespace DynaDocs.Services;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Detects file operations in bash, zsh, PowerShell and cmd commands.
/// </summary>
public partial class BashCommandAnalyzer : IBashCommandAnalyzer
{
    // The shell the analysed command runs in; Unknown reads it as either.
    private readonly ShellDialect _shell;

    public BashCommandAnalyzer() : this(ShellDialect.Unknown)
    {
    }

    internal BashCommandAnalyzer(ShellDialect shell) => _shell = shell;

    private static readonly Dictionary<string, FileOperationType> ReadCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        // Unix/Linux/macOS
        ["cat"] = FileOperationType.Read,
        ["head"] = FileOperationType.Read,
        ["tail"] = FileOperationType.Read,
        ["less"] = FileOperationType.Read,
        ["more"] = FileOperationType.Read,
        ["grep"] = FileOperationType.Read,
        ["egrep"] = FileOperationType.Read,
        ["fgrep"] = FileOperationType.Read,
        ["rg"] = FileOperationType.Read,         // ripgrep
        ["ag"] = FileOperationType.Read,         // silver searcher
        ["ack"] = FileOperationType.Read,
        ["strings"] = FileOperationType.Read,
        ["xxd"] = FileOperationType.Read,
        ["hexdump"] = FileOperationType.Read,
        ["od"] = FileOperationType.Read,
        ["file"] = FileOperationType.Read,
        ["wc"] = FileOperationType.Read,
        ["source"] = FileOperationType.Read,
        ["."] = FileOperationType.Read,          // dot command

        // Windows cmd
        ["type"] = FileOperationType.Read,
        ["find"] = FileOperationType.Read,       // Windows find
        ["findstr"] = FileOperationType.Read,

        // PowerShell
        ["get-content"] = FileOperationType.Read,
        ["gc"] = FileOperationType.Read,         // Alias
        ["select-string"] = FileOperationType.Read,
        ["import-csv"] = FileOperationType.Read,
        ["import-clixml"] = FileOperationType.Read,
        ["convertfrom-json"] = FileOperationType.Read,
    };

    private static readonly Dictionary<string, FileOperationType> WriteCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        // Unix/Linux/macOS
        ["tee"] = FileOperationType.Write,
        ["dd"] = FileOperationType.Write,
        ["install"] = FileOperationType.Write,
        ["touch"] = FileOperationType.Write,
        ["truncate"] = FileOperationType.Write,
        ["mkfifo"] = FileOperationType.Write,
        ["mknod"] = FileOperationType.Write,
        ["patch"] = FileOperationType.Write,
        ["split"] = FileOperationType.Write,
        ["csplit"] = FileOperationType.Write,

        // Windows cmd
        ["copy"] = FileOperationType.Copy,
        ["xcopy"] = FileOperationType.Copy,
        ["robocopy"] = FileOperationType.Copy,
        ["move"] = FileOperationType.Move,
        ["ren"] = FileOperationType.Move,
        ["rename"] = FileOperationType.Move,

        // PowerShell
        ["set-content"] = FileOperationType.Write,
        ["out-file"] = FileOperationType.Write,
        ["add-content"] = FileOperationType.Write,
        ["ac"] = FileOperationType.Write,        // Alias
        ["new-item"] = FileOperationType.Write,
        ["ni"] = FileOperationType.Write,        // Alias
        ["copy-item"] = FileOperationType.Copy,
        ["ci"] = FileOperationType.Copy,         // Alias
        ["cpi"] = FileOperationType.Copy,        // Alias
        ["move-item"] = FileOperationType.Move,
        ["mi"] = FileOperationType.Move,         // Alias
        ["export-csv"] = FileOperationType.Write,
        ["export-clixml"] = FileOperationType.Write,
        ["convertto-json"] = FileOperationType.Write,
    };

    private static readonly Dictionary<string, FileOperationType> DeleteCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        // Unix/Linux/macOS
        ["rm"] = FileOperationType.Delete,
        ["rmdir"] = FileOperationType.Delete,
        ["unlink"] = FileOperationType.Delete,
        ["shred"] = FileOperationType.Delete,

        // Windows cmd
        ["del"] = FileOperationType.Delete,
        ["erase"] = FileOperationType.Delete,
        ["rd"] = FileOperationType.Delete,

        // PowerShell
        ["remove-item"] = FileOperationType.Delete,
        ["ri"] = FileOperationType.Delete,       // Alias
        ["clear-content"] = FileOperationType.Write, // Clears but doesn't delete
    };

    private static readonly Dictionary<string, FileOperationType> PermissionCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        // Unix
        ["chmod"] = FileOperationType.PermissionChange,
        ["chown"] = FileOperationType.PermissionChange,
        ["chgrp"] = FileOperationType.PermissionChange,
        ["setfacl"] = FileOperationType.PermissionChange,
        ["chattr"] = FileOperationType.PermissionChange,
        ["lsattr"] = FileOperationType.Read,

        // Windows
        ["icacls"] = FileOperationType.PermissionChange,
        ["cacls"] = FileOperationType.PermissionChange,
        ["takeown"] = FileOperationType.PermissionChange,
        ["attrib"] = FileOperationType.PermissionChange,

        // PowerShell
        ["set-acl"] = FileOperationType.PermissionChange,
        ["get-acl"] = FileOperationType.Read,
    };

    private static readonly Dictionary<string, FileOperationType> CopyMoveCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cp"] = FileOperationType.Copy,
        ["mv"] = FileOperationType.Move,
        ["ln"] = FileOperationType.Write,        // Creating links
        ["rsync"] = FileOperationType.Copy,
        ["scp"] = FileOperationType.Copy,
    };

    // Upstream's anchor for the command-shaped denylist entries: a command name starts at the beginning
    // or after whitespace or a shell separator, so wrappers, launchers, assignments and keywords need no
    // list. A quote opens a command only after a shell -c flag or an ssh remote command. An optional
    // path prefix follows (/usr/bin/dd). Names inside quoted prose after whitespace match too: fail closed.
    private const string CommandContext = @"^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""]";
    private const string CommandPath = @"(?:[^\s;&|'""`()]*/)?";

    // Every command-shaped rule matches with NonBacktracking (DYD-610). As a lookbehind tried at every
    // start, the context and the path scan after it cost each position the rest of a run of { } ! or \,
    // and a body that scans the rest of the command after each command word is quadratic in a repeated
    // word; coverage instrumentation of generated matchers multiplied both past the budget.
    // NonBacktracking matches in linear time, but has no lookarounds, so its patterns consume the
    // context, and a trailing end context, instead; for IsMatch that is equivalent.
    private const string LinearCommandStart = "(?:" + CommandContext + ")" + CommandPath;

    // The generator emits no code for NonBacktracking, only a Regex built when the rule is first used,
    // which would fold case by the culture of that moment: Turkish and Azeri pair I with ı and i with İ,
    // so `ri` would miss RI. Generated matchers fold case invariantly; these rules must too.
    private const RegexOptions LinearIgnoreCase = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

    private const int DangerousPatternTimeoutMs = 250;
    private const string TooComplex = "Command too complex to verify safely";

    // A command word ends at whitespace, the end, a quote or backtick closing a -c string or substitution, or a separator.
    private const string CommandEndContext = @"\s|$|['""`);&|]";
    private const string LinearCommandEnd = "(?:" + CommandEndContext + ")";



    // Heredoc detection - strip $(cat <<'WORD'...WORD) to prevent false positives
    [GeneratedRegex(@"\$\(cat\s+<<-?'?""?(\w+)""?'?\s*\n[\s\S]*?\n\1\s*\)")]
    private static partial Regex CatHeredocRegex();

    [GeneratedRegex(@"\$\(cat\s+<<-?'?""?(\w+)""?'?[^\S\n]*\n")]
    private static partial Regex CatHeredocOpenerRegex();

    [GeneratedRegex(@"\n(\w+)\s*\)")]
    private static partial Regex CatHeredocCloserRegex();

    // An unmatched opener must not search the remaining text again. A matching delimiter after
    // the opener's first newline guarantees the original regex succeeds at that opener; keeping
    // that regex preserves its greedy header, lazy body and exact replacement extent.
    private static string StripCatHeredocs(string text)
    {
        Dictionary<string, int> closing = new(StringComparer.Ordinal);
        foreach (Match closer in CatHeredocCloserRegex().Matches(text))
            closing[closer.Groups[1].Value] = closer.Index;
        if (closing.Count == 0)
            return text;

        var result = new StringBuilder(text.Length);
        var start = 0;
        for (var opener = CatHeredocOpenerRegex().Match(text); opener.Success;
            opener = CatHeredocOpenerRegex().Match(text, Math.Max(start, opener.Index + opener.Length)))
        {
            if (closing.GetValueOrDefault(opener.Groups[1].Value, -1) < opener.Index + opener.Length)
                continue;
            var match = CatHeredocRegex().Match(text, opener.Index);
            result.Append(text, start, match.Index - start).Append("HEREDOC_STRIPPED");
            start = match.Index + match.Length;
        }
        return result.Append(text, start, text.Length - start).ToString();
    }

    // Any heredoc (<<WORD, <<'WORD', <<"WORD", <<-WORD; a word of letters, digits, _ and -, such as
    // END-OF-FILE, ending where the shell's word ends): group 1 is the opener line, then the body and
    // the delimiter line with its line break. Group 3 is the delimiter's quote: a quoted delimiter
    // makes the body literal. With <<- the delimiter may be tab-indented. A delimiter line with
    // trailing blanks, which the shell does not take, ends the match early, never late.
    [GeneratedRegex(@"(?<!<)(<<(-)?[ \t]*(['""]?)([\w-]+)\3(?=[\s;&|<>()])[^\n]*\n)[\s\S]*?^(?(2)\t*)\4[ \t]*\r?$\n?", RegexOptions.Multiline)]
    private static partial Regex HeredocBodyRegex();

    // A PowerShell here-string: @' or @" ends its line, '@ or "@ starts the closing line. @' is literal.
    [GeneratedRegex(@"@(['""])[ \t]*\r?\n[\s\S]*?^\1@", RegexOptions.Multiline)]
    private static partial Regex HereStringRegex();

    // Where HeredocBodyRegex and HereStringRegex can start, up to the body: group 1 the dash, 2 the
    // quote and 3 the delimiter of a heredoc; group 1 the quote of a here-string.
    [GeneratedRegex(@"(?<!<)<<(-)?[ \t]*(['""]?)([\w-]+)\2(?=[\s;&|<>()])")]
    private static partial Regex HeredocOpenerRegex();

    [GeneratedRegex(@"@(['""])[ \t]*\r?\n")]
    private static partial Regex HereStringOpenerRegex();

    // The delimiter HeredocBodyRegex reads after << when it takes a dash after it as the delimiter's own.
    [GeneratedRegex(@"\G[\w-]+(?=[\s;&|<>()])")]
    private static partial Regex DashDelimiterRegex();

    // The positions, in order, where HeredocBodyRegex and HereStringRegex match: each opener a later
    // line closes; and each heredoc the command's own reading read, from its << to after its last line.
    private sealed record ClosedBodyOpeners(List<int> Heredocs, List<int> HereStrings, HashSet<(int Opener, int End)> ReadHeredocs);

    /// <summary>
    /// Where a heredoc or here-string of the text can match: each opener a line after it closes.
    /// Searching from an opener that nothing closes reads to the end of the text, once per such
    /// opener, quadratic in quoted text full of them (DYD-723); the body regexes run only from these.
    /// </summary>
    private static ClosedBodyOpeners ClosedOpeners(string text, ShellReader.Reading reading)
    {
        var closing = ClosingLines(text);
        List<int> heredocs = [];
        var lineEnd = -1;
        for (var opener = HeredocOpenerRegex().Match(text); opener.Success; opener = opener.NextMatch())
        {
            var body = NextAt(text, '\n', opener.Index + opener.Length, ref lineEnd) + 1;
            var dash = opener.Groups[1].Success;
            if ((dash ? closing.Tabbed : closing.Untabbed).GetValueOrDefault(opener.Groups[3].Value, -1) >= body
                || (dash && DashDelimiterRegex().Match(text, opener.Index + 2) is { Success: true } own
                    && closing.Untabbed.GetValueOrDefault(own.Value, -1) >= body))
                heredocs.Add(opener.Index);
        }

        List<int> hereStrings = [];
        for (var opener = HereStringOpenerRegex().Match(text); opener.Success; opener = opener.NextMatch())
        {
            if ((opener.Groups[1].Value == "'" ? closing.Single : closing.Double) >= opener.Index + opener.Length)
                hereStrings.Add(opener.Index);
        }
        return new ClosedBodyOpeners(heredocs, hereStrings, [.. reading.Commands.SelectMany(command => command.Bodies)
            .Where(body => body.Kind == ShellReader.BodyKind.Heredoc).Select(body => (body.Whole.Start.Value, body.Whole.End.Value))]);
    }

    // Where each line of the text could close a body, the last line that does: a heredoc's delimiter
    // is the line less one trailing CR and its trailing blanks, less its leading tabs too after <<-;
    // a here-string closes at a line that starts with its quote and @.
    private static (Dictionary<string, int> Untabbed, Dictionary<string, int> Tabbed, int Single, int Double) ClosingLines(string text)
    {
        Dictionary<string, int> untabbed = new(StringComparer.Ordinal);
        Dictionary<string, int> tabbed = new(StringComparer.Ordinal);
        var (single, @double) = (-1, -1);
        for (var at = 0; at <= text.Length;)
        {
            var end = text.IndexOf('\n', at) is var newline and >= 0 ? newline : text.Length;
            var line = text.AsSpan(at, end - at);
            single = line.StartsWith("'@", StringComparison.Ordinal) ? at : single;
            @double = line.StartsWith("\"@", StringComparison.Ordinal) ? at : @double;
            line = (line is [.., '\r'] ? line[..^1] : line).TrimEnd(" \t");
            untabbed[line.ToString()] = at;
            tabbed[line.TrimStart('\t').ToString()] = at;
            at = end + 1;
        }
        return (untabbed, tabbed, single, @double);
    }

    // The body regex's first match from start, a line start: where a search from start finds it.
    private static Match FirstBody(Regex body, string text, List<int> openers, int start)
    {
        var at = openers.BinarySearch(start);
        at = at < 0 ? ~at : at;
        return at < openers.Count ? body.Match(text, openers[at]) : Match.Empty;
    }

    [GeneratedRegex(@"(?<!<)<<(?!<)")]
    private static partial Regex HeredocOperatorRegex();

    /// <summary>
    /// The commands a heredoc or here-string body may feed and stay literal data (DYD-406): each
    /// prints, filters or writes the text it is given and runs none of it. Any other program, under
    /// any path or quoting, runs or may run its stdin, so a body fed to it stays code, as before.
    /// </summary>
    private static readonly HashSet<string> DataCommands = new(StringComparer.OrdinalIgnoreCase)
        { "cat", "tee", "grep", "echo", "write-output", "set-content", "add-content", "out-file" };

    // Words that open or close a compound command, whose output a later line may pipe on.
    private static readonly HashSet<string> CompoundWords = new(StringComparer.OrdinalIgnoreCase)
        { "if", "then", "elif", "else", "fi", "for", "select", "while", "until", "do", "done", "case", "esac", "function", "coproc" };

    // Written into a body's command line where the body is, after ; && ||, and before ( ) { }; no
    // command line holds them.
    private const string BodyMark = "\u0004";
    private const string SequenceMark = "\u0005";
    private const string GroupMark = "\u0006";

    [GeneratedRegex(@";|&&|\|\|")]
    private static partial Regex SequenceRegex();

    [GeneratedRegex(@"[(){}]")]
    private static partial Regex GroupRegex();

    // What bash could read, outside quotes, as more than word text: a line break, a separator, a
    // group, a substitution, a redirection, a quote or a comment.
    [GeneratedRegex(@"[\r\n;&|()<>`$\\#""{}]")]
    private static partial Regex BashStructureRegex();

    /// <summary>One piece of a command's code, and the same piece with its expanding body kept.</summary>
    private sealed record CodePiece(string Code, string Expanding);

    // A literal-data body cut from text: the piece before it, and where the next piece starts.
    private sealed record DataBodyCut(CodePiece Piece, int Resume);

    /// <summary>
    /// The command cut at its literal-data bodies into the pieces a shell runs as code, each read on
    /// its own (DYD-406). A piece ends at a data body's opener line (a here-string leaves '' in its
    /// place and keeps the rest of its closing line); the next starts on the line after the
    /// terminator, in the original text, so no quote or comment of one piece reaches into another.
    /// A body is data only as <see cref="DataBody"/> says; the first that is not stays in its piece
    /// as code, and so does everything after it.
    /// </summary>
    private List<CodePiece> CodePieces(string command)
    {
        List<CodePiece> pieces = [];
        var start = 0;
        // Text with no body opener holds no body to cut, so it is not read (DYD-723). Keep the
        // original code when a child's extent is unsupported; cutting its enclosing data body would
        // discard executable text beyond an apparent substitution terminator.
        if (HasBodyOpener(command) && ShellReader.Read(command, _shell != ShellDialect.PowerShell) is { Failed: false } reading)
        {
            var openers = ClosedOpeners(command, reading);
            while (DataBody(command, start, openers) is { } cut)
            {
                pieces.Add(cut.Piece);
                start = cut.Resume;
            }
        }

        var rest = command[start..];
        pieces.Add(new CodePiece(rest, rest));
        return pieces;
    }

    private static bool HasBodyOpener(string text) => text.Contains("<<", StringComparison.Ordinal)
        || text.Contains("@'", StringComparison.Ordinal) || text.Contains("@\"", StringComparison.Ordinal);

    /// <summary>
    /// The first heredoc or here-string of <paramref name="text"/> from <paramref name="start"/>, a
    /// line start, cut out when it is literal data: its line is the first line of code, every
    /// command it feeds is a data command (<see cref="FeedsDataOnly"/>), and the text up to the line
    /// after it reads, so the piece after it starts as a shell left it. Otherwise null, failing closed.
    /// Each search and reading stays within the cut, so cutting every body costs the text's length
    /// once, not once per body (DYD-723). A heredoc the whole command's reading read to the same line
    /// reads there as it does alone: its line reads (<see cref="FeedsDataOnly"/>), only blank and
    /// comment lines come before it, and its body is the one that reading read, so it is not read again.
    /// </summary>
    private DataBodyCut? DataBody(string text, int start, ClosedBodyOpeners openers)
    {
        var heredoc = FirstBody(HeredocBodyRegex(), text, openers.Heredocs, start);
        var hereString = FirstBody(HereStringRegex(), text, openers.HereStrings, start);
        DataBodyCut? cut = null;
        var read = false;
        if (hereString.Success && (!heredoc.Success || hereString.Index < heredoc.Index))
            cut = HereStringData(text, start, hereString);
        else if (heredoc.Success)
        {
            cut = HeredocData(text, start, heredoc);
            read = openers.ReadHeredocs.Contains((heredoc.Index, heredoc.Index + heredoc.Length));
        }
        return cut != null && (read || !ShellReader.Read(text[start..cut.Resume], _shell != ShellDialect.PowerShell).Failed) ? cut : null;
    }

    // A heredoc is bash's: bash reads its opener line, and PowerShell too when it may run the command.
    private DataBodyCut? HeredocData(string text, int start, Match heredoc)
    {
        var lineStart = LineStart(text, heredoc.Index);
        var openerEnd = heredoc.Index + heredoc.Groups[1].Value.TrimEnd().Length;
        var end = heredoc.Index + heredoc.Length;
        var line = text[lineStart..heredoc.Index] + $" {BodyMark} " + text[heredoc.Index..openerEnd];
        bool[] readings = _shell == ShellDialect.Posix ? [true] : [true, false];
        if (!StartsItsPiece(text, start, lineStart) || HeredocOperatorRegex().Count(line) != 1
            || !FeedsDataOnly(line, readings, hereString: false, text, end))
            return null;

        var code = text[start..openerEnd];
        return new DataBodyCut(new CodePiece(code, heredoc.Groups[3].Length == 0 ? text[start..end] : code), end);
    }

    // A here-string is PowerShell's: PowerShell reads its line, and bash too when it may run the
    // command, which then must read the here-string as words of that line (BashReadsAsWords).
    private DataBodyCut? HereStringData(string text, int start, Match hereString)
    {
        var lineStart = LineStart(text, hereString.Index);
        var end = hereString.Index + hereString.Length;
        // A closing marker attached to more word text is not a literal-data boundary.
        if (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] is not (';' or '|' or '&' or '<' or '>'))
            return null;
        var lineEnd = text.IndexOf('\n', end) is var at and >= 0 ? at : text.Length;
        var resume = Math.Min(lineEnd + 1, text.Length);
        var line = text[lineStart..hereString.Index] + $" {BodyMark} " + text[end..lineEnd];
        var powerShell = _shell == ShellDialect.PowerShell;
        bool[] readings = powerShell ? [false] : [false, true];
        if (PwshEndsSooner(hereString) || !StartsItsPiece(text, start, lineStart) || HeredocOperatorRegex().IsMatch(line)
            || (!powerShell && !BashReadsAsWords(hereString.Value))
            || !FeedsDataOnly(line, readings, hereString: true, text, resume))
            return null;

        // A CRLF is one line break, so the piece ends before both halves: a carriage return left in it
        // would read as a bare one when the pieces are joined (DYD-641).
        var pieceEnd = lineEnd < text.Length && text[lineEnd - 1] == '\r' ? lineEnd - 1 : lineEnd;
        var code = text[start..hereString.Index] + "''" + text[end..pieceEnd];
        return new DataBodyCut(new CodePiece(code, hereString.Groups[1].Value == "\"" ? text[start..pieceEnd] : code), resume);
    }

    // pwsh 7.6.9 ends a here-string at the first line break, a bare carriage return among them, that a
    // quote of the opener's family and @ follow (DYD-641); HereStringRegex takes a line feed and the
    // opener's own quote only, so a body pwsh ends sooner holds code it runs.
    private static bool PwshEndsSooner(Match hereString) =>
        (hereString.Groups[1].Value == "'" ? PwshSingleTerminatorRegex() : PwshDoubleTerminatorRegex())
            .IsMatch(hereString.ValueSpan[..^2]);

    [GeneratedRegex(@"[\r\n]['\u2018\u2019\u201A\u201B]@")]
    private static partial Regex PwshSingleTerminatorRegex();

    [GeneratedRegex(@"[\r\n][""\u201C\u201D\u201E]@")]
    private static partial Regex PwshDoubleTerminatorRegex();

    // Only blank and comment lines come before the body's line, so no group, quote or pipe is open there.
    private static bool StartsItsPiece(string text, int start, int lineStart) => !CodeLines(text, start, lineStart).Any();

    // The lines of text from start to end that hold code, in order: neither blank nor a comment. A
    // bare carriage return ends a line, as pwsh 7.6.9 ends a comment at one (DYD-641); to bash, which
    // reads it as word text, the split can only add code, and a line a pipe starts goes on only in
    // pwsh. Each line is cut only when it is asked for.
    private static IEnumerable<string> CodeLines(string text, int start, int end)
    {
        while (start < end)
        {
            var lineEnd = text.AsSpan(start, end - start).IndexOfAny('\n', '\r') is var at and >= 0 ? start + at : end;
            if (text[start..lineEnd].Trim() is [not '#', ..] line)
                yield return line;
            start = lineEnd + 1;
        }
    }

    /// <summary>
    /// Whether a body, at the mark in its command <paramref name="line"/>, feeds only data commands
    /// in every reading of that line: the line reads for certain as simple commands joined by
    /// <c>;</c>, <c>&amp;&amp;</c>, <c>||</c> and pipes, with no group, substitution or compound
    /// command; the command reading the body is a data command named as such (a here-string may
    /// also stand alone), and so is each command its output is piped on to. The line does not go
    /// on past its break: no trailing \ or `, and no next line of code, in text from after, starting with a pipe.
    /// </summary>
    private static bool FeedsDataOnly(string line, bool[] readings, bool hereString, string text, int after)
    {
        if (line.TrimEnd() is [.., '\\' or '`'] || CodeLines(text, after, text.Length).FirstOrDefault() is ['|', ..])
            return false;

        var marked = PipeRegex().Replace(
            SequenceRegex().Replace(GroupRegex().Replace(line, $" {GroupMark} $0"), $"$0{SequenceMark} "),
            pipe => pipe.Value + PipeMark + " ");
        return readings.All(posix => ReadsAsDataOnly(ShellReader.Read(marked, posix), hereString));
    }

    private static bool ReadsAsDataOnly(ShellReader.Reading reading, bool hereString)
    {
        var commands = reading.Commands.Select(command => command.Words).ToList();
        var body = commands.FindIndex(words => words.Contains(BodyMark));
        return !reading.Failed && reading.Substitutions.Count == 0 && body >= 0
            && commands.All(IsSimple) && commands.Skip(1).All(words => words is [PipeMark or SequenceMark, ..])
            && commands.Count(words => words.Any(word => word.Contains(BodyMark, StringComparison.Ordinal))) == 1
            && ReadsBody(Unmarked(commands[body]), hereString)
            && commands.Skip(body + 1).TakeWhile(words => words is not [SequenceMark, ..])
                .All(words => Unmarked(words) is [var program, ..] && DataCommands.Contains(program));
    }

    // A command with no group mark, not part of a compound command.
    private static bool IsSimple(List<string> words) =>
        !words.Contains(GroupMark) && !(Unmarked(words) is [var first, ..] && CompoundWords.Contains(first));

    // The command reading the body: a data command, or for a here-string the here-string alone.
    private static bool ReadsBody(List<string> words, bool hereString) =>
        (hereString && words is [BodyMark]) || (words is [var program, ..] && program != BodyMark && DataCommands.Contains(program));

    private static List<string> Unmarked(List<string> words) =>
        words is [PipeMark or SequenceMark, .. var rest] ? rest : words;

    /// <summary>
    /// Whether bash reads a here-string as words of the command it stands in: @'...'@ is @ and
    /// single-quoted text, and each stretch an even number of ' in the body leaves outside quotes
    /// holds only word text (<see cref="BashStructureRegex"/>); @"..."@ holds no " or \.
    /// </summary>
    private static bool BashReadsAsWords(string hereString)
    {
        var inner = hereString[2..^2];
        if (hereString[1] == '"')
            return !inner.Contains('"') && !inner.Contains('\\');
        var stretches = inner.Split('\'');
        return stretches.Length % 2 == 1 && stretches.Where((_, i) => i % 2 == 1).All(stretch => !BashStructureRegex().IsMatch(stretch));
    }

    // A body's line starts after a line feed only: each reading of the line takes a bare carriage
    // return as its shell does, and bash's must see a comment before one hide the opener (DYD-641).
    private static int LineStart(string text, int index) => index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;

    // A double-quoted string (backslash escapes skipped, lines spanned) or a single-quoted one, the
    // first that starts.
    [GeneratedRegex(@"""(?:[^""\\]|\\.)*""|(?<single>'[^']*')")]
    private static partial Regex QuotedTextRegex();

    // A piece's layout: each double-quoted string emptied, so a line break left in the piece separates
    // commands. One spanning lines, a bare carriage return among the breaks (DYD-641), is emptied only
    // where each reading of the piece holds it in a word: an apostrophe in a comment quotes nothing, so
    // it hides no line break after it. Single-quoted text is kept, so a line break in it counts
    // (DYD-421); literal data is emptied before (DYD-422).
    private static string Layout(string code)
    {
        var words = new Lazy<List<WordSubstrings>?>(() =>
        {
            var read = CommandLines(code, ShellDialect.Unknown);
            return read.Uncertain ? null : read.Lines.Where(line => line.Text == code)
                .Select(line => new WordSubstrings(line.Commands.SelectMany(command => command.Words))).ToList();
        });
        return QuotedTextRegex().Replace(code, quoted =>
            quoted.Groups["single"].Success || (quoted.Value.AsSpan().IndexOfAny('\n', '\r') >= 0 && !IsQuotedWord(quoted, words.Value))
                ? quoted.Value
                : "\"\"");
    }

    // bash's escapes inside double quotes: \$, \`, \" and \\.
    [GeneratedRegex(@"\\([$`""\\])")]
    private static partial Regex DoubleQuotedEscapeRegex();

    private static bool IsQuotedWord(Match quoted, List<WordSubstrings>? words)
    {
        var raw = quoted.Value[1..^1];
        var unescaped = DoubleQuotedEscapeRegex().Replace(raw, "$1");
        return words != null && words.All(index => index.Contains(raw) || index.Contains(unescaped));
    }

    // A suffix automaton indexes all word substrings in linear space and time. -1 separates words
    // outside the UTF-16 alphabet, so a query cannot accidentally span two words. Layout used to
    // search every word again for every multiline quote, quadratic even on ordinary echo commands.
    private sealed class WordSubstrings
    {
        private sealed class State(int length)
        {
            public int Length { get; } = length;
            public int Link { get; set; } = -1;
            public Dictionary<int, int> Next { get; init; } = [];
        }

        private readonly List<State> _states = [new(0)];
        private int _last;

        public WordSubstrings(IEnumerable<string> words)
        {
            // A group word already contains its children's words. Index longer words first so
            // nested readings do not store the same text once per enclosing group again.
            foreach (var word in words.Distinct(StringComparer.Ordinal).OrderByDescending(word => word.Length))
            {
                if (Contains(word))
                    continue;
                foreach (var character in word)
                    Append(character);
                Append(-1);
            }
        }

        public bool Contains(string text)
        {
            var state = 0;
            foreach (var character in text)
                if (!_states[state].Next.TryGetValue(character, out state))
                    return false;
            return true;
        }

        private void Append(int character)
        {
            var current = _states.Count;
            _states.Add(new State(_states[_last].Length + 1));
            var previous = _last;
            while (previous >= 0 && _states[previous].Next.TryAdd(character, current))
                previous = _states[previous].Link;
            _states[current].Link = previous < 0 ? 0 : Link(previous, character);
            _last = current;
        }

        private int Link(int previous, int character)
        {
            var next = _states[previous].Next[character];
            if (_states[previous].Length + 1 == _states[next].Length)
                return next;
            var clone = _states.Count;
            _states.Add(new State(_states[previous].Length + 1)
            {
                Link = _states[next].Link,
                Next = new Dictionary<int, int>(_states[next].Next),
            });
            while (previous >= 0 && _states[previous].Next.GetValueOrDefault(character, -1) == next)
            {
                _states[previous].Next[character] = clone;
                previous = _states[previous].Link;
            }
            _states[next].Link = clone;
            return clone;
        }
    }

    // sed: only read and in-place-substitute flags. -i and --in-place must be bare, with no backup
    // suffix, which renames the file elsewhere.
    private const string SedShortFlags = "nErszu";
    private static readonly HashSet<string> SedLongOptions = new(StringComparer.Ordinal)
        { "posix", "regexp-extended", "separate", "null-data", "unbuffered", "quiet", "silent", "sandbox" };

    // A printf %n conversion, which assigns the variable its argument names.
    [GeneratedRegex(@"%[^A-Za-z%]*n")]
    private static partial Regex PrintfAssignmentRegex();

    // A lone & outside quotes: bash's background job, or PowerShell's call operator.
    [GeneratedRegex(@"(?<![&>])&(?![&>])")]
    private static partial Regex LoneAmpersandRegex();

    // Typographic quotes, which open and close PowerShell strings as ' and " do.
    private const string TypographicQuotes = "\u2018\u2019\u201A\u201B\u201C\u201D\u201E";

    // One sed s command: a punctuation delimiter, two parts it ends (a backslash escapes the next
    // character), then only the g, p, i, I, m, M or count flags; e would run the result, w write a file.
    [GeneratedRegex(@"^s(?<d>[^\w\s\\])(?:(?!\k<d>)(?:\\.|[^\\]))*\k<d>(?:(?!\k<d>)(?:\\.|[^\\]))*\k<d>[gpiImM0-9]*\z", RegexOptions.Singleline)]
    private static partial Regex SedSubstitutionRegex();

    // bash's echo reads leading -n, -e and -E clusters as flags and stops at the first other word.
    [GeneratedRegex(@"^-[neE]+$")]
    private static partial Regex EchoFlagsRegex();

    // A single-quoted argument one reading certifies as literal data: its quotes and the word holding
    // them, where that reading places them.
    private readonly record struct DataQuote(Range Quote, Range Word);

    /// <summary>
    /// The code with each single-quoted argument that is literal data emptied to '' (DYD-422): bash's
    /// reading and PowerShell's both certify it in the same word (<see cref="DataQuotes"/>), whatever
    /// shell the call declares, since a declared shell can still hand the text to the other (a Codex
    /// pwsh envelope from a bash caller). Text only PowerShell pairs into one string, such as
    /// don\'t ... won\'t, stays searched as on master. Anything else is kept and searched, failing closed.
    /// </summary>
    private static string WithoutLiteralData(string code)
    {
        var data = DataQuotes(code, posix: true);
        data.IntersectWith(DataQuotes(code, posix: false));
        return Emptied(code, data.Select(quote => quote.Quote));
    }

    // The text with each quote replaced by '', in one pass: the quotes one reading holds never overlap.
    private static string Emptied(string text, IEnumerable<Range> quotes)
    {
        var emptied = new StringBuilder(text.Length);
        var at = 0;
        foreach (var quote in quotes.OrderBy(quote => quote.Start.Value))
        {
            emptied.Append(text, at, quote.Start.Value - at).Append("''");
            at = quote.End.Value;
        }
        return emptied.Append(text, at, text.Length - at).ToString();
    }

    /// <summary>
    /// The single-quoted arguments one reading of a line holds as literal data: the whole line reads,
    /// for certain, as one data command (<see cref="IsOneDataCommand"/>) and has no text a shell may
    /// read otherwise (<see cref="MayReadOtherwise"/>). A substitution outside single quotes is never
    /// emptied, so it is still found.
    /// </summary>
    private static HashSet<DataQuote> DataQuotes(string code, bool posix)
    {
        // Text with no single quote of either reading holds none to empty, so it is not read (DYD-723).
        if (code.AsSpan().IndexOfAny("'\u2018\u2019\u201A\u201B") < 0)
            return [];

        var reading = ShellReader.Read(code, posix);
        var quotes = reading.Source.SingleQuotes;
        return reading.Commands is [var command] && IsOneDataCommand(reading, command)
            && !MayReadOtherwise(code, Emptied(code, quotes))
            ? [.. DataQuotesIn(command, quotes)]
            : [];
    }

    /// <summary>
    /// Whether the reading is certain and its one command, with no substitution, PowerShell group
    /// word, group, continuation, pipe, background, heredoc or here-string, is sed that only
    /// substitutes (<see cref="SedScriptIndex"/>), printf with no option and no %n conversion (-v
    /// assigns a variable, and bash evaluates its subscript), or echo with only bash's leading -n,
    /// -e and -E flags, or with no parameter word in PowerShell, where -OutVariable and the other
    /// common parameters assign variables. Named exactly, with no path, case change or assignment word.
    /// </summary>
    private static bool IsOneDataCommand(ShellReader.Reading reading, ShellReader.Command command) =>
        !reading.Failed && reading.Source.Executions.Count == 0 && !command.UncertainFeed && !command.PipesOutput
        && command.Stdin.Count == 0 && command.Bodies.Count == 0 && command.Words switch
        {
            ["sed", ..] => SedScriptIndex(command.Words) > 0,
            ["printf", .. var rest] => rest is not [['-', ..], ..] && !rest.Any(PrintfAssignmentRegex().IsMatch),
            ["echo", .. var rest] => reading.Source.Posix ? rest.TakeWhile(IsOption).All(EchoFlagsRegex().IsMatch) : !rest.Any(IsPowerShellParameter),
            _ => false,
        };

    private static bool IsOption(string word) => word is ['-', _, ..];

    // A PowerShell parameter word starts with a hyphen, an en dash, an em dash or a horizontal bar.
    private static bool IsPowerShellParameter(string word) => word is ['-' or '\u2013' or '\u2014' or '\u2015', ..];

    // Text outside single quotes a shell may read otherwise, in either reading: bash's $'...', a lone
    // &, a caret (an escape only to cmd), PowerShell's stop-parsing token --%, a typographic quote
    // anywhere, a character that expands a word into others (a glob * ? [, a brace {, a tilde ~,
    // PowerShell's @ splat), so an option, operand or target the guard reads may not be the one that
    // runs, or a break character (<see cref="IsBreakCharacter"/>).
    private static bool MayReadOtherwise(string code, string outside) =>
        outside.Contains("$'", StringComparison.Ordinal) || LoneAmpersandRegex().IsMatch(outside)
        || outside.AsSpan().IndexOfAny("^*?[{~@") >= 0 || outside.Contains("--%", StringComparison.Ordinal)
        || code.AsSpan().IndexOfAny(TypographicQuotes) >= 0 || outside.Any(IsBreakCharacter);

    // A character a shell may read as a line or statement break, or the readers may not model: a
    // carriage return (pwsh ends a statement at a bare one, where the readers see a space), any
    // other control character but tab and line feed (C0, DEL, C1 and NEL among them), and every
    // Unicode line or paragraph separator. Inside single quotes both shells keep them literal.
    private static bool IsBreakCharacter(char c) => c is not ('\t' or '\n')
        && (char.IsControl(c) || char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.LineSeparator
            or System.Globalization.UnicodeCategory.ParagraphSeparator);

    // The quotes in words the command takes as data, each with its word: any word of printf or echo,
    // and only the script of sed, whose other words are files it rewrites. Quotes and words come in
    // reading order, so one walk pairs them (DYD-723); a quote out of order is left unpaired and kept.
    private static IEnumerable<DataQuote> DataQuotesIn(ShellReader.Command command, IEnumerable<Range> quotes)
    {
        var script = command.Words[0] == "sed" ? SedScriptIndex(command.Words) : -1;
        var word = 0;
        foreach (var quote in quotes)
        {
            while (word < command.WordRanges.Count && command.WordRanges[word].End.Value < quote.End.Value)
                word++;
            if (word < command.WordRanges.Count && command.WordRanges[word].Start.Value <= quote.Start.Value
                && (script < 0 || script == word))
                yield return new DataQuote(quote, command.WordRanges[word]);
        }
    }

    /// <summary>
    /// The index of a sed's one script when the sed only substitutes, or -1: every option, before
    /// and after the script, is on sed's closed list (<see cref="IsSedOption"/>), so none names a
    /// script, a script file or a backup suffix, and the first word after the options is one s
    /// command (<see cref="SedSubstitutionRegex"/>), so no e, w, r, R or W command or flag runs or
    /// writes a file.
    /// </summary>
    private static int SedScriptIndex(List<string> words)
    {
        var script = words.FindIndex(1, word => !IsOption(word) || word == "--");
        if (script > 0 && words[script] == "--")
            script++;
        return script > 0 && script < words.Count
            && words.Skip(1).Where(word => IsOption(word) && word != "--").All(IsSedOption)
            && SedSubstitutionRegex().IsMatch(words[script])
            ? script
            : -1;
    }

    // A cluster of the listed flags, which may end in a bare i, or an exact listed long option or a
    // bare --in-place. sed reads anything after an i as its backup suffix, so -i.bak, -is and -ifoo
    // are not; nor are --in-place=, -e, -f, -l, an abbreviation or any other option.
    private static bool IsSedOption(string option) => option.StartsWith("--", StringComparison.Ordinal)
        ? option == "--in-place" || SedLongOptions.Contains(option[2..])
        : SedFlags(option).All(SedShortFlags.Contains);

    // A short cluster's letters before a trailing bare i.
    private static string SedFlags(string option) => option.EndsWith('i') ? option[1..^1] : option[1..];

    // Bypass detection. The substitution and expansion searches are scans, not regexes: a regex search
    // restarts at each unclosed $( or ${ and looks for its closer to the end, quadratic (DYD-723).
    // Each scan finds the matches the regexes \$\([^)]+\)|`[^`]+` and \$[A-Za-z_]\w*|\$\{[^}]+\} find.

    /// <summary>
    /// The bodies of the <c>$( ... )</c> and backtick substitutions in the text, left to right: each
    /// opener to the first closer after it, with something between them. Each closer is looked for
    /// once, however many openers precede it.
    /// </summary>
    internal static IEnumerable<string> SubstitutionBodies(string text)
    {
        var (paren, tick) = (-1, -1);
        for (var i = 0; i + 2 < text.Length; i++)
        {
            int body;
            int end;
            if (text[i] == '`')
                (body, end) = (i + 1, NextAt(text, '`', i + 1, ref tick));
            else if (text[i] == '$' && text[i + 1] == '(')
                (body, end) = (i + 2, NextAt(text, ')', i + 2, ref paren));
            else
                continue;
            if (end == text.Length || end == body)
                continue;
            yield return text[body..end];
            i = end;
        }
    }

    /// <summary>Whether the text expands a variable: a <c>$</c> before a name, or a <c>${ ... }</c> with something inside.</summary>
    internal static bool ExpandsVariable(string text)
    {
        var brace = -1;
        for (var i = 0; i + 1 < text.Length; i++)
        {
            if (text[i] != '$')
                continue;
            if (char.IsAsciiLetter(text[i + 1]) || text[i + 1] == '_')
                return true;
            if (text[i + 1] == '{' && NextAt(text, '}', i + 2, ref brace) is var end && end < text.Length && end > i + 2)
                return true;
        }
        return false;
    }

    // The first c at or after from, or the text's length when there is none. found holds the answer
    // to an earlier search from no later than from; it still holds when it lies at or after from.
    private static int NextAt(string text, char c, int from, ref int found)
    {
        if (found < from)
            found = text.IndexOf(c, from) is var at and >= 0 ? at : text.Length;
        return found;
    }

    [GeneratedRegex(@"base64\s+(-d|--decode)", RegexOptions.IgnoreCase)]
    private static partial Regex Base64DecodeRegex();

    [GeneratedRegex(@"xxd\s+-r|od\s+-A")]
    private static partial Regex HexDecodeRegex();

    private static readonly Regex RecursiveDeleteRootRegex = new(@"rm\s+(-[a-zA-Z]*[rfRF][a-zA-Z]*\s+)+(\./|/|~|/\*)(\s+--[a-z-]+)*\s*($|;|&&|\|\||&|\|)", RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(DangerousPatternTimeoutMs));

    private static readonly Regex RecursiveDeleteGlobRegex = new(@"rm\s+(-[a-zA-Z]*[rfRF][a-zA-Z]*\s+)+\*\s*($|;|&&|\|\||&|\|)", RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(DangerousPatternTimeoutMs));

    // The two lookaheads require Recurse and Force anywhere after Remove-Item on its line.
    // Their witnesses and the target can occur in any of six orders. Only a final target may
    // cross a newline: both flags must precede the first newline, as in the original lookaheads.
    private const string RecurseWitness = @"-Recurse\b";
    private const string ForceWitness = @"-Force\b";
    private const string DeleteTarget = @"(?:\s[/~]|\s[A-Z]:\\)\s*(?:$|;|&&|\|\|)";
    private const string SameLineDeleteTarget = @"(?:[^\S\n][/~]|[^\S\n][A-Z]:\\)[^\S\n]*(?:$|;|&&|\|\|)";

    private static readonly Regex PowerShellRecursiveDeleteRegex = new(@"Remove-Item\b.*(?:"
        + RecurseWitness + ".*" + ForceWitness + ".*" + DeleteTarget + "|"
        + ForceWitness + ".*" + RecurseWitness + ".*" + DeleteTarget + "|"
        + RecurseWitness + ".*" + SameLineDeleteTarget + ".*" + ForceWitness + "|"
        + ForceWitness + ".*" + SameLineDeleteTarget + ".*" + RecurseWitness + "|"
        + SameLineDeleteTarget + ".*(?:" + RecurseWitness + ".*" + ForceWitness + "|"
        + ForceWitness + ".*" + RecurseWitness + "))", LinearIgnoreCase, TimeSpan.FromMilliseconds(DangerousPatternTimeoutMs));

    [GeneratedRegex(@":\s*\(\s*\)\s*\{\s*:\s*\|\s*:\s*&\s*\}\s*;\s*:", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex ForkBombClassicRegex();

    [GeneratedRegex(@"\.\s*/\s*\.:", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex ForkBombAltRegex();

    [GeneratedRegex(@">\s*/dev/(?:r?disk\d*|sd[a-z]|nvme\d|vd[a-z]|mmcblk\d)", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex DirectDiskWriteRegex();

    // The device name is (?!null(?:\s|$|['";&|)`]))[^\s'";&|]+ spelled out without the lookahead: one name
    // character other than n, or n, nu, nul or null going on to anything but null's ending (DYD-610).
    [GeneratedRegex(LinearCommandStart + @"dd\s+[^;&|\r\n]*\bof\s*=\s*['""]?/dev/(?:[^\s'"";&|n]|n(?:$|[^u]|u(?:$|[^l]|l(?:$|[^l]|l[^\s'"";&|)`]))))", LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex DdDiskWriteRegex();

    // Kept unanchored on purpose: known disk targets block anywhere in the command text.
    private static readonly Regex DdKnownDiskWriteRegex = new(@"dd\s+.*of\s*=\s*/dev/(?:sd[a-z]|nvme\d|vd[a-z]|mmcblk\d)", LinearIgnoreCase, TimeSpan.FromMilliseconds(DangerousPatternTimeoutMs));

    private static readonly Regex Base64DecodePipeExecRegex = new(@"base64\s+(-d|--decode)[^|]*\|\s*(python[23]?|bash|sh|zsh|perl|ruby|node|pwsh|powershell)", LinearIgnoreCase, TimeSpan.FromMilliseconds(DangerousPatternTimeoutMs));

    private static readonly Regex CurlPipeShRegex = new(@"curl\s+[^|]*\|\s*(ba)?sh", LinearIgnoreCase, TimeSpan.FromMilliseconds(DangerousPatternTimeoutMs));

    private static readonly Regex WgetPipeShRegex = new(@"wget\s+[^|]*\|\s*(ba)?sh", LinearIgnoreCase, TimeSpan.FromMilliseconds(DangerousPatternTimeoutMs));

    private static readonly Regex WgetOutputPipeShRegex = new(@"wget\s+-O\s*-?\s+[^|]*\|\s*(ba)?sh", LinearIgnoreCase, TimeSpan.FromMilliseconds(DangerousPatternTimeoutMs));

    private static readonly Regex PowerShellIwrIexRegex = new(@"Invoke-WebRequest[^|]*\|\s*Invoke-Expression", LinearIgnoreCase, TimeSpan.FromMilliseconds(DangerousPatternTimeoutMs));

    private static readonly Regex PowerShellIwrIexShortRegex = new(@"iwr\s+[^|]*\|\s*iex", LinearIgnoreCase, TimeSpan.FromMilliseconds(DangerousPatternTimeoutMs));

    private static readonly Regex PowerShellDownloadStringRegex = new(@"DownloadString\s*\([^)]+\)[^|]*\|\s*(iex|Invoke-Expression)", LinearIgnoreCase, TimeSpan.FromMilliseconds(DangerousPatternTimeoutMs));

    [GeneratedRegex(@"eval\s+\$", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex EvalVariableRegex();

    [GeneratedRegex(@"history\s+-c", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex HistoryClearRegex();

    [GeneratedRegex(@">\s*~/\.bash_history|>\s*~/\.zsh_history", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex HistoryFileTruncateRegex();

    private static readonly Regex PowerShellHistoryDeleteRegex = new(@"Remove-Item.*ConsoleHost_history\.txt", LinearIgnoreCase, TimeSpan.FromMilliseconds(DangerousPatternTimeoutMs));

    [GeneratedRegex(@"setenforce\s+0", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex SelinuxDisableRegex();

    [GeneratedRegex(@"iptables\s+-F", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex FirewallFlushRegex();

    [GeneratedRegex(@"cat\s+/etc/shadow|head\s+/etc/shadow|tail\s+/etc/shadow", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex ShadowFileAccessRegex();

    // The old echo.*>> alternative only found writes this > alternative already matches, while
    // restarting its suffix scan at every echo made a long command quadratic.
    [GeneratedRegex(@">\s*/etc/passwd", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex PasswdModifyRegex();

    [GeneratedRegex(LinearCommandStart + @"rm\s+(?:(?:-[a-zA-Z]+|--[a-z-]+)\s+)*(?:['"" ]?/(?:Users(?:/[^/\s'"";&|]+)?/?|\*|)|['"" ]?(?:~|\$HOME|\$\{HOME\})(?:/\*)?/?|[^;&|]*--no-preserve-root)(?:['""\s]|$|[;&|])", LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex OndrejRmRegex();

    [GeneratedRegex(LinearCommandStart + @"(?:mkfs(?:\.[a-z0-9]+)?" + LinearCommandEnd + @"|diskutil\s+(?:erase\w*|partitionDisk|zeroDisk|secureErase|apfs\s+(?:delete|erase)\w*)" + LinearCommandEnd + @")|>\s*/dev/(?:r?disk\d*|sd[a-z]|nvme\d+)", LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex OndrejDiskRegex();

    // Every sudo inside a chain of option arguments is also a command start, and each backtracking restart
    // rescans the rest of the chain: quadratic in its length. NonBacktracking matches in linear time.
    [GeneratedRegex(LinearCommandStart + @"sudo(?:\s+-[a-zA-Z-]*(?:=[^\s;&|]*|\s+[^-\s;&|][^\s;&|]*)?)*\s+rm(?:" + CommandEndContext + ")", RegexOptions.NonBacktracking, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejSudoRmRegex();

    [GeneratedRegex(LinearCommandStart + @"(?:curl|wget)\s+[^;&|]*\|\s*(?:sudo\s+)?(?:ba|z|da)?sh" + LinearCommandEnd, LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex OndrejCurlPipeRegex();

    // After push, its end context and the rest of the command were (?=end)[^;&|\r\n]*\s...: a first character
    // the rest takes is one of the end characters it can hold, or the rest is empty and its \s ends push (DYD-610).
    [GeneratedRegex(LinearCommandStart + @"git\s+push(?:(?:[^\S\r\n]|['""`)])[^;&|\r\n]*)?(?:\s(?:-f|--force|--delete|-d)" + LinearCommandEnd + @"|\s\+[a-z0-9._/-]|\s:[a-z0-9._/-])", LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex OndrejGitPushRegex();

    [GeneratedRegex(LinearCommandStart + @"git\s+(?:reflog\s+expire\b[^;&|\r\n]*--expire(?:-unreachable)?(?:=|\s+)now\b|gc\b[^;&|\r\n]*--prune(?:=|\s+)(?:now|all)\b)", LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex OndrejGitRecoveryRegex();

    [GeneratedRegex(LinearCommandStart + @"(?:chmod\s+[^;&|\r\n]*\b777\s['""]?/['""]?(?:\s|$|[;&|])|chown\s+-R\b[^;&|\r\n]*\s['""]?/['""]?(?:\s|$|[;&|]))", LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex OndrejSystemPermissionsRegex();

    [GeneratedRegex(LinearCommandStart + @"gh\s+(?:(?:repo|release|secret|ssh-key|gpg-key)\s+delete)" + LinearCommandEnd, LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex OndrejGhDeleteRegex();

    [GeneratedRegex(LinearCommandStart + @"gh\s+api\b[^;&|\r\n]*(?:-X|--method)(?:=|\s+)DELETE" + LinearCommandEnd, LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex OndrejGhApiDeleteRegex();

    [GeneratedRegex(LinearCommandStart + @"gh\s+repo\s+edit\b[^;&|\r\n]*--visibility(?:=|\s+)public" + LinearCommandEnd, LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex OndrejGhVisibilityRegex();

    [GeneratedRegex(LinearCommandStart + @"gh\s+auth\s+token" + LinearCommandEnd, LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex OndrejGhTokenRegex();

    [GeneratedRegex(LinearCommandStart + @"(?:bw|bws|lpass|keepassxc-cli|rbw|nordpass)" + LinearCommandEnd, LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex OndrejPasswordCliRegex();

    // Deliberate difference from upstream DYD-188: the original `pass\s+\S` also matches prose
    // shaped like the password-store CLI (a body line, a table cell, "pass the lease to..."),
    // so a PR body or commit message containing the word "pass" gets denied. This rule blocks
    // three shapes: a known subcommand, a flag, or exactly one name token (`pass <name>` prints
    // the secret) that is bare, quoted (`pass "My Bank"`, spaces allowed inside the quotes) or a
    // `$(...)` substitution. Prose with two or more words after "pass" stays allowed.
    // Named gaps, accepted on purpose: an unquoted all-digit name (`pass 12`), a second word
    // (`pass x y`), an fd of more than one digit (`pass x 10>f`), and the name form written as a
    // markdown table cell: `pass` after a single pipe with its name before a single pipe, as in
    // "| unit | pass OK |", which also lets `true | pass github | xclip` through (the name form
    // ignores stdin). Subcommand and flag forms stay blocked inside a table cell.
    // The subcommand must end on a real word boundary, not `\b`, which also fires between a
    // letter and a hyphen, so "pass show-stopper" would otherwise match "show". After the name,
    // optional spaces lead to one terminator: end, newline, a separator or quote, a redirect with
    // at most one fd digit tied to it (`pass x > f`, `pass x 2>&1`, `pass x &> f`), or a `#`
    // comment. A digit not followed by a redirect is prose ("pass stage 2"). gopass takes the same
    // shapes. The lead-in skips only spaces and tabs, the path prefix stops
    // at `{`, and the quoted and `$(...)` names stop at a newline, quote or paren: each newline
    // or `{` is its own start, so a long run of them cannot rescan the input from every start.
    // These shapes are read on the raw text; RunsPasswordStore then asks that a pass or gopass
    // runs at command position, so a heredoc body, a quoted grep pattern or a PR body saying
    // "pass" is no password-store access (DYD-406).
    [GeneratedRegex(@"(?:^|(?<cell>(?<!\|)\|(?!\|))|\|\||[;&\r\n({`])[ \t]*(?:[^\s;&|'""`(){]*/)?(?:go)?pass(?:\s+(?:show|insert|add|edit|generate|rm|remove|delete|mv|rename|cp|copy|git|init|ls|list|find|search|grep|otp)(?=[ \t]|$|[\r\n;&|<>'""`)])|\s+-\S|\s+(?:(?<q>['""])[^'""\r\n]+\k<q>|\$\([^()\r\n]*\)|(?![0-9]+(?:[\s;&|'""`()<>]|$))[^\s;&|'""`()<>]+)(?=[ \t]*(?:[0-9]?[<>]|$|[\r\n;&|'""`)])|[ \t]+#)(?(cell)(?![ \t]*\|(?!\|))))", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejPassRegex();

    // A command line that cannot be read for certain keeps the raw shape's verdict, failing closed.
    private static bool RunsPasswordStore(string command)
    {
        if (!OndrejPassRegex().IsMatch(command))
            return false;
        var (found, uncertain) = Runs(command, words => ProgramCall(words) is { Program: "pass" or "gopass" });
        return found || uncertain;
    }

    [GeneratedRegex(@"\bpy(?:thon[0-9.]*)?w?(?:\.exe)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex PythonWordRegex();

    // python -, the form a command line that cannot be read for certain is still refused for.
    [GeneratedRegex(@"\bpy(?:thon[0-9.]*)?w?(?:\.exe)?(?:\s+-\S+)*\s+-(?=\s|$|[;&|)])", RegexOptions.IgnoreCase)]
    private static partial Regex PythonDashRegex();

    [GeneratedRegex(@"^py(?:thon[0-9.]*)?w?$")]
    private static partial Regex PythonProgramRegex();

    private static readonly HashSet<string> PythonInfoFlags = new(StringComparer.Ordinal)
        { "-V", "-VV", "--version", "-h", "-?", "--help", "--help-env", "--help-xoptions", "--help-all", "--list", "--list-paths", "-0", "-0p" };

    // Where a Python call takes its program from, when not from a script, -c or -m; a later one reads stdin more surely.
    private enum PythonSource { None, NoOperand, Dash }

    /// <summary>
    /// A Python program is read from stdin when a Python call names - as its program, or names none
    /// while something feeds its stdin: a pipe, a &lt; redirection, a heredoc or a here-string.
    /// </summary>
    private static bool RunsPythonFromStdin(string command)
    {
        if (!PythonWordRegex().IsMatch(command))
            return false;
        var (dash, uncertain) = Runs(command, words => PythonSourceOf(words) == PythonSource.Dash);
        return dash || (uncertain && PythonDashRegex().IsMatch(command))
            || CommandLines(MarkFeeds(command), ShellDialect.Unknown).Lines.SelectMany(line => line.Commands)
                .Any(read => read.Fed && PythonSourceOf(read.Words) == PythonSource.NoOperand);
    }

    // Written as words of their own after a pipe and before a < redirection; no command line holds them.
    private const string PipeMark = "\u0002";
    private const string RedirectMark = "\u0003";

    // A pipe (| or |&, not ||, >| or \|), with the spaces and the ( or { of a group it leads into.
    [GeneratedRegex(@"(?<![|>\\])\|&?(?!\|)[ \t({]*")]
    private static partial Regex PipeRegex();

    // A < redirection, with any fd digits that start its word; << and <<< are one, \< is no
    // redirection and <# opens a PowerShell comment.
    [GeneratedRegex(@"(?:(?<=^|[\s;&|(){}])\d+|(?<![<\d\\]))<(?!#)")]
    private static partial Regex InputRedirectionRegex();

    /// <summary>
    /// The command line with a mark word after each pipe and before each &lt; redirection, so the
    /// reading (<see cref="CommandLines"/>) finds a mark only where the shell pipes or redirects: as the
    /// first word of the command a pipe leads into, the first in a group or a substitution included,
    /// or as a word of the command a &lt; feeds. In quotes, a comment or a body a command reads, a mark
    /// is text of that word or body, and in a command string it is read with that string.
    /// </summary>
    private static string MarkFeeds(string command) =>
        InputRedirectionRegex().Replace(PipeRegex().Replace(command, pipe => pipe.Value + PipeMark + " "), redirect => $" {RedirectMark} {redirect.Value}");

    /// <summary>
    /// Where a Python call (python, python3.12, pythonw or py, under any path, case or .exe) takes its
    /// program: <c>Dash</c> when its first operand is -, <c>NoOperand</c> when it has none, so it reads
    /// stdin when something feeds it and prompts otherwise. The call is the command's program, or the
    /// program a run subcommand starts (<see cref="RunPythonPrograms"/>). A script operand, -c or -m (a
    /// short option cluster naming c or m), a help or version flag, operands xargs supplies, a lookup
    /// by command -v or -V, and any other command are <c>None</c>.
    /// </summary>
    private static PythonSource PythonSourceOf(IReadOnlyList<string> words)
    {
        var at = CommandPosition.Skip(words);
        if (at >= words.Count || LooksUp(words, at))
            return PythonSource.None;

        IEnumerable<int> programs = IsPythonProgram(words[at]) ? [at] : RunPythonPrograms(words, at);
        var source = programs.Select(program => PythonArgumentsSource(words, program)).DefaultIfEmpty().Max();
        return source == PythonSource.NoOperand && words.Take(at).Any(word => CommandPosition.ProgramName(word) == "xargs")
            ? PythonSource.None
            : source;
    }

    private static bool IsPythonProgram(string word) => PythonProgramRegex().IsMatch(CommandPosition.ProgramName(word));

    // Where the Python call at words[program] takes its program from, by the words after it.
    private static PythonSource PythonArgumentsSource(IReadOnlyList<string> words, int program)
    {
        for (var i = program + 1; i < words.Count; i++)
        {
            var word = words[i];
            if (word == "-")
                return PythonSource.Dash;
            if (word.Length < 2 || word[0] != '-' || PythonInfoFlags.Contains(word)
                || (word[1] != '-' && word.AsSpan(1).IndexOfAny('c', 'm') >= 0))
                return PythonSource.None;
            if (word is "-W" or "-X" or "--check-hash-based-pycs")
                i++;
        }

        return PythonSource.NoOperand;
    }

    /// <summary>
    /// The Python words a run subcommand may start, by one shape whatever the runner (uv run, poetry
    /// run, conda run -n env, ...): the program's first operand is the word run, and Python is the
    /// first operand after it, with options before each. An operand after an option may be that
    /// option's value, so each run word that may be the first operand counts (<c>uv --directory run
    /// run python -</c>), and so does each Python word that may be run's.
    /// </summary>
    private static IEnumerable<int> RunPythonPrograms(IReadOnlyList<string> words, int program)
    {
        var through = program;
        foreach (var run in LeadingOperands(words, program + 1).Where(i => words[i] == "run"))
        {
            if (run < through)
                continue;
            foreach (var operand in LeadingOperands(words, run + 1))
            {
                through = operand;
                if (IsPythonProgram(words[operand]))
                    yield return operand;
            }
        }
    }

    // The operands from words[from] on that may be the first: each after an option, then the first
    // after an operand or none (--name=value names its value).
    private static IEnumerable<int> LeadingOperands(IReadOnlyList<string> words, int from)
    {
        var afterOption = false;
        for (var i = from; i < words.Count; i++)
        {
            var option = words[i].Length > 1 && words[i][0] == '-';
            if (!option)
            {
                yield return i;
                if (!afterOption)
                    yield break;
            }

            afterOption = option && !words[i].Contains('=');
        }
    }

    // command -v or -V before the program only names it.
    private static bool LooksUp(IReadOnlyList<string> words, int program) =>
        words.Take(program).SkipWhile(word => CommandPosition.ProgramName(word) != "command").Skip(1)
            .TakeWhile(word => word.StartsWith('-')).Any(word => word.AsSpan(1).IndexOfAny('v', 'V') >= 0);

    private const int MaxCommandLineDepth = 8;

    /// <summary>One reading of one command line: its text, its commands, and whether it nests too deep to read.</summary>
    private sealed record CommandLine(string Text, List<ReadCommand> Commands, bool TooDeep);

    /// <summary>
    /// One command as its shell reads it: its words, and whether something feeds its stdin, a heredoc
    /// or here-string, or in a line from <see cref="MarkFeeds"/> a pipe or a &lt; redirection.
    /// </summary>
    private sealed record ReadCommand(IReadOnlyList<string> Words, bool Fed);

    // Takes the marks MarkFeeds wrote out of the command's words, so its program and its script read
    // as the line has them.
    private static ReadCommand Unmark(ShellReader.Command command)
    {
        var piped = command.Words is [PipeMark, ..];
        var redirected = command.Words.RemoveAll(word => word == RedirectMark) > 0;
        if (piped)
            command.Words.RemoveAt(0);
        return new ReadCommand(command.Words, piped || redirected || command.Stdin.Count > 0);
    }

    /// <summary>
    /// The command lines a command line runs, read as its shell reads them (<see cref="ShellReader"/>;
    /// both ways when the shell is unknown): one per reading of each. Quotes, comments, heredoc and
    /// here-string bodies are no commands. The command string a shell, eval or env -S is given
    /// (<see cref="CommandPosition.ScriptOf"/>), the heredoc or here-string a shell given none reads,
    /// and a bash backtick substitution are command lines of their own. <c>Uncertain</c> when a
    /// reading cannot finish, a command string is encoded, a shell reads a pipe the line does not
    /// show, or nesting passes the limit. A line past the limit, or past the reading's budget, is too
    /// deep and holds no commands (DYD-723).
    /// </summary>
    private static (List<CommandLine> Lines, bool Uncertain) CommandLines(string command, ShellDialect dialect)
    {
        var reading = new CommandLineReading(command.Length);
        var uncertain = ReadCommandLines(command, dialect, 0, reading);
        return (reading.Lines, uncertain);
    }

    /// <summary>
    /// The lines one reading of a command's lines holds so far, and what it may still read: each line
    /// once, by its text, dialect and level, however many readings run it, and in all about the
    /// command read both ways and a line as long at each level below it, with room for a short chain
    /// of cmd. cmd reads its command string both ways, so each level of cmd doubled the readings and a
    /// chain of them was read 2^9 times; each is read once now, but a long chain of cmd, or of text the
    /// two ways read apart, still reads more each level and stops at the budget (DYD-723).
    /// </summary>
    private sealed class CommandLineReading(int length)
    {
        public List<CommandLine> Lines { get; } = [];
        public HashSet<(string Text, ShellDialect Dialect, int Depth)> Read { get; } = [];
        public long Budget { get; set; } = (MaxCommandLineDepth + 2L) * length + 4_096;
    }

    private static bool ReadCommandLines(string text, ShellDialect dialect, int depth, CommandLineReading lines)
    {
        if (!lines.Read.Add((text, dialect, depth)))
            return false;
        bool[] readings = dialect switch
        {
            ShellDialect.Posix => [true],
            ShellDialect.PowerShell => [false],
            _ => [true, false],
        };
        lines.Budget -= readings.Length * text.Length;
        if (depth > MaxCommandLineDepth || lines.Budget < 0)
        {
            lines.Lines.Add(new CommandLine(text, [], TooDeep: true));
            return true;
        }

        var uncertain = false;
        foreach (var posix in readings)
        {
            var reading = ShellReader.Read(text, posix);
            lines.Lines.Add(new CommandLine(text, [.. reading.Commands.Select(Unmark)], reading.TooDeep));
            uncertain |= reading.Failed;
            foreach (var substitution in reading.Substitutions)
                uncertain |= ReadCommandLines(substitution, ShellDialect.Posix, depth + 1, lines);
            foreach (var command in reading.Commands)
                uncertain |= ReadRunLines(command, depth, lines);
        }

        return uncertain;
    }

    // The command lines one command runs: its command string, or the stdin bodies a shell reads.
    private static bool ReadRunLines(ShellReader.Command command, int depth, CommandLineReading lines)
    {
        if (ProgramCall(command.Words) is not { } call)
            return false;
        if (CommandPosition.ScriptOf(call.Program, call.Arguments) is { } script)
            return script.Encoded || ReadCommandLines(script.Text, script.Dialect, depth + 1, lines);
        if (StdinShellDialect(call.Program, call.Arguments) is not { } dialect)
            return false;

        var uncertain = command.Stdin.Count == 0;
        foreach (var body in command.Stdin)
            uncertain |= ReadCommandLines(body, dialect, depth + 1, lines);
        return uncertain;
    }

    // The dialect of a shell that reads its commands from stdin, or null for any other program.
    private static ShellDialect? StdinShellDialect(string program, List<string> rest)
    {
        if (CommandPosition.PosixShells.Contains(program))
            return CommandPosition.PosixShellString(rest) == null ? ShellDialect.Posix : null;
        if (program is not ("pwsh" or "powershell"))
            return null;
        return CommandPosition.ReadPwsh(rest) is (CommandPosition.PwshMode.Command, [] or ["-"]) or (CommandPosition.PwshMode.Positional, [])
            ? ShellDialect.PowerShell
            : null;
    }

    /// <summary>A command's program name and the words after it, past assignments and runners; null when it has none.</summary>
    private static (string Program, List<string> Arguments)? ProgramCall(IReadOnlyList<string> words)
    {
        var at = CommandPosition.Skip(words);
        return at < words.Count ? (CommandPosition.ProgramName(words[at]), words.Skip(at + 1).ToList()) : null;
    }

    // Whether a command the line runs, read both ways, meets isCall on its words, and whether the reading was certain.
    private static (bool Found, bool Uncertain) Runs(string command, Func<IReadOnlyList<string>, bool> isCall)
    {
        var (lines, uncertain) = CommandLines(command, ShellDialect.Unknown);
        return (lines.SelectMany(line => line.Commands).Any(command => isCall(command.Words)), uncertain);
    }

    // Written where a nudge's match starts; no command line holds it.
    private const string LaunchMark = "\u0001";

    /// <summary>
    /// Whether a raw nudge match counts. Every unquoted or unknown quoted operand counts;
    /// only the recorded quoted search/filter roles, comments and data bodies are exempt.
    /// Known executable strings are read recursively, preserving code inside quoted arguments.
    /// </summary>
    internal static bool RunsAt(string command, int index, ShellDialect dialect)
    {
        return HasLaunchMark(command) || CountsNudge(command.Insert(index, LaunchMark), dialect, 0, new NudgeFrame(BodyOutput.Data));
    }

    private enum BodyOutput { Data, Executable, Unknown }
    private enum Accounting { Absent, Accounted, Missing }
    private sealed record NudgeFrame(BodyOutput Output, ShellReader.Position? Origin = null, NudgeFrame? Parent = null);

    private static bool CountsNudge(string text, ShellDialect dialect, int depth, NudgeFrame frame)
    {
        if (depth > MaxCommandLineDepth || !HasLaunchMark(text) || text.IndexOf(LaunchMark, StringComparison.Ordinal)
            != text.LastIndexOf(LaunchMark, StringComparison.Ordinal)
            || !ValidFrame(frame))
            return true;
        bool[] readings = dialect switch
        {
            ShellDialect.Posix => [true],
            ShellDialect.PowerShell => [false],
            _ => [true, false],
        };
        foreach (var posix in readings)
        {
            var reading = ShellReader.Read(text, posix);
            if (reading.Failed || AccountedFor(reading.Source) != Accounting.Accounted
                || reading.Captures.Any(c => HasLaunchMark(c.Source.Text[c.Range])
                    && CaptureCountsNudge(c, frame, depth))
                || reading.Commands.Any(c => CommandCountsNudge(c, frame, depth)))
                return true;
        }
        return false;
    }

    private static bool HasLaunchMark(string text) => text.Contains(LaunchMark, StringComparison.Ordinal);

    private static bool ValidFrame(NudgeFrame frame) => frame.Parent == null
        ? frame.Origin == null && frame.Output == BodyOutput.Data
        : frame.Origin is { } origin && MarkedAt(origin) && ValidFrame(frame.Parent)
            && (frame.Parent.Output == BodyOutput.Data || frame.Output != BodyOutput.Data);

    private static bool MarkedAt(ShellReader.Position position) =>
        position.Source.Contains(position.Offset..(position.Offset + LaunchMark.Length))
        && position.Source.Text[position.Offset] == LaunchMark[0];

    private static int MarkedIndex(ShellReader.Scope source, Range range)
    {
        if (!source.Contains(range))
            return -1;
        var index = source.Text.IndexOf(LaunchMark, range.Start.Value, range.End.Value - range.Start.Value, StringComparison.Ordinal);
        return index;
    }

    // Every marked representation must have its own emitted origin or completed execution edge.
    // A comment/body elsewhere, or absence of a counted flat command, cannot discharge this source.
    private static Accounting AccountedFor(ShellReader.Scope source, Dictionary<ShellReader.Scope, Accounting>? known = null)
    {
        known ??= [];
        if (known.TryGetValue(source, out var previous))
            return previous;
        known[source] = Accounting.Missing; // An ancestry cycle cannot certify itself.
        var result = AccountSource(source, known);
        known[source] = result;
        return result;
    }

    private static Accounting AccountSource(ShellReader.Scope source, Dictionary<ShellReader.Scope, Accounting> known)
    {
        var marker = MarkedIndex(source, source.Start..source.End);
        if (marker < 0)
            return Accounting.Absent;
        var accounted = source.Comments.Any(r => MarkedIndex(source, r) == marker);
        if (source.DerivedBody is { } derived)
            accounted |= ReferenceEquals(derived.ExpansionSource, source)
                && OriginMatches(derived.Origins, marker, derived.Owner.Source, derived.Content);
        foreach (var command in source.Commands)
        {
            if (!CommandAccountsFor(command, marker, known, out var owns))
                return Accounting.Missing;
            accounted |= owns;
        }
        foreach (var edge in source.Executions.Where(e => MarkedIndex(source, e.Token) == marker))
        {
            if (!ExecutionAccountsFor(edge, known))
                return Accounting.Missing;
            accounted = true;
        }
        if (source.Children.Any(child => AccountedFor(child, known) == Accounting.Missing))
            return Accounting.Missing;
        return accounted ? Accounting.Accounted : Accounting.Missing;
    }

    private static bool CommandAccountsFor(ShellReader.Command command, int marker,
        Dictionary<ShellReader.Scope, Accounting> known, out bool owns)
    {
        owns = command.Redirections.Any(HasLaunchMark);
        for (var i = 0; i < command.Words.Count; i++)
        {
            if (MarkedIndex(command.Source, command.WordRanges[i]) != marker)
                continue;
            owns = true;
            var normalized = command.Words[i].IndexOf(LaunchMark, StringComparison.Ordinal);
            if (!OriginMatches(command.WordOrigins[i], normalized, command.Source, command.WordRanges[i])
                && !command.Source.Executions.Any(e => ReferenceEquals(e.Command, command)
                    && e.WordIndex == i && MarkedIndex(e.Source, e.Token) == marker && ExecutionAccountsFor(e, known)))
                return false;
        }
        foreach (var body in command.Bodies.Where(b => MarkedIndex(command.Source, b.Content) == marker))
        {
            owns = true;
            var normalized = body.Text.IndexOf(LaunchMark, StringComparison.Ordinal);
            if (!OriginMatches(body.Origins, normalized, command.Source, body.Content)
                && !body.Expansions.Any(e => RawToken(e) is { } r && MarkedIndex(command.Source, r) == marker
                    && ExecutionAccountsFor(e, known)))
                return false;
        }
        return true;
    }

    private static bool OriginMatches(ShellReader.TextOrigins origins, int index, ShellReader.Scope source, Range range) =>
        index >= 0 && origins.At(index) is { } origin && ReferenceEquals(origin.Source, source)
        && origin.Offset == MarkedIndex(source, range) && MarkedAt(origin);

    private static bool ExecutionAccountsFor(ShellReader.Host edge, Dictionary<ShellReader.Scope, Accounting>? known = null)
    {
        if (!CompletedExecution(edge))
            return false;
        if (edge.Child is { } child)
            return AccountedFor(child, known) == Accounting.Accounted;
        var capture = edge.Capture!;
        return OriginMatches(capture.Origins, capture.Text.IndexOf(LaunchMark, StringComparison.Ordinal),
            capture.Source, capture.Range);
    }

    private static bool CaptureCountsNudge(ShellReader.Capture capture, NudgeFrame frame, int depth)
    {
        var marker = capture.Text.IndexOf(LaunchMark, StringComparison.Ordinal);
        if (!ExecutionAccountsFor(capture.Host) || capture.Origins.At(marker) is not { } origin)
            return true;
        return CountsNudge(capture.Text, ShellDialect.Posix, depth + 1,
            new NudgeFrame(CapturedOutput(capture.Host, frame.Output), origin, frame));
    }

    private static bool CommandCountsNudge(ShellReader.Command command, NudgeFrame frame, int depth)
    {
        if (command.Redirections.Any(HasLaunchMark))
            return true;
        var scriptWord = -1;
        if (CommandPosition.ScriptOf(command, LaunchMark) is { } script
            && HasLaunchMark(script.Text))
        {
            if (script.Encoded || script.Origin is not { } origin || !ReferenceEquals(origin.Command, command)
                || origin.Offset != script.Text.IndexOf(LaunchMark, StringComparison.Ordinal)
                || !MarkedScriptWordIsQuoted(command, origin.Word)
                || CountsNudge(script.Text, script.Dialect, depth + 1,
                    new NudgeFrame(OutputOf(command, frame.Output), origin.Source, frame)))
                return true;
            scriptWord = origin.Word;
        }

        for (var i = 0; i < command.Words.Count; i++)
        {
            if (i != scriptWord && WordCountsNudge(command, i, frame, depth))
                return true;
        }
        return command.Bodies.Where(b => b.WordIndex < 0
                && (HasLaunchMark(b.Text) || MarkedIndex(command.Source, b.Content) >= 0))
            .Any(body => BodyCountsNudge(body, frame, depth));
    }

    private static bool WordCountsNudge(ShellReader.Command command, int index, NudgeFrame frame, int depth)
    {
        var raw = MarkedIndex(command.Source, command.WordRanges[index]);
        if (!HasLaunchMark(command.Words[index]))
            return raw >= 0 && !command.Source.Executions.Any(e => ReferenceEquals(e.Command, command)
                && e.WordIndex == index && MarkedIndex(e.Source, e.Token) == raw && ExecutionAccountsFor(e));
        return raw < 0 || !QuotedNudgeData(command, index, frame, depth);
    }

    // Re-reading an executable string must not turn an originally unquoted match into data.
    // Unsupported concatenated strings stay counted even if the shell could eventually quote them.
    private static bool MarkedScriptWordIsQuoted(ShellReader.Command command, int word)
    {
        var pattern = command.Source.Posix ? PosixScriptWordRegex() : PowerShellScriptWordRegex();
        return pattern.IsMatch(command.RawWord(word));
    }

    [GeneratedRegex("\\A(?:'[^']*'|\"(?:\\\\[\\s\\S]|[^\"\\\\])*\")\\z")]
    private static partial Regex PosixScriptWordRegex();

    [GeneratedRegex("\\A(?:'(?:''|[^'])*'|\"(?:`[\\s\\S]|\"\"|[^\"`])*\")\\z")]
    private static partial Regex PowerShellScriptWordRegex();

    private static bool BodyCountsNudge(ShellReader.Body body, NudgeFrame frame, int depth)
    {
        var owner = body.Owner;
        var marker = MarkedIndex(owner.Source, body.Content);
        if (marker < 0 || !BodyPositionIsData(body, marker))
            return true;
        var delegated = body.Expansions.Where(e => RawToken(e) is { } token
            && MarkedIndex(owner.Source, token) == marker).ToArray();
        if (delegated.Length != 0)
            return delegated.Length != 1 || !ExecutionAccountsFor(delegated[0]);
        var normalized = body.Text.IndexOf(LaunchMark, StringComparison.Ordinal);
        if (!OriginMatches(body.Origins, normalized, owner.Source, body.Content))
            return true;
        if (body.Kind != ShellReader.BodyKind.HereString && ProgramCall(owner.Words) is { } shell
            && StdinShellDialect(shell.Program, shell.Arguments) is { } dialect)
            return CountsNudge(body.Text, dialect, depth + 1,
                new NudgeFrame(OutputOf(owner, frame.Output), body.Origins.At(normalized), frame));
        return !BodyIsData(body) || OutputOf(owner, frame.Output) != BodyOutput.Data;
    }

    private static bool BodyIsData(ShellReader.Body body) =>
        (body.Owner.Words is [var first, ..] && DataCommands.Contains(first))
        || (!body.Owner.Source.Posix && body.Kind == ShellReader.BodyKind.HereString
            && body.WordIndex == 0 && body.Owner.Words.Count == 1);

    private static bool BodyPositionIsData(ShellReader.Body body, int position)
    {
        if (BodySurface(body, position) is not { } surface)
            return false;
        var (raw, at) = surface;
        if (body.Kind == ShellReader.BodyKind.HereWord)
            return PosixScriptWordRegex().IsMatch(raw);
        if (body.Kind != ShellReader.BodyKind.HereString)
            return true;
        if (!body.Owner.WordRanges[body.WordIndex].Equals(body.Whole))
            return false;
        if (!body.Owner.Source.Posix)
            return true;
        return BashReadsAsWords(raw) && at >= 2 && at < raw.Length - 2
            && (raw[1] == '"' || raw[2..at].Count(c => c == '\'') % 2 == 0);
    }

    // Project only completed immediate tokens. In particular, the selected descendant marker is
    // replaced by its own edge's insertion site; child quotes never become parent quote state.
    private static (string Text, int Position)? BodySurface(ShellReader.Body body, int position)
    {
        var source = body.Owner.Source;
        if (!source.Contains(body.Whole) || !source.Contains(body.Content)
            || position < body.Content.Start.Value || position >= body.Content.End.Value)
            return null;
        var surface = new StringBuilder();
        var cursor = body.Whole.Start.Value;
        var projected = position - cursor;
        foreach (var edge in body.Expansions)
        {
            if (!ReferenceEquals(edge.Body, body) || RawToken(edge) is not { } token
                || token.Start.Value < cursor || token.Start.Value < body.Content.Start.Value
                || token.End.Value > body.Content.End.Value)
                return null;
            surface.Append(source.Text, cursor, token.Start.Value - cursor);
            if (position >= token.Start.Value && position < token.End.Value)
                projected = surface.Length;
            else if (position >= token.End.Value)
                projected -= token.End.Value - token.Start.Value - 1;
            surface.Append('x');
            cursor = token.End.Value;
        }
        surface.Append(source.Text, cursor, body.Whole.End.Value - cursor);
        return (surface.ToString(), projected);
    }

    private static bool CompletedExecution(ShellReader.Host edge)
    {
        if (!edge.Complete || !edge.Source.Contains(edge.Token) || !edge.Source.HasExecution(edge)
            || edge.Token.End.Value <= edge.Token.Start.Value)
            return false;
        if (edge.Child is { } child)
        {
            var opener = edge.Kind == ShellReader.ExecutionKind.Group && edge.Source.Text[edge.Token.Start.Value] == '(' ? 1 : 2;
            return edge.Kind != ShellReader.ExecutionKind.Backtick && edge.Capture == null && ReferenceEquals(child.Host, edge)
                && edge.Source.HasChild(child) && child.Start == edge.Token.Start.Value + opener
                && child.End + 1 == edge.Token.End.Value;
        }
        return edge.Kind == ShellReader.ExecutionKind.Backtick && edge.Capture is { } capture && ReferenceEquals(capture.Host, edge)
            && ReferenceEquals(capture.Source, edge.Source) && capture.Range.Start.Value == edge.Token.Start.Value + 1
            && capture.Range.End.Value == edge.Token.End.Value - 1;
    }

    private static Range? RawToken(ShellReader.Host edge)
    {
        if (!CompletedExecution(edge) || edge.Body is not { } body
            || !ReferenceEquals(body.Owner, edge.Command) || !body.HasExpansion(edge)
            || !edge.Command.HasBody(body) || edge.WordIndex != body.WordIndex)
            return null;
        if (ReferenceEquals(edge.Source, body.Owner.Source))
            return body.Kind == ShellReader.BodyKind.HereWord ? edge.Token : null;
        return ReferenceEquals(edge.Source, body.ExpansionSource) && ReferenceEquals(edge.Source.DerivedBody, body)
            ? body.Origins.RangeIn(body.Owner.Source, edge.Token) : null;
    }

    private static BodyOutput CapturedOutput(ShellReader.Host host, BodyOutput output, HashSet<ShellReader.Host>? ancestors = null)
    {
        ancestors ??= [];
        if (!ancestors.Add(host) || RawToken(host) is not { } token
            || !BodyPositionIsData(host.Body!, token.Start.Value))
            return BodyOutput.Unknown;
        if (ProgramCall(host.Command.Words) is { } shell && StdinShellDialect(shell.Program, shell.Arguments) != null)
            return BodyOutput.Executable;
        return BodyIsData(host.Body!) ? OutputOf(host.Command, output, ancestors) : BodyOutput.Unknown;
    }

    private static BodyOutput OutputOf(ShellReader.Command command, BodyOutput output, HashSet<ShellReader.Host>? ancestors = null)
    {
        // Next links commands in this reader's scope; inspect its compound syntax once per pipe.
        var hasCompound = command.Source.Commands.Any(c => c.Words.Count > 0 && CompoundWords.Contains(c.Words[0]));
        while (true)
        {
            if (hasCompound || command.UncertainFeed || command.HeaderExpansion
                || command.RedirectTargets.Any(r => !LiteralRedirect(command.Source, r)))
                return BodyOutput.Unknown;
            if (!command.PipesOutput)
                break;
            if (command.Next is not { } next)
                return BodyOutput.Unknown;
            command = next;
            if (command.Words is [var first, ..] && DataCommands.Contains(first))
                continue;
            return ProgramCall(command.Words) is { } call
                && (StdinShellDialect(call.Program, call.Arguments) != null || call.Program is "iex" or "invoke-expression")
                ? BodyOutput.Executable : BodyOutput.Unknown;
        }
        return command.Source.Host is { } host ? CapturedOutput(host, output, ancestors) : output;
    }

    private static bool LiteralRedirect(ShellReader.Scope source, ShellReader.RedirectTarget target) =>
        source.Contains(target.Range) && (target.Operator is "<" or ">" or ">>" or "0<" or "1>" or "1>>")
        && LiteralRedirectWordRegex().IsMatch(source.Text[target.Range][target.Operator.Length..]);

    [GeneratedRegex("\\A\\s*(?:'[^']*'|\"[^\"$`]*\"|[^\\s$`'\"()<>;&|]+)\\z")]
    private static partial Regex LiteralRedirectWordRegex();

    // Q1-Q4 of the captain's finite GS-03 inventory. Prefixes identify operands, never whole argv.
    private static bool QuotedNudgeData(ShellReader.Command command, int index, NudgeFrame frame, int depth)
    {
        var text = command.Source.Text;
        var posix = command.Source.Posix;
        var range = command.WordRanges[index];
        var raw = text[range];
        if (command.Bodies.SingleOrDefault(b => b.WordIndex == index) is { } body)
            return !BodyCountsNudge(body, frame, depth);
        if (!LiteralNudgeQuoteRegex().IsMatch(raw))
            return false;
        var literalPrefix = Enumerable.Range(0, index).All(i => text[command.WordRanges[i]] == command.Words[i]);
        if (literalPrefix && QuotedSearchPattern(command, index, raw[0]))
            return true;
        return !posix && ProcessFilterPrefixRegex().IsMatch(text[..range.Start.Value])
            && ProcessFilterSuffixRegex().IsMatch(text[range.End.Value..]);
    }

    private static bool QuotedSearchPattern(ShellReader.Command command, int index, char quote)
    {
        var prefix = command.Words.Take(index).ToArray();
        var tail = command.Words.Skip(index + 1).ToArray();
        return prefix switch
        {
            ["rg", "-n"] => quote == '\'' && RgPatternTail(tail),
            ["git", "grep", "-n", "-E"] or ["git", "grep", "-n", "-i", "-E"] => quote == '\'' && GitGrepPatternTail(tail),
            ["grep", "-iE"] => quote == '"' && tail.All(w => !w.StartsWith('-')),
            _ => false,
        };
    }

    private static bool RgPatternTail(string[] words)
    {
        var at = 0;
        while (at + 1 < words.Length && words[at] == "--glob")
            at += 2;
        return words.Skip(at).All(w => !w.StartsWith('-'));
    }

    private static bool GitGrepPatternTail(string[] words)
    {
        var at = words.Length > 0 && !words[0].StartsWith('-') ? 1 : 0;
        return at == words.Length || (words[at] == "--" && words.Skip(at + 1).All(w => !w.StartsWith('-')));
    }

    // Interpolation or concatenation is not a literal data operand. Its raw match stays counted.
    [GeneratedRegex("\\A(?:'[^']*'|\"[^\"$`]*\")\\z")]
    private static partial Regex LiteralNudgeQuoteRegex();

    [GeneratedRegex(@"(?:\A|[;\r\n])\s*Get-CimInstance\s+Win32_Process\s*\|\s*Where-Object\s*\{\s*(?:\$_\.CreationDate\s+-gt\s+\$cut\s+-or\s+)?(?:\$_\.CommandLine|\(\[string\]\$_\.CommandLine\))\s+-match\s+\z", RegexOptions.IgnoreCase)]
    private static partial Regex ProcessFilterPrefixRegex();

    [GeneratedRegex(@"\A\s*(?:-and\s+\$_\.Name\s+-notmatch\s+'bash'\s*)?\}", RegexOptions.IgnoreCase)]
    private static partial Regex ProcessFilterSuffixRegex();

    [GeneratedRegex(LinearCommandStart + @"op\s+(?:read|run|inject|item|document|vault|connect|service-account|events-api|signin)" + LinearCommandEnd, LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex OndrejOpRegex();

    [GeneratedRegex(LinearCommandStart + @"security\s+(?:(?:-[a-z]+|--[a-z-]+)\s+)*(?:find-generic-password|find-internet-password|dump-keychain)" + LinearCommandEnd, LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex OndrejKeychainRegex();

    [GeneratedRegex(@"--export-secret-(?:key|keys|subkey|subkeys)(?=\s|=|$)", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejGpgSecretRegex();

    [GeneratedRegex(@"(?:~|\$HOME|\$\{HOME\}|/Users/[^/\s'""]+)/\.password-store(?:/|(?=\s|$|['"";&|]))", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejPasswordStorePathRegex();

    [GeneratedRegex(LinearCommandStart + @"(?:open\s+-a\s+['""]?(?:1Password|Bitwarden|NordPass|KeePass)|brew\s+(?:uninstall|remove|rm)\b[^;&|\r\n]*\s['""]?(?:1password|bitwarden(?:-cli)?|nordpass|keepassxc|lastpass))(?:\s|$|['""/;&|])|(?:^|[/\s'""])(?:1Password|Bitwarden|NordPass|KeePassXC)\.app(?:\s|$|['""/;&|])", LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex OndrejPasswordAppRegex();

    private const string WorktreeRemovalReason =
        "Raw worktree removal can lose uncommitted work or follow a link out of the tree. "
        + "From outside the tree run dydo clean --dry-run <worktree>, then dydo clean <worktree>";

    // One git -C/-c value: double- or single-quoted (spaces allowed), or a bare word.
    private const string GitOptionValue = @"(?:""[^""\r\n]*""|'[^'\r\n]*'|[^\s;&|'""]+)";

    // git worktree remove with a force flag (-f, git's repeated -ff, --force), after any git -C/-c
    // options, quoted or not. Plain remove refuses a dirty tree, so it stays allowed.
    [GeneratedRegex(LinearCommandStart + @"git(?:\s+-[cC]\s+" + GitOptionValue + @")*\s+worktree\s+remove\b[^;&|\r\n]*\s(?:-f+|--force)" + LinearCommandEnd, LinearIgnoreCase, DangerousPatternTimeoutMs)]
    internal static partial Regex ForcedWorktreeRemoveRegex();

    // A worktree path argument: an optional prefix ending in a separator, then a worktrees folder, one
    // entry in it, or T3's .t3/worktrees/<project>/<thread>, then optional trailing separators and the
    // character that ends the argument.
    private const string BareWorktreeArgument =
        @"\s(?:[^\s'""`;&|]*[/\\])?(?:worktrees(?:[/\\]+[^\s/\\'""`;&|]+)?|\.t3[/\\]+worktrees(?:[/\\]+[^\s/\\'""`;&|]+){2})[/\\]*(?:[\s'""`);&|]|$)";

    private const string DoubleQuotedWorktreeArgument =
        @"""(?:[^""\r\n]*[/\\])?(?:worktrees(?:[/\\]+[^/\\""\r\n]+)?|\.t3[/\\]+worktrees(?:[/\\]+[^/\\""\r\n]+){2})[/\\]*""";

    private const string SingleQuotedWorktreeArgument =
        @"'(?:[^'\r\n]*[/\\])?(?:worktrees(?:[/\\]+[^/\\'\r\n]+)?|\.t3[/\\]+worktrees(?:[/\\]+[^/\\'\r\n]+){2})[/\\]*'";

    // A recursive delete of a worktree itself: the argument is a worktrees folder or one entry in it
    // (T3's .t3/worktrees/<project>/<thread> counts two deep), so a delete inside a tree, such as its
    // bin or obj, stays allowed. Recursive means rm -r/-R/--recursive (flag clusters of rm's own
    // letters, so -f alone and PowerShell's -Force do not count), PowerShell -Recurse and its prefixes,
    // and cmd's rd /s. The argument is bare (a name ends at whitespace) or quoted (a name may hold
    // spaces and ends at the closing quote). An accident guard, not a shell parser: a worktree outside
    // a worktrees folder is not recognised.
    // NonBacktracking has no lookaheads, so the delete word is found first, then the flag and the
    // argument are two matches from the whitespace after it, each before the next separator (DYD-610).
    private const string RecursiveDeleteWord = CommandPath + @"(?:rm|rmdir|rd|del|erase|ri|Remove-Item)\s";

    [GeneratedRegex(@"(?:^|[\s;&|(){}`!\\])" + RecursiveDeleteWord, LinearIgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex RecursiveDeleteWordRegex();

    // The quote-opening contexts are searched apart: ssh's can span delete words the search above must
    // still find one by one.
    [GeneratedRegex(@"(?:\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])" + RecursiveDeleteWord, LinearIgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex QuotedRecursiveDeleteWordRegex();

    [GeneratedRegex(@"\A[^;&|\r\n]*\s(?:-[dfiIvrR]*[rR][dfiIvrR]*|--recursive|-rec(?:u(?:r(?:s(?:e)?)?)?)?|/s)(?:[\s:]|$)", LinearIgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex RecursiveFlagRegex();

    [GeneratedRegex(@"\A[^;&|\r\n]*(?:" + BareWorktreeArgument + "|" + DoubleQuotedWorktreeArgument + "|" + SingleQuotedWorktreeArgument + ")", LinearIgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex WorktreeArgumentRegex();

    internal static bool IsRecursiveWorktreeDelete(string command) =>
        DeletesAWorktree(command, RecursiveDeleteWordRegex()) || DeletesAWorktree(command, QuotedRecursiveDeleteWordRegex());

    // Each delete word in turn. A later word before the separator that ends the earlier one's text sees
    // only part of that text, so it cannot match where the earlier one did not, and is skipped.
    private static bool DeletesAWorktree(string command, Regex deleteWord)
    {
        var seenUntil = -1;
        for (var word = deleteWord.Match(command); word.Success; word = deleteWord.Match(command, word.Index + word.Length - 1))
        {
            var after = word.Index + word.Length - 1;
            if (after <= seenUntil)
                continue;
            var rest = command.AsSpan(after);
            if (RecursiveFlagRegex().IsMatch(rest) && WorktreeArgumentRegex().IsMatch(rest))
                return true;
            var separator = rest.IndexOfAny(";&|\r\n");
            seenUntil = separator < 0 ? command.Length : after + separator;
        }
        return false;
    }

    // Matches inline interpreter execution: python -c, node -e, ruby -e, perl -e/-E, php -r.
    // Does NOT include bash/sh/zsh -c - those are handled by shell -c subcommand extraction
    // in AnalyzeSubCommand, which analyzes the inner command's file operations instead of
    // blocking outright (blocking all bash -c would cause massive false positives).
    // Does NOT match script file execution (python script.py) or version flags (python --version).
    [GeneratedRegex(@"\b(?:python[23]?|node|ruby|perl)\s+(?:-\w+\s+)*-[ceE]\s|\bphp\s+(?:-\w+\s+)*-r\s", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex InlineInterpreterRegex();

    // Coaching: detect needless change-directory + command compounds.
    // Covers Bash `cd` and PowerShell forms (Set-Location, sl, chdir, Push-Location, pushd) -
    // all break Claude Code auto-approve when chained (`cd dir && cmd`, `Set-Location dir; cmd`).
    [GeneratedRegex(@"^\s*(?:cd|Set-Location|sl|chdir|Push-Location|pushd)\s+(?:""([^""]+)""|'([^']+)'|(\S+))\s*(?:&&|;)\s*(.*)", RegexOptions.IgnoreCase)]
    private static partial Regex CdThenCommandRegex();

    [GeneratedRegex(@"(?<!\d)(>{1,2})\s*([^\s|;&><]+)")]
    private static partial Regex OutputRedirectRegex();

    [GeneratedRegex(@"<\s*([^\s|;&><]+)")]
    private static partial Regex InputRedirectRegex();

    private static readonly (Func<string, bool> IsMatch, string Reason)[] DangerousPatterns =
    [
        (RecursiveDeleteRootRegex.IsMatch, "Recursive delete of root or home directory"),
        (RecursiveDeleteGlobRegex.IsMatch, "Recursive delete with dangerous glob pattern"),

        (PowerShellRecursiveDeleteRegex.IsMatch, "PowerShell recursive delete of root directory"),

        (ForkBombClassicRegex().IsMatch, "Fork bomb detected"),
        (ForkBombAltRegex().IsMatch, "Fork bomb variant detected"),

        (DirectDiskWriteRegex().IsMatch, "Direct disk write attempt"),
        (DdDiskWriteRegex().IsMatch, "Direct disk write via dd"),
        (DdKnownDiskWriteRegex.IsMatch, "Direct disk write via dd"),

        (Base64DecodePipeExecRegex.IsMatch, "Base64 decode piped to interpreter"),

        (CurlPipeShRegex.IsMatch, "Download and execute pattern (curl | sh)"),
        (WgetPipeShRegex.IsMatch, "Download and execute pattern (wget | sh)"),
        (WgetOutputPipeShRegex.IsMatch, "Download and execute pattern (wget -O | sh)"),

        (PowerShellIwrIexRegex.IsMatch, "Download and execute pattern (PowerShell IWR | IEX)"),
        (PowerShellIwrIexShortRegex.IsMatch, "Download and execute pattern (PowerShell iwr | iex)"),
        (PowerShellDownloadStringRegex.IsMatch, "Download and execute pattern (DownloadString | IEX)"),

        (EvalVariableRegex().IsMatch, "Eval of variable content"),

        (HistoryClearRegex().IsMatch, "History clearing detected"),
        (HistoryFileTruncateRegex().IsMatch, "History file truncation"),
        (PowerShellHistoryDeleteRegex.IsMatch, "PowerShell history deletion"),

        (SelinuxDisableRegex().IsMatch, "SELinux disable attempt"),
        (FirewallFlushRegex().IsMatch, "Firewall flush attempt"),

        (ShadowFileAccessRegex().IsMatch, "Shadow file access attempt"),
        (PasswdModifyRegex().IsMatch, "Password file modification attempt"),

        // David Ondrej's global command denylist, pinned in DYD-188.
        (OndrejRmRegex().IsMatch, "Deletion of system or home directory"),
        (OndrejDiskRegex().IsMatch, "Disk format or erase attempt"),
        (OndrejSudoRmRegex().IsMatch, "Privileged delete attempt"),
        (OndrejCurlPipeRegex().IsMatch, "Download and execute as shell"),
        (OndrejGitPushRegex().IsMatch, "Force push or remote branch deletion. GitHub deletes a merged PR's branch itself; clean up locally from outside the tree with dydo clean --dry-run <worktree>, then dydo clean <worktree>, which removes the worktree, keeps unmerged branches and runs git fetch --prune; name any other remote branch for the user"),
        (OndrejGitRecoveryRegex().IsMatch, "Git recovery history destruction"),
        (OndrejSystemPermissionsRegex().IsMatch, "System permission or ownership change"),
        (OndrejGhDeleteRegex().IsMatch, "Destructive GitHub operation"),
        (OndrejGhApiDeleteRegex().IsMatch, "GitHub API delete operation"),
        (OndrejGhVisibilityRegex().IsMatch, "GitHub repository visibility change"),
        (OndrejGhTokenRegex().IsMatch, "GitHub authentication token access"),
        (OndrejPasswordCliRegex().IsMatch, "Password manager access"),
        (RunsPasswordStore, "Password store access"),
        (OndrejOpRegex().IsMatch, "1Password secret access"),
        (OndrejKeychainRegex().IsMatch, "Keychain password access"),
        (OndrejGpgSecretRegex().IsMatch, "GPG secret key export"),
        (OndrejPasswordStorePathRegex().IsMatch, "Password store path access"),
        (OndrejPasswordAppRegex().IsMatch, "Password manager application access"),

        // Raw worktree removal - dydo clean archives uncommitted work and never follows a link.
        (ForcedWorktreeRemoveRegex().IsMatch, WorktreeRemovalReason),
        (IsRecursiveWorktreeDelete, WorktreeRemovalReason),

        (InlineInterpreterRegex().IsMatch, "Inline interpreter execution bypasses file operation analysis. Write a script file instead."),
        (RunsPythonFromStdin, "Python program on stdin bypasses file operation analysis, as python -c does. "
            + "Change files with the file tools or write a script file; a change to a protected file goes to the user."),
    ];

    public (bool IsMatch, string? CdPath, string? RestCommand) DetectNeedlessCd(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return (false, null, null);

        var match = CdThenCommandRegex().Match(command);
        if (!match.Success)
            return (false, null, null);

        var cdPath = match.Groups[1].Value;
        if (!match.Groups[1].Success)
            cdPath = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;

        return (true, cdPath, match.Groups[4].Value.Trim());
    }

    public BashAnalysisResult Analyze(string command)
    {
        var result = new BashAnalysisResult();

        if (string.IsNullOrWhiteSpace(command))
            return result;

        var (isDangerous, reason) = CheckDangerousPatterns(command);
        if (isDangerous)
        {
            result.HasDangerousPattern = true;
            result.DangerousPatternReason = reason;
            return result;
        }

        // Strip cat heredoc blocks - literal text, not shell code
        var strippedCommand = StripCatHeredocs(command);

        // A literal body only data commands read is neither expansion nor code, so each piece of code
        // is read on its own and only its expanding text is searched for bypasses (DYD-406), without
        // the single-quoted literal data of a command that is one piece, all code (DYD-422).
        var pieces = CodePieces(strippedCommand);
        List<CodePiece> searched = pieces is [var only] && only.Expanding == only.Code ? [Relieved(only.Code)] : pieces;
        CheckBypassAttempts(pieces, searched, string.Join("; ", searched.Select(piece => Layout(piece.Code))), result);

        foreach (var subCmd in pieces.SelectMany(piece => SplitCommand(piece.Code)))
            AnalyzeSubCommand(subCmd.Trim(), result);

        // Analyze contents of command substitutions so hidden operations are detected, in literal
        // data too: it relieves the bypass only, never a path check (DYD-422).
        pieces.ForEach(piece => AnalyzeCommandSubstitutions(piece.Expanding, result));

        return result;
    }

    // The command's code with its literal data emptied. A command with a heredoc or here-string body
    // is searched whole, so no piece's state change, such as an alias, reaches another piece's data.
    private static CodePiece Relieved(string code)
    {
        var relieved = WithoutLiteralData(code);
        return new CodePiece(relieved, relieved);
    }

    private void AnalyzeCommandSubstitutions(string command, BashAnalysisResult result)
    {
        foreach (var inner in SubstitutionBodies(command))
        {
            foreach (var subCmd in SplitCommand(inner))
                AnalyzeSubCommand(subCmd.Trim(), result);
        }
    }

    public (bool IsDangerous, string? Reason) CheckDangerousPatterns(string command)
    {
        // A command line nested past a bound, in a reading (ShellReader.MaxNesting) or in the command
        // lines it runs (MaxCommandLineDepth and the reading's budget), is not read to its end, so
        // nothing it says can be verified (DYD-723).
        if (CommandLines(command, _shell).Lines.Any(line => line.TooDeep))
            return (true, TooComplex);

        try
        {
            foreach (var (isMatch, reason) in DangerousPatterns)
            {
                if (isMatch(command))
                    return (true, reason);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return (true, TooComplex);
        }
        return (false, null);
    }

    // Each check reads the expanding text of each piece, a substitution or variable search without its
    // literal data (DYD-422); a line break counts only in the layout of the pieces, joined, where it
    // separates commands rather than sitting in quoted text, a body or literal data.
    private static readonly (Func<string, bool, bool> Check, bool WithoutLiteralData, string Warning)[] BypassChecks =
    [
        ((code, _) => SubstitutionBodies(code).Any(), true, "Command contains command substitution - paths may be dynamic"),
        ((code, _) => Base64DecodeRegex().IsMatch(code), false, "Command contains base64 decode - potential obfuscation"),
        ((code, _) => HexDecodeRegex().IsMatch(code), false, "Command contains hex decode - potential obfuscation"),
        ((code, _) => ExpandsVariable(code), true, "Command contains variable expansion - paths may be dynamic"),
        ((code, separates) => separates || code.Contains("$'\\n'") || code.Contains("%0a"), false, "Command contains embedded newlines"),
    ];

    // Whether more than one line of the layout holds a command. A blank or comment-only line, such as
    // the one a heredoc's trailing line break leaves, holds none unless the line before continues
    // into it (a trailing \ or `), where its # no longer starts a comment. A bare carriage return
    // ends a line too: pwsh 7.6.9 starts a statement after one (DYD-641).
    private static bool SeparatesCommandLines(string layout)
    {
        var lines = layout.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', '\r');
        return lines.Where((line, i) => line.TrimStart() is [not '#', ..] || (i > 0 && lines[i - 1] is [.., '\\' or '`']))
            .Skip(1).Any();
    }

    private static void CheckBypassAttempts(List<CodePiece> pieces, List<CodePiece> searched, string layout, BashAnalysisResult result)
    {
        // The joined layout is shared by every piece; scanning it per body was quadratic.
        var separates = SeparatesCommandLines(layout);
        foreach (var (check, withoutLiteralData, warning) in BypassChecks)
        {
            if ((withoutLiteralData ? searched : pieces).Any(piece => check(piece.Expanding, separates)))
            {
                result.Warnings.Add(warning);
                result.HasBypassAttempt = true;
            }
        }
    }

    // To pwsh 7.6.9 a bare carriage return separates commands as a line feed does; bash reads it as word text (DYD-641).
    private static int CheckCommandSeparator(string command, int i, bool posix)
    {
        var c = command[i];
        if (c == ';' || c == '\n' || (!posix && c == '\r' && (i + 1 == command.Length || command[i + 1] != '\n')))
            return 1;
        if (c == '&' && i + 1 < command.Length && command[i + 1] == '&')
            return 2;
        if (c == '|' && i + 1 < command.Length && command[i + 1] == '|')
            return 2;
        if (c == '|')
            return 1;
        return 0;
    }

    private static bool AppendQuote(char c, StringBuilder current,
        ref bool inSingleQuote, ref bool inDoubleQuote)
    {
        if (c == '\'' && !inDoubleQuote)
            inSingleQuote = !inSingleQuote;
        else if (c == '"' && !inSingleQuote)
            inDoubleQuote = !inDoubleQuote;
        else
            return false;

        current.Append(c);
        return true;
    }

    // How many characters at i an unquoted separator or, with dropComments, a comment consumes;
    // 0 when neither starts there. A comment stops before its newline, so the newline still splits.
    private static int ConsumeUnquoted(string command, int i, StringBuilder current,
        List<string> parts, bool dropComments, bool posix)
    {
        if (dropComments && command[i] == '#' && (current.Length == 0 || char.IsWhiteSpace(current[^1])))
        {
            var newline = command.IndexOf('\n', i);
            return (newline < 0 ? command.Length : newline) - i;
        }

        var sepLen = CheckCommandSeparator(command, i, posix);
        if (sepLen > 0 && current.Length > 0)
        {
            parts.Add(current.ToString());
            current.Clear();
        }
        return sepLen;
    }

    /// <summary>
    /// Splits a command at unquoted separators. A backslash is literal inside single quotes. With
    /// <paramref name="dropComments"/>, an unquoted <c>#</c> that begins a word drops the rest of
    /// its line, as a shell comment. pwsh 7.6.9 also ends a command at a bare carriage return, where
    /// bash goes on with the same command's arguments (DYD-641), so both readings' sub-commands are
    /// returned: pwsh's, then each of bash's that pwsh's lack.
    /// </summary>
    internal static IEnumerable<string> SplitCommand(string command, bool dropComments = false)
    {
        var pwsh = SplitCommand(command, dropComments, posix: false);
        var pwshParts = new HashSet<string>(pwsh, StringComparer.Ordinal);
        return pwsh.Concat(SplitCommand(command, dropComments, posix: true).Where(part => !pwshParts.Contains(part)));
    }

    private static List<string> SplitCommand(string command, bool dropComments, bool posix)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var escapeNext = false;

        for (int i = 0; i < command.Length; i++)
        {
            var c = command[i];

            if (escapeNext)
            {
                current.Append(c);
                escapeNext = false;
                continue;
            }

            if (c == '\\' && !inSingleQuote)
            {
                escapeNext = true;
                current.Append(c);
                continue;
            }

            if (AppendQuote(c, current, ref inSingleQuote, ref inDoubleQuote))
                continue;

            if (!inSingleQuote && !inDoubleQuote)
            {
                var consumed = ConsumeUnquoted(command, i, current, parts, dropComments, posix);
                if (consumed > 0)
                {
                    i += consumed - 1;
                    continue;
                }
            }

            current.Append(c);
        }

        if (current.Length > 0)
            parts.Add(current.ToString());

        return parts;
    }

    private static readonly HashSet<string> ShellInterpreters =
        new(StringComparer.OrdinalIgnoreCase) { "bash", "sh", "zsh" };

    private void AnalyzeSubCommand(string command, BashAnalysisResult result)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;

        if (ReadAssignmentSource(command) is { } source)
        {
            AnalyzeSubCommand(source, result);
            return;
        }

        var tokens = TokenizeCommand(command);
        if (tokens.Count == 0)
            return;

        var cmdName = tokens[0];

        if (TryAnalyzeShellC(tokens, result))
            return;

        AnalyzeSedCommand(cmdName, tokens, result);
        AnalyzeAwkCommand(cmdName, tokens, result);

        // Redirection operators apply to all commands
        AnalyzeRedirection(command, result);

        if (AllCommands.TryGetValue(cmdName, out var opType))
            AddPathOperations(result, tokens.Skip(1), opType, cmdName);
        else
            AnalyzeUncertainCommand(cmdName, tokens, result);
    }

    [GeneratedRegex(@"^\$[A-Za-z_]\w*\s*=\s*(?<source>\S.*)$", RegexOptions.Singleline)]
    private static partial Regex VariableAssignmentRegex();

    private static readonly HashSet<string> ContentReaders = new(StringComparer.OrdinalIgnoreCase)
        { "get-content", "gc", "cat", "type", "select-string" };

    /// <summary>
    /// The read a PowerShell assignment makes (<c>$p = Get-Content notes.md</c>, spaced or not): its
    /// right side when that is a content reader, read on its own. Any other right side stays an unknown
    /// <c>$</c> command, flagged as an uncertain write. No PowerShell evaluation beyond this.
    /// </summary>
    private static string? ReadAssignmentSource(string command)
    {
        var assignment = VariableAssignmentRegex().Match(command);
        if (!assignment.Success)
            return null;

        var source = assignment.Groups["source"].Value;
        return TokenizeCommand(source) is [var reader, ..] && ContentReaders.Contains(reader) ? source : null;
    }

    private bool TryAnalyzeShellC(List<string> tokens, BashAnalysisResult result)
    {
        if (!ShellInterpreters.Contains(tokens[0]))
            return false;

        var cArgIndex = FindShellCArgument(tokens);
        if (cArgIndex < 0 || cArgIndex + 1 >= tokens.Count)
            return false;

        var innerCommand = tokens[cArgIndex + 1];
        foreach (var subCmd in SplitCommand(innerCommand))
            AnalyzeSubCommand(subCmd.Trim(), result);
        AnalyzeRedirection(innerCommand, result);
        return true;
    }

    private static void AnalyzeSedCommand(string cmdName, List<string> tokens, BashAnalysisResult result)
    {
        if (!cmdName.Equals("sed", StringComparison.OrdinalIgnoreCase))
            return;

        if (tokens.Any(t => t == "-i" || t.StartsWith("-i") || t == "--in-place"))
        {
            var lastToken = tokens.LastOrDefault();
            if (lastToken != null && !lastToken.StartsWith("-") && LooksLikePath(lastToken))
            {
                result.Operations.Add(new FileOperation
                {
                    Type = FileOperationType.Write,
                    Path = lastToken,
                    Command = "sed -i"
                });
            }
        }
        else
        {
            foreach (var file in ExtractPaths(tokens.Skip(1)))
            {
                result.Operations.Add(new FileOperation
                {
                    Type = FileOperationType.Read,
                    Path = file,
                    Command = "sed"
                });
            }
        }
    }

    private static void AnalyzeAwkCommand(string cmdName, List<string> tokens, BashAnalysisResult result)
    {
        if (!cmdName.Equals("awk", StringComparison.OrdinalIgnoreCase) &&
            !cmdName.Equals("gawk", StringComparison.OrdinalIgnoreCase))
            return;

        var inputFiles = ExtractPaths(tokens.Skip(1).Where(t => !t.StartsWith("'") && !t.StartsWith("\"")));
        foreach (var file in inputFiles)
        {
            result.Operations.Add(new FileOperation
            {
                Type = FileOperationType.Read,
                Path = file,
                Command = cmdName
            });
        }
    }

    /// <summary>
    /// When the command name is a variable ($CMD) or substitution (`cmd`), the actual
    /// command is unknown - flag path-like arguments as uncertain writes.
    /// </summary>
    private static void AnalyzeUncertainCommand(string cmdName, List<string> tokens, BashAnalysisResult result)
    {
        if (!cmdName.StartsWith('$') && !cmdName.StartsWith('`'))
            return;

        foreach (var path in ExtractPaths(tokens.Skip(1)))
        {
            result.Operations.Add(new FileOperation
            {
                Type = FileOperationType.Write,
                Path = path,
                Command = cmdName,
                IsUncertain = true
            });
        }
    }

    private static int FindShellCArgument(List<string> tokens)
    {
        for (int i = 1; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token == "-c")
                return i;
            // Combined flags: -xc, -exc, etc. - if starts with - (not --) and contains 'c'
            if (token.Length > 1 && token[0] == '-' && token[1] != '-' && token.Contains('c'))
                return i;
        }
        return -1;
    }

    private static readonly Dictionary<string, FileOperationType> AllCommands = BuildAllCommands();

    private static Dictionary<string, FileOperationType> BuildAllCommands()
    {
        var all = new Dictionary<string, FileOperationType>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in ReadCommands) all.TryAdd(k, v);
        foreach (var (k, v) in WriteCommands) all.TryAdd(k, v);
        foreach (var (k, v) in DeleteCommands) all.TryAdd(k, v);
        foreach (var (k, v) in PermissionCommands) all.TryAdd(k, v);
        foreach (var (k, v) in CopyMoveCommands) all.TryAdd(k, v);
        return all;
    }

    private static void AddPathOperations(BashAnalysisResult result, IEnumerable<string> tokens, FileOperationType type, string cmdName)
    {
        var args = tokens.ToList();
        var paths = ExtractPaths(args).ToList();
        var destination = type == FileOperationType.Copy ? FindCopyDestination(args, paths, cmdName) : null;
        if (destination != null && !paths.Contains(destination))
            paths.Add(destination);

        foreach (var path in paths)
        {
            result.Operations.Add(new FileOperation
            {
                Type = type,
                Path = path,
                Command = cmdName,
                IsCopySource = destination != null && path != destination
            });
        }
    }

    // The flags that name a copy's destination, per command. Without one, a copy writes its last
    // path; robocopy its second (`robocopy <src> <dst> [files]`).
    private static readonly Dictionary<string, string[]> CopyTargetFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cp"] = ["-t", "--target-directory"],
        ["copy-item"] = ["-destination"],
        ["ci"] = ["-destination"],
        ["cpi"] = ["-destination"],
    };

    private static string? FindCopyDestination(List<string> args, List<string> paths, string cmdName)
    {
        var flags = CopyTargetFlags.GetValueOrDefault(cmdName, []);
        for (var i = 0; i < args.Count; i++)
        {
            var flagged = FlagValue(args, i, flags);
            if (flagged != null)
                return flagged;
        }

        var index = cmdName.Equals("robocopy", StringComparison.OrdinalIgnoreCase) ? 1 : paths.Count - 1;
        return paths.ElementAtOrDefault(index);
    }

    private static string? FlagValue(List<string> args, int i, string[] flags)
    {
        foreach (var flag in flags)
        {
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
                return args.ElementAtOrDefault(i + 1);
            if (args[i].StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase))
                return args[i][(flag.Length + 1)..];
        }
        return null;
    }

    private static void AnalyzeRedirection(string command, BashAnalysisResult result)
    {
        var outputRedirects = OutputRedirectRegex().Matches(command);
        foreach (Match match in outputRedirects)
        {
            var path = match.Groups[2].Value.Trim('"', '\'');
            if (!string.IsNullOrEmpty(path) && !path.StartsWith("&") && LooksLikePath(path))
            {
                var redirectOp = match.Groups[1].Value;
                result.Operations.Add(new FileOperation
                {
                    Type = FileOperationType.Write,
                    Path = path,
                    Command = redirectOp
                });
            }
        }

        var inputRedirects = InputRedirectRegex().Matches(command);
        foreach (Match match in inputRedirects)
        {
            var path = match.Groups[1].Value.Trim('"', '\'');
            if (!string.IsNullOrEmpty(path) && !path.StartsWith("<") && LooksLikePath(path))
            {
                result.Operations.Add(new FileOperation
                {
                    Type = FileOperationType.Read,
                    Path = path,
                    Command = "<"
                });
            }
        }
    }

    private static List<string> TokenizeCommand(string command)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var escapeNext = false;

        foreach (var c in command)
        {
            if (escapeNext)
            {
                current.Append(c);
                escapeNext = false;
                continue;
            }

            if (c == '\\' && !inSingleQuote)
            {
                escapeNext = true;
                continue;
            }

            if (c == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (c == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inSingleQuote && !inDoubleQuote)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }

    private static IEnumerable<string> ExtractPaths(IEnumerable<string> tokens)
    {
        return tokens
            .Where(t => !t.StartsWith("-") && !string.IsNullOrEmpty(t))
            .Where(t => !IsShellOperator(t))
            .Where(LooksLikePath);
    }

    private static readonly HashSet<string> ShellOperators =
        ["|", ">", ">>", "<", "<<", "&&", "||", ";", "&", "2>", "2>>", "&>", "|&"];

    private static bool IsShellOperator(string token) => ShellOperators.Contains(token);

    private static readonly HashSet<string> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".json", ".yaml", ".yml", ".xml", ".md", ".env", ".sh", ".ps1", ".py", ".cs", ".js", ".ts" };

    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
        { ".env", ".npmrc", ".pypirc", "secrets", "credentials", "config", "passwd", "shadow" };

    private static bool IsDigitColonPattern(string value)
    {
        foreach (var c in value)
        {
            if (c != ':' && !char.IsDigit(c))
                return false;
        }
        return value.Contains(':');
    }

    private static bool HasKnownExtension(string value)
    {
        var dot = value.LastIndexOf('.');
        return dot >= 0 && KnownExtensions.Contains(value[dot..]);
    }

    private static bool HasSensitiveName(string value)
    {
        foreach (var name in SensitiveNames)
        {
            if (value.Contains(name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsNotPathPrefix(string value)
    {
        if (value.StartsWith('-') || value.StartsWith("&") || value.StartsWith("2>"))
            return true;
        if (int.TryParse(value, out _))
            return true;
        return false;
    }

    private static bool HasPathIndicators(string value)
    {
        return value.Contains('/') || value.Contains('\\') || (value.Contains('.') && !value.StartsWith("."));
    }

    private static bool LooksLikePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (IsNotPathPrefix(value))
            return false;

        if (HasPathIndicators(value) || HasKnownExtension(value) || HasSensitiveName(value))
            return true;

        if (value.AsSpan().IndexOfAny("[]{}") >= 0 || IsDigitColonPattern(value))
            return false;

        return !value.Contains(' ') && value.Length < 100;
    }
}
