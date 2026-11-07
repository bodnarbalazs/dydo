namespace DynaDocs.Services;

using System.Text;

/// <summary>
/// One reading of a shell command line into commands and their words. <c>posix</c> reads it as bash
/// does (backslash escapes, backtick substitution); otherwise as PowerShell or cmd do (backtick and
/// caret escapes, doubled quotes inside quotes, backslashes literal, <c>&lt;# ... #&gt;</c> block comments).
/// That reading follows pwsh 7.6.9 (DYD-641): a bare carriage return ends a line as a line feed does,
/// and ‘ ’ ‚ ‛ quote as ' does, “ ” „ as " does. A caret is cmd's escape and literal to pwsh; where
/// the two would read the line differently, the reading holds both and fails. Commands split only on unquoted separators: ; &amp; | ( ) { }
/// and line breaks. Quotes, PowerShell here-strings and heredoc bodies are word text; a heredoc or
/// <c>&lt;&lt;&lt;</c> word is the command's stdin, and a redirection target is dropped. The text of
/// a <c>$(...)</c> inside double quotes or a here-string is read as commands, and a backtick
/// substitution is returned for a fresh scan. In PowerShell an array <c>@(...)</c>, or a
/// <c>(...)</c> group in argument position, is read as commands and is also one word: the words of
/// those commands joined by spaces, so <c>-ArgumentList @('mode', 'set')</c> keeps its value. A nested
/// group's commands reach it once, through the nested group's own word. A reading nested deeper than
/// <see cref="MaxNesting"/> substitutions, groups and ( fails as <see cref="Reading.TooDeep"/>.
/// </summary>
internal sealed class ShellReader
{
    // A scope owns its coordinates even when its commands also appear in the compatibility flat list.
    internal sealed class Scope(string text, bool posix, int start, Host? host)
    {
        public string Text { get; } = text;
        public bool Posix { get; } = posix;
        public int Start { get; } = start;
        public int End { get; set; } = text.Length;
        public Host? Host { get; } = host;
        public List<Command> Commands { get; } = [];
        public List<Scope> Children { get; } = [];
        public List<Range> Comments { get; } = [];
        // Each '...' this reading opened and closed outside quotes, its quotes included.
        public List<Range> SingleQuotes { get; } = [];
        public List<Host> Executions { get; } = [];
        public Body? DerivedBody { get; init; }

        // Membership is queried only after the reading finishes. Keep the ordered lists for walks.
        private HashSet<Host>? _executions;
        private HashSet<Scope>? _children;
        public bool HasExecution(Host host) => (_executions ??= Executions.ToHashSet()).Contains(host);
        public bool HasChild(Scope child) => (_children ??= Children.ToHashSet()).Contains(child);

        public bool Contains(Range range) => !range.Start.IsFromEnd && !range.End.IsFromEnd
            && range.Start.Value >= Start && range.End.Value >= range.Start.Value && range.End.Value <= End;
    }

    internal readonly record struct Position(Scope Source, int Offset);

    // Only the reader's existing text construction changes coordinates. Synthetic separators have
    // no origin; a selected occurrence or token must map through emitted characters, never a guess.
    internal sealed class TextOrigins
    {
        // The emitted text in order: each part a run of one source's characters, or origins appended
        // whole. Those are referenced, not copied, so a group word costs one part per word it takes,
        // however deep the groups inside that word (DYD-723).
        private readonly List<Part> _parts = new(1);
        private int _length;

        private readonly record struct Part(int Offset, int Length, Position Origin, TextOrigins? Appended);

        public void Add(int offset, int length, Scope source, int start)
        {
            if (length <= 0)
                return;
            _length = Math.Max(_length, offset + length);
            if (_parts.Count > 0 && _parts[^1] is { Appended: null } last && last.Offset + last.Length == offset
                && ReferenceEquals(last.Origin.Source, source) && last.Origin.Offset + last.Length == start)
                _parts[^1] = last with { Length = last.Length + length };
            else
                _parts.Add(new Part(offset, length, new Position(source, start), null));
        }

