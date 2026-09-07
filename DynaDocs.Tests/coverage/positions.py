"""Canonical spans: one-based lines, zero-based UTF-16 columns, exclusive end."""


def canonical_text(raw):
    try:
        return raw.decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise ValueError("Source must be valid UTF-8") from error


def utf16_column(line, column, encoding):
    if type(column) is not int or column < 0:
        raise ValueError("Invalid source column")
    if encoding == "unicode":
        if column > len(line):
            raise ValueError("Source column outside line")
        prefix = line[:column]
    else:
        codec, width = {"utf8": ("utf-8", 1), "utf16": ("utf-16-le", 2)}.get(encoding, (None, None))
        if codec is None:
            raise ValueError("Unknown column encoding")
        raw = line.encode(codec)
        if column * width > len(raw):
            raise ValueError("Source column outside line")
        try:
            prefix = raw[:column * width].decode(codec)
        except UnicodeDecodeError as error:
            raise ValueError("Source column splits an encoded character") from error
    return len(prefix.encode("utf-16-le")) // 2


def contains(span, line, column):
    if len(span) != 4 or any(type(value) is not int for value in span):
        raise ValueError("Invalid source span")
    start_line, start_column, end_line, end_column = span
    if min(start_line, end_line) < 1 or min(start_column, end_column) < 0:
        raise ValueError("Invalid source span")
    if (start_line, start_column) >= (end_line, end_column):
        raise ValueError("Empty or reversed source span")
    return (start_line, start_column) <= (line, column) < (end_line, end_column)

