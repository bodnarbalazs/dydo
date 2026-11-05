namespace DynaDocs.Services;

using System.Text;

/// <summary>
/// One reading of a shell command line into commands and their words. <c>posix</c> reads it as bash
/// does (backslash escapes, backtick substitution); otherwise as PowerShell or cmd do (backtick and
/// caret escapes, doubled quotes inside quotes, backslashes literal, <c>&lt;# ... #&gt;</c> block comments). Commands split only on unquoted separators: ; &amp; | ( ) { }
/// and line breaks. Quotes, PowerShell here-strings and heredoc bodies are word text; a heredoc or
/// <c>&lt;&lt;&lt;</c> word is the command's stdin, and a redirection target is dropped. The text of
/// a <c>$(...)</c> inside double quotes or a here-string is read as commands, and a backtick
/// substitution is returned for a fresh scan. In PowerShell an array <c>@(...)</c>, or a
/// <c>(...)</c> group in argument position, is read as commands and is also one word: the words of
/// those commands joined by spaces, so <c>-ArgumentList @('mode', 'set')</c> keeps its value.
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

        public bool Contains(Range range) => !range.Start.IsFromEnd && !range.End.IsFromEnd
            && range.Start.Value >= Start && range.End.Value >= range.Start.Value && range.End.Value <= End;
    }

    internal readonly record struct Position(Scope Source, int Offset);
    internal sealed record OriginRun(int Offset, int Length, Position Origin);

    // Only the reader's existing text construction changes coordinates. Synthetic separators have
    // no origin; a selected occurrence or token must map through emitted characters, never a guess.
    internal sealed class TextOrigins
    {
        private readonly List<OriginRun> _runs = [];

        public void Add(int offset, int length, Scope source, int start)
        {
            if (length <= 0)
                return;
            if (_runs.Count > 0 && _runs[^1] is var last && last.Offset + last.Length == offset
                && ReferenceEquals(last.Origin.Source, source) && last.Origin.Offset + last.Length == start)
                _runs[^1] = last with { Length = last.Length + length };
            else
                _runs.Add(new OriginRun(offset, length, new Position(source, start)));
        }

        public void Append(TextOrigins other, int offset)
        {
            foreach (var run in other._runs)
                _runs.Add(run with { Offset = run.Offset + offset });
        }

        public Position? At(int offset)
        {
            foreach (var run in _runs)
                if (offset >= run.Offset && offset < run.Offset + run.Length)
                    return run.Origin with { Offset = run.Origin.Offset + offset - run.Offset };
            return null;
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
            foreach (var run in _runs.Where(r => r.Offset < range.End.Value && r.Offset + r.Length > covered))
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
        public List<RedirectTarget> RedirectTargets { get; } = [];
        public bool PipesOutput { get; set; }
        public bool UncertainFeed { get; set; }
        public bool HeaderExpansion { get; set; }
        public Command? Next { get; set; }

        public string RawWord(int index) => Source.Text[WordRanges[index]];
    }

    internal sealed record Reading(IReadOnlyList<Command> Commands, IReadOnlyList<string> Substitutions,
        bool Failed, IReadOnlyList<Capture> Captures, Scope Source);

    private readonly string _text;
    private readonly bool _posix;
    private readonly bool _nested;
    private readonly List<Command> _commands;
    private readonly List<string> _substitutions;
    private readonly List<Capture> _captures;
    private readonly Scope _scope;
    private readonly Body? _expandingBody;
    private readonly List<(Command Command, string Delimiter, bool StripTabs, bool Expands, int Opener)> _heredocs = [];
    private Command _command;
    private Command? _last;
    private StringBuilder? _word;
    private int _wordStart;
    private int _i;
    private int _depth;
    private bool _redirectNext;
    private bool _hereStringNext;
    private bool _failed;
    private bool _done;
    private int _groups;
    private int _redirectStart;
    private string _redirectOperator = "";
    private readonly List<Host> _hereWordHosts = [];
    private TextOrigins? _wordOrigins;

    private ShellReader(Scope scope, bool nested, List<Command> commands, List<string> substitutions,
        List<Capture> captures, Body? expandingBody = null)
    {
        _scope = scope;
        _text = scope.Text;
        _posix = scope.Posix;
        _i = scope.Start;
        _nested = nested;
        _commands = commands;
        _substitutions = substitutions;
        _captures = captures;
        _expandingBody = expandingBody;
        _command = new Command(scope);
    }

    public static Reading Read(string text, bool posix)
    {
        var reader = new ShellReader(new Scope(text, posix, 0, null), nested: false, [], [], []);
        reader.Run();
        return new Reading(reader._commands, reader._substitutions, reader._failed, reader._captures, reader._scope);
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

    private bool IsEscape(char c) => _posix ? c == '\\' : c is '`' or '^';

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
            case '\'': SingleQuoted(); return;
            case '"': DoubleQuoted(); return;
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
        _groups += c is '(' or '{' ? 1 : 0;
        if (c == '}')
            _groups = Math.Max(0, _groups - 1);
        _i++;
    }

    // An escaped line break continues the line; any other escaped character is literal.
    private void Escape()
    {
        var next = Peek(1);
        if (next == '\r' && Peek(2) == '\n')
        {
            _command.UncertainFeed = true;
            _i += 3;
            return;
        }

        if (next == '\n')
            _command.UncertainFeed = true;
        if (next != '\n' && next != '\0')
            Append(next, _i + 1);
        _i += 2;
    }

    private void SingleQuoted()
    {
        var end = _text.IndexOf('\'', _i + 1);
        if (end < 0)
        {
            _failed = true;
            return;
        }

        _scope.SingleQuotes.Add(_i..(end + 1));
        AppendSlice(_i + 1, end - _i - 1);
        _i = end + 1;
        if (_posix || Peek(0) != '\'')
            return;
        Append('\'');
        SingleQuoted();
    }

    private void DoubleQuoted()
    {
        StartWord();
        var at = _i + 1;
        while (at < _text.Length && !_failed)
        {
            var c = _text[at];
            var next = at + 1 < _text.Length ? _text[at + 1] : '\0';
            if (c == '"' && !IsQuotedEscape(c, next))
            {
                _i = at + 1;
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
        : (c == '`' && next != '\0') || (c == '"' && next == '"');

    // Reads $( ... ) from start as commands of its own; returns the index after the closing parenthesis.
    private int Substitution(int start, int open, ExecutionKind kind = ExecutionKind.Substitution)
    {
        var host = CaptureHost(open, kind);
        var source = new Scope(_text, _posix, start, host);
        host.Child = source;
        _scope.Children.Add(source);
        var inner = new ShellReader(source,
            nested: true, _commands, _substitutions, _captures);
        inner.Run();
        // A bare case word can introduce arm terminators we cannot balance. Quoted words and
        // literal bodies are not syntax; no apparent closing ')' may certify this child otherwise.
        inner._failed |= _posix && source.Commands.Any(command =>
            command.WordRanges.Any(range => _text[range] == "case"));
        _failed |= inner._failed;
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
    // become one word of the enclosing command.
    private void GroupWord(int start)
    {
        var word = StartWord();
        var first = _commands.Count;
        _i = Substitution(start, _i, ExecutionKind.Group);
        var offset = word.Length;
        foreach (var command in _commands.Skip(first))
        {
            for (var i = 0; i < command.Words.Count; i++)
            {
                _wordOrigins!.Append(command.WordOrigins[i], offset);
                offset += command.Words[i].Length + 1;
            }
        }
        word.AppendJoin(' ', _commands.Skip(first).SelectMany(command => command.Words));
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
        if (Peek(1) is not ('\'' or '"'))
            return 0;
        var at = _i + 2;
        while (at < _text.Length && _text[at] is ' ' or '\t' or '\r')
            at++;
        return at < _text.Length && _text[at] == '\n' ? at + 1 : 0;
    }

    private void HereString()
    {
        var quote = Peek(1);
        var bodyStart = HereStringBodyStart();
        var terminator = "\n" + quote + "@";
        var end = _text.IndexOf(terminator, bodyStart - 1, StringComparison.Ordinal);
        if (end < 0)
        {
            _failed = true;
            return;
        }

        var body = end > bodyStart ? _text[bodyStart..end].TrimEnd('\r') : "";
        var descriptor = new Body(_command, body, bodyStart..Math.Max(bodyStart, end),
            _i..(end + terminator.Length), BodyKind.HereString, quote == '"', _command.Words.Count);
        descriptor.Origins.Add(0, body.Length, _scope, bodyStart);
        _command.Bodies.Add(descriptor);
        if (descriptor.Expands)
            ReadExpansions(descriptor);
        var word = StartWord();
        _wordOrigins!.Append(descriptor.Origins, word.Length);
        word.Append(body);
        _i = end + terminator.Length;
    }

    // The $( ... ) and, for bash, backtick substitutions in an expanding here-string or heredoc body.
    private void ReadExpansions(Body source)
    {
        var body = source.Text;
        var scope = new Scope(body, _posix, 0, null) { DerivedBody = source };
        source.ExpansionSource = scope;
        var reader = new ShellReader(scope, nested: false, _commands, _substitutions, _captures, source);
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
    }

    private void SkipComment()
    {
        var start = _i;
        var end = _text.IndexOf('\n', _i);
        _i = end < 0 ? _text.Length : end;
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
            _scope.Commands.Add(_command);
            if (_last != null)
                _last.Next = _command;
            _last = _command;
        }
        _command = new Command(_scope);
    }
}
