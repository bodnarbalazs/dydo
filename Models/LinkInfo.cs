namespace DynaDocs.Models;

public enum LinkType
{
    Markdown,
    Wikilink,
    External
}

public record LinkInfo(
    string RawText,
    string DisplayText,
    string Target,
    string? Anchor,
    LinkType Type,
    int LineNumber
);
