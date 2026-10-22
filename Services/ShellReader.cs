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
    internal sealed class Command
    {
        public List<string> Words { get; } = [];
        public List<string> Stdin { get; } = [];
    }

    internal sealed record Reading(IReadOnlyList<Command> Commands, IReadOnlyList<string> Substitutions, bool Failed);

    private readonly string _text;
    private readonly bool _posix;
    private readonly bool _nested;
    private readonly List<Command> _commands;
    private readonly List<string> _substitutions;
    private readonly List<(Command Command, string Delimiter, bool StripTabs, bool Expands)> _heredocs = [];
    private Command _command = new();
    private StringBuilder? _word;
    private int _i;
    private int _depth;
    private bool _redirectNext;
    private bool _hereStringNext;
    private bool _failed;
    private bool _done;

    private ShellReader(string text, bool posix, int start, bool nested, List<Command> commands, List<string> substitutions)
    {
        _text = text;
        _posix = posix;
        _i = start;
        _nested = nested;
        _commands = commands;
        _substitutions = substitutions;
    }

    public static Reading Read(string text, bool posix)
    {
        var reader = new ShellReader(text, posix, 0, nested: false, [], []);
        reader.Run();
        return new Reading(reader._commands, reader._substitutions, reader._failed);
    }

    private void Run()
    {
        while (_i < _text.Length && !_failed && !_done)
            Step();
        EndCommand();
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
            case '\n': EndCommand(); _i++; ReadHeredocBodies(); return;
            case ')': CloseParen(); return;
            case ';' or '&' or '|' or '{' or '}' or '(':
                _depth += c == '(' ? 1 : 0;
                EndCommand();
                _i++;
                return;
        }

        if (char.IsWhiteSpace(c))
            EndWord();
        else
            Append(c);
        _i++;
    }

    // An escaped line break continues the line; any other escaped character is literal.
    private void Escape()
    {
        var next = Peek(1);
        if (next == '\r' && Peek(2) == '\n')
        {
            _i += 3;
            return;
        }

        if (next != '\n' && next != '\0')
            Append(next);
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

        StartWord().Append(_text, _i + 1, end - _i - 1);
        _i = end + 1;
        if (_posix || Peek(0) != '\'')
            return;
        _word!.Append('\'');
        SingleQuoted();
    }

    private void DoubleQuoted()
    {
        var word = StartWord();
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
                at = Substitution(at + 2);
            else if (_posix && c == '`')
                at = BacktickSubstitution(at);
            else if (IsQuotedEscape(c, next))
            {
                if (next != '\n')
                    word.Append(next);
                at += 2;
            }
            else
            {
                word.Append(c);
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
    private int Substitution(int start)
    {
        var inner = new ShellReader(_text, _posix, start, nested: true, _commands, _substitutions);
        inner.Run();
        _failed |= inner._failed;
        return inner._i + 1;
    }

    // A PowerShell @( ... ) or argument ( ... ) from start: its commands are read, and their words
    // become one word of the enclosing command.
    private void GroupWord(int start)
    {
        var first = _commands.Count;
        _i = Substitution(start);
        StartWord().AppendJoin(' ', _commands.Skip(first).SelectMany(command => command.Words));
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

        _substitutions.Add(_text[(open + 1)..at].Replace("\\`", "`"));
        return at + 1;
    }

    private void CloseParen()
    {
        if (_nested && _depth == 0)
        {
            _done = true;
            return;
        }

        _depth = Math.Max(0, _depth - 1);
        EndCommand();
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
        if (quote == '"')
            ReadExpansions(body);
        StartWord().Append(body);
        _i = end + terminator.Length;
    }

    // The $( ... ) and, for bash, backtick substitutions in an expanding here-string or heredoc body.
    private void ReadExpansions(string body)
    {
        var reader = new ShellReader(body, _posix, 0, nested: false, _commands, _substitutions);
        var at = 0;
        while (at < body.Length && !reader._failed)
        {
            if (body[at] == '$' && at + 1 < body.Length && body[at + 1] == '(')
                at = reader.Substitution(at + 2);
            else if (_posix && body[at] == '`')
                at = reader.BacktickSubstitution(at);
            else
                at += _posix && body[at] == '\\' ? 2 : 1;
        }

        _failed |= reader._failed;
    }

    private void SkipComment()
    {
        var end = _text.IndexOf('\n', _i);
        _i = end < 0 ? _text.Length : end;
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

        StartWord().Append(_text, _i, end - _i + 1);
        _i = end + 1;
    }

    private void Redirect()
    {
        if (_word is { Length: > 0 } && _word.ToString().All(char.IsAsciiDigit))
            _word = null;
        EndWord();

        if (_text[_i] == '<' && Peek(1) == '<')
        {
            Heredoc();
            return;
        }

        while (_i < _text.Length && _text[_i] is '<' or '>' or '&' or '|')
            _i++;
        _redirectNext = true;
    }

    private void Heredoc()
    {
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
        var delimiter = string.Concat(raw.Where(c => c is not ('\'' or '"' or '\\')));
        if (delimiter.Length == 0)
        {
            _failed = true;
            return;
        }

        _heredocs.Add((_command, delimiter, stripTabs, Expands: delimiter.Length == raw.Length));
    }

    private void ReadHeredocBodies()
    {
        foreach (var heredoc in _heredocs)
        {
            var body = new StringBuilder();
            while (_i < _text.Length)
            {
                var lineEnd = _text.IndexOf('\n', _i);
                var line = (lineEnd < 0 ? _text[_i..] : _text[_i..lineEnd]).TrimEnd('\r');
                _i = lineEnd < 0 ? _text.Length : lineEnd + 1;
                if ((heredoc.StripTabs ? line.TrimStart('\t') : line) == heredoc.Delimiter)
                    break;
                body.Append(line).Append('\n');
            }

            heredoc.Command.Stdin.Add(body.ToString());
            if (heredoc.Expands)
                ReadExpansions(body.ToString());
        }

        _heredocs.Clear();
    }

    private StringBuilder StartWord() => _word ??= new StringBuilder();

    private void Append(char c) => StartWord().Append(c);

    private void EndWord()
    {
        if (_word == null)
            return;

        var word = _word.ToString();
        _word = null;
        if (_redirectNext)
            _redirectNext = false;
        else if (_hereStringNext)
        {
            _hereStringNext = false;
            _command.Stdin.Add(word);
        }
        else
            _command.Words.Add(word);
    }

    private void EndCommand()
    {
        EndWord();
        _redirectNext = false;
        _hereStringNext = false;
        if (_command.Words.Count > 0 || _command.Stdin.Count > 0)
            _commands.Add(_command);
        _command = new Command();
    }
}
