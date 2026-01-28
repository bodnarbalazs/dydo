namespace DynaDocs.Models;

public record LinkInfo(
    string RawText,
    string DisplayText,
    string Target,
    string? Anchor,
    LinkType Type,
    int LineNumber
);
