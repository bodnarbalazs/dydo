"""Native lexical facts for behavior selection; comments never become token edits."""
import io
import tokenize

from positions import utf16_column


def python_tokens(source):
    try:
        compile(source, '<native-token-inventory>', 'exec', dont_inherit=True)
    except SyntaxError as error:
        raise ValueError('Incomplete Python native token inventory') from error
    lines = source.splitlines(keepends=True)
    rows = []
    omitted = {tokenize.COMMENT, tokenize.NL, tokenize.ENCODING, tokenize.ENDMARKER}
    try:
        for token in tokenize.generate_tokens(io.StringIO(source).readline):
            if token.type in omitted:
                continue
            start, end = token.start, token.end
            if token.type == tokenize.NEWLINE and not token.string:
                end = start
            # NEWLINE and indentation are grammar, unlike incidental whitespace.
            text = token.string
            if token.type in {tokenize.NEWLINE, tokenize.INDENT, tokenize.DEDENT}:
                text = tokenize.tok_name[token.type]
            def column(position):
                line, offset = position
                content = lines[line - 1] if line <= len(lines) else ''
                return utf16_column(content, offset, 'unicode')
            rows.append({'kind': tokenize.tok_name[token.type], 'text': text,
                         'line': start[0], 'column': column(start),
                         'end_line': end[0], 'end_column': column(end)})
    except (tokenize.TokenError, IndentationError) as error:
        raise ValueError('Incomplete Python native token inventory') from error
    return rows
