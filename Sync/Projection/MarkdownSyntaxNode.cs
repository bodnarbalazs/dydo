namespace DynaDocs.Sync.Projection;

/// <summary>A parsed Markdown block or inline node with a clamped source span and semantic identity.</summary>
public sealed class MarkdownSyntaxNode
{
    internal MarkdownSyntaxNode(string kind, string syntax, string literal, IReadOnlyList<MarkdownSyntaxNode> children,
        int start, int end, int gapBeforeStart, int gapBeforeEnd)
    {
        Kind = kind;
        Syntax = syntax;
        Literal = literal;
        Children = children;
        Start = start;
        End = end;
        GapBeforeStart = gapBeforeStart;
        GapBeforeEnd = gapBeforeEnd;
        Identity = syntax + "\u001f" + literal + "\u001f" + string.Join("\u001e", children.Select(child => child.Identity));
    }

    public string Kind { get; }
    public string Syntax { get; }
    public string Literal { get; }
    public IReadOnlyList<MarkdownSyntaxNode> Children { get; }
    public int Start { get; }
    public int End { get; }
    public int GapBeforeStart { get; }
    public int GapBeforeEnd { get; }
    public string Identity { get; }

    public static bool TryParse(string source, string? localPageTitle, out IReadOnlyList<MarkdownSyntaxNode> nodes, out string? reason) =>
        MarkdownSyntaxParser.TryParse(source, localPageTitle, out nodes, out reason);
}
