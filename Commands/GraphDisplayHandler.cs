namespace DynaDocs.Commands;

using DynaDocs.Services;

/// <summary>
/// Handles graph output display for GraphCommand subcommands.
/// </summary>
internal static class GraphDisplayHandler
{
    public static void ShowIncoming(IDocGraph graph, string targetPath, string file)
    {
        var incomingLinks = graph.GetIncoming(targetPath);
        Console.WriteLine($"Incoming links to {Path.GetFileName(file)} ({incomingLinks.Count} docs link here):");

        if (incomingLinks.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var (doc, lineNumber) in incomingLinks.OrderBy(x => x.Doc))
            {
                Console.WriteLine($"  {doc}:{lineNumber}");
            }
        }
    }

    public static void ShowDegree(IDocGraph graph, string targetPath, string file, int degree)
    {
        var withinDegree = graph.GetWithinDegree(targetPath, degree);
        Console.WriteLine($"{Path.GetFileName(file)}");

        if (withinDegree.Count == 0)
        {
            Console.WriteLine("  (no outgoing links)");
        }
        else
        {
            var grouped = withinDegree.GroupBy(x => x.Degree).OrderBy(g => g.Key);
            foreach (var group in grouped)
            {
                foreach (var (doc, deg) in group.OrderBy(x => x.Doc))
                {
                    var indent = new string(' ', deg * 2);
                    Console.WriteLine($"{indent}[degree {deg}] {doc}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Found {withinDegree.Count} docs within {degree} hops of {Path.GetFileName(file)}");
    }

    public static void ShowCombined(IDocGraph graph, string targetPath, int degree)
    {
        Console.WriteLine();
        var withinDegree = graph.GetWithinDegree(targetPath, degree);

        Console.WriteLine($"Outgoing within {degree} hops ({withinDegree.Count} docs):");

        if (withinDegree.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            var grouped = withinDegree.GroupBy(x => x.Degree).OrderBy(g => g.Key);
            foreach (var group in grouped)
            {
                var docList = string.Join(", ", group.OrderBy(x => x.Doc).Select(x => Path.GetFileName(x.Doc)));
                Console.WriteLine($"  [degree {group.Key}] {docList}");
            }
        }
    }

    public static void ShowStats(IDocGraph graph, int top)
    {
        var stats = graph.GetStats();
        var totalLinks = stats.Sum(s => s.IncomingCount);

        Console.WriteLine($"Document Link Statistics (Top {Math.Min(top, stats.Count)})");
        Console.WriteLine(new string('─', 50));
        Console.WriteLine($"{"#",3}  {"In",4}  Document");

        var rank = 1;
        foreach (var (doc, incomingCount) in stats.Take(top))
        {
            Console.WriteLine($"{rank,3}  {incomingCount,4}  {doc}");
            rank++;
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {stats.Count} documents, {totalLinks} internal links");
    }
}
