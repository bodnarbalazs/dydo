namespace DynaDocs.Services.Statechart;

// Follows a relative link the way the filesystem does: one component at a time, expanding a link where it
// occurs and applying `..` to the directory reached, so `l/..` is the parent of l's target, not of l.
internal static class StatechartLinks
{
    private const int MaximumLinks = 40;

    // The root-relative, '/'-separated entry the link reaches from directory `from`. `link` gives an entry's
    // raw link target or null; `directory` says whether an existing entry is a directory, null when absent.
    public static string Resolve(string from, string value, Func<string, string?> link, Func<string, bool?> directory)
    {
        var reached = from.Length == 0 ? new List<string>() : [.. from.Split('/')];
        var pending = new Stack<string>();
        Push(pending, value);
        var expanded = 0;
        while (pending.TryPop(out var part))
        {
            if (part is "" or ".") continue;
            if (part == "..")
            {
                if (reached.Count == 0) throw new IOException("Statechart link leaves its root: " + value);
                reached.RemoveAt(reached.Count - 1);
                continue;
            }
            var candidate = string.Join('/', reached.Append(part));
            if (link(candidate) is { } target)
            {
                if (++expanded > MaximumLinks) throw new IOException("Statechart links loop: " + value);
                Push(pending, target);
                continue;
            }
            var isDirectory = directory(candidate) ?? throw new IOException("Broken statechart link: " + value);
            if (!isDirectory && pending.Count != 0) throw new IOException("Statechart link passes through a file: " + value);
            reached.Add(part);
        }
        return string.Join('/', reached);
    }

    public static string Parent(string name)
    {
        var index = name.LastIndexOf('/');
        return index < 0 ? "" : name[..index];
    }

    private static void Push(Stack<string> pending, string value)
    {
        if (value.Length == 0 || value.StartsWith('/') || value.Contains('\\') || value.Contains(':'))
            throw new IOException("Unsafe statechart link target: " + value);
        foreach (var part in value.Split('/').Reverse()) pending.Push(part);
    }
}