        public void Append(TextOrigins other, int offset)
        {
            if (other._length == 0)
                return;
            _length = Math.Max(_length, offset + other._length);
            _parts.Add(new Part(offset, other._length, default, other));
        }

        public Position? At(int offset)
        {
            var at = FirstPart(offset);
            if (at == _parts.Count || offset < _parts[at].Offset)
                return null;
            var part = _parts[at];
            return part.Appended is { } appended
                ? appended.At(offset - part.Offset)
                : part.Origin with { Offset = part.Origin.Offset + offset - part.Offset };
        }

        public IEnumerable<(Scope Source, Range Range)> SourceRanges(int start = 0, int end = int.MaxValue)
        {
            foreach (var run in Runs(0, start, end))
            {
                var from = Math.Max(start, run.Offset);
                var to = Math.Min(end, run.Offset + run.Length);
                var raw = run.Origin.Offset + from - run.Offset;
                yield return (run.Origin.Source, raw..(raw + to - from));
            }
        }

        // Parts are appended in output order. Skip earlier runs when locating one execution in a
        // body with many origin runs (for example, one per CRLF line).
        private int FirstPart(int offset)
        {
            var low = 0;
            var high = _parts.Count;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (_parts[middle].Offset + _parts[middle].Length <= offset)
                    low = middle + 1;
                else
                    high = middle;
            }
            return low;
        }

        public Range? RangeIn(Scope source, Range range)
        {
            var start = At(range.Start.Value);
            var end = At(range.End.Value - 1);
            if (start is not { } first || end is not { } last
                || !ReferenceEquals(first.Source, source) || !ReferenceEquals(last.Source, source))
                return null;
            var covered = range.Start.Value;
            var previous = first.Offset;
            foreach (var run in Runs(0, range.Start.Value, range.End.Value))
            {
                var from = Math.Max(covered, run.Offset);
                var raw = run.Origin.Offset + from - run.Offset;
                if (from != covered || !ReferenceEquals(run.Origin.Source, source) || raw < previous)
                    return null;
                covered = Math.Min(range.End.Value, run.Offset + run.Length);
                previous = raw + covered - from;
            }
            return covered == range.End.Value ? first.Offset..(last.Offset + 1) : null;
        }

