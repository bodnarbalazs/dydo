namespace DynaDocs.Tests;

using DynaDocs.Models;

internal static class LinkTestFactory
{
    public static LinkInfo Create(string target, LinkType type) =>
        new($"[link]({target})", "link", target, null, type, 1);

    public static LinkInfo CreateWithAnchor(string target, string anchor, LinkType type) =>
        new($"[link]({target}#{anchor})", "link", target, anchor, type, 1);
}
