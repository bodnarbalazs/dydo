namespace DynaDocs.Sync.Projection;

internal static class MarkdownTerminalOwnership
{
    public static int ConsumeTerminalNewline(string source, int position) =>
        TerminalNewline(source, position) is { Length: > 0 } newline ? position + newline.Length : position;

    public static int BeforeTerminalNewline(string source, int position) =>
        NewlineBefore(source, position) is { Length: > 0 } newline ? position - newline.Length : position;

    public static int StartOfLineIndentation(string source, int position)
    {
        while (position > 0 && (source[position - 1] == ' ' || source[position - 1] == '\t'))
            position--;
        return position;
    }

    public static string RemoveDuplicateNestedSeparator(string text) =>
        text.StartsWith("\n\n", StringComparison.Ordinal) ? text[1..] : text;

    public static string TerminalNewline(string source) => TerminalNewline(source, source.Length);

    private static string NewlineBefore(string source, int position) =>
        position >= 2 && source[(position - 2)..position] == "\r\n" ? "\r\n"
            : position >= 1 && source[position - 1] == '\n' ? "\n"
            : "";

    private static string TerminalNewline(string source, int position) =>
        NewlineBefore(source, position) is { Length: > 0 } newline ? newline
            : position < source.Length && source[position..].StartsWith("\r\n", StringComparison.Ordinal) ? "\r\n"
            : position < source.Length && source[position] == '\n' ? "\n"
            : "";
}
