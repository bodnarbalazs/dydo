namespace DynaDocs.Utils;

/// <summary>
/// Turns a slug into a human-readable title (issue 0290): "swarm-0119" → "Swarm 0119",
/// "agent-graph-metrics" → "Agent Graph Metrics". A value containing a space is real prose and
/// passes through verbatim. Never returns blank: a value whose segments all vanish (e.g. "-")
/// falls back to the input itself.
/// </summary>
public static class TitlePrettifier
{
    public static string Prettify(string value)
    {
        if (value.Contains(' '))
            return value;
        var pretty = string.Join(" ", value
            .Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
        return pretty.Length > 0 ? pretty : value;
    }
}