        // The runs of one source each, appended origins expanded in place, shifted by shift.
        private IEnumerable<Part> Runs(int shift, int start, int end)
        {
            for (var i = FirstPart(start - shift); i < _parts.Count && _parts[i].Offset + shift < end; i++)
            {
                var part = _parts[i];
                if (part.Appended is not { } appended)
                    yield return part with { Offset = part.Offset + shift };
                else
                    foreach (var run in appended.Runs(shift + part.Offset, start, end))
                        yield return run;
            }
        }
    }

    internal enum ExecutionKind { Substitution, Backtick, Group }
    internal sealed class Host(Scope source, Command command, Body? body, int wordIndex, ExecutionKind kind)
    {
        public Scope Source { get; } = source;
        public Command Command { get; } = command;
        public Body? Body { get; set; } = body;
        public int WordIndex { get; set; } = wordIndex;
        public ExecutionKind Kind { get; } = kind;
        public Range Token { get; set; }
        public bool Complete { get; set; }
        public Scope? Child { get; set; }
        public Capture? Capture { get; set; }
    }
    internal enum BodyKind { Heredoc, HereString, HereWord }
    internal sealed record Body(Command Owner, string Text, Range Content, Range Whole,
        BodyKind Kind, bool Expands, int WordIndex = -1)
    {
        public TextOrigins Origins { get; init; } = new();
        public Scope? ExpansionSource { get; set; }
        public List<Host> Expansions { get; } = [];
        private HashSet<Host>? _expansions;
        public bool HasExpansion(Host host) => (_expansions ??= Expansions.ToHashSet()).Contains(host);
    }
    internal sealed record Capture(string Text, Scope Source, Range Range, Host Host)
    {
        public TextOrigins Origins { get; init; } = new();
    }
    internal sealed record RedirectTarget(string Operator, Range Range);

    internal sealed class Command(Scope source)
    {
        public Scope Source { get; } = source;
        public List<string> Words { get; } = [];
        public List<Range> WordRanges { get; } = [];
        public List<TextOrigins> WordOrigins { get; } = [];
        public List<string> Redirections { get; } = [];
        public List<string> Stdin { get; } = [];
        public List<Body> Bodies { get; } = [];
        private HashSet<Body>? _bodies;
        public bool HasBody(Body body) =>
            (_bodies ??= new HashSet<Body>(Bodies, ReferenceEqualityComparer.Instance)).Contains(body);
        public List<RedirectTarget> RedirectTargets { get; } = [];
        public bool PipesOutput { get; set; }
        public bool UncertainFeed { get; set; }
        public bool HeaderExpansion { get; set; }
        public Command? Next { get; set; }

        public string RawWord(int index) => Source.Text[WordRanges[index]];
    }

    internal sealed record Reading(IReadOnlyList<Command> Commands, IReadOnlyList<string> Substitutions,
        bool Failed, IReadOnlyList<Capture> Captures, Scope Source, bool TooDeep);

    // Each level of nesting holds a reader on the stack, and a group word the text of every level
    // inside it, so a reading's cost grows with depth times length: a deeper reading fails (DYD-723).
    internal const int MaxNesting = 32;

    private readonly string _text;
    private readonly bool _posix;
    private readonly bool _caretEscapes;
    private readonly bool _nested;
    private readonly List<Command> _commands;
    private readonly List<string> _substitutions;
    private readonly List<Capture> _captures;
    private readonly Scope _scope;
    private readonly Body? _expandingBody;
    // The commands whose words make the group word this reader is inside, or null outside any group.
    private readonly List<Command>? _grouped;
    private readonly int _nesting;
    private readonly List<(Command Command, string Delimiter, bool StripTabs, bool Expands, int Opener)> _heredocs = [];
    private Command _command;
    private Command? _last;
    private StringBuilder? _word;
    private int _wordStart;
    // Just after a quote that opened between words closes; pwsh 7.6.9 ends that argument there.
    private int _quotedEnd = -1;
    private int _i;
    private int _depth;
    private bool _redirectNext;
    private bool _hereStringNext;
    private bool _failed;
    private bool _caretDiverges;
    private bool _tooDeep;
    private bool _done;
    private int _groups;
    private int _redirectStart;
    private string _redirectOperator = "";
    private readonly List<Host> _hereWordHosts = [];
    private TextOrigins? _wordOrigins;

    private ShellReader(Scope scope, bool nested, List<Command> commands, List<string> substitutions,
        List<Capture> captures, bool caretEscapes, List<Command>? grouped, int nesting, Body? expandingBody = null)
    {
        _scope = scope;
        _text = scope.Text;
        _posix = scope.Posix;
        _caretEscapes = caretEscapes;
        _i = scope.Start;
        _nested = nested;
        _commands = commands;
        _substitutions = substitutions;
        _captures = captures;
        _expandingBody = expandingBody;
        _grouped = grouped;
        _nesting = nesting;
        _command = new Command(scope);
    }

    public static Reading Read(string text, bool posix)
    {
        var reader = ReadWith(text, posix, caretEscapes: !posix);
        if (!reader._caretDiverges)
            return new Reading(reader._commands, reader._substitutions, reader._failed, reader._captures, reader._scope, reader._tooDeep);

        // cmd escapes with a caret and pwsh keeps it literal: where the two read the line apart,
        // the reading holds both and fails, so no consumer takes either for certain.
        var literal = ReadWith(text, posix, caretEscapes: false);
        return new Reading([.. reader._commands, .. literal._commands], [.. reader._substitutions, .. literal._substitutions],
            Failed: true, [.. reader._captures, .. literal._captures], reader._scope, reader._tooDeep || literal._tooDeep);
    }

    private static ShellReader ReadWith(string text, bool posix, bool caretEscapes)
    {
        var reader = new ShellReader(new Scope(text, posix, 0, null), nested: false, [], [], [], caretEscapes, grouped: null, nesting: 0);
        reader.Run();
        return reader;
    }

    private void Run()
    {
        while (_i < _text.Length && !_failed && !_done)
            Step();
        EndCommand();
        _scope.End = Math.Min(_i, _text.Length);
        if (_last is { PipesOutput: true, Next: null })
            _last.UncertainFeed = true;
        _failed |= _nested && !_done;
    }

    private char Peek(int offset) => _i + offset < _text.Length ? _text[_i + offset] : '\0';

    private bool IsEscape(char c) => _posix ? c == '\\' : c == '`' || (_caretEscapes && c == '^');

    // pwsh opens and closes a string at any quote of a family (measured on 7.6.9); bash only at ' and ".
    private string SingleQuoteChars => _posix ? "'" : "'\u2018\u2019\u201A\u201B";

    private string DoubleQuoteChars => _posix ? "\"" : "\"\u201C\u201D\u201E";

    private void Step()
    {
        var c = _text[_i];
        if (IsEscape(c))
        {
            Escape();
            return;
        }

        switch (c)
        {
            case var quote when SingleQuoteChars.Contains(quote): SingleQuoted(); return;
            case var quote when DoubleQuoteChars.Contains(quote): DoubleQuoted(); return;
            case '`': _i = BacktickSubstitution(_i); StartWord(); return;
            case '@' when _word == null && HereStringBodyStart() > 0: HereString(); return;
            case '@' when !_posix && Peek(1) == '(': GroupWord(_i + 2); return;
            case '(' when !_posix && (_word != null || _command.Words.Count > 0): GroupWord(_i + 1); return;
            case '#' when _word == null: SkipComment(); return;
            case '<' when !_posix && Peek(1) == '#': SkipBlockComment(); return;
            case '$' when Peek(1) == '{': VariableInBraces(); return;
            case '<' or '>':
            case '&' when Peek(1) == '>': Redirect(); return;
            case '\n': EndLine(); return;
            case '\r' when !_posix && Peek(1) != '\n': EndLine(); return;
            case ')': CloseParen(); return;
            case ';' or '&' or '|' or '{' or '}' or '(':
                Separator(c); return;
        }

        if (char.IsWhiteSpace(c))
            EndWord();
        else
            Append(c);
        _i++;
    }

    private void EndLine()
    {
        EndCommand();
        if (_last is { PipesOutput: true })
            _last.UncertainFeed = true;
        _i++;
        ReadHeredocBodies();
    }

    private void Separator(char c)
    {
        var group = c is '(' or '{' or '}';
        _command.UncertainFeed |= group || _groups > 0;
        EndCommand();
        if (_last != null)
        {
            var pipe = c == '|' && Peek(1) != '|' && (_i == 0 || _text[_i - 1] != '|');
            _last.PipesOutput = pipe;
            _last.UncertainFeed |= group || (c == '&' && Peek(1) != '&' && (_i == 0 || _text[_i - 1] != '&'));
        }
        _depth += c == '(' ? 1 : 0;
        // A ( that opens a group is a level of nesting as a substitution is (DYD-723).
        if (_nesting + _depth > MaxNesting)
            _failed = _tooDeep = true;
        _groups += c is '(' or '{' ? 1 : 0;
        if (c == '}')
            _groups = Math.Max(0, _groups - 1);
        _i++;
    }

    // An escaped line break continues the line; any other escaped character is literal. pwsh 7.6.9
    // continues at an escaped carriage return, bare or before a line feed, only between words (DYD-665);
    // inside a word it keeps the carriage return, and a line feed after it still ends the line.
    // pwsh and cmd read a caret before a letter, a digit, a caret or the end alike; before anything
    // else they read the line apart.
    private void Escape()
    {
        var next = Peek(1);
        _caretDiverges |= _text[_i] == '^' && _i + 1 < _text.Length && !char.IsAsciiLetterOrDigit(next) && next != '^';
        var crlf = next == '\r' && Peek(2) == '\n';
        var continues = next == '\n' || (crlf && (_posix || _text[_i] == '^'));
        if (!continues && next == '\r' && !_posix && BetweenWords)
        {
            EndWord();
            continues = true;
        }

        if (continues)
            _command.UncertainFeed = true;
        else if (next != '\0')
            Append(next, _i + 1);
        _i += crlf && continues ? 3 : 2;
    }

    // In pwsh a quote that opens between words is an argument of its own, ended where it closes.
    private bool BetweenWords => _word == null || (!_posix && _quotedEnd == _i);

    // In PowerShell a doubled quote is one literal quote, and the string goes on from the second.
    private void SingleQuoted()
    {
        var standalone = BetweenWords;
        while (true)
        {
            var length = _text.AsSpan(_i + 1).IndexOfAny(SingleQuoteChars);
            if (length < 0)
            {
                _failed = true;
                return;
            }

            var end = _i + 1 + length;
            _scope.SingleQuotes.Add(_i..(end + 1));
            AppendSlice(_i + 1, end - _i - 1);
            _i = end + 1;
            if (_posix || !SingleQuoteChars.Contains(Peek(0)))
            {
                if (standalone)
                    _quotedEnd = _i;
                return;
            }
            Append(Peek(0));
        }
    }

    private void DoubleQuoted()
    {
        var standalone = BetweenWords;
        StartWord();
        var at = _i + 1;
        while (at < _text.Length && !_failed)
        {
            var c = _text[at];
            var next = at + 1 < _text.Length ? _text[at + 1] : '\0';
            if (DoubleQuoteChars.Contains(c) && !IsQuotedEscape(c, next))
            {
                _i = at + 1;
                if (standalone)
                    _quotedEnd = _i;
                return;
            }

            if (c == '$' && next == '(')
                at = Substitution(at + 2, at);
            else if (_posix && c == '`')
                at = BacktickSubstitution(at);
            else if (IsQuotedEscape(c, next))
            {
                if (next != '\n')
                    Append(next, at + 1);
                at += 2;
            }
            else
            {
                Append(c, at);
                at++;
            }
        }

        _failed = true;
    }

    // PowerShell keeps a backslash literal: only a backtick or a doubled quote escapes inside "...".
    private bool IsQuotedEscape(char c, char next) => _posix
        ? c == '\\' && next is '$' or '`' or '"' or '\\' or '\n'
        : (c == '`' && next != '\0') || (DoubleQuoteChars.Contains(c) && DoubleQuoteChars.Contains(next));

    // Reads $( ... ) from start as commands of its own; returns the index after the closing parenthesis.
    // A group's commands go to its own grouped list; any other substitution's to the enclosing one.
    private int Substitution(int start, int open, ExecutionKind kind = ExecutionKind.Substitution, List<Command>? grouped = null)
    {
        if (_nesting == MaxNesting)
        {
            _failed = _tooDeep = true;
            return _text.Length;
        }

        var host = CaptureHost(open, kind);
        var source = new Scope(_text, _posix, start, host);
        host.Child = source;
        _scope.Children.Add(source);
        var inner = new ShellReader(source,
            nested: true, _commands, _substitutions, _captures, _caretEscapes, grouped ?? _grouped, _nesting + 1);
        inner.Run();
        // A bare case word can introduce arm terminators we cannot balance. Quoted words and
        // literal bodies are not syntax; no apparent closing ')' may certify this child otherwise.
        inner._failed |= _posix && source.Commands.Any(command =>
            command.WordRanges.Any(range => _text[range] == "case"));
        _failed |= inner._failed;
        _caretDiverges |= inner._caretDiverges;
        _tooDeep |= inner._tooDeep;
        host.Complete = inner._done && !inner._failed && inner._i < _text.Length && _text[inner._i] == ')';
        host.Token = open..Math.Min(inner._i + 1, _text.Length);
        return inner._i + 1;
    }

    private Host CaptureHost(int open, ExecutionKind kind)
    {
        var owner = _expandingBody?.Owner ?? _command;
        var host = new Host(_scope, owner, _expandingBody, _expandingBody?.WordIndex ?? owner.Words.Count, kind)
        {
            Token = open..open,
        };
        _scope.Executions.Add(host);
        if (_expandingBody != null)
            _expandingBody.Expansions.Add(host);
        else if (_hereStringNext)
            _hereWordHosts.Add(host);
        else
            _command.HeaderExpansion = true;
        return host;
    }

    // A PowerShell @( ... ) or argument ( ... ) from start: its commands are read, and their words
    // become one word of the enclosing command. A nested group's commands are in that group's word,
    // not taken again, so the word grows with the text rather than doubling with each level (DYD-723).
    private void GroupWord(int start)
    {
        var word = StartWord();
        List<Command> grouped = [];
        _i = Substitution(start, _i, ExecutionKind.Group, grouped);
        var offset = word.Length;
        foreach (var command in grouped)
        {
            for (var i = 0; i < command.Words.Count; i++)
            {
                _wordOrigins!.Append(command.WordOrigins[i], offset);
                offset += command.Words[i].Length + 1;
            }
        }
        word.AppendJoin(' ', grouped.SelectMany(command => command.Words));
    }

    private int BacktickSubstitution(int open)
    {
        var at = open + 1;
        while (at < _text.Length && _text[at] != '`')
            at += _text[at] == '\\' ? 2 : 1;
        if (at >= _text.Length)
        {
            _failed = true;
            return _text.Length;
        }

        var text = _text[(open + 1)..at].Replace("\\`", "`");
        _substitutions.Add(text);
        var host = CaptureHost(open, ExecutionKind.Backtick);
        host.Token = open..(at + 1);
        host.Complete = true;
        var capture = new Capture(text, _scope, (open + 1)..at, host)
        {
            Origins = BacktickOrigins(open + 1, at),
        };
        host.Capture = capture;
        _captures.Add(capture);
        return at + 1;
    }

    private TextOrigins BacktickOrigins(int start, int end)
    {
        var origins = new TextOrigins();
        var offset = 0;
        while (start < end)
        {
            var escape = _text.IndexOf("\\`", start, end - start, StringComparison.Ordinal);
            var stop = escape < 0 ? end : escape;
            origins.Add(offset, stop - start, _scope, start);
            offset += stop - start;
            if (escape < 0)
                break;
            origins.Add(offset++, 1, _scope, escape + 1);
            start = escape + 2;
        }
        return origins;
    }

    private void CloseParen()
    {
        if (_nested && _depth == 0)
        {
            _done = true;
            return;
        }

        _depth = Math.Max(0, _depth - 1);
        _command.UncertainFeed = true;
        EndCommand();
        _groups = Math.Max(0, _groups - 1);
        _i++;
    }

    // @' or @" ending its line opens a PowerShell here-string; returns where its body starts, or 0.
    private int HereStringBodyStart()
    {
        var quote = Peek(1);
        if (!SingleQuoteChars.Contains(quote) && !DoubleQuoteChars.Contains(quote))
            return 0;
        var at = _i + 2;
        while (at < _text.Length && (_text[at] is ' ' or '\t' || (_posix && _text[at] == '\r')))
            at++;
        var length = LineBreakLength(at);
        return length > 0 ? at + length : 0;
    }

    // The length of the line break at at: \n, \r\n, and in PowerShell a bare \r; 0 for none.
    private int LineBreakLength(int at) => at >= _text.Length ? 0 : _text[at] switch
    {
        '\n' => 1,
        '\r' when at + 1 < _text.Length && _text[at + 1] == '\n' => 2,
        '\r' when !_posix => 1,
        _ => 0,
    };

    // The here-string's terminator starts a line: a quote of the opener's family, then @.
    private void HereString()
    {
        var expands = DoubleQuoteChars.Contains(Peek(1));
        var bodyStart = HereStringBodyStart();
        var end = HereStringEnd(bodyStart - 1, expands ? DoubleQuoteChars : SingleQuoteChars);
        if (end < 0)
        {
            _failed = true;
            return;
        }

        var body = end > bodyStart ? _text[bodyStart..end].TrimEnd('\r') : "";
        var descriptor = new Body(_command, body, bodyStart..Math.Max(bodyStart, end),
            _i..(end + 3), BodyKind.HereString, expands, _command.Words.Count);
        descriptor.Origins.Add(0, body.Length, _scope, bodyStart);
        _command.Bodies.Add(descriptor);
        if (descriptor.Expands)
            ReadExpansions(descriptor);
        var word = StartWord();
        _wordOrigins!.Append(descriptor.Origins, word.Length);
        word.Append(body);
        _i = end + 3;
    }

    // The line break before a here-string's terminator, from from on; -1 when there is none.
    private int HereStringEnd(int from, string quotes)
    {
        for (var at = from; at + 2 < _text.Length; at++)
        {
            if (LineBreakLength(at) == 1 && quotes.Contains(_text[at + 1]) && _text[at + 2] == '@')
                return at;
        }
        return -1;
    }

    // The $( ... ) and, for bash, backtick substitutions in an expanding here-string or heredoc body.
    private void ReadExpansions(Body source)
    {
        var body = source.Text;
        var scope = new Scope(body, _posix, 0, null) { DerivedBody = source };
        source.ExpansionSource = scope;
        var reader = new ShellReader(scope, nested: false, _commands, _substitutions, _captures, _caretEscapes, _grouped, _nesting, source);
        _scope.Children.Add(reader._scope);
        var at = 0;
        while (at < body.Length && !reader._failed)
        {
            if (body[at] == '$' && at + 1 < body.Length && body[at + 1] == '(')
                at = reader.Substitution(at + 2, at);
            else if (_posix && body[at] == '`')
                at = reader.BacktickSubstitution(at);
            else
            {
                at += _posix && body[at] == '\\' ? 2 : 1;
                continue;
            }
        }

        _failed |= reader._failed;
        _caretDiverges |= reader._caretDiverges;
        _tooDeep |= reader._tooDeep;
    }

    // A comment runs to the end of its line; pwsh 7.6.9 ends it at a carriage return too.
    private void SkipComment()
    {
        var start = _i;
        var length = _posix ? _text.AsSpan(_i).IndexOf('\n') : _text.AsSpan(_i).IndexOfAny('\n', '\r');
        _i = length < 0 ? _text.Length : _i + length;
        _scope.Comments.Add(start.._i);
    }

    // A PowerShell <# ... #> comment, which can span lines; unterminated, the reading fails.
    private void SkipBlockComment()
    {
        EndWord();
        var end = _text.IndexOf("#>", _i + 2, StringComparison.Ordinal);
        if (end < 0)
        {
            _failed = true;
            return;
        }

        _scope.Comments.Add(_i..(end + 2));
        _i = end + 2;
    }

    private void VariableInBraces()
    {
        var end = _text.IndexOf('}', _i);
        if (end < 0)
        {
            _failed = true;
            return;
        }

        AppendSlice(_i, end - _i + 1);
        _i = end + 1;
    }

    private void Redirect()
    {
        _redirectStart = _i;
        if (_word is { Length: > 0 } && _word.ToString().All(char.IsAsciiDigit))
        {
            _redirectStart = _wordStart;
            _word = null;
        }
        EndWord();

        if (_text[_i] == '<' && Peek(1) == '<')
        {
            Heredoc();
            return;
        }

        while (_i < _text.Length && _text[_i] is '<' or '>' or '&' or '|')
            _i++;
        _redirectOperator = _text[_redirectStart.._i];
        _redirectNext = true;
    }

    private void Heredoc()
    {
        var opener = _i;
        if (Peek(2) == '<')
        {
            _i += 3;
            _hereStringNext = true;
            return;
        }

        _i += 2;
        var stripTabs = Peek(0) == '-';
        _i += stripTabs ? 1 : 0;
        while (_i < _text.Length && _text[_i] is ' ' or '\t')
            _i++;

        var start = _i;
        while (_i < _text.Length && !char.IsWhiteSpace(_text[_i]) && _text[_i] is not (';' or '&' or '|' or '(' or ')' or '<' or '>'))
            _i++;
        var raw = _text[start.._i];
        _command.Redirections.Add(raw);
        var delimiter = string.Concat(raw.Where(c => c is not ('\'' or '"' or '\\')));
        if (delimiter.Length == 0)
        {
            _failed = true;
            return;
        }

        _heredocs.Add((_command, delimiter, stripTabs, Expands: delimiter.Length == raw.Length, opener));
    }

    private void ReadHeredocBodies()
    {
        foreach (var heredoc in _heredocs)
        {
            var body = new StringBuilder();
            var origins = new TextOrigins();
            var terminated = false;
            var bodyStart = _i;
            var contentEnd = _i;
            while (_i < _text.Length)
            {
                var lineStart = _i;
                var lineEnd = _text.IndexOf('\n', _i);
                var line = (lineEnd < 0 ? _text[_i..] : _text[_i..lineEnd]).TrimEnd('\r');
                _i = lineEnd < 0 ? _text.Length : lineEnd + 1;
                if ((heredoc.StripTabs ? line.TrimStart('\t') : line) == heredoc.Delimiter)
                {
                    terminated = true;
                    contentEnd = lineStart;
                    break;
                }
                AppendHeredocLine(body, origins, line, lineStart, lineEnd);
                contentEnd = _i;
            }

            heredoc.Command.Stdin.Add(body.ToString());
            var descriptor = new Body(heredoc.Command, body.ToString(), bodyStart..contentEnd,
                heredoc.Opener.._i, BodyKind.Heredoc, heredoc.Expands) { Origins = origins };
            heredoc.Command.Bodies.Add(descriptor);
            _failed |= !terminated;
            if (descriptor.Expands)
                ReadExpansions(descriptor);
        }

        _heredocs.Clear();
    }

    private void AppendHeredocLine(StringBuilder body, TextOrigins origins, string line, int start, int end)
    {
        origins.Add(body.Length, line.Length, _scope, start);
        if (end >= 0)
            origins.Add(body.Length + line.Length, 1, _scope, end);
        body.Append(line).Append('\n');
    }

    private StringBuilder StartWord()
    {
        if (_word == null)
        {
            _wordStart = _i;
            _word = new StringBuilder();
            _wordOrigins = new TextOrigins();
        }
        return _word;
    }

    private void Append(char c, int? origin = null)
    {
        var word = StartWord();
        _wordOrigins!.Add(word.Length, 1, _scope, origin ?? _i);
        word.Append(c);
    }

    private void AppendSlice(int start, int length)
    {
        var word = StartWord();
        _wordOrigins!.Add(word.Length, length, _scope, start);
        word.Append(_text, start, length);
    }

    private void EndWord()
    {
        if (_word == null)
            return;

        var word = _word.ToString();
        var origins = _wordOrigins!;
        _word = null;
        _wordOrigins = null;
        if (_redirectNext)
        {
            _redirectNext = false;
            _command.Redirections.Add(word);
            _command.RedirectTargets.Add(new RedirectTarget(_redirectOperator, _redirectStart..Math.Min(_i, _text.Length)));
        }
        else if (_hereStringNext)
        {
            _hereStringNext = false;
            _command.Stdin.Add(word);
            var range = _wordStart..Math.Min(_i, _text.Length);
            var body = new Body(_command, word, range, range, BodyKind.HereWord, _text[_wordStart] != '\'')
            {
                Origins = origins,
            };
            _command.Bodies.Add(body);
            foreach (var host in _hereWordHosts)
            {
                host.Body = body;
                host.WordIndex = body.WordIndex;
                body.Expansions.Add(host);
            }
            _hereWordHosts.Clear();
        }
        else
        {
            _command.Words.Add(word);
            _command.WordRanges.Add(_wordStart..Math.Min(_i, _text.Length));
            _command.WordOrigins.Add(origins);
        }
    }

    private void EndCommand()
    {
        EndWord();
        _redirectNext = false;
        _hereStringNext = false;
        if (_command.Words.Count > 0 || _command.Stdin.Count > 0 || _command.Redirections.Count > 0)
        {
            _command.UncertainFeed |= _groups > 0;
            _commands.Add(_command);
            _grouped?.Add(_command);
            _scope.Commands.Add(_command);
            if (_last != null)
                _last.Next = _command;
            _last = _command;
        }
        _command = new Command(_scope);
    }
}
